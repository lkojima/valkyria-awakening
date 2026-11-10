using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Equipment : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public EquipmentSlot Slot { get; private set; }
        public EquipmentRarity Rarity { get; private set; }
        public int RequiredLevel { get; private set; }

        // Base Stats
        public int BaseHP { get; private set; }
        public int BaseAttack { get; private set; }
        public int BaseDefense { get; private set; }
        public int BaseSpeed { get; private set; }
        public float BaseCritRate { get; private set; }
        public float BaseCritDamage { get; private set; }
        public float BaseResistance { get; private set; }
        public float BaseAccuracy { get; private set; }

        // Sub-stats (random rolls)
        public List<EquipmentSubStat> SubStats { get; private set; } = new();
        public int MaxSubStats { get; private set; }

        // Set
        public string SetName { get; private set; }
        public List<SetBonus> SetBonuses { get; private set; } = new();

        // Enhancement
        public int EnhancementLevel { get; private set; }
        public int MaxEnhancementLevel { get; private set; }
        public List<EnhancementCost> EnhancementCosts { get; private set; } = new();

        // Visual
        public string IconPath { get; private set; }
        public string ModelPath { get; private set; }

        private Equipment() { }

        public Equipment(
            string name,
            string description,
            EquipmentSlot slot,
            EquipmentRarity rarity,
            int requiredLevel,
            int baseHP,
            int baseAttack,
            int baseDefense,
            int baseSpeed,
            float baseCritRate,
            float baseCritDamage,
            float baseResistance,
            float baseAccuracy,
            int maxSubStats,
            string setName,
            int maxEnhancementLevel,
            string iconPath,
            string modelPath)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Slot = slot;
            Rarity = rarity;
            RequiredLevel = requiredLevel;
            BaseHP = baseHP;
            BaseAttack = baseAttack;
            BaseDefense = baseDefense;
            BaseSpeed = baseSpeed;
            BaseCritRate = baseCritRate;
            BaseCritDamage = baseCritDamage;
            BaseResistance = baseResistance;
            BaseAccuracy = baseAccuracy;
            MaxSubStats = maxSubStats;
            SetName = setName;
            MaxEnhancementLevel = maxEnhancementLevel;
            EnhancementLevel = 0;
            IconPath = iconPath;
            ModelPath = modelPath;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddSubStat(EquipmentSubStat subStat)
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

            SubStats[index] = SubStats[index] with { Value = newValue, IsUpgraded = true };
            UpdatedAt = DateTime.UtcNow;
        }

        public void Enhance(List<EnhancementCost> costs, Action<CurrencyAmount> spendCurrency, Action<Guid, int> spendMaterials)
        {
            if (EnhancementLevel >= MaxEnhancementLevel)
                throw new InvalidOperationException("Already at max enhancement");

            var nextLevel = EnhancementLevel + 1;
            var cost = EnhancementCosts.FirstOrDefault(c => c.Level == nextLevel);
            if (cost == null)
                throw new InvalidOperationException("No cost defined for next level");

            foreach (var currency in cost.Currencies)
            {
                spendCurrency(currency);
            }

            foreach (var material in cost.Materials)
            {
                spendMaterials(material.ResourceId, material.Quantity);
            }

            EnhancementLevel = nextLevel;
            ApplyEnhancementBonus();
            UpdatedAt = DateTime.UtcNow;
        }

        private void ApplyEnhancementBonus()
        {
            // Base stats increase by ~5% per enhancement level
            var multiplier = 1f + EnhancementLevel * 0.05f;
            // This would be calculated dynamically when getting stats
        }

        public EquipmentStats GetTotalStats()
        {
            var enhancementMultiplier = 1f + EnhancementLevel * 0.05f;

            return new EquipmentStats
            {
                HP = (int)(BaseHP * enhancementMultiplier) + SubStats.Where(s => s.Type == SubStatType.HP).Sum(s => s.Value),
                Attack = (int)(BaseAttack * enhancementMultiplier) + SubStats.Where(s => s.Type == SubStatType.Attack).Sum(s => s.Value),
                Defense = (int)(BaseDefense * enhancementMultiplier) + SubStats.Where(s => s.Type == SubStatType.Defense).Sum(s => s.Value),
                Speed = (int)(BaseSpeed * enhancementMultiplier) + SubStats.Where(s => s.Type == SubStatType.Speed).Sum(s => s.Value),
                CritRate = BaseCritRate + SubStats.Where(s => s.Type == SubStatType.CritRate).Sum(s => s.Value),
                CritDamage = BaseCritDamage + SubStats.Where(s => s.Type == SubStatType.CritDamage).Sum(s => s.Value),
                Resistance = BaseResistance + SubStats.Where(s => s.Type == SubStatType.Resistance).Sum(s => s.Value),
                Accuracy = BaseAccuracy + SubStats.Where(s => s.Type == SubStatType.Accuracy).Sum(s => s.Value),
                HPPercent = SubStats.Where(s => s.Type == SubStatType.HPPercent).Sum(s => s.Value),
                AttackPercent = SubStats.Where(s => s.Type == SubStatType.AttackPercent).Sum(s => s.Value),
                DefensePercent = SubStats.Where(s => s.Type == SubStatType.DefensePercent).Sum(s => s.Value)
            };
        }
    }

    public class EquipmentSubStat
    {
        public SubStatType Type { get; set; }
        public float Value { get; set; }
        public int RollCount { get; set; }
        public bool IsUpgraded { get; set; }
        public float MaxValue { get; set; }
    }

    public class EnhancementCost
    {
        public int Level { get; set; }
        public List<CurrencyAmount> Currencies { get; set; } = new();
        public List<ResourceQuantity> Materials { get; set; } = new();
        public int SuccessRate { get; set; } = 100; // Percentage
        public bool CanFail { get; set; }
        public bool CanDowngrade { get; set; }
    }

    public class EquipmentStats
    {
        public int HP { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public float CritRate { get; set; }
        public float CritDamage { get; set; }
        public float Resistance { get; set; }
        public float Accuracy { get; set; }
        public float HPPercent { get; set; }
        public float AttackPercent { get; set; }
        public float DefensePercent { get; set; }
    }

    public enum SubStatType
    {
        HP = 1,
        Attack = 2,
        Defense = 3,
        Speed = 4,
        CritRate = 5,
        CritDamage = 6,
        Resistance = 7,
        Accuracy = 8,
        HPPercent = 9,
        AttackPercent = 10,
        DefensePercent = 11,
        ElementalDamage = 12,
        ElementalResistance = 13,
        HealingBonus = 14,
        ShieldBonus = 15,
        EnergyRegen = 16,
        CooldownReduction = 17
    }
}