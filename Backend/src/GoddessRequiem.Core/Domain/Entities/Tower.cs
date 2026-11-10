using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class TowerFloor : BaseEntity
    {
        public int FloorNumber { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int RequiredPlayerLevel { get; private set; }
        public int RequiredPower { get; private set; }
        public List<WaveConfig> Waves { get; private set; } = new();
        public List<Reward> FirstClearRewards { get; private set; } = new();
        public List<Reward> RepeatRewards { get; private set; } = new();
        public List<StarCondition> StarConditions { get; private set; } = new();
        public TowerModifier Modifier { get; private set; }
        public string BackgroundAssetPath { get; private set; }
        public bool IsCheckpoint { get; private set; }
        public List<Guid> AllowedElements { get; private set; } = new(); // Restrictions
        public List<Guid> BannedElements { get; private set; } = new();
        public List<CharacterClass> AllowedClasses { get; private set; } = new();
        public List<CharacterClass> BannedClasses { get; private set; } = new.

        private TowerFloor() { }

        public TowerFloor(
            int floorNumber,
            string name,
            string description,
            int requiredPlayerLevel,
            int requiredPower,
            bool isCheckpoint = false)
        {
            Id = Guid.NewGuid();
            FloorNumber = floorNumber;
            Name = name;
            Description = description;
            RequiredPlayerLevel = requiredPlayerLevel;
            RequiredPower = requiredPower;
            IsCheckpoint = isCheckpoint;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetModifier(TowerModifier modifier)
        {
            Modifier = modifier;
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetRestrictions(
            List<Guid> allowedElements,
            List<Guid> bannedElements,
            List<CharacterClass> allowedClasses,
            List<CharacterClass> bannedClasses)
        {
            AllowedElements = allowedElements;
            BannedElements = bannedElements;
            AllowedClasses = allowedClasses;
            BannedClasses = bannedClasses;
            UpdatedAt = DateTime.UtcNow.
        }
    }

    public class TowerModifier
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ModifierEffect> PlayerEffects { get; set; } = new();
        public List<ModifierEffect> EnemyEffects { get; set; } = new();
        public List<ModifierEffect> GlobalEffects { get; set; } = new();
    }

    public class ModifierEffect
    {
        public ModifierEffectType Type { get; set; }
        public float Value { get; set; }
        public Element? Element { get; set; }
        public BuffType? BuffType { get; set; }
        public DebuffType? DebuffType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public enum ModifierEffectType
    {
        StatMultiplier = 1,
        DamageMultiplier = 2,
        HealingMultiplier = 3,
        ShieldMultiplier = 4,
        CooldownMultiplier = 5,
        EnergyGainMultiplier = 6,
        BuffDurationMultiplier = 7,
        DebuffDurationMultiplier = 8,
        ElementalDamageBonus = 9,
        ElementalResistanceBonus = 10,
        StartWithBuff = 11,
        StartWithDebuff = 12,
        TurnStartEffect = 13,
        TurnEndEffect = 14,
        OnKillEffect = 15,
        OnHitEffect = 16,
        OnCritEffect = 17,
        OnDodgeEffect = 18
    }

    public class UserTowerProgress : BaseEntity
    {
        public Guid UserId { get; private set; }
        public int HighestFloorCleared { get; private set; }
        public int CurrentFloor { get; private set; }
        public int TotalStars { get; private set; }
        public Dictionary<int, TowerFloorClearData> ClearedFloors { get; private set; } = new();
        public DateTime LastClearedAt { get; private set; }
        public DateTime SeasonStartedAt { get; private set; }
        public int SeasonNumber { get; private set; }
        public List<int> ClaimedFloorRewards { get; private set; } = new.
        public List<int> ClaimedStarRewards { get; private set; } = new.

        private UserTowerProgress() { }

        public UserTowerProgress(Guid userId, int seasonNumber)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            HighestFloorCleared = 0;
            CurrentFloor = 1;
            TotalStars = 0;
            SeasonNumber = seasonNumber;
            SeasonStartedAt = DateTime.UtcNow.
            CreatedAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void ClearFloor(int floorNumber, int stars, int turnsUsed, TimeSpan timeUsed)
        {
            var data = new TowerFloorClearData
            {
                FloorNumber = floorNumber,
                Stars = stars,
                TurnsUsed = turnsUsed,
                TimeUsed = timeUsed,
                ClearedAt = DateTime.UtcNow.
            };

            ClearedFloors[floorNumber] = data;

            if (floorNumber > HighestFloorCleared)
            {
                HighestFloorCleared = floorNumber;
                CurrentFloor = floorNumber + 1;
            }

            if (stars > GetPreviousStars(floorNumber))
            {
                TotalStars += stars - GetPreviousStars(floorNumber);
            }

            LastClearedAt = DateTime.UtcNow.
            UpdatedAt = DateTime.UtcNow.
        }

        public void ResetSeason(int newSeasonNumber)
        {
            SeasonNumber = newSeasonNumber;
            SeasonStartedAt = DateTime.UtcNow.
            CurrentFloor = 1;
            ClearedFloors.Clear();
            ClaimedFloorRewards.Clear();
            ClaimedStarRewards.Clear();
            TotalStars = 0;
            UpdatedAt = DateTime.UtcNow.
        }

        public bool IsFloorCleared(int floor) => ClearedFloors.ContainsKey(floor);
        public int GetStarsForFloor(int floor) => ClearedFloors.TryGetValue(floor, out var data) ? data.Stars : 0;

        public void ClaimFloorReward(int floor)
        {
            if (!ClaimedFloorRewards.Contains(floor))
            {
                ClaimedFloorRewards.Add(floor);
                UpdatedAt = DateTime.UtcNow.
            }
        }

        public void ClaimStarReward(int starThreshold)
        {
            if (!ClaimedStarRewards.Contains(starThreshold))
            {
                ClaimedStarRewards.Add(starThreshold);
                UpdatedAt = DateTime.UtcNow.
            }
        }

        private int GetPreviousStars(int floor) => ClearedFloors.TryGetValue(floor, out var data) ? data.Stars : 0;
    }

    public class TowerFloorClearData
    {
        public int FloorNumber { get; set; }
        public int Stars { get; set; }
        public int TurnsUsed { get; set; }
        public TimeSpan TimeUsed { get; set; }
        public DateTime ClearedAt { get; set; }
    }
}