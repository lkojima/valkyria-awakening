using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Skin : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Guid CharacterId { get; private set; }
        public SkinRarity Rarity { get; private set; }
        public SkinCategory Category { get; private set; }

        // Visual Assets
        public string PortraitPath { get; private set; }
        public string FullBodyPath { get; private set; }
        public string SpritePath { get; private set; }
        public string Live2DPath { get; private set; }
        public string BattleModelPath { get; private set; }
        public string SummonAnimationPath { get; private set; }
        public string SkillVFXOverridePaths { get; private set; } // JSON

        // Audio
        public string VoiceLinesPath { get; private set; }
        public string BGMusicPath { get; private set; }

        // Effects
        public List<SkinEffect> Effects { get; private set; } = new();

        // Acquisition
        public bool IsLimited { get; private set; }
        public DateTimeRange? AvailablePeriod { get; private set; }
        public List<CurrencyAmount> Prices { get; private set; } = new();
        public bool IsDyeable { get; private set; }
        public List<SkinDyeOption> DyeOptions { get; private set; } = new();

        private Skin() { }

        public Skin(
            string name,
            string description,
            Guid characterId,
            SkinRarity rarity,
            SkinCategory category,
            string portraitPath,
            string fullBodyPath,
            string spritePath,
            string battleModelPath,
            bool isLimited = false,
            DateTimeRange? availablePeriod = null,
            bool isDyeable = false)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            CharacterId = characterId;
            Rarity = rarity;
            Category = category;
            PortraitPath = portraitPath;
            FullBodyPath = fullBodyPath;
            SpritePath = spritePath;
            BattleModelPath = battleModelPath;
            IsLimited = isLimited;
            AvailablePeriod = availablePeriod;
            IsDyeable = isDyeable;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetLive2D(string path)
        {
            Live2DPath = path;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSummonAnimation(string path)
        {
            SummonAnimationPath = path;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSkillVFXOverrides(string jsonPaths)
        {
            SkillVFXOverridePaths = jsonPaths;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAudio(string voiceLines, string bgMusic)
        {
            VoiceLinesPath = voiceLines;
            BGMusicPath = bgMusic;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddEffect(SkinEffect effect)
        {
            Effects.Add(effect);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPrices(List<CurrencyAmount> prices)
        {
            Prices = prices;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddDyeOption(SkinDyeOption option)
        {
            DyeOptions.Add(option);
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsAvailable => !IsLimited || (AvailablePeriod.HasValue && AvailablePeriod.Value.IsActive);
    }

    public class SkinEffect
    {
        public SkinEffectType Type { get; set; }
        public float Value { get; set; }
        public string Description { get; set; }
    }

    public class SkinDyeOption
    {
        public string Name { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
        public string AccentColor { get; set; }
        public string TextureMaskPath { get; set; }
        public CurrencyAmount Price { get; set; }
    }

    public enum SkinRarity
    {
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5
    }

    public enum SkinCategory
    {
        Default = 1,
        Seasonal = 2,
        Event = 3,
        Collaboration = 4,
        Original = 5,
        Alternate = 6,
        Wedding = 7,
        Swimsuit = 8,
        Holiday = 9,
        Cyber = 10,
        Fantasy = 11,
        Traditional = 12
    }

    public enum SkinEffectType
    {
        None = 0,
        StatBonus = 1,
        SkillVFXChange = 2,
        BattleEntryAnimation = 3,
        VictoryPose = 4,
        HomeScreenAnimation = 5,
        VoiceChange = 6,
        BGMusicChange = 7,
        ProfileFrame = 8,
        ChatBubble = 9,
        DamageNumberStyle = 10
    }
}