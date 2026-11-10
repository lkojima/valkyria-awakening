using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Rune : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public RuneType Type { get; private set; }
        public RuneSlot Slot { get; private set; }
        public EquipmentRarity Rarity { get; private set; }
        public int RequiredLevel { get; private set; }

        // Primary Effect
        public RuneEffect PrimaryEffect { get; private set; }

        // Secondary Effects (unlock at enhancement levels)
        public List<RuneEffect> SecondaryEffects { get; private set; } = new();
        public int MaxSecondaryEffects { get; private set; }

        // Set
        public string SetName { get; private set; }
        public List<SetBonus> SetBonuses { get; private set; } = new();

        // Enhancement
        public int EnhancementLevel { get; private set; }
        public int MaxEnhancementLevel { get; private set; }

        // Visual
        public string IconPath { get; private set; }
        public string ModelPath { get; private set; }

        private Rune() { }

        public Rune(
            string name,
            string description,
            RuneType type,
            RuneSlot slot,
            EquipmentRarity rarity,
            int requiredLevel,
            RuneEffect primaryEffect,
            int maxSecondaryEffects,
            string setName,
            int maxEnhancementLevel,
            string iconPath,
            string modelPath)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            Slot = slot;
            Rarity = rarity;
            RequiredLevel = requiredLevel;
            PrimaryEffect = primaryEffect;
            MaxSecondaryEffects = maxSecondaryEffects;
            SetName = setName;
            MaxEnhancementLevel = maxEnhancementLevel;
            EnhancementLevel = 0;
            IconPath = iconPath;
            ModelPath = modelPath;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddSecondaryEffect(RuneEffect effect)
        {
            if (SecondaryEffects.Count >= MaxSecondaryEffects)
                throw new InvalidOperationException("Max secondary effects reached");

            SecondaryEffects.Add(effect);
            UpdatedAt = DateTime.UtcNow;
        }

        public void Enhance()
        {
            if (EnhancementLevel >= MaxEnhancementLevel)
                throw new InvalidOperationException("Already at max enhancement");

            EnhancementLevel++;

            // Unlock secondary effects at specific levels
            if (EnhancementLevel == 3 || EnhancementLevel == 6 || EnhancementLevel == 9 || EnhancementLevel == 12)
            {
                // Would unlock a new secondary effect (randomly generated)
            }

            // Upgrade primary effect
            PrimaryEffect = PrimaryEffect with { Value = PrimaryEffect.Value * 1.1f };

            // Upgrade secondary effects
            for (int i = 0; i < SecondaryEffects.Count; i++)
            {
                SecondaryEffects[i] = SecondaryEffects[i] with { Value = SecondaryEffects[i].Value * 1.05f };
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public RuneStats GetTotalStats()
        {
            var effects = new List<RuneEffect> { PrimaryEffect };
            effects.AddRange(SecondaryEffects);

            return new RuneStats
            {
                Effects = effects
            };
        }
    }

    public class RuneEffect
    {
        public RuneEffectType Type { get; set; }
        public float Value { get; set; }
        public float Chance { get; set; } = 1f;
        public int Cooldown { get; set; } = 0;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class RuneStats
    {
        public List<RuneEffect> Effects { get; set; } = new();
    }

    public enum RuneType
    {
        Offense = 1,
        Defense = 2,
        Utility = 3,
        Elemental = 4,
        Special = 5
    }

    public enum RuneSlot
    {
        Slot1 = 1,
        Slot2 = 2,
        Slot3 = 3,
        Slot4 = 4,
        Slot5 = 5,
        Slot6 = 6
    }

    public enum RuneEffectType
    {
        AttackPercent = 1,
        DefensePercent = 2,
        HPPercent = 3,
        SpeedPercent = 4,
        CritRate = 5,
        CritDamage = 6,
        ElementalDamage = 7,
        ElementalResistance = 8,
        HealingEffectiveness = 9,
        ShieldStrength = 10,
        DebuffResistance = 11,
        BuffDuration = 12,
        CooldownReduction = 13,
        EnergyRegeneration = 14,
        Lifesteal = 15,
        CounterChance = 16,
        ReflectDamage = 17,
        ExecuteThreshold = 18,
        ReviveChance = 19,
        SummonPower = 20,
        SkillDamage = 21,
        UltimateDamage = 22,
        FirstStrike = 23,
        LastStand = 24,
        AdaptiveDamage = 25
    }
}