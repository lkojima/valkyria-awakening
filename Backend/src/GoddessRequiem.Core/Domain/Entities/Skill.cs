using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Skill : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string ShortDescription { get; private set; }
        public SkillType Type { get; private set; }
        public Element Element { get; private set; }
        public SkillTargetType TargetType { get; private set; }
        public int TargetCount { get; private set; }
        public int Cooldown { get; private set; }
        public int EnergyCost { get; private set; }
        public int MaxLevel { get; private set; }

        // Scaling
        public float BaseDamageMultiplier { get; private set; }
        public float DamageMultiplierPerLevel { get; private set; }
        public float BaseHealMultiplier { get; private set; }
        public float HealMultiplierPerLevel { get; private set; }
        public float BaseShieldMultiplier { get; private set; }
        public float ShieldMultiplierPerLevel { get; private set; }

        // Effects
        public List<SkillEffect> Effects { get; private set; } = new();
        public List<SkillEffect> UpgradeEffects { get; private set; } = new(); // Additional effects per level

        // Visual
        public string IconPath { get; private set; }
        public string AnimationPath { get; private set; }
        public string SoundEffectPath { get; private set; }
        public string VFXPath { get; private set; }

        // AI
        public int AIPriority { get; private set; }
        public AICondition[] AIConditions { get; private set; } = Array.Empty<AICondition>();

        private Skill() { }

        public Skill(
            string name,
            string description,
            string shortDescription,
            SkillType type,
            Element element,
            SkillTargetType targetType,
            int targetCount,
            int cooldown,
            int energyCost,
            int maxLevel,
            float baseDamageMultiplier,
            float damageMultiplierPerLevel,
            float baseHealMultiplier,
            float healMultiplierPerLevel,
            float baseShieldMultiplier,
            float shieldMultiplierPerLevel,
            string iconPath,
            string animationPath,
            string soundEffectPath,
            string vfxPath,
            int aiPriority = 50)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            ShortDescription = shortDescription;
            Type = type;
            Element = element;
            TargetType = targetType;
            TargetCount = targetCount;
            Cooldown = cooldown;
            EnergyCost = energyCost;
            MaxLevel = maxLevel;
            BaseDamageMultiplier = baseDamageMultiplier;
            DamageMultiplierPerLevel = damageMultiplierPerLevel;
            BaseHealMultiplier = baseHealMultiplier;
            HealMultiplierPerLevel = healMultiplierPerLevel;
            BaseShieldMultiplier = baseShieldMultiplier;
            ShieldMultiplierPerLevel = shieldMultiplierPerLevel;
            IconPath = iconPath;
            AnimationPath = animationPath;
            SoundEffectPath = soundEffectPath;
            VFXPath = vfxPath;
            AIPriority = aiPriority;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddEffect(SkillEffect effect)
        {
            Effects.Add(effect);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddUpgradeEffect(int level, SkillEffect effect)
        {
            // This would be handled by a separate upgrade configuration
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAIConditions(AICondition[] conditions)
        {
            AIConditions = conditions;
            UpdatedAt = DateTime.UtcNow;
        }

        public SkillData GetData(int level)
        {
            if (level < 1) level = 1;
            if (level > MaxLevel) level = MaxLevel;

            return new SkillData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Type = Type,
                Element = Element,
                TargetType = TargetType,
                TargetCount = TargetCount,
                Cooldown = Cooldown,
                EnergyCost = EnergyCost,
                Level = level,
                DamageMultiplier = BaseDamageMultiplier + (level - 1) * DamageMultiplierPerLevel,
                HealMultiplier = BaseHealMultiplier + (level - 1) * HealMultiplierPerLevel,
                ShieldMultiplier = BaseShieldMultiplier + (level - 1) * ShieldMultiplierPerLevel,
                Effects = Effects,
                IconPath = IconPath,
                AnimationPath = AnimationPath,
                SoundEffectPath = SoundEffectPath,
                VFXPath = VFXPath
            };
        }
    }

    public class SkillEffect
    {
        public SkillEffectType Type { get; set; }
        public float Value { get; set; }
        public float Chance { get; set; } = 1f;
        public int Duration { get; set; } // Turns
        public int TickInterval { get; set; } = 1;
        public Element? Element { get; set; }
        public BuffType? BuffType { get; set; }
        public DebuffType? DebuffType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Description { get; set; }
        public bool IsStackable { get; set; }
        public int MaxStacks { get; set; } = 1;
        public bool IsDispellable { get; set; } = true;
        public bool IgnoresImmunity { get; set; } = false;
    }

    public class AICondition
    {
        public string ConditionType { get; set; } // HPBelow, EnemyCount, AllyBuffed, etc.
        public float Threshold { get; set; }
        public string Comparison { get; set; } // <, >, <=, >=, ==
        public int Priority { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class SkillData
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public SkillType Type { get; set; }
        public Element Element { get; set; }
        public SkillTargetType TargetType { get; set; }
        public int TargetCount { get; set; }
        public int Cooldown { get; set; }
        public int EnergyCost { get; set; }
        public int Level { get; set; }
        public float DamageMultiplier { get; set; }
        public float HealMultiplier { get; set; }
        public float ShieldMultiplier { get; set; }
        public List<SkillEffect> Effects { get; set; } = new();
        public string IconPath { get; set; }
        public string AnimationPath { get; set; }
        public string SoundEffectPath { get; set; }
        public string VFXPath { get; set; }
    }
}