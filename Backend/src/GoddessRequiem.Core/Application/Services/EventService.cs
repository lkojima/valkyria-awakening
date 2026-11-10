using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GoddessRequiem.Core.Application.Services
{
    public class EventService : IEventService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EventService> _logger;
        private readonly IBattleService _battleService;

        public EventService(IUnitOfWork unitOfWork, ILogger<EventService> logger, IBattleService battleService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleService = battleService;
        }

        public async Task<IReadOnlyList<GameEventDto>> GetActiveEventsAsync(Guid userId)
        {
            var events = await _unitOfWork.GameEvents.GetActiveAsync(DateTime.UtcNow);
            var userProgress = await _unitOfWork.EventProgress.GetByUserIdAsync(userId);

            return events.Select(e => MapToDto(e, userProgress)).ToList();
        }

        public async Task<EventProgressDto> GetEventProgressAsync(Guid userId, Guid eventId)
        {
            var gameEvent = await _unitOfWork.GameEvents.GetByIdAsync(eventId);
            if (gameEvent == null) return null;

            var progress = await _unitOfWork.EventProgress.GetByUserAndEventAsync(userId, eventId);
            if (progress == null)
            {
                progress = new EventProgress { UserId = userId, EventId = eventId };
                await _unitOfWork.EventProgress.AddAsync(progress);
                await _unitOfWork.SaveChangesAsync();
            }

            var stageProgress = await _unitOfWork.EventProgress.GetStageProgressAsync(userId, eventId);
            var questProgress = await _unitOfWork.EventProgress.GetQuestProgressAsync(userId, eventId);
            var claimedMilestones = progress.ClaimedMilestones;
            var claimedQuestRewards = progress.ClaimedQuestRewards;
            var shopPurchases = progress.ShopPurchases;

            return new EventProgressDto
            {
                EventId = eventId,
                EventName = gameEvent.Name,
                TotalPoints = progress.TotalPoints,
                CurrentStage = progress.CurrentStage,
                StageProgress = stageProgress.Select(MapToStageProgressDto).ToList(),
                QuestProgress = questProgress.Select(MapToQuestProgressDto).ToList(),
                ClaimedMilestones = claimedMilestones,
                ClaimedQuestRewards = claimedQuestRewards,
                ShopPurchases = shopPurchases,
                EndAt = gameEvent.EndAt,
                IsActive = gameEvent.IsActive(DateTime.UtcNow)
            };
        }

        public async Task<EventStageClearResult> ClearEventStageAsync(Guid userId, Guid eventId, Guid stageId, BattleResultData result)
        {
            var gameEvent = await _unitOfWork.GameEvents.GetByIdAsync(eventId);
            if (gameEvent == null)
                throw new KeyNotFoundException("Event not found");

            if (!gameEvent.IsActive(DateTime.UtcNow))
                throw new InvalidOperationException("Event not active");

            var stage = gameEvent.Stages.FirstOrDefault(s => s.Id == stageId);
            if (stage == null)
                throw new KeyNotFoundException("Stage not found");

            var progress = await _unitOfWork.EventProgress.GetByUserAndEventAsync(userId, eventId);
            if (progress == null)
                throw new KeyNotFoundException("Event progress not found");

            var stageProgress = progress.GetOrCreateStageProgress(stageId);
            var isFirstClear = stageProgress.ClearCount == 0;
            var starsEarned = CalculateStars(result, stage.StarConditions);

            stageProgress.UpdateClear(result, starsEarned, isFirstClear);

            // Update event points
            var pointsEarned = stage.BasePoints + (starsEarned * stage.StarBonusPoints);
            progress.TotalPoints += pointsEarned;

            // Update current stage
            if (isFirstClear)
            {
                var nextStage = gameEvent.Stages.FirstOrDefault(s => s.Order > stage.Order);
                if (nextStage != null)
                {
                    progress.CurrentStage = nextStage.Id;
                }
            }

            // Grant rewards
            var rewards = new List<Reward>();

            if (isFirstClear)
            {
                rewards.AddRange(stage.FirstClearRewards);
            }

            rewards.AddRange(stage.ClearRewards);

            if (starsEarned == 3)
            {
                rewards.AddRange(stage.ThreeStarRewards);
            }

            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.EventProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            // Check milestone rewards
            var newMilestones = CheckMilestones(progress, gameEvent);

            return new EventStageClearResult
            {
                Success = true,
                StageId = stageId,
                StarsEarned = starsEarned,
                PointsEarned = pointsEarned,
                TotalPoints = progress.TotalPoints,
                IsFirstClear = isFirstClear,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                NewMilestones = newMilestones.Select(MapToMilestoneDto).ToList()
            };
        }

        public async Task<EventQuestProgressResult> UpdateEventQuestProgressAsync(Guid userId, Guid eventId, Guid questId, Dictionary<string, int> progressUpdates)
        {
            var gameEvent = await _unitOfWork.GameEvents.GetByIdAsync(eventId);
            if (gameEvent == null)
                throw new KeyNotFoundException("Event not found");

            var quest = gameEvent.Quests.FirstOrDefault(q => q.Id == questId);
            if (quest == null)
                throw new KeyNotFoundException("Quest not found");

            var progress = await _unitOfWork.EventProgress.GetByUserAndEventAsync(userId, eventId);
            if (progress == null)
                throw new KeyNotFoundException("Event progress not found");

            var questProgress = progress.GetOrCreateQuestProgress(questId);

            foreach (var update in progressUpdates)
            {
                var objective = quest.Objectives.FirstOrDefault(o => o.Id == update.Key);
                if (objective != null)
                {
                    var current = questProgress.ObjectiveProgress.GetValueOrDefault(update.Key, 0);
                    questProgress.ObjectiveProgress[update.Key] = Math.Min(current + update.Value, objective.RequiredAmount);
                }
            }

            // Check if quest completed
            var allCompleted = quest.Objectives.All(o => questProgress.ObjectiveProgress.GetValueOrDefault(o.Id, 0) >= o.RequiredAmount);

            if (allCompleted && questProgress.Status != QuestStatus.Completed)
            {
                questProgress.Status = QuestStatus.Completed;
                questProgress.CompletedAt = DateTime.UtcNow;

                // Grant quest points
                progress.TotalPoints += quest.PointReward;
            }

            await _unitOfWork.EventProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            return new EventQuestProgressResult
            {
                QuestId = questId,
                ObjectiveProgress = questProgress.ObjectiveProgress,
                IsCompleted = allCompleted,
                PointsEarned = allCompleted ? quest.PointReward : 0
            };
        }

        public async Task<ClaimResult> ClaimEventQuestRewardAsync(Guid userId, Guid eventId, Guid questId)
        {
            var progress = await _unitOfWork.EventProgress.GetByUserAndEventAsync(userId, eventId);
            if (progress == null)
                throw new KeyNotFoundException("Event progress not found");

            if (progress.ClaimedQuestRewards.Contains(questId))
                throw new InvalidOperationException("Quest reward already claimed");

            var questProgress = progress.QuestProgress.GetValueOrDefault(questId);
            if (questProgress == null || questProgress.Status != QuestStatus.Completed)
                throw new InvalidOperationException("Quest not completed");

            var gameEvent = await _unitOfWork.GameEvents.GetByIdAsync(eventId);
            var quest = gameEvent?.Quests.FirstOrDefault(q => q.Id == questId);
            var rewards = quest?.Rewards ?? new List<Reward>();

            await GrantRewardsAsync(userId, rewards);

            progress.ClaimedQuestRewards.Add(questId);
            await _unitOfWork.EventProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            return new ClaimResult
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<ClaimResult> ClaimMilestoneRewardAsync(Guid userId, Guid eventId, int pointsThreshold)
        {
            var progress = await _unitOfWork.EventProgress.GetByUserAndEventAsync(userId, eventId);
            if (progress == null)
                throw new KeyNotFoundException("Event progress not found");

            if (progress.ClaimedMilestones.Contains(pointsThreshold))
                throw new InvalidOperationException("Milestone already claimed");

            if (progress.TotalPoints < pointsThreshold)
                throw new InvalidOperationException("Milestone not reached");

            var gameEvent = await _unitOfWork.GameEvents.GetByIdAsync(eventId);
            var milestone = gameEvent?.Milestones.FirstOrDefault(m => m.PointsThreshold == pointsThreshold);
            var rewards = milestone?.Rewards ?? new List<Reward>();

            await GrantRewardsAsync(userId, rewards);

            progress.ClaimedMilestones.Add(pointsThreshold);
            await _unitOfWork.EventProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            return new ClaimResult
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<ShopPurchaseResult> PurchaseFromEventShopAsync(Guid userId, Guid eventId, Guid shopId, Guid itemId, int quantity)
        {
            var gameEvent = await _unitOfWork.GameEvents.GetByIdAsync(eventId);
            if (gameEvent == null)
                throw new KeyNotFoundException("Event not found");

            var shop = gameEvent.Shops.FirstOrDefault(s => s.Id == shopId);
            if (shop == null)
                throw new KeyNotFoundException("Shop not found");

            var item = shop.Items.FirstOrDefault(i => i.ItemId == itemId);
            if (item == null)
                throw new KeyNotFoundException("Item not found");

            var progress = await _unitOfWork.EventProgress.GetByUserAndEventAsync(userId, eventId);
            if (progress == null)
                throw new KeyNotFoundException("Event progress not found");

            // Check purchase limit
            var purchased = progress.ShopPurchases.GetValueOrDefault(itemId, 0);
            if (item.PurchaseLimit > 0 && purchased + quantity > item.PurchaseLimit)
                throw new InvalidOperationException("Purchase limit exceeded");

            // Check currency
            var totalCost = new CurrencyAmount(item.CurrencyType, item.Price * quantity);
            var canAfford = await CheckCurrencyAsync(userId, totalCost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient currency");

            // Spend currency
            await SpendCurrencyAsync(userId, totalCost);

            // Grant item
            var rewards = new List<Reward>
            {
                new Reward { Type = item.RewardType, ResourceId = item.RewardId, Quantity = item.Quantity * quantity, IsGuaranteed = true }
            };
            await GrantRewardsAsync(userId, rewards);

            // Update purchase count
            progress.ShopPurchases[itemId] = purchased + quantity;
            await _unitOfWork.EventProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            return new ShopPurchaseResult
            {
                Success = true,
                ItemId = itemId,
                Quantity = quantity,
                CurrencySpent = totalCost,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                RemainingPurchases = item.PurchaseLimit > 0 ? item.PurchaseLimit - purchased - quantity : -1
            };
        }

        private GameEventDto MapToDto(GameEvent e, EventProgress userProgress)
        {
            var progress = userProgress?.Events.GetValueOrDefault(e.Id);
            return new GameEventDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                Type = e.Type,
                StartAt = e.StartAt,
                EndAt = e.EndAt,
                BannerImage = e.BannerImage,
                IsActive = e.IsActive(DateTime.UtcNow),
                CurrentPoints = progress?.TotalPoints ?? 0,
                CurrentStage = progress?.CurrentStage ?? e.Stages.OrderBy(s => s.Order).FirstOrDefault()?.Id
            };
        }

        private int CalculateStars(BattleResultData result, List<StarCondition> conditions)
        {
            int stars = 0;
            foreach (var condition in conditions)
            {
                if (CheckCondition(result, condition))
                    stars++;
            }
            return Math.Min(stars, 3);
        }

        private bool CheckCondition(BattleResultData result, StarCondition condition)
        {
            return condition.Type switch
            {
                StarConditionType.Win => result.Result == BattleResult.Win,
                StarConditionType.NoDeaths => result.PlayerDeaths == 0,
                StarConditionType.TurnLimit => result.TurnsTaken <= condition.Value,
                StarConditionType.TimeLimit => result.DurationSeconds <= condition.Value,
                _ => false
            };
        }

        private List<EventMilestone> CheckMilestones(EventProgress progress, GameEvent gameEvent)
        {
            var newMilestones = new List<EventMilestone>();

            foreach (var milestone in gameEvent.Milestones)
            {
                if (!progress.ClaimedMilestones.Contains(milestone.PointsThreshold) &&
                    progress.TotalPoints >= milestone.PointsThreshold)
                {
                    newMilestones.Add(milestone);
                }
            }

            return newMilestones;
        }

        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }
        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }

        private EventStageProgressDto MapToStageProgressDto(EventStageProgress sp) => new EventStageProgressDto
        {
            StageId = sp.StageId,
            Stars = sp.Stars,
            ClearCount = sp.ClearCount,
            BestTurns = sp.BestTurns,
            FirstClearedAt = sp.FirstClearedAt
        };

        private EventQuestProgressDto MapToQuestProgressDto(EventQuestProgress qp) => new EventQuestProgressDto
        {
            QuestId = qp.QuestId,
            Status = qp.Status,
            ObjectiveProgress = qp.ObjectiveProgress,
            CompletedAt = qp.CompletedAt
        };

        private MilestoneDto MapToMilestoneDto(EventMilestone m) => new MilestoneDto
        {
            PointsThreshold = m.PointsThreshold,
            Rewards = m.Rewards.Select(MapToRewardPreview).ToList()
        };

        private RewardPreviewDto MapToRewardPreview(Reward r) => new RewardPreviewDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity,
            Probability = r.Probability,
            IsGuaranteed = r.IsGuaranteed
        };

        private RewardDto MapToRewardDto(RewardPreviewDto r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
        private RewardDto MapToRewardDto(Reward r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
    }
}