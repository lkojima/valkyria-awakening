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
    public class ExpeditionService : IExpeditionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExpeditionService> _logger;

        public ExpeditionService(IUnitOfWork unitOfWork, ILogger<ExpeditionService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ExpeditionDto>> GetAvailableExpeditionsAsync(Guid userId)
        {
            var expeditions = await _unitOfWork.Expeditions.GetAvailableAsync(DateTime.UtcNow);
            var userExpeditions = await _unitOfWork.UserExpeditions.GetActiveByUserIdAsync(userId);
            var activeIds = userExpeditions.Select(ue => ue.ExpeditionId).ToHashSet();

            return expeditions.Select(e => MapToDto(e, activeIds.Contains(e.Id))).ToList();
        }

        public async Task<ExpeditionStartResult> StartExpeditionAsync(Guid userId, Guid expeditionId, List<Guid> characterIds, bool autoRepeat, int maxRepeats)
        {
            var expedition = await _unitOfWork.Expeditions.GetByIdAsync(expeditionId);
            if (expedition == null)
                throw new KeyNotFoundException("Expedition not found");

            if (!expedition.IsAvailable(DateTime.UtcNow))
                throw new InvalidOperationException("Expedition not available");

            // Validate characters
            if (characterIds.Count < expedition.MinCharacters || characterIds.Count > expedition.MaxCharacters)
                throw new ArgumentException($"Expedition requires {expedition.MinCharacters}-{expedition.MaxCharacters} characters");

            var characters = await _unitOfWork.UserCharacters.GetByIdsAsync(characterIds);
            if (characters.Any(c => c.UserId != userId))
                throw new UnauthorizedAccessException("Character not owned");

            // Check if characters are already on expedition
            var busyCharacters = await _unitOfWork.UserExpeditions.GetBusyCharacterIdsAsync(userId);
            if (characterIds.Any(id => busyCharacters.Contains(id)))
                throw new InvalidOperationException("Some characters are already on expedition");

            // Check party power requirement
            var totalPower = characters.Sum(c => c.CalculatePower());
            if (totalPower < expedition.RequiredPower)
                throw new InvalidOperationException("Insufficient party power");

            // Check expedition slot availability
            var activeCount = await _unitOfWork.UserExpeditions.GetActiveCountAsync(userId);
            var maxSlots = GetMaxExpeditionSlots(userId); // Would get from VIP
            if (activeCount >= maxSlots)
                throw new InvalidOperationException("Maximum expedition slots reached");

            // Create user expedition
            var userExpedition = new UserExpedition
            {
                UserId = userId,
                ExpeditionId = expeditionId,
                CharacterIds = characterIds,
                StartedAt = DateTime.UtcNow,
                Duration = expedition.Duration,
                AutoRepeat = autoRepeat,
                MaxRepeats = maxRepeats,
                CurrentRepeat = 1,
                Status = ExpeditionStatus.InProgress
            };

            await _unitOfWork.UserExpeditions.AddAsync(userExpedition);
            await _unitOfWork.SaveChangesAsync();

            // Schedule completion
            await ScheduleCompletionAsync(userExpedition);

            return new ExpeditionStartResult
            {
                Success = true,
                UserExpeditionId = userExpedition.Id,
                ExpeditionId = expeditionId,
                StartedAt = userExpedition.StartedAt,
                EstimatedCompletionAt = userExpedition.StartedAt.Add(expedition.Duration),
                AutoRepeat = autoRepeat,
                MaxRepeats = maxRepeats
            };
        }

        public async Task<ExpeditionCompleteResult> CompleteExpeditionAsync(Guid userId, Guid userExpeditionId)
        {
            var userExpedition = await _unitOfWork.UserExpeditions.GetByIdAsync(userExpeditionId);
            if (userExpedition == null || userExpedition.UserId != userId)
                throw new KeyNotFoundException("Expedition not found");

            if (userExpedition.Status != ExpeditionStatus.InProgress && userExpedition.Status != ExpeditionStatus.Completed)
                throw new InvalidOperationException("Expedition not in progress");

            var expedition = await _unitOfWork.Expeditions.GetByIdAsync(userExpedition.ExpeditionId);
            if (expedition == null)
                throw new KeyNotFoundException("Expedition not found");

            var now = DateTime.UtcNow;
            if (now < userExpedition.StartedAt.Add(userExpedition.Duration))
                throw new InvalidOperationException("Expedition not yet completed");

            // Calculate rewards
            var rewards = CalculateRewards(expedition, userExpedition);

            // Update status
            userExpedition.Status = ExpeditionStatus.Completed;
            userExpedition.CompletedAt = now;
            userExpedition.Rewards = rewards;

            // Handle auto-repeat
            if (userExpedition.AutoRepeat && userExpedition.CurrentRepeat < userExpedition.MaxRepeats)
            {
                userExpedition.CurrentRepeat++;
                userExpedition.StartedAt = now;
                userExpedition.Status = ExpeditionStatus.InProgress;
                userExpedition.CompletedAt = null;
                userExpedition.Rewards = new List<Reward>();

                await ScheduleCompletionAsync(userExpedition);
            }

            await _unitOfWork.UserExpeditions.UpdateAsync(userExpedition);
            await _unitOfWork.SaveChangesAsync();

            return new ExpeditionCompleteResult
            {
                Success = true,
                UserExpeditionId = userExpeditionId,
                ExpeditionId = expeditionId,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                AutoRepeatTriggered = userExpedition.AutoRepeat && userExpedition.CurrentRepeat <= userExpedition.MaxRepeats,
                NextRepeatAt = userExpedition.AutoRepeat && userExpedition.CurrentRepeat <= userExpedition.MaxRepeats ? now.Add(expedition.Duration) : null
            };
        }

        public async Task<ClaimResult> ClaimExpeditionRewardsAsync(Guid userId, Guid userExpeditionId)
        {
            var userExpedition = await _unitOfWork.UserExpeditions.GetByIdAsync(userExpeditionId);
            if (userExpedition == null || userExpedition.UserId != userId)
                throw new KeyNotFoundException("Expedition not found");

            if (userExpedition.Status != ExpeditionStatus.Completed && userExpedition.Status != ExpeditionStatus.RewardsClaimed)
                throw new InvalidOperationException("No rewards to claim");

            if (userExpedition.RewardsClaimed)
                throw new InvalidOperationException("Rewards already claimed");

            var rewards = userExpedition.Rewards;
            await GrantRewardsAsync(userId, rewards);

            userExpedition.RewardsClaimed = true;
            userExpedition.Status = ExpeditionStatus.RewardsClaimed;

            // If auto-repeat and more repeats available, start next
            if (userExpedition.AutoRepeat && userExpedition.CurrentRepeat < userExpedition.MaxRepeats)
            {
                userExpedition.CurrentRepeat++;
                userExpedition.StartedAt = DateTime.UtcNow;
                userExpedition.Status = ExpeditionStatus.InProgress;
                userExpedition.Rewards = new List<Reward>();
                userExpedition.RewardsClaimed = false;

                var expedition = await _unitOfWork.Expeditions.GetByIdAsync(userExpedition.ExpeditionId);
                await ScheduleCompletionAsync(userExpedition);
            }

            await _unitOfWork.UserExpeditions.UpdateAsync(userExpedition);
            await _unitOfWork.SaveChangesAsync();

            return new ClaimResult
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                AutoRepeatContinued = userExpedition.AutoRepeat && userExpedition.CurrentRepeat <= userExpedition.MaxRepeats
            };
        }

        public async Task<IReadOnlyList<UserExpeditionDto>> GetActiveExpeditionsAsync(Guid userId)
        {
            var expeditions = await _unitOfWork.UserExpeditions.GetActiveByUserIdAsync(userId);
            return expeditions.Select(MapToUserExpeditionDto).ToList();
        }

        public async Task<IReadOnlyList<UserExpeditionDto>> GetCompletedExpeditionsAsync(Guid userId)
        {
            var expeditions = await _unitOfWork.UserExpeditions.GetCompletedByUserIdAsync(userId, 20);
            return expeditions.Select(MapToUserExpeditionDto).ToList();
        }

        public async Task<bool> CancelExpeditionAsync(Guid userId, Guid userExpeditionId)
        {
            var userExpedition = await _unitOfWork.UserExpeditions.GetByIdAsync(userExpeditionId);
            if (userExpedition == null || userExpedition.UserId != userId)
                return false;

            if (userExpedition.Status != ExpeditionStatus.InProgress)
                return false;

            userExpedition.Status = ExpeditionStatus.Cancelled;
            userExpedition.CancelledAt = DateTime.UtcNow;

            await _unitOfWork.UserExpeditions.UpdateAsync(userExpedition);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task ProcessExpeditionsAsync()
        {
            var now = DateTime.UtcNow;
            var completedExpeditions = await _unitOfWork.UserExpeditions.GetReadyToCompleteAsync(now);

            foreach (var ue in completedExpeditions)
            {
                if (ue.Status == ExpeditionStatus.InProgress)
                {
                    ue.Status = ExpeditionStatus.Completed;
                    ue.CompletedAt = now;

                    var expedition = await _unitOfWork.Expeditions.GetByIdAsync(ue.ExpeditionId);
                    ue.Rewards = CalculateRewards(expedition, ue);

                    await _unitOfWork.UserExpeditions.UpdateAsync(ue);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private List<Reward> CalculateRewards(Expedition expedition, UserExpedition userExpedition)
        {
            var rewards = new List<Reward>();

            // Base rewards
            rewards.AddRange(expedition.BaseRewards);

            // Bonus for character synergy
            var synergyBonus = CalculateSynergyBonus(userExpedition.CharacterIds, expedition);
            if (synergyBonus > 0)
            {
                rewards.Add(new Reward
                {
                    Type = RewardType.Currency,
                    ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Quantity = (int)(expedition.BaseRewards.FirstOrDefault(r => r.Type == RewardType.Currency)?.Quantity * synergyBonus ?? 0),
                    IsGuaranteed = true
                });
            }

            // Rare drops
            foreach (var rareDrop in expedition.RareDrops)
            {
                if (new Random().NextDouble() < rareDrop.Probability)
                {
                    rewards.Add(new Reward
                    {
                        Type = RewardType.Item,
                        ResourceId = rareDrop.ItemId,
                        Quantity = rareDrop.Quantity,
                        IsGuaranteed = false
                    });
                }
            }

            return rewards;
        }

        private float CalculateSynergyBonus(List<Guid> characterIds, Expedition expedition)
        {
            // Check for element/role synergy
            return 0f; // Simplified
        }

        private int GetMaxExpeditionSlots(Guid userId) => 3; // Would get from VIP

        private async Task ScheduleCompletionAsync(UserExpedition userExpedition)
        {
            // Would schedule background job or use timer
            await Task.CompletedTask;
        }

        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private ExpeditionDto MapToDto(Expedition e, bool isActive) => new ExpeditionDto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Type = e.Type,
            MinCharacters = e.MinCharacters,
            MaxCharacters = e.MaxCharacters,
            RequiredPower = e.RequiredPower,
            Duration = e.Duration,
            BaseRewards = e.BaseRewards.Select(MapToRewardPreview).ToList(),
            RareDrops = e.RareDrops.Select(MapToRareDropDto).ToList(),
            IsAvailable = e.IsAvailable(DateTime.UtcNow),
            IsActive = isActive,
            UnlockCondition = e.UnlockCondition
        };

        private UserExpeditionDto MapToUserExpeditionDto(UserExpedition ue) => new UserExpeditionDto
        {
            Id = ue.Id,
            ExpeditionId = ue.ExpeditionId,
            ExpeditionName = ue.ExpeditionId.ToString(), // Would get actual name
            CharacterIds = ue.CharacterIds,
            Status = ue.Status,
            StartedAt = ue.StartedAt,
            CompletedAt = ue.CompletedAt,
            Duration = ue.Duration,
            Progress = ue.Status == ExpeditionStatus.InProgress ? (float)(DateTime.UtcNow - ue.StartedAt).TotalSeconds / (float)ue.Duration.TotalSeconds : 1f,
            AutoRepeat = ue.AutoRepeat,
            CurrentRepeat = ue.CurrentRepeat,
            MaxRepeats = ue.MaxRepeats,
            Rewards = ue.Rewards?.Select(MapToRewardDto).ToList() ?? new List<RewardDto>(),
            RewardsClaimed = ue.RewardsClaimed
        };

        private RewardPreviewDto MapToRewardPreview(Reward r) => new RewardPreviewDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity,
            Probability = r.Probability,
            IsGuaranteed = r.IsGuaranteed
        };

        private RareDropDto MapToRareDropDto(ExpeditionRareDrop r) => new RareDropDto
        {
            ItemId = r.ItemId,
            Quantity = r.Quantity,
            Probability = r.Probability
        };

        private RewardDto MapToRewardDto(RewardPreviewDto r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
        private RewardDto MapToRewardDto(Reward r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
    }
}