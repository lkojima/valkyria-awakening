using UnityEngine;
using System.Collections.Generic;
using System;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Combat
{
    [System.Serializable]
    public class SkillData
    {
        public Guid Id;
        public string Name;
        public string Description;
        public SkillType Type;
        public SkillTargetType TargetType;
        public SkillEffectType EffectType;
        public Element Element;
        public int BaseDamage;
        public int BaseHeal;
        public float AttackMultiplier;
        public float HealMultiplier;
        public int MPCost;
        public int Cooldown;
        public int CurrentCooldown;
        public int TargetCount;
        public bool TargetsEnemies;
        public bool TargetsAllies;
        public string AnimationTrigger;
        public Sprite Icon;
        public List<StatusEffectData> Effects = new();
    }

    [System.Serializable]
    public class StatusEffectData
    {
        public StatusEffectType Type;
        public float Value;
        public int Duration;
        public float Chance;
        public Sprite Icon;
    }

    [System.Serializable]
    public class EquipmentSetup
    {
        public Guid EquipmentId;
        public EquipmentSlot Slot;
        public int Level;
        public int RefinementLevel;
        public List<EquipmentStat> Stats = new();
        public List<SetBonus> SetBonuses = new();
    }

    [System.Serializable]
    public class RelicSetup
    {
        public Guid RelicId;
        public int Level;
        public EquipmentStat MainStat;
        public List<EquipmentStat> SubStats = new();
        public List<SetBonus> SetBonuses = new();
    }

    [System.Serializable]
    public class RuneSetup
    {
        public Guid RuneId;
        public int Level;
        public int SlotIndex;
        public List<EquipmentStat> Stats = new();
        public List<SetBonus> SetBonuses = new();
    }

    [System.Serializable]
    public class UnitSetup
    {
        public string CharacterId;
        public string CharacterName;
        public int Level;
        public int Ascension;
        public Rarity Rarity;
        public Element Element;
        public CharacterClass Class;
        public CharacterRole Role;
        public int BaseHP;
        public int BaseAttack;
        public int BaseDefense;
        public int BaseSpeed;
        public float BaseCritRate;
        public float BaseCritDamage;
        public float BaseResistance;
        public float BaseAccuracy;
        public List<SkillData> Skills = new();
        public EquipmentSetup Equipment;
        public RelicSetup Relic;
        public List<RuneSetup> Runes = new();
        public string SpriteAssetPath;
        public string Live2DModelPath;
        public string PortraitAssetPath;
    }

    [System.Serializable]
    public class BattleSetup
    {
        public List<UnitSetup> PlayerUnits;
        public List<UnitSetup> EnemyUnits;
        public BattleSpeed Speed;
        public AutoBattleMode AutoMode;
        public string Seed;
        public int StageId;
        public GameMode Mode;
    }

    [System.Serializable]
    public class BattleResult
    {
        public BattleResult Outcome;
        public int Turns;
        public List<BattleUnit> PlayerUnits;
        public List<BattleUnit> EnemyUnits;
        public BattleAction[] ActionLog;
        public string Seed;
        public List<RewardDto> Rewards;
    }

    [System.Serializable]
    public class BattleAction
    {
        public BattleUnit Actor;
        public SkillData Skill;
        public List<BattleUnit> Targets;
        public List<ActionResult> Results;
    }

    [System.Serializable]
    public class ActionResult
    {
        public BattleUnit Target;
        public SkillData Skill;
        public int Value;
        public bool IsHit;
        public bool IsCrit;
        public bool IsDodged;
        public bool IsDamage;
        public bool IsHeal;
        public List<StatusEffectData> AppliedEffects = new();
    }

    [System.Serializable]
    public class RewardDto
    {
        public RewardType Type;
        public string ResourceId;
        public string ResourceName;
        public int Quantity;
        public Rarity Rarity;
    }

    // Enums (mirroring backend enums)
    public enum SkillType
    {
        Basic = 1,
        Special = 2,
        Ultimate = 3,
        Passive = 4,
        Counter = 5,
        Reaction = 6
    }

    public enum SkillTargetType
    {
        Self = 1,
        SingleAlly = 2,
        AllAllies = 3,
        SingleEnemy = 4,
        AllEnemies = 5,
        RandomEnemy = 6,
        RandomAlly = 7,
        FrontRow = 8,
        BackRow = 9,
        LowestHPAlly = 10,
        HighestHPEnemy = 11,
        Area = 12,
        Line = 13,
        Cross = 14
    }

    public enum SkillEffectType
    {
        Damage = 1,
        Heal = 2,
        Shield = 3,
        Buff = 4,
        Debuff = 5,
        Cleanse = 6,
        Dispel = 7,
        Taunt = 8,
        Stun = 9,
        Silence = 10,
        Root = 11,
        Sleep = 12,
        Fear = 13,
        Charm = 14,
        Provoke = 15,
        Stealth = 16,
        Invulnerability = 17,
        DamageReduction = 18,
        CritRateUp = 19,
        CritDamageUp = 20,
        AttackUp = 21,
        DefenseUp = 22,
        SpeedUp = 23,
        HPRegeneration = 24,
        ManaRegeneration = 25,
        EnergyGain = 26,
        EnergyDrain = 27,
        Revive = 28,
        Summon = 29,
        Transform = 30,
        Teleport = 31,
        Pull = 32,
        Push = 33,
        Swap = 34,
        Copy = 35,
        Reflect = 36,
        Absorb = 37,
        Convert = 38,
        Delay = 39,
        Accelerate = 40,
        Execute = 41,
        TrueDamage = 42,
        Pierce = 43,
        Lifesteal = 44,
        Thorns = 45,
        Counterattack = 46,
        FollowUp = 47,
        Chain = 48,
        Resonance = 49,
        Overload = 50
    }

    public enum StatusEffectType
    {
        AttackUp,
        DefenseUp,
        SpeedUp,
        CritRateUp,
        CritDamageUp,
        ResistanceUp,
        AccuracyUp,
        EvasionUp,
        HPRegeneration,
        Shield,
        Immunity,
        Invulnerability,
        DamageReduction,
        Lifesteal,
        Thorns,
        Counterattack,
        EnergyGain,
        CooldownReduction,
        BuffDurationUp,
        DebuffResistance,
        HealingReceivedUp,
        ShieldStrengthUp,
        ElementalDamageUp,
        ElementalResistanceUp,
        // Debuffs
        AttackDown,
        DefenseDown,
        SpeedDown,
        CritRateDown,
        CritDamageDown,
        ResistanceDown,
        AccuracyDown,
        EvasionDown,
        Poison,
        Burn,
        Bleed,
        Curse,
        Stun,
        Silence,
        Root,
        Sleep,
        Fear,
        Charm,
        Provoke,
        Taunt,
        Blind,
        Confusion,
        Petrify,
        Freeze,
        Shackle,
        EnergyDrain,
        CooldownIncrease,
        HealingReceivedDown,
        ShieldStrengthDown,
        ElementalDamageDown,
        ElementalResistanceDown,
        ArmorBreak,
        MagicBreak,
        TrueDamage,
        Execute
    }

    public enum Element
    {
        None = 0,
        Fire = 1,
        Water = 2,
        Nature = 3,
        Light = 4,
        Dark = 5
    }

    public enum Rarity
    {
        R = 1,
        SR = 2,
        SSR = 3,
        UR = 4
    }

    public enum CharacterClass
    {
        Warrior = 1,
        Mage = 2,
        Archer = 3,
        Assassin = 4,
        Tank = 5,
        Support = 6,
        Healer = 7,
        Summoner = 8,
        Bard = 9,
        Paladin = 10
    }

    public enum CharacterRole
    {
        DPS = 1,
        Tank = 2,
        Support = 3,
        Healer = 4,
        Controller = 5,
        Buffer = 6,
        Debuffer = 7,
        Hybrid = 8
    }

    public enum EquipmentSlot
    {
        Weapon = 1,
        Helmet = 2,
        Armor = 3,
        Gloves = 4,
        Boots = 5,
        Accessory1 = 6,
        Accessory2 = 7,
        Relic = 8
    }

    public enum RewardType
    {
        Currency = 1,
        Item = 2,
        Character = 3,
        CharacterFragment = 4,
        Equipment = 5,
        Relic = 6,
        Rune = 7,
        Skin = 8,
        Experience = 9,
        Title = 10,
        Avatar = 11,
        Frame = 12,
        ChatBubble = 13
    }

    public enum CurrencyType
    {
        Gold = 1,
        Gem = 2,
        SummonTicket = 3,
        PremiumSummonTicket = 4,
        Experience = 19,
        CharacterExp = 20,
        WeaponExp = 21,
        Stamina = 22,
        Energy = 23,
        ActionPoint = 24
    }

    [System.Serializable]
    public class EquipmentStat
    {
        public EquipmentStatType Type;
        public float Value;
        public bool IsPercentage;
    }

    public enum EquipmentStatType
    {
        HP,
        HPPercent,
        Attack,
        AttackPercent,
        Defense,
        DefensePercent,
        Speed,
        CritRate,
        CritDamage,
        Resistance,
        Accuracy,
        Evasion,
        ElementalDamage,
        ElementalResistance,
        HealingReceived,
        ShieldStrength
    }

    [System.Serializable]
    public class SetBonus
    {
        public SetBonusType Type;
        public float Value;
        public int RequiredPieces;
        public string SetName;
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

    // Extension for DOTween
    public static class TweenExtensions
    {
        public static UniTask AsyncWaitForCompletion(this DG.Tweening.Tween tween)
        {
            var tcs = new Cysharp.Threading.Tasks.UniTaskCompletionSource<bool>();
            tween.OnComplete(() => tcs.TrySetResult(true));
            return tcs.Task;
        }
    }
}