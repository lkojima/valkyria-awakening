using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Character : BaseEntity
    {
        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string Lore { get; private set; }
        public Rarity Rarity { get; private set; }
        public Element Element { get; private set; }
        public CharacterClass Class { get; private set; }
        public CharacterRole Role { get; private set; }

        // Base Stats
        public int BaseHP { get; private set; }
        public int BaseAttack { get; private set; }
        public int BaseDefense { get; private set; }
        public int BaseSpeed { get; private set; }
        public float BaseCritRate { get; private set; }
        public float BaseCritDamage { get; private set; }
        public float BaseResistance { get; private set; }
        public float BaseAccuracy { get; private set; }

        // Growth Rates (per level)
        public float HPGrowth { get; private set; }
        public float AttackGrowth { get; private set; }
        public float DefenseGrowth { get; private set; }
        public float SpeedGrowth { get; private set; }

        // Visual
        public string PortraitAssetPath { get; private set; }
        public string FullBodyAssetPath { get; private set; }
        public string SpriteAssetPath { get; private set; }
        public string Live2DModelPath { get; private set; }
        public string SummonAnimationPath { get; private set; }
        public string SkillAnimationPaths { get; private set; } // JSON array

        // Audio
        public string VoiceActor { get; private set; }
        public string VoiceLinesPath { get; private set; }

        // Skills
        public Guid BasicSkillId { get; private set; }
        public Guid SpecialSkill1Id { get; private set; }
        public Guid SpecialSkill2Id { get; private set; }
        public Guid UltimateSkillId { get; private set; }

        // Evolution
        public int MaxLevel { get; private set; }
        public int MaxAscension { get; private set; }
        public Guid? EvolutionCharacterId { get; private set; }
        public List<CharacterEvolutionMaterial> EvolutionMaterials { get; private set; } = new();

        // Affinity
        public int MaxAffinityLevel { get; private set; }
        public List<AffinityReward> AffinityRewards { get; private set; } = new();

        // Metadata
        public DateTime ReleaseDate { get; private set; }
        public bool IsLimited { get; private set; }
        public bool IsObtainable { get; private set; }
        public List<string> Tags { get; private set; } = new();

        private Character() { } // EF Core

        public Character(
            string name,
            string displayName,
            string description,
            string lore,
            Rarity rarity,
            Element element,
            CharacterClass characterClass,
            CharacterRole role,
            int baseHP,
            int baseAttack,
            int baseDefense,
            int baseSpeed,
            float baseCritRate,
            float baseCritDamage,
            float baseResistance,
            float baseAccuracy,
            float hpGrowth,
            float attackGrowth,
            float defenseGrowth,
            float speedGrowth,
            string portraitAssetPath,
            string fullBodyAssetPath,
            string spriteAssetPath,
            Guid basicSkillId,
            Guid specialSkill1Id,
            Guid specialSkill2Id,
            Guid ultimateSkillId,
            int maxLevel = 80,
            int maxAscension = 6)
        {
            Id = Guid.NewGuid();
            Name = name;
            DisplayName = displayName;
            Description = description;
            Lore = lore;
            Rarity = rarity;
            Element = element;
            Class = characterClass;
            Role = role;
            BaseHP = baseHP;
            BaseAttack = baseAttack;
            BaseDefense = baseDefense;
            BaseSpeed = baseSpeed;
            BaseCritRate = baseCritRate;
            BaseCritDamage = baseCritDamage;
            BaseResistance = baseResistance;
            BaseAccuracy = baseAccuracy;
            HPGrowth = hpGrowth;
            AttackGrowth = attackGrowth;
            DefenseGrowth = defenseGrowth;
            SpeedGrowth = speedGrowth;
            PortraitAssetPath = portraitAssetPath;
            FullBodyAssetPath = fullBodyAssetPath;
            SpriteAssetPath = spriteAssetPath;
            BasicSkillId = basicSkillId;
            SpecialSkill1Id = specialSkill1Id;
            SpecialSkill2Id = specialSkill2Id;
            UltimateSkillId = ultimateSkillId;
            MaxLevel = maxLevel;
            MaxAscension = maxAscension;
            ReleaseDate = DateTime.UtcNow;
            IsLimited = false;
            IsObtainable = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateStats(int hp, int attack, int defense, int speed, float critRate, float critDamage, float resistance, float accuracy)
        {
            BaseHP = hp;
            BaseAttack = attack;
            BaseDefense = defense;
            BaseSpeed = speed;
            BaseCritRate = critRate;
            BaseCritDamage = critDamage;
            BaseResistance = resistance;
            BaseAccuracy = accuracy;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetVisualAssets(string portrait, string fullBody, string sprite, string live2D = null, string summonAnim = null, string skillAnims = null)
        {
            PortraitAssetPath = portrait;
            FullBodyAssetPath = fullBody;
            SpriteAssetPath = sprite;
            Live2DModelPath = live2D;
            SummonAnimationPath = summonAnim;
            SkillAnimationPaths = skillAnims;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAudio(string voiceActor, string voiceLinesPath)
        {
            VoiceActor = voiceActor;
            VoiceLinesPath = voiceLinesPath;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetEvolution(Guid? evolutionCharacterId, List<CharacterEvolutionMaterial> materials)
        {
            EvolutionCharacterId = evolutionCharacterId;
            EvolutionMaterials = materials;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAffinity(int maxLevel, List<AffinityReward> rewards)
        {
            MaxAffinityLevel = maxLevel;
            AffinityRewards = rewards;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetLimited(bool isLimited)
        {
            IsLimited = isLimited;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetObtainable(bool obtainable)
        {
            IsObtainable = obtainable;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddTag(string tag)
        {
            if (!Tags.Contains(tag))
            {
                Tags.Add(tag);
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public CharacterStats CalculateStats(int level, int ascension, int affinityLevel, EquipmentSet equipment, RelicSet relics, RuneSet runes)
        {
            float levelMultiplier = 1f + (level - 1) * 0.02f;
            float ascensionMultiplier = 1f + ascension * 0.05f;
            float affinityMultiplier = 1f + affinityLevel * 0.01f;

            var stats = new CharacterStats
            {
                HP = (int)(BaseHP * levelMultiplier * ascensionMultiplier * affinityMultiplier) + equipment.HPBonus + relics.HPBonus + runes.HPBonus,
                Attack = (int)(BaseAttack * levelMultiplier * ascensionMultiplier * affinityMultiplier) + equipment.AttackBonus + relics.AttackBonus + runes.AttackBonus,
                Defense = (int)(BaseDefense * levelMultiplier * ascensionMultiplier * affinityMultiplier) + equipment.DefenseBonus + relics.DefenseBonus + runes.DefenseBonus,
                Speed = (int)(BaseSpeed * levelMultiplier * ascensionMultiplier) + equipment.SpeedBonus + relics.SpeedBonus + runes.SpeedBonus,
                CritRate = Math.Min(1f, BaseCritRate + equipment.CritRateBonus + relics.CritRateBonus + runes.CritRateBonus),
                CritDamage = BaseCritDamage + equipment.CritDamageBonus + relics.CritDamageBonus + runes.CritDamageBonus,
                Resistance = Math.Min(1f, BaseResistance + equipment.ResistanceBonus + relics.ResistanceBonus + runes.ResistanceBonus),
                Accuracy = Math.Min(1f, BaseAccuracy + equipment.AccuracyBonus + relics.AccuracyBonus + runes.AccuracyBonus)
            };

            // Apply set bonuses
            ApplySetBonuses(stats, equipment, relics, runes);

            return stats;
        }

        private void ApplySetBonuses(CharacterStats stats, EquipmentSet equipment, RelicSet relics, RuneSet runes)
        {
            // Equipment set bonuses
            foreach (var set in equipment.SetBonuses)
            {
                ApplySetBonus(stats, set);
            }

            // Relic set bonuses
            foreach (var set in relics.SetBonuses)
            {
                ApplySetBonus(stats, set);
            }

            // Rune set bonuses
            foreach (var set in runes.SetBonuses)
            {
                ApplySetBonus(stats, set);
            }
        }

        private void ApplySetBonus(CharacterStats stats, SetBonus bonus)
        {
            switch (bonus.Type)
            {
                case SetBonusType.HPPercent:
                    stats.HP = (int)(stats.HP * (1 + bonus.Value));
                    break;
                case SetBonusType.AttackPercent:
                    stats.Attack = (int)(stats.Attack * (1 + bonus.Value));
                    break;
                case SetBonusType.DefensePercent:
                    stats.Defense = (int)(stats.Defense * (1 + bonus.Value));
                    break;
                case SetBonusType.SpeedFlat:
                    stats.Speed += (int)bonus.Value;
                    break;
                case SetBonusType.CritRate:
                    stats.CritRate = Math.Min(1f, stats.CritRate + bonus.Value);
                    break;
                case SetBonusType.CritDamage:
                    stats.CritDamage += bonus.Value;
                    break;
                case SetBonusType.ElementalDamage:
                    // Handled in damage calculation
                    break;
            }
        }
    }

    public class CharacterEvolutionMaterial
    {
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; }
        public int Quantity { get; set; }
        public int RequiredAscensionLevel { get; set; }
    }

    public class AffinityReward
    {
        public int AffinityLevel { get; set; }
        public RewardType RewardType { get; set; }
        public Guid RewardId { get; set; }
        public int Quantity { get; set; }
        public string Description { get; set; }
    }

    public class CharacterStats
    {
        public int HP { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public float CritRate { get; set; }
        public float CritDamage { get; set; }
        public float Resistance { get; set; }
        public float Accuracy { get; set; }

        // Elemental bonuses
        public float FireDamageBonus { get; set; }
        public float WaterDamageBonus { get; set; }
        public float NatureDamageBonus { get; set; }
        public float LightDamageBonus { get; set; }
        public float DarkDamageBonus { get; set; }

        public float FireResistanceBonus { get; set; }
        public float WaterResistanceBonus { get; set; }
        public float NatureResistanceBonus { get; set; }
        public float LightResistanceBonus { get; set; }
        public float DarkResistanceBonus { get; set; }
    }

    public class EquipmentSet
    {
        public int HPBonus { get; set; }
        public int AttackBonus { get; set; }
        public int DefenseBonus { get; set; }
        public int SpeedBonus { get; set; }
        public float CritRateBonus { get; set; }
        public float CritDamageBonus { get; set; }
        public float ResistanceBonus { get; set; }
        public float AccuracyBonus { get; set; }
        public List<SetBonus> SetBonuses { get; set; } = new();
    }

    public class RelicSet
    {
        public int HPBonus { get; set; }
        public int AttackBonus { get; set; }
        public int DefenseBonus { get; set; }
        public int SpeedBonus { get; set; }
        public float CritRateBonus { get; set; }
        public float CritDamageBonus { get; set; }
        public float ResistanceBonus { get; set; }
        public float AccuracyBonus { get; set; }
        public List<SetBonus> SetBonuses { get; set; } = new();
    }

    public class RuneSet
    {
        public int HPBonus { get; set; }
        public int AttackBonus { get; set; }
        public int DefenseBonus { get; set; }
        public int SpeedBonus { get; set; }
        public float CritRateBonus { get; set; }
        public float CritDamageBonus { get; set; }
        public float ResistanceBonus { get; set; }
        public float AccuracyBonus { get; set; }
        public List<SetBonus> SetBonuses { get; set; } = new();
    }

    public class SetBonus
    {
        public SetBonusType Type { get; set; }
        public float Value { get; set; }
        public int RequiredPieces { get; set; }
        public string SetName { get; set; }
    }

    public enum SetBonusType
    {
        HPPercent,
        AttackPercent,
        DefensePercent,
        SpeedFlat,
        CritRate,
        CritDamage,
        ElementalDamage,
        ElementalResistance,
        HealingReceived,
        ShieldStrength,
        DebuffResistance,
        BuffDuration
    }
}