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
    public class ArenaService : IArenaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ArenaService> _logger;
        private readonly IBattleService _battleService;
        private readonly ILeaderboardService _leaderboardService;

        public ArenaService(IUnitOfWork unitOfWork, ILogger<ArenaService> logger, IBattleService battleService, ILeaderboardService leaderboardService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleService = battleService;
            _leaderboardService = leaderboardService;
        }

        public async Task<ArenaInfoDto> GetArenaInfoAsync(Guid userId)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null)
            {
                arena = new ArenaProfile { UserId = userId, Rating = 1000, Division = ArenaDivision.Bronze };
                await _unitOfWork.ArenaProfiles.AddAsync(arena);
                await _unitOfWork.SaveChangesAsync();
            }

            var defenseTeam = await _unitOfWork.ArenaProfiles.GetDefenseTeamAsync(userId);
            var season = GetCurrentSeason();

            return new ArenaInfoDto
            {
                Rating = arena.Rating,
                Division = arena.Division,
                Rank = arena.Rank,
                Season = season,
                SeasonEndAt = GetSeasonEnd(),
                DailyWins = arena.DailyWins,
                DailyLosses = arena.DailyLosses,
                WeeklyWins = arena.WeeklyWins,
                WeeklyLosses = arena.WeeklyLosses,
                DefenseTeam = defenseTeam?.Select(MapToDefenseUnitDto).ToList() ?? new List<DefenseUnitDto>(),
                DefenseWins = arena.DefenseWins,
                DefenseLosses = arena.DefenseLosses,
                Tickets = arena.Tickets,
                MaxTickets = GetMaxTickets(arena.VipLevel),
                TicketRegenAt = arena.TicketRegenAt,
                CanClaimDailyReward = !arena.DailyRewardClaimed,
                CanClaimWeeklyReward = arena.WeeklyWins >= 10 && !arena.WeeklyRewardClaimed,
                CanClaimSeasonReward = arena.SeasonRewardClaimed == false
            };
        }

        public async Task<IReadOnlyList<ArenaOpponentDto>> GetOpponentsAsync(Guid userId)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null)
                throw new KeyNotFoundException("Arena profile not found");

            // Get opponents around player's rating
            var opponents = await _unitOfWork.ArenaProfiles.GetOpponentsAsync(arena.Rating, 5);

            return opponents.Select(o => MapToOpponentDto(o, arena.Rating)).ToList();
        }

        public async Task<BattleStartResult> StartBattleAsync(Guid userId, Guid opponentId, bool isRevenge)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null)
                throw new KeyNotFoundException("Arena profile not found");

            if (!isRevenge && arena.Tickets <= 0)
                throw new InvalidOperationException("No arena tickets");

            var opponent = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(opponentId);
            if (opponent == null)
                throw new KeyNotFoundException("Opponent not found");

            // Get defense teams
            var playerTeam = await _unitOfWork.UserCharacters.GetByIdsAsync((await _unitOfWork.ArenaProfiles.GetAttackTeamAsync(userId))?.Select(c => c.Id).ToList() ?? new List<Guid>());
            var enemyTeam = await _unitOfWork.UserCharacters.GetByIdsAsync((await _unitOfWork.ArenaProfiles.GetDefenseTeamAsync(opponentId))?.Select(c => c.Id).ToList() ?? new List<Guid>());

            if (!playerTeam.Any())
                throw new InvalidOperationException("No attack team set");

            if (!enemyTeam.Any())
                throw new InvalidOperationException("Opponent has no defense team");

            // Consume ticket
            if (!isRevenge)
            {
                arena.Tickets--;
                await _unitOfWork.ArenaProfiles.UpdateAsync(arena);
            }

            // Start battle
            var battleResult = await _battleService.StartBattleAsync(userId, GameMode.Arena, 0,
                playerTeam.Select(c => c.Id).ToList(), BattleSpeed.Fast, AutoBattleMode.SemiAuto);

            // Store battle context
            var arenaBattle = new ArenaBattle
            {
                BattleId = battleResult.BattleId,
                AttackerId = userId,
                DefenderId = opponentId,
                IsRevenge = isRevenge,
                AttackerRatingBefore = arena.Rating,
                DefenderRatingBefore = opponent.Rating
            };
            await _unitOfWork.ArenaBattles.AddAsync(arenaBattle);

            await _unitOfWork.SaveChangesAsync();

            return battleResult;
        }

        public async Task<ArenaBattleResult> ProcessBattleResultAsync(Guid userId, BattleResultData result)
        {
            var arenaBattle = await _unitOfWork.ArenaBattles.GetByAttackerAndStatusAsync(userId, ArenaBattleStatus.InProgress);
            if (arenaBattle == null)
                throw new KeyNotFoundException("Active arena battle not found");

            var attacker = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            var defender = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(arenaBattle.DefenderId);

            if (attacker == null || defender == null)
                throw new KeyNotFoundException("Arena profile not found");

            // Calculate rating change
            var ratingChange = CalculateRatingChange(attacker.Rating, defender.Rating, result.Result == BattleResult.Win);
            var attackerNewRating = Math.Max(0, attacker.Rating + ratingChange);
            var defenderNewRating = Math.Max(0, defender.Rating - ratingChange / 2); // Defender loses less

            // Update divisions
            var attackerNewDivision = CalculateDivision(attackerNewRating);
            var defenderNewDivision = CalculateDivision(defenderNewRating);

            // Update attacker
            attacker.Rating = attackerNewRating;
            attacker.Division = attackerNewDivision;
            if (result.Result == BattleResult.Win)
            {
                attacker.DailyWins++;
                attacker.WeeklyWins++;
                attacker.TotalWins++;
            }
            else
            {
                attacker.DailyLosses++;
                attacker.WeeklyLosses++;
                attacker.TotalLosses++;
            }
            attacker.LastBattleAt = DateTime.UtcNow;

            // Update defender
            defender.Rating = defenderNewRating;
            defender.Division = defenderNewDivision;
            if (result.Result == BattleResult.Win)
            {
                defender.DefenseLosses++;
            }
            else
            {
                defender.DefenseWins++;
            }

            // Update battle record
            arenaBattle.Status = ArenaBattleStatus.Finished;
            arenaBattle.AttackerRatingAfter = attackerNewRating;
            arenaBattle.DefenderRatingAfter = defenderNewRating;
            arenaBattle.Result = result.Result;
            arenaBattle.FinishedAt = DateTime.UtcNow;

            // Update leaderboard
            await _leaderboardService.UpdateScoreAsync($"arena_season_{GetCurrentSeason()}", userId, attackerNewRating,
                new Dictionary<string, object> { { "division", attackerNewDivision.ToString() }, { "wins", attacker.TotalWins } });
            await _leaderboardService.UpdateScoreAsync($"arena_season_{GetCurrentSeason()}", arenaBattle.DefenderId, defenderNewRating,
                new Dictionary<string, object> { { "division", defenderNewDivision.ToString() }, { "wins", defender.TotalWins } });

            await _unitOfWork.ArenaProfiles.UpdateAsync(attacker);
            await _unitOfWork.ArenaProfiles.UpdateAsync(defender);
            await _unitOfWork.ArenaBattles.UpdateAsync(arenaBattle);
            await _unitOfWork.SaveChangesAsync();

            // Calculate rewards
            var rewards = CalculateBattleRewards(attacker, result.Result == BattleResult.Win);
            await GrantRewardsAsync(userId, rewards);

            return new ArenaBattleResult
            {
                Result = result.Result,
                RatingChange = ratingChange,
                NewRating = attackerNewRating,
                NewDivision = attackerNewDivision,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                DefenseResult = result.Result == BattleResult.Lose ? "Defense Failed" : "Defense Successful"
            };
        }

        public async Task<RewardResult> ClaimDailyRewardAsync(Guid userId)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null || arena.DailyRewardClaimed)
                return new RewardResult { Success = false, Error = "Already claimed or not eligible" };

            var rewards = GetDailyRewards(arena.Division);
            await GrantRewardsAsync(userId, rewards);

            arena.DailyRewardClaimed = true;
            await _unitOfWork.ArenaProfiles.UpdateAsync(arena);
            await _unitOfWork.SaveChangesAsync();

            return new RewardResult { Success = true, Rewards = rewards.Select(MapToRewardDto).ToList() };
        }

        public async Task<RewardResult> ClaimWeeklyRewardAsync(Guid userId)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null || arena.WeeklyRewardClaimed || arena.WeeklyWins < 10)
                return new RewardResult { Success = false, Error = "Not eligible" };

            var rewards = GetWeeklyRewards(arena.Division);
            await GrantRewardsAsync(userId, rewards);

            arena.WeeklyRewardClaimed = true;
            await _unitOfWork.ArenaProfiles.UpdateAsync(arena);
            await _unitOfWork.SaveChangesAsync();

            return new RewardResult { Success = true, Rewards = rewards.Select(MapToRewardDto).ToList() };
        }

        public async Task<RewardResult> ClaimSeasonRewardAsync(Guid userId)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null || arena.SeasonRewardClaimed)
                return new RewardResult { Success = false, Error = "Already claimed" };

            var rewards = GetSeasonRewards(arena.Division, arena.Rank);
            await GrantRewardsAsync(userId, rewards);

            arena.SeasonRewardClaimed = true;
            await _unitOfWork.ArenaProfiles.UpdateAsync(arena);
            await _unitOfWork.SaveChangesAsync();

            return new RewardResult { Success = true, Rewards = rewards.Select(MapToRewardDto).ToList() };
        }

        public async Task<RefreshResult> RefreshOpponentsAsync(Guid userId)
        {
            var arena = await _unitOfWork.ArenaProfiles.GetByUserIdAsync(userId);
            if (arena == null)
                throw new KeyNotFoundException("Arena profile not found");

            var cost = new CurrencyAmount(CurrencyType.Gold, 1000);
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient gold");

            await SpendCurrencyAsync(userId, cost);

            var opponents = await GetOpponentsAsync(userId);

            return new RefreshResult
            {
                Success = true,
                Opponents = opponents,
                Cost = cost
            };
        }

        public async Task<DefenseTeamResult> SetDefenseTeamAsync(Guid userId, List<Guid> characterIds)
        {
            if (characterIds.Count < 1 || characterIds.Count > 5)
                throw new ArgumentException("Defense team must have 1-5 characters");

            var characters = await _unitOfWork.UserCharacters.GetByIdsAsync(characterIds);
            if (characters.Any(c => c.UserId != userId))
                throw new UnauthorizedAccessException("Character not owned");

            await _unitOfWork.ArenaProfiles.SetDefenseTeamAsync(userId, characterIds);
            await _unitOfWork.SaveChangesAsync();

            return new DefenseTeamResult
            {
                Success = true,
                Team = characterIds
            };
        }

        private int CalculateRatingChange(int attackerRating, int defenderRating, bool attackerWon)
        {
            var expected = 1.0 / (1.0 + Math.Pow(10, (defenderRating - attackerRating) / 400.0));
            var actual = attackerWon ? 1.0 : 0.0;
            var kFactor = GetKFactor(attackerRating);
            return (int)Math.Round(kFactor * (actual - expected));
        }

        private int GetKFactor(int rating)
        {
            if (rating < 1500) return 40;
            if (rating < 2000) return 30;
            if (rating < 2500) return 20;
            return 10;
        }

        private ArenaDivision CalculateDivision(int rating)
        {
            return rating switch
            {
                >= 3000 => ArenaDivision.Legend,
                >= 2600 => ArenaDivision.Diamond,
                >= 2200 => ArenaDivision.Platinum,
                >= 1800 => ArenaDivision.Gold,
                >= 1400 => ArenaDivision.Silver,
                _ => ArenaDivision.Bronze
            };
        }

        private List<Reward> CalculateBattleRewards(ArenaProfile arena, bool isWin)
        {
            var rewards = new List<Reward>();

            if (isWin)
            {
                rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Quantity = 500 + (int)arena.Division * 100, IsGuaranteed = true });
                rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Quantity = 50 + (int)arena.Division * 10, IsGuaranteed = true }); // Arena Coins
            }
            else
            {
                rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Quantity = 100, IsGuaranteed = true });
                rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Quantity = 10, IsGuaranteed = true });
            }

            return rewards;
        }

        private List<Reward> GetDailyRewards(ArenaDivision division)
        {
            var baseCoins = division switch
            {
                ArenaDivision.Bronze => 100,
                ArenaDivision.Silver => 200,
                ArenaDivision.Gold => 350,
                ArenaDivision.Platinum => 500,
                ArenaDivision.Diamond => 750,
                ArenaDivision.Legend => 1000,
                _ => 100
            };

            return new List<Reward>
            {
                new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Quantity = baseCoins, IsGuaranteed = true }
            };
        }

        private List<Reward> GetWeeklyRewards(ArenaDivision division)
        {
            var baseCoins = division switch
            {
                ArenaDivision.Bronze => 500,
                ArenaDivision.Silver => 1000,
                ArenaDivision.Gold => 2000,
                ArenaDivision.Platinum => 3500,
                ArenaDivision.Diamond => 5000,
                ArenaDivision.Legend => 7500,
                _ => 500
            };

            return new List<Reward>
            {
                new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Quantity = baseCoins, IsGuaranteed = true }
            };
        }

        private List<Reward> GetSeasonRewards(ArenaDivision division, int rank)
        {
            var rewards = new List<Reward>();

            // Division rewards
            var divisionCoins = division switch
            {
                ArenaDivision.Bronze => 2000,
                ArenaDivision.Silver => 5000,
                ArenaDivision.Gold => 10000,
                ArenaDivision.Platinum => 20000,
                ArenaDivision.Diamond => 50000,
                ArenaDivision.Legend => 100000,
                _ => 2000
            };

            rewards.Add(new Reward { Type = RewardType.Currency, ResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Quantity = divisionCoins, IsGuaranteed = true });

            // Rank rewards
            if (rank <= 100)
            {
                rewards.Add(new Reward { Type = RewardType.Item, ResourceId = Guid.NewGuid(), Quantity = 1, IsGuaranteed = true }); // Exclusive skin/item
            }

            return rewards;
        }

        private int GetMaxTickets(int vipLevel) => 5 + vipLevel;

        private int GetCurrentSeason() => (int)((DateTime.UtcNow - new DateTime(2024, 1, 1)).TotalDays / 7) + 1;

        private DateTime GetSeasonEnd()
        {
            var seasonStart = new DateTime(2024, 1, 1).AddDays((GetCurrentSeason() - 1) * 7);
            return seasonStart.AddDays(7);
        }

        private ArenaOpponentDto MapToOpponentDto(ArenaProfile opponent, int playerRating)
        {
            var ratingDiff = opponent.Rating - playerRating;
            return new ArenaOpponentDto
            {
                UserId = opponent.UserId,
                DisplayName = opponent.DisplayName,
                Level = opponent.Level,
                Rating = opponent.Rating,
                Division = opponent.Division,
                Rank = opponent.Rank,
                DefenseTeam = opponent.DefenseTeam?.Select(MapToDefenseUnitDto).ToList() ?? new List<DefenseUnitDto>(),
                EstimatedDifficulty = ratingDiff > 200 ? "Hard" : ratingDiff > -200 ? "Normal" : "Easy",
                RevengeAvailable = false
            };
        }

        private DefenseUnitDto MapToDefenseUnitDto(UserCharacter c) => new DefenseUnitDto
        {
            CharacterId = c.CharacterId,
            CharacterName = c.CharacterId.ToString(), // Would get actual name
            Level = c.Level,
            Rarity = c.CharacterRarity,
            Position = 0
        };

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private RewardDto MapToRewardDto(Reward r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
    }
}