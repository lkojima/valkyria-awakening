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
    public class TowerService : ITowerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TowerService> _logger;
        private readonly IBattleService _battleService;

        public TowerService(IUnitOfWork unitOfWork, ILogger<TowerService> logger, IBattleService battleService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleService = battleService;
        }

        public async Task<TowerProgressDto> GetProgressAsync(Guid userId)
        {
            var progress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
            if (progress == null)
            {
                progress = new TowerProgress { UserId = userId, CurrentFloor = 1, Season = GetCurrentSeason() };
                await _unitOfWork.TowerProgress.AddAsync(progress);
                await _unitOfWork.SaveChangesAsync();
            }

            var clearedFloors = await _unitOfWork.TowerProgress.GetClearedFloorsAsync(userId, progress.Season);
            var claimedFloorRewards = await _unitOfWork.TowerProgress.GetClaimedFloorRewardsAsync(userId, progress.Season);
            var claimedStarRewards = await _unitOfWork.TowerProgress.GetClaimedStarRewardsAsync(userId, progress.Season);

            return new TowerProgressDto
            {
                Season = progress.Season,
                CurrentFloor = progress.CurrentFloor,
                HighestFloorReached = progress.HighestFloorReached,
                ClearedFloors = clearedFloors.Select(MapToFloorClearDto).ToList(),
                ClaimedFloorRewards = claimedFloorRewards,
                ClaimedStarRewards = claimedStarRewards,
                TotalStars = clearedFloors.Sum(f => f.Stars),
                RemainingTickets = progress.Tickets,
                LastResetAt = progress.LastSeasonReset,
                NextResetAt = GetNextSeasonReset()
            };
        }

        public async Task<TowerFloorDto> GetFloorDetailAsync(Guid userId, int floor)
        {
            var towerFloor = await _unitOfWork.TowerFloors.GetByFloorAsync(floor);
            if (towerFloor == null) return null;

            var progress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
            var clearData = await _unitOfWork.TowerProgress.GetFloorClearDataAsync(userId, floor, progress?.Season ?? GetCurrentSeason());

            return new TowerFloorDto
            {
                Floor = floor,
                Name = towerFloor.Name,
                Description = towerFloor.Description,
                RecommendedPower = towerFloor.RecommendedPower,
                Enemies = towerFloor.Enemies.Select(MapToEnemyInfo).ToList(),
                Rewards = towerFloor.Rewards.Select(MapToRewardPreview).ToList(),
                FirstClearRewards = towerFloor.FirstClearRewards.Select(MapToRewardPreview).ToList(),
                StarRewards = towerFloor.StarRewards.Select(MapToStarRewardDto).ToList(),
                StarConditions = towerFloor.StarConditions.Select(MapToStarCondition).ToList(),
                IsUnlocked = floor <= (progress?.CurrentFloor ?? 1),
                IsCleared = clearData != null,
                BestStars = clearData?.Stars ?? 0,
                BestTurns = clearData?.TurnsTaken,
                ClearCount = clearData?.ClearCount ?? 0
            };
        }

        public async Task<FloorClearResult> ClearFloorAsync(Guid userId, int floor, BattleResultData result)
        {
            var towerFloor = await _unitOfWork.TowerFloors.GetByFloorAsync(floor);
            if (towerFloor == null)
                throw new KeyNotFoundException("Floor not found");

            var progress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Tower progress not found");

            if (floor > progress.CurrentFloor)
                throw new InvalidOperationException("Floor not unlocked");

            var existingClear = await _unitOfWork.TowerProgress.GetFloorClearDataAsync(userId, floor, progress.Season);
            var isFirstClear = existingClear == null;
            var starsEarned = CalculateStars(result, towerFloor.StarConditions);

            var clearData = existingClear ?? new TowerFloorClear { UserId = userId, Floor = floor, Season = progress.Season };
            clearData.UpdateClear(result, starsEarned, isFirstClear);

            if (existingClear == null)
                await _unitOfWork.TowerProgress.AddFloorClearAsync(clearData);
            else
                await _unitOfWork.TowerProgress.UpdateFloorClearAsync(clearData);

            // Update progress
            if (isFirstClear && floor == progress.CurrentFloor)
            {
                progress.CurrentFloor++;
                progress.HighestFloorReached = Math.Max(progress.HighestFloorReached, floor);
                await _unitOfWork.TowerProgress.UpdateAsync(progress);
            }

            // Grant rewards
            var rewards = new List<Reward>();

            if (isFirstClear)
            {
                rewards.AddRange(towerFloor.FirstClearRewards);
            }

            rewards.AddRange(towerFloor.Rewards);

            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new FloorClearResult
            {
                Success = true,
                Floor = floor,
                StarsEarned = starsEarned,
                IsFirstClear = isFirstClear,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                NextFloorUnlocked = isFirstClear && floor == progress.CurrentFloor - 1 ? floor + 1 : (int?)null
            };
        }

        public async Task<RewardResult> ClaimFloorRewardAsync(Guid userId, int floor)
        {
            var progress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Tower progress not found");

            if (progress.ClaimedFloorRewards.Contains(floor))
                throw new InvalidOperationException("Floor reward already claimed");

            var towerFloor = await _unitOfWork.TowerFloors.GetByFloorAsync(floor);
            if (towerFloor == null)
                throw new KeyNotFoundException("Floor not found");

            var clearData = await _unitOfWork.TowerProgress.GetFloorClearDataAsync(userId, floor, progress.Season);
            if (clearData == null || clearData.Stars == 0)
                throw new InvalidOperationException("Floor not cleared");

            progress.ClaimedFloorRewards.Add(floor);
            await _unitOfWork.TowerProgress.UpdateAsync(progress);

            var rewards = towerFloor.Rewards;
            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new RewardResult
            {
                Success = true,
                Rewards = rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<RewardResult> ClaimStarRewardAsync(Guid userId, int stars)
        {
            var progress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Tower progress not found");

            if (progress.ClaimedStarRewards.Contains(stars))
                throw new InvalidOperationException("Star reward already claimed");

            var totalStars = await _unitOfWork.TowerProgress.GetTotalStarsAsync(userId, progress.Season);
            if (totalStars < stars)
                throw new InvalidOperationException("Insufficient stars");

            var starReward = await _unitOfWork.TowerStarRewards.GetByStarsAsync(stars);
            if (starReward == null)
                throw new KeyNotFoundException("Star reward not found");

            progress.ClaimedStarRewards.Add(stars);
            await _unitOfWork.TowerProgress.UpdateAsync(progress);

            await GrantRewardsAsync(userId, starReward.Rewards);

            await _unitOfWork.SaveChangesAsync();

            return new RewardResult
            {
                Success = true,
                Rewards = starReward.Rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<SeasonResetResult> ResetSeasonAsync(Guid userId)
        {
            var progress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Tower progress not found");

            var currentSeason = GetCurrentSeason();
            if (progress.Season == currentSeason)
                throw new InvalidOperationException("Already on current season");

            // Grant season-end rewards based on highest floor
            var seasonRewards = await CalculateSeasonRewardsAsync(progress.HighestFloorReached, progress.Season);
            await GrantRewardsAsync(userId, seasonRewards);

            // Reset for new season
            progress.Season = currentSeason;
            progress.CurrentFloor = 1;
            progress.HighestFloorReached = 0;
            progress.ClearedFloors.Clear();
            progress.ClaimedFloorRewards.Clear();
            progress.ClaimedStarRewards.Clear();
            progress.Tickets = GetBaseTicketsForVip(0); // Would get user VIP
            progress.LastSeasonReset = DateTime.UtcNow;

            await _unitOfWork.TowerProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            return new SeasonResetResult
            {
                Success = true,
                NewSeason = currentSeason,
                SeasonRewards = seasonRewards.Select(MapToRewardDto).ToList(),
                PreviousHighestFloor = progress.HighestFloorReached
            };
        }

        private int GetCurrentSeason() => (int)((DateTime.UtcNow - new DateTime(2024, 1, 1)).TotalDays / 14) + 1;

        private DateTime GetNextSeasonReset()
        {
            var seasonStart = new DateTime(2024, 1, 1).AddDays((GetCurrentSeason() - 1) * 14);
            return seasonStart.AddDays(14);
        }

        private int GetBaseTicketsForVip(int vipLevel) => 5 + vipLevel;

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

        private async Task<List<Reward>> CalculateSeasonRewardsAsync(int highestFloor, int season)
        {
            var rewards = new List<Reward>();
            var seasonReward = await _unitOfWork.TowerSeasonRewards.GetByFloorAsync(highestFloor);
            if (seasonReward != null)
            {
                rewards.AddRange(seasonReward.Rewards);
            }
            return rewards;
        }

        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private EnemyInfoDto MapToEnemyInfo(TowerFloorEnemy e) => new EnemyInfoDto
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

        private StarRewardDto MapToStarRewardDto(TowerStarReward r) => new StarRewardDto
        {
            RequiredStars = r.RequiredStars,
            Rewards = r.Rewards.Select(MapToRewardPreview).ToList()
        };

        private StarConditionDto MapToStarCondition(StarCondition c) => new StarConditionDto
        {
            Type = c.Type,
            Description = c.Description,
            Value = c.Value
        };

        private FloorClearDto MapToFloorClearDto(TowerFloorClear c) => new FloorClearDto
        {
            Floor = c.Floor,
            Stars = c.Stars,
            TurnsTaken = c.TurnsTaken,
            ClearCount = c.ClearCount,
            FirstClearedAt = c.FirstClearedAt
        };

        private RewardDto MapToRewardDto(RewardPreviewDto r) => new RewardDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity
        };
    }
}