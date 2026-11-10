using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class CampaignStage : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int Chapter { get; private set; }
        public int StageNumber { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.Campaign;
        public int RequiredPlayerLevel { get; private set; }
        public int RequiredStageId { get; private set; } // Previous stage
        public int StaminaCost { get; private set; }
        public int RecommendedPower { get; private set; }
        public List<WaveConfig> Waves { get; private set; } = new();
        public List<Reward> FirstClearRewards { get; private set; } = new();
        public List<Reward> RepeatRewards { get; private set; } = new();
        public List<StarCondition> StarConditions { get; private set; } = new();
        public bool IsBossStage { get; private set; }
        public string BossCharacterId { get; private set; }
        public string StoryDialogueBefore { get; private set; }
        public string StoryDialogueAfter { get; private set; }
        public string BackgroundAssetPath { get; private set; }
        public string MusicAssetPath { get; private set; }
        public DateTimeRange? AvailablePeriod { get; private set; }
        public bool AutoBattleAllowed { get; private set; } = true;
        public bool AutoProgressAllowed { get; private set; } = true;

        private CampaignStage() { }

        public CampaignStage(
            string name,
            string description,
            int chapter,
            int stageNumber,
            int requiredPlayerLevel,
            int staminaCost,
            int recommendedPower,
            bool isBossStage = false)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Chapter = chapter;
            StageNumber = stageNumber;
            RequiredPlayerLevel = requiredPlayerLevel;
            StaminaCost = staminaCost;
            RecommendedPower = recommendedPower;
            IsBossStage = isBossStage;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddWave(WaveConfig wave)
        {
            Waves.Add(wave);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRewards(List<Reward> firstClear, List<Reward> repeat)
        {
            FirstClearRewards = firstClear;
            RepeatRewards = repeat;
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetStarConditions(List<StarCondition> conditions)
        {
            StarConditions = conditions;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetStory(string before, string after)
        {
            StoryDialogueBefore = before;
            StoryDialogueAfter = after;
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetAssets(string background, string music)
        {
            BackgroundAssetPath = background;
            MusicAssetPath = music;
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetRequiredStage(int stageId)
        {
            RequiredStageId = stageId;
            UpdatedAt = DateTime.UtcNow.
        }

        public void SetAutoPermissions(bool autoBattle, bool autoProgress)
        {
            AutoBattleAllowed = autoBattle;
            AutoProgressAllowed = autoProgress;
            UpdatedAt = DateTime.UtcNow.
        }

        public bool IsAvailable => !AvailablePeriod.HasValue || AvailablePeriod.Value.IsActive;
    }

    public class WaveConfig
    {
        public int WaveNumber { get; set; }
        public List<EnemySpawn> Enemies { get; set; } = new();
        public int TurnLimit { get; set; } = 0; // 0 = no limit
        public List<WaveEvent> Events { get; set; } = new();
        public string BackgroundOverride { get; set; }
        public string MusicOverride { get; set; }
    }

    public class EnemySpawn
    {
        public Guid EnemyId { get; set; }
        public string EnemyName { get; set; }
        public int Level { get; set; }
        public int Ascension { get; set; }
        public Position Position { get; set; }
        public bool IsBoss { get; set; }
        public Dictionary<string, object> CustomStats { get; set; } = new();
        public List<Guid> SkillOverrides { get; set; } = new();
        public AIPersonality AIType { get; set; } = AIPersonality.Balanced;
    }

    public class WaveEvent
    {
        public WaveEventType Type { get; set; }
        public int TriggerTurn { get; set; }
        public float TriggerHpPercent { get; set; }
        public string Description { get; set; }
        public List<BattleActionEffect> Effects { get; set; } = new();
        public List<EnemySpawn> Reinforcements { get; set; } = new();
    }

    public enum WaveEventType
    {
        SpawnReinforcements = 1,
        BossPhaseChange = 2,
        ApplyBuff = 3,
        ApplyDebuff = 4,
        ChangeBackground = 5,
        ChangeMusic = 6,
        Dialogue = 7,
        Cinematic = 8,
        EnvironmentalDamage = 9,
        EnvironmentalHeal = 10,
        SummonMinions = 11,
        Transform = 12,
        Enrage = 13,
        ShieldPhase = 14,
        InvulnerabilityPhase = 15
    }

    public class StarCondition
    {
        public int Stars { get; set; } // 1, 2, or 3
        public StarConditionType Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public enum StarConditionType
    {
        Complete = 1,
        NoDeaths = 2,
        TurnLimit = 3,
        TimeLimit = 4,
        SpecificCharacterSurvives = 5,
        SpecificCharacterDealsMostDamage = 6,
        NoDebuffsReceived = 7,
        KillAllEnemiesInOneTurn = 8,
        FinishWithFullHP = 9,
        UseUltimateLessThan = 10
    }

    public enum AIPersonality
    {
        Aggressive = 1,
        Defensive = 2,
        Balanced = 3,
        Supportive = 4,
        Control = 5,
        Berserker = 6,
        Tactical = 7,
        Random = 8
    }

    public class UserCampaignProgress : BaseEntity
    {
        public Guid UserId { get; private set; }
        public int CurrentChapter { get; private set; }
        public int CurrentStage { get; private set; }
        public int HighestClearedStage { get; private set; }
        public Dictionary<int, StageClearData> ClearedStages { get; private set; } = new();
        public int TotalStars { get; private set; }
        public DateTime LastClearedAt { get; private set; }
        public List<int> ClaimedChapterRewards { get; private set; } = new();

        private UserCampaignProgress() { }

        public UserCampaignProgress(Guid userId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            CurrentChapter = 1;
            CurrentStage = 1;
            HighestClearedStage = 0;
            TotalStars = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public void ClearStage(int stageId, int stars, int turnsUsed, TimeSpan timeUsed, List<Guid> survivingCharacters)
        {
            var data = new StageClearData
            {
                StageId = stageId,
                Stars = stars,
                TurnsUsed = turnsUsed,
                TimeUsed = timeUsed,
                SurvivingCharacters = survivingCharacters,
                ClearedAt = DateTime.UtcNow
            };

            ClearedStages[stageId] = data;

            if (stageId > HighestClearedStage)
            {
                HighestClearedStage = stageId;

                // Update current progress
                var stage = GetStageConfig(stageId); // Would fetch from config
                if (stage != null)
                {
                    CurrentChapter = stage.Chapter;
                    CurrentStage = stage.StageNumber + 1;
                }
            }

            if (stars > GetPreviousStars(stageId))
            {
                TotalStars += stars - GetPreviousStars(stageId);
            }

            LastClearedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public bool IsStageCleared(int stageId) => ClearedStages.ContainsKey(stageId);
        public int GetStarsForStage(int stageId) => ClearedStages.TryGetValue(stageId, out var data) ? data.Stars : 0;
        public bool IsStageThreeStarred(int stageId) => GetStarsForStage(stageId) >= 3;

        public void ClaimChapterReward(int chapter)
        {
            if (!ClaimedChapterRewards.Contains(chapter))
            {
                ClaimedChapterRewards.Add(chapter);
                UpdatedAt = DateTime.UtcNow.
            }
        }

        public bool IsChapterRewardClaimed(int chapter) => ClaimedChapterRewards.Contains(chapter);

        private int GetPreviousStars(int stageId) => ClearedStages.TryGetValue(stageId, out var data) ? data.Stars : 0;
        private CampaignStage GetStageConfig(int stageId) => null; // Would be injected
    }

    public class StageClearData
    {
        public int StageId { get; set; }
        public int Stars { get; set; }
        public int TurnsUsed { get; set; }
        public TimeSpan TimeUsed { get; set; }
        public List<Guid> SurvivingCharacters { get; set; } = new();
        public DateTime ClearedAt { get; set; }
    }
}