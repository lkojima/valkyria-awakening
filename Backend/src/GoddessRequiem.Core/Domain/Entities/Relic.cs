using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Relic : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public RelicType Type { get; private set; }
        public EquipmentRarity Rarity { get; private set; }
        public int RequiredLevel { get; private set; }

        // Main Stat
        public RelicMainStat MainStat { get; private set; }

        // Sub-stats
        public List<RelicSubStat> SubStats { get; private set; } = new();
        public int MaxSubStats { get; private set; }

        // Set
        public string SetName { get; private set; }
        public List<SetBonus> SetBonuses { get; private set; } = new();

        // Enhancement
        public int EnhancementLevel { get; private set; }
        public int MaxEnhancementLevel { get; private set; }

        // Visual
        public string IconPath { get; private set; }
        public string ModelPath { get; private set; }

        private Relic() { }

        public Relic(
            string name,
            string description,
            RelicType type,
            EquipmentRarity rarity,
            int requiredLevel,
            RelicMainStat mainStat,
            int maxSubStats,
            string setName,
            int maxEnhancementLevel,
            string iconPath,
            string modelPath)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            Rarity = rarity;
            RequiredLevel = requiredLevel;
            MainStat = mainStat;
            MaxSubStats = maxSubStats;
            SetName = setName;
            MaxEnhancementLevel = maxEnhancementLevel;
            EnhancementLevel = 0;
            IconPath = iconPath;
            ModelPath = modelPath;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddSubStat(RelicSubStat subStat)
        {
            if (SubStats.Count >= MaxSubStats)
                throw new InvalidOperationException("Max sub-stats reached");

            SubStats.Add(subStat);
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpgradeSubStat(int index, float newValue)
        {
            if (index < 0 || index >= SubStats.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            SubStats[index] = SubStats[index] with { Value = newValue };
            UpdatedAt = DateTime.UtcNow;
        }

        public void Enhance()
        {
            if (EnhancementLevel >= MaxEnhancementLevel)
                throw new InvalidOperationException("Already at max enhancement");

            EnhancementLevel++;

            // At certain levels, unlock/upgrade sub-stats
            if (EnhancementLevel % 3 == 0 && SubStats.Count < MaxSubStats)
            {
                // New sub-stat unlocked (would be randomly generated)
            }
            else if (SubStats.Count > 0)
            {
                // Upgrade random existing sub-stat
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public RelicStats GetTotalStats()
        {
            var enhancementMultiplier = 1f + EnhancementLevel * 0.03f;

            return new RelicStats
            {
                MainStatValue = MainStat.Value * enhancementMultiplier,
                SubStats = SubStats.ToDictionary(s => s.Type, s => s.Value * enhancementMultiplier)
            };
        }
    }

    public class RelicMainStat
    {
        public RelicMainStatType Type { get; set; }
        public float Value { get; set; }
    }

    public class RelicSubStat
    {
        public RelicSubStatType Type { get; set; }
        public float Value { get; set; }
        public int Rolls { get; set; }
    }

    public class RelicStats
    {
        public float MainStatValue { get; set; }
        public Dictionary<RelicSubStatType, float> SubStats { get; set; } = new();
    }

    public enum RelicType
    {
        Flower = 1,
        Feather = 2,
        Sands = 3,
        Goblet = 4,
        Circlet = 5
    }

    public enum RelicMainStatType
    {
        HP = 1,
        Attack = 2,
        Defense = 3,
        HPPercent = 4,
        AttackPercent = 5,
        DefensePercent = 6,
        CritRate = 7,
        CritDamage = 8,
        HealingBonus = 9,
        ElementalDamageBonus = 10,
        EnergyRecharge = 11
    }

    public enum RelicSubStatType
    {
        HP = 1,
        Attack = 2,
        Defense = 3,
        Speed = 4,
        HPPercent = 5,
        AttackPercent = 6,
        DefensePercent = 7,
        CritRate = 8,
        CritDamage = 9,
        Resistance = 10,
        Accuracy = 11,
        ElementalResistance = 12
    }
}