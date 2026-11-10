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
    public class QuestService : IQuestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<QuestService> _logger;

        public QuestService(IUnitOfWork unitOfWork, ILogger<QuestService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IReadOnlyList<UserQuestDto>> GetActiveQuestsAsync(Guid userId)
        {
            var quests = await _unitOfWork.UserQuests.GetActiveByUserIdAsync(userId);
            return quests.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UserQuestDto>> GetAvailableQuestsAsync(Guid userId)
        {
            var allQuests = await _unitOfWork.Quests.GetAvailableAsync(DateTime.UtcNow);
            var userQuests = await _unitOfWork.UserQuests.GetByUserIdAsync(userId);
            var acceptedIds = userQuests.Select(uq => uq.QuestId).ToHashSet();
            var completedIds = userQuests.Where(uq => uq.Status == QuestStatus.Completed).Select(uq => uq.QuestId).ToHashSet();

            return allQuests
                .Where(q => !acceptedIds.Contains(q.Id) && !completedIds.Contains(q.Id))
                .Where(q => IsQuestUnlocked(q, userId).Result)
                .Select(q => new UserQuestDto
                {
                    QuestId = q.Id,
                    QuestName = q.Name,
                    Description = q.Description,
                    Type = q.Type,
                    Status = QuestStatus.Available,
                    Objectives = q.Objectives.Select(MapToObjectiveDto).ToList(),
                    Rewards = q.Rewards.Select(MapToRewardPreview).ToList(),
                    ExpiresAt = q.ExpiresAt
                }).ToList();
        }

        public async Task<QuestAcceptResult> AcceptQuestAsync(Guid userId, Guid questId)
        {
            var quest = await _unitOfWork.Quests.GetByIdAsync(questId);
            if (quest == null)
                throw new KeyNotFoundException("Quest not found");

            if (!quest.IsAvailable(DateTime.UtcNow))
                throw new InvalidOperationException("Quest not available");

            var existing = await _unitOfWork.UserQuests.GetByUserAndQuestAsync(userId, questId);
            if (existing != null)
                throw new InvalidOperationException("Quest already accepted or completed");

            var unlocked = await IsQuestUnlocked(quest, userId);
            if (!unlocked)
                throw new InvalidOperationException("Quest not unlocked");

            var userQuest = new UserQuest
            {
                UserId = userId,
                QuestId = questId,
                Status = QuestStatus.InProgress,
                StartedAt = DateTime.UtcNow,
                ObjectiveProgress = quest.Objectives.ToDictionary(o => o.Id, o => 0)
            };

            await _unitOfWork.UserQuests.AddAsync(userQuest);
            await _unitOfWork.SaveChangesAsync();

            return new QuestAcceptResult
            {
                Success = true,
                QuestId = questId,
                Objectives = quest.Objectives.Select(MapToObjectiveDto).ToList()
            };
        }

        public async Task<QuestProgressResult> UpdateProgressAsync(Guid userId, Guid questId, string objectiveId, int amount)
        {
            var userQuest = await _unitOfWork.UserQuests.GetByUserAndQuestAsync(userId, questId);
            if (userQuest == null)
                throw new KeyNotFoundException("Quest not accepted");

            if (userQuest.Status != QuestStatus.InProgress)
                throw new InvalidOperationException("Quest not in progress");

            var quest = await _unitOfWork.Quests.GetByIdAsync(questId);
            if (quest == null)
                throw new KeyNotFoundException("Quest not found");

            var objective = quest.Objectives.FirstOrDefault(o => o.Id == objectiveId);
            if (objective == null)
                throw new KeyNotFoundException("Objective not found");

            var currentProgress = userQuest.ObjectiveProgress.GetValueOrDefault(objectiveId, 0);
            var newProgress = Math.Min(currentProgress + amount, objective.RequiredAmount);
            userQuest.ObjectiveProgress[objectiveId] = newProgress;

            // Check if all objectives completed
            var allCompleted = quest.Objectives.All(o => userQuest.ObjectiveProgress.GetValueOrDefault(o.Id, 0) >= o.RequiredAmount);

            if (allCompleted)
            {
                userQuest.Status = QuestStatus.Completed;
                userQuest.CompletedAt = DateTime.UtcNow;
            }

            await _unitOfWork.UserQuests.UpdateAsync(userQuest);
            await _unitOfWork.SaveChangesAsync();

            return new QuestProgressResult
            {
                QuestId = questId,
                ObjectiveId = objectiveId,
                CurrentProgress = newProgress,
                RequiredAmount = objective.RequiredAmount,
                IsCompleted = newProgress >= objective.RequiredAmount,
                QuestCompleted = allCompleted
            };
        }

        public async Task<ClaimResult> ClaimQuestRewardAsync(Guid userId, Guid questId)
        {
            var userQuest = await _unitOfWork.UserQuests.GetByUserAndQuestAsync(userId, questId);
            if (userQuest == null)
                throw new KeyNotFoundException("Quest not found");

            if (userQuest.Status != QuestStatus.Completed)
                throw new InvalidOperationException("Quest not completed");

            if (userQuest.RewardsClaimed)
                throw new InvalidOperationException("Rewards already claimed");

            var quest = await _unitOfWork.Quests.GetByIdAsync(questId);
            var rewards = quest?.Rewards ?? new List<Reward>();

            await GrantRewardsAsync(userId, rewards);

            userQuest.RewardsClaimed = true;
            userQuest.Status = QuestStatus.RewardsClaimed;

            await _unitOfWork.UserQuests.UpdateAsync(userQuest);
            await _unitOfWork.SaveChangesAsync();

            return new ClaimResult
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<DailyQuestResult> RefreshDailyQuestsAsync(Guid userId)
        {
            var userQuests = await _unitOfWork.UserQuests.GetByUserIdAsync(userId);
            var dailyQuests = userQuests.Where(uq => uq.QuestType == QuestType.Daily).ToList();

            // Reset completed daily quests
            foreach (var uq in dailyQuests.Where(q => q.Status == QuestStatus.RewardsClaimed))
            {
                uq.Status = QuestStatus.Available;
                uq.ObjectiveProgress.Clear();
                uq.RewardsClaimed = false;
                uq.StartedAt = null;
                uq.CompletedAt = null;
            }

            // Get new daily quests if needed
            var availableDailies = await _unitOfWork.Quests.GetDailyQuestsAsync(DateTime.UtcNow);
            var currentQuestIds = dailyQuests.Select(dq => dq.QuestId).ToHashSet();

            var newQuests = availableDailies.Where(q => !currentQuestIds.Contains(q.Id)).Take(3 - dailyQuests.Count(q => q.Status != QuestStatus.RewardsClaimed)).ToList();

            foreach (var quest in newQuests)
            {
                var userQuest = new UserQuest
                {
                    UserId = userId,
                    QuestId = quest.Id,
                    QuestType = QuestType.Daily,
                    Status = QuestStatus.Available,
                    ObjectiveProgress = quest.Objectives.ToDictionary(o => o.Id, o => 0)
                };
                await _unitOfWork.UserQuests.AddAsync(userQuest);
            }

            await _unitOfWork.SaveChangesAsync();

            var updatedQuests = await GetActiveQuestsAsync(userId);

            return new DailyQuestResult
            {
                Success = true,
                Quests = updatedQuests.Where(q => q.Type == QuestType.Daily).ToList(),
                RefreshedAt = DateTime.UtcNow
            };
        }

        private async Task<bool> IsQuestUnlocked(Quest quest, Guid userId)
        {
            if (quest.UnlockConditions == null || !quest.UnlockConditions.Any())
                return true;

            foreach (var condition in quest.UnlockConditions)
            {
                switch (condition.Type)
                {
                    case QuestUnlockType.CampaignStage:
                        var stageCleared = await _unitOfWork.CampaignProgress.IsStageClearedAsync(userId, condition.Value);
                        if (!stageCleared) return false;
                        break;
                    case QuestUnlockType.PlayerLevel:
                        var user = await _unitOfWork.Users.GetByIdAsync(userId);
                        if (user?.Level < condition.Value) return false;
                        break;
                    case QuestUnlockType.QuestCompleted:
                        var prevQuest = await _unitOfWork.UserQuests.GetByUserAndQuestAsync(userId, Guid.Parse(condition.Value.ToString()));
                        if (prevQuest == null || prevQuest.Status != QuestStatus.RewardsClaimed) return false;
                        break;
                }
            }

            return true;
        }

        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private UserQuestDto MapToDto(UserQuest uq) => new UserQuestDto
        {
            QuestId = uq.QuestId,
            QuestName = uq.QuestId.ToString(), // Would get actual name
            Description = "",
            Type = uq.QuestType,
            Status = uq.Status,
            Objectives = uq.ObjectiveProgress.Select(kvp => new QuestObjectiveDto
            {
                Id = kvp.Key,
                Description = "",
                CurrentProgress = kvp.Value,
                RequiredAmount = 0,
                IsCompleted = false
            }).ToList(),
            Rewards = new List<RewardPreviewDto>(),
            StartedAt = uq.StartedAt,
            CompletedAt = uq.CompletedAt
        };

        private QuestObjectiveDto MapToObjectiveDto(QuestObjective o) => new QuestObjectiveDto
        {
            Id = o.Id,
            Description = o.Description,
            CurrentProgress = 0,
            RequiredAmount = o.RequiredAmount,
            IsCompleted = false
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