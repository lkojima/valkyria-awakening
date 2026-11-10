namespace GoddessRequiem.Core.Domain.Enums
{
    public enum Rarity
    {
        R = 1,
        SR = 2,
        SSR = 3,
        UR = 4
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

    public enum BuffType
    {
        AttackUp = 1,
        DefenseUp = 2,
        SpeedUp = 3,
        CritRateUp = 4,
        CritDamageUp = 5,
        ResistanceUp = 6,
        AccuracyUp = 7,
        EvasionUp = 8,
        HPRegeneration = 9,
        Shield = 10,
        Immunity = 11,
        Stealth = 12,
        Invulnerability = 13,
        DamageReduction = 14,
        Lifesteal = 15,
        Thorns = 16,
        Counterattack = 17,
        EnergyGain = 18,
        CooldownReduction = 19,
        BuffDurationUp = 20,
        DebuffResistance = 21,
        HealingReceivedUp = 22,
        ShieldStrengthUp = 23,
        ElementalDamageUp = 24,
        ElementalResistanceUp = 25
    }

    public enum DebuffType
    {
        AttackDown = 1,
        DefenseDown = 2,
        SpeedDown = 3,
        CritRateDown = 4,
        CritDamageDown = 5,
        ResistanceDown = 6,
        AccuracyDown = 7,
        EvasionDown = 8,
        Poison = 9,
        Burn = 10,
        Bleed = 11,
        Curse = 12,
        Stun = 13,
        Silence = 14,
        Root = 15,
        Sleep = 16,
        Fear = 17,
        Charm = 18,
        Provoke = 19,
        Taunt = 20,
        Blind = 21,
        Confusion = 22,
        Petrify = 23,
        Freeze = 24,
        Shackle = 25,
        EnergyDrain = 26,
        CooldownIncrease = 27,
        HealingReceivedDown = 28,
        ShieldStrengthDown = 29,
        ElementalDamageDown = 30,
        ElementalResistanceDown = 31,
        ArmorBreak = 32,
        MagicBreak = 33,
        TrueDamage = 34,
        Execute = 35
    }

    public enum BattlePhase
    {
        Preparation = 1,
        Deployment = 2,
        TurnStart = 3,
        ActionSelection = 4,
        ActionExecution = 5,
        EffectResolution = 6,
        TurnEnd = 7,
        Victory = 8,
        Defeat = 9,
        Draw = 10
    }

    public enum GameMode
    {
        Campaign = 1,
        Tower = 2,
        Arena = 3,
        WorldBoss = 4,
        Dungeon = 5,
        Event = 6,
        Expedition = 7,
        DailyChallenge = 8,
        Endless = 9,
        GuildWar = 10,
        CoOp = 11
    }

    public enum SummonType
    {
        Normal = 1,
        Premium = 2,
        RateUp = 3,
        Event = 4,
        Selective = 5,
        Guaranteed = 6,
        Friendship = 7,
        Ticket = 8
    }

    public enum CurrencyType
    {
        Gold = 1,
        Gem = 2,
        SummonTicket = 3,
        PremiumSummonTicket = 4,
        EventCurrency = 5,
        FriendshipPoint = 6,
        GuildCoin = 7,
        ArenaCoin = 8,
        TowerCoin = 9,
        DungeonCoin = 10,
        ExpeditionCoin = 11,
        EvolutionMaterial = 12,
        AwakeningMaterial = 13,
        SkillMaterial = 14,
        EquipmentMaterial = 15,
        RelicMaterial = 16,
        RuneMaterial = 17,
        SkinToken = 18,
        Experience = 19,
        CharacterExp = 20,
        WeaponExp = 21,
        Stamina = 22,
        Energy = 23,
        ActionPoint = 24
    }

    public enum ItemType
    {
        Consumable = 1,
        Material = 2,
        Equipment = 3,
        Relic = 4,
        Rune = 5,
        CharacterFragment = 6,
        Skin = 7,
        Currency = 8,
        QuestItem = 9,
        Gift = 10,
        Ticket = 11,
        Box = 12,
        Key = 13
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

    public enum EquipmentRarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6
    }

    public enum QuestType
    {
        Main = 1,
        Side = 2,
        Daily = 3,
        Weekly = 4,
        Monthly = 5,
        Event = 6,
        Achievement = 7,
        Tutorial = 8,
        Character = 9,
        Guild = 10
    }

    public enum QuestStatus
    {
        NotStarted = 1,
        InProgress = 2,
        Completed = 3,
        Claimed = 4,
        Failed = 5,
        Expired = 6
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

    public enum ShopType
    {
        General = 1,
        Arena = 2,
        Tower = 3,
        Guild = 4,
        Event = 5,
        Premium = 6,
        VIP = 7,
        BlackMarket = 8,
        Friendship = 9,
        Expedition = 10
    }

    public enum PurchaseStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3,
        Refunded = 4,
        Cancelled = 5,
        Verified = 6
    }

    public enum SubscriptionTier
    {
        None = 0,
        Basic = 1,
        Premium = 2,
        Ultimate = 3
    }

    public enum BattleSpeed
    {
        Normal = 1,
        Fast = 2,
        UltraFast = 3
    }

    public enum AutoBattleMode
    {
        Off = 0,
        Basic = 1,
        Smart = 2,
        Full = 3
    }

    public enum AutoProgressMode
    {
        Off = 0,
        StagesOnly = 1,
        StagesAndBosses = 2,
        Full = 3
    }

    public enum NotificationType
    {
        EnergyFull = 1,
        ExpeditionComplete = 2,
        QuestComplete = 3,
        EventStart = 4,
        EventEnd = 5,
        FriendRequest = 6,
        GuildInvite = 7,
        GuildWar = 8,
        RankUp = 9,
        DailyReset = 10,
        WeeklyReset = 11,
        Maintenance = 12,
        Update = 13,
        RewardClaim = 14,
        SummonAvailable = 15,
        ShopRefresh = 16
    }

    public enum LogType
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4,
        Audit = 5,
        Security = 6,
        Performance = 7,
        Business = 8
    }

    public enum AccountStatus
    {
        Active = 1,
        Suspended = 2,
        Banned = 3,
        Deleted = 4,
        PendingVerification = 5,
        Restricted = 6
    }

    public enum Platform
    {
        Android = 1,
        iOS = 2,
        Windows = 3,
        Mac = 4,
        Web = 5
    }

    public enum Language
    {
        Portuguese = 1,
        English = 2,
        Spanish = 3,
        French = 4,
        German = 5,
        Japanese = 6,
        Korean = 7,
        ChineseSimplified = 8,
        ChineseTraditional = 9
    }
}