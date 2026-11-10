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
    public class DungeonService : IDungeonService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DungeonService> _logger;
        private readonly IBattleService _battleService;

        public DungeonService(IUnitOfWork unitOfWork, ILogger<DungeonService> logger, IBattleService battleService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleService = battleService;
        }

        public async Task<IReadOnlyList<DungeonDto>> GetAvailableDungeonsAsync(Guid userId)
        {
            var dungeons = await _unitOfWork.Dungeons.GetAvailableAsync(DateTime.UtcNow);
            var progress = await _unitOfWork.DungeonProgress.GetByUserIdAsync(userId);

            return dungeons.Select(d => MapToDto(d, progress)).ToList();
        }

        public async Task<DungeonDetailDto> GetDungeonDetailAsync(Guid userId, Guid dungeonId)
        {
            var dungeon = await _unitOfWork.Dungeons.GetByIdAsync(dungeonId);
            if (dungeon == null) return null;

            var progress = await _unitOfWork.DungeonProgress.GetByUserIdAsync(userId);
            var userProgress = progress?.Dungeons.GetValueOrDefault(dungeonId);

            return new DungeonDetailDto
            {
                Id = dungeon.Id,
                Name = dungeon.Name,
                Description = dungeon.Description,
                Element = dungeon.Element,
                Type = dungeon.Type,
                Tiers = dungeon.Tiers.Select(t => MapToTierDto(t, userProgress)).ToList(),
                DailyEntryLimit = dungeon.DailyEntryLimit,
                WeeklyEntryLimit = dungeon.WeeklyEntryLimit,
                ResetTime = dungeon.ResetTime,
                EntriesUsedToday = userProgress?.EntriesUsedToday ?? 0,
                EntriesUsedThisWeek = userProgress?.EntriesUsedThisWeek ?? 0
            };
        }

        public async Task<DungeonClearResult> ClearDungeonAsync(Guid userId, Guid dungeonId, int tier, BattleResultData result)
        {
            var dungeon = await _unitOfWork.Dungeons.GetByIdAsync(dungeonId);
            if (dungeon == null)
                throw new KeyNotFoundException("Dungeon not found");

            var tierData = dungeon.Tiers.FirstOrDefault(t => t.Tier == tier);
            if (tierData == null)
                throw new KeyNotFoundException("Tier not found");

            var progress = await _unitOfWork.DungeonProgress.GetByUserIdAsync(userId);
            if (progress == null)
            {
                progress = new DungeonProgress { UserId = userId };
                await _unitOfWork.DungeonProgress.AddAsync(progress);
            }

            // Check entry limits
            var userDungeonProgress = progress.GetOrCreateDungeonProgress(dungeonId);
            if (userDungeonProgress.EntriesUsedToday >= dungeon.DailyEntryLimit)
                throw new InvalidOperationException("Daily entry limit reached");

            if (userDungeonProgress.EntriesUsedThisWeek >= dungeon.WeeklyEntryLimit)
                throw new InvalidOperationException("Weekly entry limit reached");

            // Check tier unlocked
            if (tier > userDungeonProgress.HighestClearedTier + 1)
                throw new InvalidOperationException("Previous tier not cleared");

            // Consume entry
            userDungeonProgress.EntriesUsedToday++;
            userDungeonProgress.EntriesUsedThisWeek++;
            userDungeonProgress.LastEntryAt = DateTime.UtcNow;

            var isFirstClear = tier > userDungeonProgress.HighestClearedTier;
            var starsEarned = CalculateStars(result, tierData.StarConditions);

            // Update clear data
            var tierProgress = userDungeonProgress.GetOrCreateTierProgress(tier);
            tierProgress.UpdateClear(result, starsEarned, isFirstClear);

            if (isFirstClear)
            {
                userDungeonProgress.HighestClearedTier = tier;
            }

            // Grant rewards
            var rewards = new List<Reward>();

            if (isFirstClear)
            {
                rewards.AddRange(tierData.FirstClearRewards);
            }

            rewards.AddRange(tierData.Rewards);

            // Bonus for stars
            if (starsEarned == 3)
            {
                rewards.AddRange(tierData.ThreeStarBonusRewards);
            }

            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.DungeonProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            return new DungeonClearResult
            {
                Success = true,
                DungeonId = dungeonId,
                Tier = tier,
                StarsEarned = starsEarned,
                IsFirstClear = isFirstClear,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                NextTierUnlocked = isFirstClear && tier < dungeon.Tiers.Max(t => t.Tier) ? tier + 1 : (int?)null
            };
        }

        public async Task<RewardResult> ClaimStarRewardsAsync(Guid userId, Guid dungeonId, int tier)
        {
            var progress = await _unitOfWork.DungeonProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Dungeon progress not found");

            var userDungeonProgress = progress.Dungeons.GetValueOrDefault(dungeonId);
            if (userDungeonProgress == null)
                throw new KeyNotFoundException("Dungeon progress not found");

            var tierProgress = userDungeonProgress.Tiers.GetValueOrDefault(tier);
            if (tierProgress == null || tierProgress.StarRewardsClaimed)
                throw new InvalidOperationException("Star rewards not available or already claimed");

            var dungeon = await _unitOfWork.Dungeons.GetByIdAsync(dungeonId);
            var tierData = dungeon?.Tiers.FirstOrDefault(t => t.Tier == tier);
            if (tierData == null)
                throw new KeyNotFoundException("Tier not found");

            // Check if all stars earned
            if (tierProgress.TotalStars < 3)
                throw new InvalidOperationException("Not all stars earned");

            tierProgress.StarRewardsClaimed = true;
            await _unitOfWork.DungeonProgress.UpdateAsync(progress);

            var rewards = tierData.StarRewards;
            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new RewardResult
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList()
            };
        }

        private DungeonDto MapToDto(Dungeon d, DungeonProgress progress)
        {
            var userProgress = progress?.Dungeons.GetValueOrDefault(d.Id);
            return new DungeonDto
            {
                Id = d.Id,
                Name = d.Name,
                Element = d.Element,
                Type = d.Type,
                MaxTier = d.Tiers.Max(t => t.Tier),
                HighestClearedTier = userProgress?.HighestClearedTier ?? 0,
                DailyEntryLimit = d.DailyEntryLimit,
                EntriesUsedToday = userProgress?.EntriesUsedToday ?? 0,
                IsAvailable = d.IsAvailable(DateTime.UtcNow),
                RecommendedPower = d.Tiers.Min(t => t.RecommendedPower)
            };
        }

        private DungeonTierDto MapToTierDto(DungeonTier t, DungeonProgress.DungeonProgressData userProgress)
        {
            var tierProgress = userProgress?.Tiers.GetValueOrDefault(t.Tier);
            return new DungeonTierDto
            {
                Tier = t.Tier,
                Name = t.Name,
                RecommendedPower = t.RecommendedPower,
                EnergyCost = t.EnergyCost,
                Enemies = t.Enemies.Select(MapToEnemyInfo).ToList(),
                Rewards = t.Rewards.Select(MapToRewardPreview).ToList(),
                FirstClearRewards = t.FirstClearRewards.Select(MapToRewardPreview).ToList(),
                StarConditions = t.StarConditions.Select(MapToStarCondition).ToList(),
                IsUnlocked = tierProgress != null || t.Tier == 1 || (userProgress?.HighestClearedTier ?? 0) >= t.Tier - 1,
                IsCleared = tierProgress?.IsCleared ?? false,
                BestStars = tierProgress?.BestStars ?? 0,
                StarRewardsClaimed = tierProgress?.StarRewardsClaimed ?? false
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

        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private EnemyInfoDto MapToEnemyInfo(DungeonEnemy e) => new EnemyInfoDto
        {
            Id = e.EnemyId,
            Name = e.Name,
            Level = e.Level,
            Element = e.Element,
            HP = e.HP,
            Attack = e.Attack,
            Skills = e.Skills.Select(s => s.ToString()).ToList()
        };

        private RewardPreviewDto MapToRewardPreview(Reward r) => new RewardPreviewDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity,
            Probability = r.Probability,
            IsGuaranteed = r.IsGuaranteed
        };

        private StarConditionDto MapToStarCondition(StarCondition c) => new StarConditionDto
        {
            Type = c.Type,
            Description = c.Description,
            Value = c.Value
        };

        private RewardDto MapToRewardDto(RewardPreviewDto r) => new RewardDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity
        };
    }
}