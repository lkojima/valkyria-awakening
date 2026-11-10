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
    public class WorldBossService : IWorldBossService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WorldBossService> _logger;
        private readonly IBattleService _battleService;
        private readonly ILeaderboardService _leaderboardService;

        public WorldBossService(IUnitOfWork unitOfWork, ILogger<WorldBossService> logger, IBattleService battleService, ILeaderboardService leaderboardService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleService = battleService;
            _leaderboardService = leaderboardService;
        }

        public async Task<WorldBossInfoDto> GetActiveBossAsync()
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null) return null;

            return new WorldBossInfoDto
            {
                Id = boss.Id,
                Name = boss.Name,
                Description = boss.Description,
                Element = boss.Element,
                Level = boss.Level,
                MaxHP = boss.MaxHP,
                CurrentHP = boss.CurrentHP,
                StartAt = boss.StartAt,
                EndAt = boss.EndAt,
                Phase = boss.CurrentPhase,
                PhaseThresholds = boss.PhaseThresholds,
                Mechanics = boss.Mechanics.Select(m => m.Description).ToList(),
                DamageThresholds = boss.DamageThresholds.Select(MapToThresholdDto).ToList(),
                ParticipationRewards = boss.ParticipationRewards.Select(MapToRewardPreview).ToList(),
                RankingRewards = boss.RankingRewards.Select(MapToRankingRewardDto).ToList(),
                IsActive = boss.IsActive(DateTime.UtcNow),
                TimeRemaining = boss.EndAt - DateTime.UtcNow
            };
        }

        public async Task<WorldBossProgressDto> GetProgressAsync(Guid userId)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null) return null;

            var progress = await _unitOfWork.WorldBossProgress.GetByUserAndBossAsync(userId, boss.Id);
            if (progress == null)
            {
                progress = new WorldBossProgress { UserId = userId, BossId = boss.Id };
                await _unitOfWork.WorldBossProgress.AddAsync(progress);
                await _unitOfWork.SaveChangesAsync();
            }

            var claimedDamageRewards = await _unitOfWork.WorldBossProgress.GetClaimedDamageRewardsAsync(userId, boss.Id);
            var claimedParticipation = progress.ParticipationRewardClaimed;
            var claimedRanking = progress.RankingRewardClaimed;

            return new WorldBossProgressDto
            {
                BossId = boss.Id,
                TotalDamage = progress.TotalDamage,
                BestSingleDamage = progress.BestSingleDamage,
                BattlesCount = progress.BattlesCount,
                ParticipationRewardClaimed = claimedParticipation,
                ClaimedDamageRewards = claimedDamageRewards,
                RankingRewardClaimed = claimedRanking,
                FreeEntriesRemaining = progress.FreeEntries,
                PaidEntriesUsed = progress.PaidEntries,
                LastBattleAt = progress.LastBattleAt,
                CurrentRank = progress.CurrentRank
            };
        }

        public async Task<BattleStartResult> StartBattleAsync(Guid userId, bool useFreeEntry)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null || !boss.IsActive(DateTime.UtcNow))
                throw new InvalidOperationException("No active world boss");

            var progress = await _unitOfWork.WorldBossProgress.GetByUserAndBossAsync(userId, boss.Id);
            if (progress == null)
            {
                progress = new WorldBossProgress { UserId = userId, BossId = boss.Id, FreeEntries = 3 };
                await _unitOfWork.WorldBossProgress.AddAsync(progress);
            }

            // Check entries
            if (useFreeEntry)
            {
                if (progress.FreeEntries <= 0)
                    throw new InvalidOperationException("No free entries remaining");
                progress.FreeEntries--;
            }
            else
            {
                var cost = new CurrencyAmount(CurrencyType.Gold, 50000);
                var canAfford = await CheckCurrencyAsync(userId, cost);
                if (!canAfford)
                    throw new InvalidOperationException("Insufficient gold");
                await SpendCurrencyAsync(userId, cost);
                progress.PaidEntries++;
            }

            // Get user's team
            var team = await GetUserWorldBossTeamAsync(userId);
            if (!team.Any())
                throw new InvalidOperationException("No team set for world boss");

            // Start battle
            var battleResult = await _battleService.StartBattleAsync(userId, GameMode.WorldBoss, 0,
                team.Select(c => c.Id).ToList(), BattleSpeed.Normal, AutoBattleMode.SemiAuto);

            // Store context
            var wbBattle = new WorldBossBattle
            {
                BattleId = battleResult.BattleId,
                UserId = userId,
                BossId = boss.Id,
                StartedAt = DateTime.UtcNow
            };
            await _unitOfWork.WorldBossBattles.AddAsync(wbBattle);

            await _unitOfWork.SaveChangesAsync();

            return battleResult;
        }

        public async Task<WorldBossBattleResult> SubmitDamageAsync(Guid userId, long damage, BattleResultData result)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null)
                throw new InvalidOperationException("No active world boss");

            var wbBattle = await _unitOfWork.WorldBossBattles.GetByUserAndStatusAsync(userId, WorldBossBattleStatus.InProgress);
            if (wbBattle == null)
                throw new KeyNotFoundException("Active world boss battle not found");

            var progress = await _unitOfWork.WorldBossProgress.GetByUserAndBossAsync(userId, boss.Id);
            if (progress == null)
                throw new KeyNotFoundException("World boss progress not found");

            // Validate damage (server-side simulation would be better)
            var maxPossibleDamage = CalculateMaxPossibleDamage(progress);
            if (damage > maxPossibleDamage * 1.5) // Allow 50% variance
            {
                _logger.LogWarning("Suspicious damage submission: user {UserId} damage {Damage} max possible {Max}", userId, damage, maxPossibleDamage);
                damage = Math.Min(damage, maxPossibleDamage);
            }

            // Update boss HP
            boss.CurrentHP = Math.Max(0, boss.CurrentHP - damage);

            // Update progress
            progress.TotalDamage += damage;
            progress.BestSingleDamage = Math.Max(progress.BestSingleDamage, damage);
            progress.BattlesCount++;
            progress.LastBattleAt = DateTime.UtcNow;

            // Update battle record
            wbBattle.Status = WorldBossBattleStatus.Finished;
            wbBattle.DamageDealt = damage;
            wbBattle.FinishedAt = DateTime.UtcNow;
            wbBattle.BattleResult = result;

            // Check for phase transition
            var oldPhase = boss.CurrentPhase;
            boss.UpdatePhase();
            var phaseChanged = boss.CurrentPhase != oldPhase;

            // Check if boss defeated
            var bossDefeated = boss.CurrentHP <= 0;
            if (bossDefeated)
            {
                boss.Status = WorldBossStatus.Defeated;
                boss.DefeatedAt = DateTime.UtcNow;
                boss.DefeatedByUserId = userId;
            }

            // Update leaderboard
            await _leaderboardService.UpdateScoreAsync($"worldboss_{boss.Id}", userId, progress.TotalDamage,
                new Dictionary<string, object> { { "best_single", progress.BestSingleDamage }, { "battles", progress.BattlesCount } });

            await _unitOfWork.WorldBosses.UpdateAsync(boss);
            await _unitOfWork.WorldBossProgress.UpdateAsync(progress);
            await _unitOfWork.WorldBossBattles.UpdateAsync(wbBattle);
            await _unitOfWork.SaveChangesAsync();

            // Calculate rewards
            var rewards = CalculateDamageRewards(damage, boss, progress);
            await GrantRewardsAsync(userId, rewards);

            return new WorldBossBattleResult
            {
                DamageDealt = damage,
                TotalDamage = progress.TotalDamage,
                BossHPRemaining = boss.CurrentHP,
                BossMaxHP = boss.MaxHP,
                PhaseChanged = phaseChanged,
                NewPhase = boss.CurrentPhase,
                BossDefeated = bossDefeated,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                RankUpdated = true
            };
        }

        public async Task<RewardResult> ClaimDamageRewardAsync(Guid userId, int thresholdIndex)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null) return new RewardResult { Success = false, Error = "No active boss" };

            var progress = await _unitOfWork.WorldBossProgress.GetByUserAndBossAsync(userId, boss.Id);
            if (progress == null) return new RewardResult { Success = false, Error = "No progress" };

            if (progress.ClaimedDamageRewards.Contains(thresholdIndex))
                return new RewardResult { Success = false, Error = "Already claimed" };

            if (thresholdIndex >= boss.DamageThresholds.Count)
                return new RewardResult { Success = false, Error = "Invalid threshold" };

            var threshold = boss.DamageThresholds[thresholdIndex];
            if (progress.TotalDamage < threshold.RequiredDamage)
                return new RewardResult { Success = false, Error = "Threshold not reached" };

            progress.ClaimedDamageRewards.Add(thresholdIndex);
            await _unitOfWork.WorldBossProgress.UpdateAsync(progress);

            var rewards = threshold.Rewards;
            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new RewardResult { Success = true, Rewards = rewards.Select(MapToRewardDto).ToList() };
        }

        public async Task<RewardResult> ClaimParticipationRewardAsync(Guid userId)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null) return new RewardResult { Success = false, Error = "No active boss" };

            var progress = await _unitOfWork.WorldBossProgress.GetByUserAndBossAsync(userId, boss.Id);
            if (progress == null || progress.BattlesCount == 0)
                return new RewardResult { Success = false, Error = "Did not participate" };

            if (progress.ParticipationRewardClaimed)
                return new RewardResult { Success = false, Error = "Already claimed" };

            progress.ParticipationRewardClaimed = true;
            await _unitOfWork.WorldBossProgress.UpdateAsync(progress);

            var rewards = boss.ParticipationRewards;
            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new RewardResult { Success = true, Rewards = rewards.Select(MapToRewardDto).ToList() };
        }

        public async Task<RewardResult> ClaimRankingRewardAsync(Guid userId)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null || boss.Status != WorldBossStatus.Defeated && boss.Status != WorldBossStatus.Ended)
                return new RewardResult { Success = false, Error = "Boss not defeated yet" };

            var progress = await _unitOfWork.WorldBossProgress.GetByUserAndBossAsync(userId, boss.Id);
            if (progress == null)
                return new RewardResult { Success = false, Error = "No progress" };

            if (progress.RankingRewardClaimed)
                return new RewardResult { Success = false, Error = "Already claimed" };

            var rank = await _leaderboardService.GetRankAsync($"worldboss_{boss.Id}", userId);
            if (rank == null)
                return new RewardResult { Success = false, Error = "Not ranked" };

            progress.RankingRewardClaimed = true;
            progress.CurrentRank = rank.Rank;
            await _unitOfWork.WorldBossProgress.UpdateAsync(progress);

            // Find appropriate ranking reward
            var rankingReward = boss.RankingRewards.FirstOrDefault(r => r.MinRank <= rank.Rank && r.MaxRank >= rank.Rank);
            var rewards = rankingReward?.Rewards ?? new List<Reward>();

            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new RewardResult { Success = true, Rewards = rewards.Select(MapToRewardDto).ToList() };
        }

        public async Task<IReadOnlyList<WorldBossRankingEntry>> GetRankingAsync(int count)
        {
            var boss = await _unitOfWork.WorldBosses.GetActiveAsync(DateTime.UtcNow);
            if (boss == null) return new List<WorldBossRankingEntry>();

            var topEntries = await _leaderboardService.GetTopAsync($"worldboss_{boss.Id}", count);

            return topEntries.Select((e, i) => new WorldBossRankingEntry
            {
                Rank = i + 1,
                UserId = e.UserId,
                DisplayName = e.Metadata.GetValueOrDefault("displayName", "Unknown").ToString(),
                TotalDamage = e.Score,
                BestSingleDamage = (long)e.Metadata.GetValueOrDefault("best_single", 0L),
                BattlesCount = (int)e.Metadata.GetValueOrDefault("battles", 0)
            }).ToList();
        }

        private async Task<List<UserCharacter>> GetUserWorldBossTeamAsync(Guid userId)
        {
            // Would get user's saved world boss team
            return await _unitOfWork.UserCharacters.GetByUserIdAsync(userId);
        }

        private long CalculateMaxPossibleDamage(WorldBossProgress progress)
        {
            // Rough estimate based on best single damage * battles
            return progress.BestSingleDamage * Math.Max(1, progress.BattlesCount);
        }

        private List<Reward> CalculateDamageRewards(long damage, WorldBoss boss, WorldBossProgress progress)
        {
            var rewards = new List<Reward>();

            // Base participation reward per battle
            rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Quantity = 10000, IsGuaranteed = true });
            rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Quantity = (int)(damage / 10000), IsGuaranteed = true }); // World Boss Coins

            return rewards;
        }

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private DamageThresholdDto MapToThresholdDto(WorldBossDamageThreshold t) => new DamageThresholdDto
        {
            RequiredDamage = t.RequiredDamage,
            Rewards = t.Rewards.Select(MapToRewardPreview).ToList()
        };

        private RankingRewardDto MapToRankingRewardDto(WorldBossRankingReward r) => new RankingRewardDto
        {
            MinRank = r.MinRank,
            MaxRank = r.MaxRank,
            Rewards = r.Rewards.Select(MapToRewardPreview).ToList()
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
    }
}