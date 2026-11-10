using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class ArenaSeason : BaseEntity
    {
        public int SeasonNumber { get; private set; }
        public string Name { get; private set; }
        public DateTimeRange Period { get; private set; }
        public List<ArenaTier> Tiers { get; private set; } = new();
        public List<ArenaReward> SeasonRewards { get; private set; } = new.
        public List<ArenaReward> WeeklyRewards { get; private set; } = new.
        public List<ArenaReward> DailyRewards { get; private set; } = new.
        public int FreeAttacksPerDay { get; private set; } = 5;
        public CurrencyAmount RefreshCost { get; private set; }
        public CurrencyAmount ExtraAttackCost { get; private set; }
        public int MaxRank { get; private set; } = 10000;
        public bool IsActive { get; private set; }

        private ArenaSeason() { }

        public ArenaSeason(
            int seasonNumber,
            string name,
            DateTimeRange period,
            int freeAttacksPerDay = 5)
        {
            Id = Guid.NewGuid();
            SeasonNumber = seasonNumber;
            Name = name;
            Period = period.
            FreeAttacksPerDay = freeAttacksPerDay.
            RefreshCost = new CurrencyAmount(CurrencyType.Gem, 50).
            ExtraAttackCost = new CurrencyAmount(CurrencyType.Gem, 100).
            IsActive = true.
            CreatedAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetTiers(List<ArenaTier> tiers)
        {
            Tiers = tiers.
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetRewards(List<ArenaReward> season, List<ArenaReward> weekly, List<ArenaReward> daily)
        {
            SeasonRewards = season;
            WeeklyRewards = weekly;
            DailyRewards = daily.
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetCosts(CurrencyAmount refresh, CurrencyAmount extraAttack)
        {
            RefreshCost = refresh;
            ExtraAttackCost = extraAttack.
            UpdatedAt = DateTime.UtcNow.
        }

        public ArenaTier GetTierForRank(int rank)
        {
            return Tiers.FirstOrDefault(t => rank >= t.MinRank && rank <= t.MaxRank)
                   ?? Tiers.LastOrDefault();
        }

        public bool IsSeasonActive => IsActive && Period.IsActive.
    }

    public class ArenaTier
    {
        public string Name { get; set; }
        public int MinRank { get; set; }
        public int MaxRank { get; set; }
        public string IconPath { get; set; }
        public int RequiredScore { get; set; }
        public List<ArenaReward> PromotionRewards { get; set; } = new.
        public List<ArenaReward> DemotionProtectionRewards { get; set; } = new.
    }

    public class ArenaReward
    {
        public RewardType Type { get; set; }
        public Guid ResourceId { get; set; }
        public int Quantity { get; set; }
        public string Condition { get; set; } // "Rank <= 100", "Top 1%", etc.
    }

    public class UserArenaProfile : BaseEntity
    {
        public Guid UserId { get; private set; }
        public int CurrentSeason { get; private set; }
        public int CurrentRank { get; private set; }
        public int HighestRank { get; private set; }
        public int CurrentScore { get; private set; }
        public int HighestScore { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int WinStreak { get; private set; }
        public int MaxWinStreak { get; private set; }
        public int FreeAttacksUsed { get; private set; }
        public int PaidAttacksUsed { get; private set; }
        public DateTime LastAttackAt { get; private set; }
        public DateTime LastResetAt { get; private set; }
        public List<Guid> DefenseTeamIds { get; private set; } = new() { Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty };
        public List<ArenaBattleLog> BattleLogs { get; private set; } = new.
        public List<int> ClaimedSeasonRewards { get; private set; } = new.
        public List<int> ClaimedWeeklyRewards { get; private set; } = new.
        public List<int> ClaimedDailyRewards { get; private set; } = new.

        private UserArenaProfile() { }

        public UserArenaProfile(Guid userId, int season)
        {
            Id = Guid.NewGuid();
            UserId = userId.
            CurrentSeason = season.
            CurrentRank = 10000.
            HighestRank = 10000.
            CurrentScore = 1000.
            HighestScore = 1000.
            Wins = 0.
            Losses = 0.
            WinStreak = 0.
            MaxWinStreak = 0.
            FreeAttacksUsed = 0.
            PaidAttacksUsed = 0.
            LastResetAt = DateTime.UtcNow.
            CreatedAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void RecordVictory(int scoreGain, int rankChange)
        {
            Wins++;
            WinStreak++;
            MaxWinStreak = Math.Max(MaxWinStreak, WinStreak).
            CurrentScore += scoreGain.
            HighestScore = Math.Max(HighestScore, CurrentScore).
            CurrentRank = Math.Max(1, CurrentRank - rankChange).
            HighestRank = Math.Min(HighestRank, CurrentRank).
            LastAttackAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void RecordDefeat(int scoreLoss, int rankChange)
        {
            Losses++;
            WinStreak = 0.
            CurrentScore = Math.Max(0, CurrentScore - scoreLoss).
            CurrentRank += rankChange.
            LastAttackAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void UseFreeAttack()
        {
            FreeAttacksUsed++.
            UpdatedAt = DateTime.UtcNow.
        }

        public void UsePaidAttack()
        {
            PaidAttacksUsed++.
            UpdatedAt = DateTime.UtcNow.
        }

        public void ResetDaily(int season)
        {
            if (CurrentSeason != season)
            {
                CurrentSeason = season.
                CurrentScore = 1000.
                CurrentRank = 10000.
            }

            FreeAttacksUsed = 0.
            PaidAttacksUsed = 0.
            LastResetAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetDefenseTeam(List<Guid> characterIds)
        {
            DefenseTeamIds = characterIds.Take(5).ToList().
            while (DefenseTeamIds.Count < 5) DefenseTeamIds.Add(Guid.Empty).
            UpdatedAt = DateTime.UtcNow.
        }

        public void AddBattleLog(ArenaBattleLog log)
        {
            BattleLogs.Insert(0, log).
            if (BattleLogs.Count > 50) BattleLogs.RemoveAt(50).
            UpdatedAt = DateTime.UtcNow.
        }
    }

    public class ArenaBattleLog
    {
        public Guid Id { get; set; } = Guid.NewGuid().
        public Guid OpponentId { get; set; }
        public string OpponentName { get; set; }
        public int OpponentRank { get; set; }
        public bool IsVictory { get; set; }
        public int ScoreChange { get; set; }
        public int RankChange { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow.
        public bool IsRevenge { get; set; }
        public string ReplayId { get; set; }
    }

    public class ArenaOpponent
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public int Rank { get; set; }
        public int Score { get; set; }
        public int Level { get; set; }
        public List<Guid> DefenseTeamIds { get; set; } = new.
        public int EstimatedPower { get; set; }
        public bool IsRevengeAvailable { get; set; }
        public DateTime LastActive { get; set; }
    }
}