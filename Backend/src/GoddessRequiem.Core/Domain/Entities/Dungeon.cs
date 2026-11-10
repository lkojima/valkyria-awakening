using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Dungeon : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public DungeonType Type { get; private set; }
        public Element Element { get; private set; }
        public int RequiredPlayerLevel { get; private set; }
        public int StaminaCost { get; private set; }
        public List<DungeonDifficulty> Difficulties { get; private set; } = new();
        public List<DungeonReward> Rewards { get; private set; } = new()
        public DateTimeRange? AvailablePeriod { get; private set; }
        public List<int> AvailableDaysOfWeek { get; private set; } = new() { 0, 1, 2, 3, 4, 5, 6 };
        public int MaxEntriesPerDay { get; private set; } = 10;
        public string BackgroundPath { get; private set; }
        public string MusicPath { get; private set; }
        public bool IsActive { get; private set; }

        private Dungeon() { }

        public Dungeon(
            string name,
            string description,
            DungeonType type,
            Element element,
            int requiredPlayerLevel,
            int staminaCost)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            Element = element;
            RequiredPlayerLevel = requiredPlayerLevel;
            StaminaCost = staminaCost;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddDifficulty(DungeonDifficulty difficulty)
        {
            Difficulties.Add(difficulty);
            Difficulties.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRewards(List<DungeonReward> rewards)
        {
            Rewards = rewards;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSchedule(DateTimeRange? period, List<int> daysOfWeek)
        {
            AvailablePeriod = period;
            AvailableDaysOfWeek = daysOfWeek;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsAvailableNow
        {
            get
            {
                if (!IsActive) return false;
                if (AvailablePeriod.HasValue && !AvailablePeriod.Value.IsActive) return false;
                if (!AvailableDaysOfWeek.Contains((int)DateTime.UtcNow.DayOfWeek)) return false;
                return true;
            }
        }

        public DungeonDifficulty GetDifficulty(int tier)
        {
            return Difficulties.FirstOrDefault(d => d.Tier == tier);
        }
    }

    public enum DungeonType
    {
        EvolutionMaterial = 1,
        EquipmentMaterial = 2,
        RelicMaterial = 3,
        RuneMaterial = 4,
        Gold = 5,
        Experience = 6,
        SkillMaterial = 7,
        AwakeningMaterial = 8,
        CharacterFragment = 9,
        SkinToken = 10,
        Mixed = 11
    }

    public class DungeonDifficulty
    {
        public int Tier { get; set; } // 1-10+
        public string Name { get; set; }
        public int RecommendedPower { get; set; }
        public List<WaveConfig> Waves { get; set; } = new();
        public List<Reward> FirstClearRewards { get; set; } = new();
        public List<Reward> RepeatRewards { get; set; } = new();
        public List<StarCondition> StarConditions { get; set; } = new();
        public int StaminaCostMultiplier { get; set; } = 1;
        public bool AutoBattleAllowed { get; set; } = true;
        public bool AutoProgressAllowed { get; set; } = true;
    }

    public class DungeonReward
    {
        public RewardType Type { get; set; }
        public Guid ResourceId { get; set; }
        public int MinQuantity { get; set; }
        public int MaxQuantity { get; set; }
        public float DropRate { get; set; }
        public int RequiredStars { get; set; } = 0;
        public int RequiredDifficultyTier { get; set; } = 1;
        public bool IsGuaranteed { get; set; }
    }

    public class UserDungeonProgress : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid DungeonId { get; private set; }
        public int HighestClearedTier { get; private set; }
        public Dictionary<int, DungeonDifficultyClearData> ClearedDifficulties { get; private set; } = new();
        public int EntriesUsedToday { get; private set; }
        public DateTime LastEntryDate { get; private set; }
        public DateTime LastResetAt { get; private set; }

        private UserDungeonProgress() { }

        public UserDungeonProgress(Guid userId, Guid dungeonId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            DungeonId = dungeonId;
            HighestClearedTier = 0;
            LastResetAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ClearDifficulty(int tier, int stars, int turnsUsed)
        {
            CheckDailyReset();

            var data = new DungeonDifficultyClearData
            {
                Tier = tier,
                Stars = stars,
                TurnsUsed = turnsUsed,
                ClearedAt = DateTime.UtcNow
            };

            ClearedDifficulties[tier] = data;

            if (tier > HighestClearedTier)
                HighestClearedTier = tier;

            EntriesUsedToday++;
            LastEntryDate = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool CanEnter(int maxEntries)
        {
            CheckDailyReset();
            return EntriesUsedToday < maxEntries;
        }

        private void CheckDailyReset()
        {
            if (LastResetAt.Date < DateTime.UtcNow.Date)
            {
                EntriesUsedToday = 0;
                LastResetAt = DateTime.UtcNow;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public int GetStarsForTier(int tier) => ClearedDifficulties.TryGetValue(tier, out var data) ? data.Stars : 0;
        public bool IsTierCleared(int tier) => ClearedDifficulties.ContainsKey(tier);
        public bool IsTierThreeStarred(int tier) => GetStarsForTier(tier) >= 3;
    }

    public class DungeonDifficultyClearData
    {
        public int Tier { get; set; }
        public int Stars { get; set; }
        public int TurnsUsed { get; set; }
        public DateTime ClearedAt { get; set; }
    }
}