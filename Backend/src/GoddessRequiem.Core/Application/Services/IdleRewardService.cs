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
    public class IdleRewardService : IIdleRewardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<IdleRewardService> _logger;
        private readonly IConfigurationService _configService;

        public IdleRewardService(IUnitOfWork unitOfWork, ILogger<IdleRewardService> logger, IConfigurationService configService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configService = configService;
        }

        public async Task<IdleRewardStatusDto> GetStatusAsync(Guid userId)
        {
            var idleReward = await _unitOfWork.IdleRewards.GetByUserIdAsync(userId);
            if (idleReward == null)
            {
                idleReward = new IdleReward { UserId = userId, LastClaimedAt = DateTime.UtcNow };
                await _unitOfWork.IdleRewards.AddAsync(idleReward);
                await _unitOfWork.SaveChangesAsync();
            }

            var config = await GetConfigAsync();
            var now = DateTime.UtcNow;
            var elapsed = now - idleReward.LastClaimedAt;
            var effectiveElapsed = elapsed > config.MaxAccumulationTime ? config.MaxAccumulationTime : elapsed;

            var rewards = CalculateAccumulatedRewards(idleReward, effectiveElapsed, config);

            return new IdleRewardStatusDto
            {
                IsAutoClaimEnabled = idleReward.AutoClaimEnabled,
                MaxAccumulationHours = (int)config.MaxAccumulationTime.TotalHours,
                CurrentAccumulationHours = (int)effectiveElapsed.TotalHours,
                LastClaimedAt = idleReward.LastClaimedAt,
                NextAutoClaimAt = idleReward.AutoClaimEnabled ? idleReward.LastClaimedAt.Add(config.AutoClaimInterval) : null,
                PendingRewards = rewards.Select(MapToRewardDto).ToList(),
                EstimatedRewardsPerHour = GetEstimatedRewardsPerHour(idleReward, config),
                CurrentStage = idleReward.CurrentStage,
                CampaignProgress = idleReward.CampaignProgress
            };
        }

        public async Task<IdleRewardSummary> ClaimRewardsAsync(Guid userId)
        {
            var idleReward = await _unitOfWork.IdleRewards.GetByUserIdAsync(userId);
            if (idleReward == null)
                return new IdleRewardSummary { Success = false, Error = "Idle reward not found" };

            var config = await GetConfigAsync();
            var now = DateTime.UtcNow;
            var elapsed = now - idleReward.LastClaimedAt;
            var effectiveElapsed = elapsed > config.MaxAccumulationTime ? config.MaxAccumulationTime : elapsed;

            if (effectiveElapsed < TimeSpan.FromMinutes(5))
                return new IdleRewardSummary { Success = false, Error = "Too soon to claim", MinimumIntervalMinutes = 5 };

            var rewards = CalculateAccumulatedRewards(idleReward, effectiveElapsed, config);

            // Grant rewards
            foreach (var reward in rewards)
            {
                await GrantRewardAsync(userId, reward);
            }

            // Update last claimed time
            idleReward.LastClaimedAt = now;
            idleReward.TotalClaims++;
            idleReward.TotalGoldClaimed += rewards.Where(r => r.Type == RewardType.Currency && r.ResourceId == CurrencyType.Gold).Sum(r => r.Quantity);
            idleReward.TotalExpClaimed += rewards.Where(r => r.Type == RewardType.Experience).Sum(r => r.Quantity);

            await _unitOfWork.IdleRewards.UpdateAsync(idleReward);
            await _unitOfWork.SaveChangesAsync();

            return new IdleRewardSummary
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                AccumulationTime = effectiveElapsed,
                GoldAmount = rewards.Where(r => r.Type == RewardType.Currency && r.ResourceId == CurrencyType.Gold).Sum(r => r.Quantity),
                ExperienceAmount = rewards.Where(r => r.Type == RewardType.Experience).Sum(r => r.Quantity),
                ItemRewards = rewards.Where(r => r.Type == RewardType.Item).Select(MapToRewardDto).ToList()
            };
        }

        public async Task<bool> SetAutoClaimAsync(Guid userId, bool enabled)
        {
            var idleReward = await _unitOfWork.IdleRewards.GetByUserIdAsync(userId);
            if (idleReward == null) return false;

            idleReward.AutoClaimEnabled = enabled;
            if (enabled)
            {
                idleReward.LastAutoClaimAt = DateTime.UtcNow;
            }

            await _unitOfWork.IdleRewards.UpdateAsync(idleReward);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> SetMaxAccumulationAsync(Guid userId, int hours)
        {
            if (hours < 1 || hours > 24)
                throw new ArgumentException("Max accumulation must be 1-24 hours");

            var idleReward = await _unitOfWork.IdleRewards.GetByUserIdAsync(userId);
            if (idleReward == null) return false;

            var config = await GetConfigAsync();
            // Note: This would typically be a global config, not per-user
            // But we can store user preference for notification timing
            idleReward.CustomMaxAccumulation = TimeSpan.FromHours(hours);

            await _unitOfWork.IdleRewards.UpdateAsync(idleReward);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<IdleRewardConfigDto> GetConfigAsync()
        {
            var config = await _configService.GetIdleRewardConfigAsync();
            return new IdleRewardConfigDto
            {
                BaseGoldPerHour = config.BaseGoldPerHour,
                BaseExpPerHour = config.BaseExpPerHour,
                GoldPerStage = config.GoldPerStage,
                ExpPerStage = config.ExpPerStage,
                ItemDropChance = config.ItemDropChance,
                EquipmentDropChance = config.EquipmentDropChance,
                RelicDropChance = config.RelicDropChance,
                MaxAccumulationHours = (int)config.MaxAccumulationTime.TotalHours,
                AutoClaimIntervalMinutes = (int)config.AutoClaimInterval.TotalMinutes,
                StageMultipliers = config.StageMultipliers,
                VipMultipliers = config.VipMultipliers
            };
        }

        public async Task ProcessAutoClaimsAsync()
        {
            var usersToClaim = await _unitOfWork.IdleRewards.GetUsersForAutoClaimAsync(DateTime.UtcNow);

            foreach (var idleReward in usersToClaim)
            {
                try
                {
                    var summary = await ClaimRewardsAsync(idleReward.UserId);
                    if (summary.Success)
                    {
                        idleReward.LastAutoClaimAt = DateTime.UtcNow;
                        await _unitOfWork.IdleRewards.UpdateAsync(idleReward);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process auto claim for user {UserId}", idleReward.UserId);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateCampaignProgressAsync(Guid userId, int stageId, int chapter)
        {
            var idleReward = await _unitOfWork.IdleRewards.GetByUserIdAsync(userId);
            if (idleReward == null) return;

            idleReward.CurrentStage = stageId;
            idleReward.CampaignProgress = chapter;
            await _unitOfWork.IdleRewards.UpdateAsync(idleReward);
            await _unitOfWork.SaveChangesAsync();
        }

        private List<Reward> CalculateAccumulatedRewards(IdleReward idleReward, TimeSpan elapsed, IdleRewardConfig config)
        {
            var rewards = new List<Reward>();
            var hours = (float)elapsed.TotalHours;

            // Get stage multiplier
            var stageMultiplier = config.StageMultipliers.GetValueOrDefault(idleReward.CurrentStage, 1f);

            // Get VIP multiplier (would fetch user VIP level)
            var vipMultiplier = 1f; // config.VipMultipliers.GetValueOrDefault(userVipLevel, 1f);

            var totalMultiplier = stageMultiplier * vipMultiplier;

            // Gold
            var goldAmount = (long)(config.BaseGoldPerHour * hours * totalMultiplier);
            if (goldAmount > 0)
            {
                rewards.Add(new Reward
                {
                    Type = RewardType.Currency,
                    ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"), // Gold
                    Quantity = goldAmount,
                    IsGuaranteed = true
                });
            }

            // Experience
            var expAmount = (long)(config.BaseExpPerHour * hours * totalMultiplier);
            if (expAmount > 0)
            {
                rewards.Add(new Reward
                {
                    Type = RewardType.Experience,
                    Quantity = expAmount,
                    IsGuaranteed = true
                });
            }

            // Item drops (based on probability per hour)
            var itemRolls = (int)(hours * 4); // 4 rolls per hour
            for (int i = 0; i < itemRolls; i++)
            {
                var roll = new Random().NextDouble();

                if (roll < config.EquipmentDropChance)
                {
                    var equipment = GetRandomEquipmentDrop();
                    rewards.Add(new Reward
                    {
                        Type = RewardType.Item,
                        ResourceId = equipment.Id,
                        Quantity = 1,
                        IsGuaranteed = false
                    });
                }
                else if (roll < config.EquipmentDropChance + config.RelicDropChance)
                {
                    var relic = GetRandomRelicDrop();
                    rewards.Add(new Reward
                    {
                        Type = RewardType.Item,
                        ResourceId = relic.Id,
                        Quantity = 1,
                        IsGuaranteed = false
                    });
                }
                else if (roll < config.EquipmentDropChance + config.RelicDropChance + config.ItemDropChance)
                {
                    var material = GetRandomMaterialDrop();
                    rewards.Add(new Reward
                    {
                        Type = RewardType.Item,
                        ResourceId = material.Id,
                        Quantity = new Random().Next(1, 5),
                        IsGuaranteed = false
                    });
                }
            }

            return rewards;
        }

        private EstimatedRewardsDto GetEstimatedRewardsPerHour(IdleReward idleReward, IdleRewardConfig config)
        {
            var stageMultiplier = config.StageMultipliers.GetValueOrDefault(idleReward.CurrentStage, 1f);
            var vipMultiplier = 1f;
            var totalMultiplier = stageMultiplier * vipMultiplier;

            return new EstimatedRewardsDto
            {
                GoldPerHour = (long)(config.BaseGoldPerHour * totalMultiplier),
                ExpPerHour = (long)(config.BaseExpPerHour * totalMultiplier),
                ItemDropRatePerHour = config.ItemDropChance * 4,
                EquipmentDropRatePerHour = config.EquipmentDropChance * 4,
                RelicDropRatePerHour = config.RelicDropChance * 4
            };
        }

        private Guid GetRandomEquipmentDrop() => Guid.NewGuid();
        private Guid GetRandomRelicDrop() => Guid.NewGuid();
        private Guid GetRandomMaterialDrop() => Guid.NewGuid();

        private async Task GrantRewardAsync(Guid userId, Reward reward)
        {
            switch (reward.Type)
            {
                case RewardType.Currency:
                    await AddCurrencyAsync(userId, reward.ResourceId, reward.Quantity);
                    break;
                case RewardType.Item:
                    await AddItemAsync(userId, reward.ResourceId, reward.Quantity);
                    break;
                case RewardType.Experience:
                    await AddExperienceAsync(userId, reward.Quantity);
                    break;
            }
        }

        private RewardDto MapToRewardDto(Reward r) => new RewardDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity
        };

        private async Task AddCurrencyAsync(Guid userId, Guid currencyId, int amount) { }
        private async Task AddItemAsync(Guid userId, Guid itemId, int quantity) { }
        private async Task AddExperienceAsync(Guid userId, int amount) { }
    }
}