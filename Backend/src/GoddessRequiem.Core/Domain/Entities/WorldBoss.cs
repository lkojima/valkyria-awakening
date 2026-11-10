using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class WorldBoss : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int Level { get; private set; }
        public long BaseHP { get; private set; }
        public int CurrentHP { get; private set; }
        public int MaxHP { get; private set; }
        public Element Element { get; private set; }
        public List<BossPhase> Phases { get; private set; } = new()
        public List<BossSkill> Skills { get; private set; } = new()
        public List<Reward> DamageRewards { get; private set; } = new()
        public List<Reward> ParticipationRewards { get; private set; } = new()
        public List<Reward> RankingRewards { get; private set; } = new()
        public List<Reward> LastHitRewards { get; private set; } = new()
        public DateTimeRange ActivePeriod { get; private set; }
        public int MaxParticipants { get; private set; }
        public int RequiredPlayerLevel { get; private set; }
        public CurrencyAmount EntryCost { get; private set; }
        public int FreeEntriesPerDay { get; private set; }
        public string ModelPath { get; private set; }
        public string BackgroundPath { get; private set; }
        public string MusicPath { get; private set; }
        public bool IsActive { get; private set; }

        private WorldBoss() { }

        public WorldBoss(
            string name,
            string description,
            int level,
            long baseHP,
            Element element,
            DateTimeRange activePeriod,
            int maxParticipants = 1000000,
            int requiredLevel = 1)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Level = level;
            BaseHP = baseHP;
            MaxHP = (int)Math.Min(baseHP, int.MaxValue);
            CurrentHP = MaxHP;
            Element = element;
            ActivePeriod = activePeriod;
            MaxParticipants = maxParticipants;
            RequiredPlayerLevel = requiredLevel;
            EntryCost = new CurrencyAmount(CurrencyType.Gem, 0);
            FreeEntriesPerDay = 3;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddPhase(BossPhase phase)
        {
            Phases.Add(phase);
            Phases.Sort((a, b) => a.HpThresholdPercent.CompareTo(b.HpThresholdPercent));
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddSkill(BossSkill skill)
        {
            Skills.Add(skill);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRewards(
            List<Reward> damage,
            List<Reward> participation,
            List<Reward> ranking,
            List<Reward> lastHit)
        {
            DamageRewards = damage;
            ParticipationRewards = participation;
            RankingRewards = ranking;
            LastHitRewards = lastHit;
            UpdatedAt = DateTime.UtcNow;
        }

        public void TakeDamage(long damage)
        {
            CurrentHP = Math.Max(0, CurrentHP - (int)Math.Min(damage, int.MaxValue));
            UpdatedAt = DateTime.UtcNow;
        }

        public void ResetHP()
        {
            CurrentHP = MaxHP;
            UpdatedAt = DateTime.UtcNow;
        }

        public BossPhase GetCurrentPhase()
        {
            var hpPercent = (float)CurrentHP / MaxHP * 100;
            return Phases.LastOrDefault(p => hpPercent <= p.HpThresholdPercent) ?? Phases.FirstOrDefault();
        }

        public bool IsAvailable => IsActive && ActivePeriod.IsActive;
        public bool IsDefeated => CurrentHP <= 0;
        public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    }

    public class BossPhase
    {
        public int PhaseNumber { get; set; }
        public string Name { get; set; }
        public float HpThresholdPercent { get; set; } // e.g., 70, 40, 10
        public string Description { get; set; }
        public List<BossSkill> PhaseSkills { get; set; } = new();
        public List<ModifierEffect> GlobalModifiers { get; set; } = new();
        public string BackgroundOverride { get; set; }
        public string MusicOverride { get; set; }
        public string CinematicPath { get; set; }
    }

    public class BossSkill
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public SkillType Type { get; set; }
        public Element Element { get; set; }
        public SkillTargetType TargetType { get; set; }
        public int Cooldown { get; set; }
        public int MinTurn { get; set; }
        public int MaxTurn { get; set; } // 0 = no max
        public float HpTriggerPercent { get; set; } // 0 = no trigger
        public List<SkillEffect> Effects { get; set; } = new();
        public AICondition[] AIConditions { get; set; } = Array.Empty<AICondition>();
        public string AnimationPath { get; set; }
        public string VFXPath { get; set; }
        public string SoundPath { get; set; }
        public bool IsTelegraphed { get; set; }
        public int TelegraphTurns { get; set; }
        public string TelegraphMessage { get; set; }
    }

    public class UserWorldBossProgress : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid WorldBossId { get; private set; }
        public long TotalDamageDealt { get; private set; }
        public int BestSingleDamage { get; private set; }
        public int EntriesUsed { get; private set; }
        public int FreeEntriesUsed { get; private set; }
        public DateTime LastEntryAt { get; private set; }
        public int CurrentRank { get; private set; }
        public int HighestRank { get; private set; }
        public List<WorldBossBattleLog> BattleLogs { get; private set; } = new()
        public List<int> ClaimedDamageRewards { get; private set; } = new()
        public bool ClaimedParticipationReward { get; private set; }
        public bool ClaimedRankingReward { get; private set; }
        public bool ClaimedLastHitReward { get; private set; }

        private UserWorldBossProgress() { }

        public UserWorldBossProgress(Guid userId, Guid worldBossId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            WorldBossId = worldBossId;
            TotalDamageDealt = 0;
            BestSingleDamage = 0;
            EntriesUsed = 0;
            FreeEntriesUsed = 0;
            CurrentRank = 0;
            HighestRank = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RecordBattle(int damage, bool isFreeEntry)
        {
            TotalDamageDealt += damage;
            BestSingleDamage = Math.Max(BestSingleDamage, damage);
            EntriesUsed++;
            if (isFreeEntry) FreeEntriesUsed++;
            LastEntryAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateRank(int rank)
        {
            CurrentRank = rank;
            if (HighestRank == 0 || rank < HighestRank)
                HighestRank = rank;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddBattleLog(WorldBossBattleLog log)
        {
            BattleLogs.Insert(0, log);
            if (BattleLogs.Count > 20) BattleLogs.RemoveAt(20);
            UpdatedAt = DateTime.UtcNow;
        }

        public void ClaimDamageReward(int thresholdIndex)
        {
            if (!ClaimedDamageRewards.Contains(thresholdIndex))
            {
                ClaimedDamageRewards.Add(thresholdIndex);
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void ClaimParticipation() { ClaimedParticipationReward = true; UpdatedAt = DateTime.UtcNow; }
        public void ClaimRanking() { ClaimedRankingReward = true; UpdatedAt = DateTime.UtcNow; }
        public void ClaimLastHit() { ClaimedLastHitReward = true; UpdatedAt = DateTime.UtcNow; }
    }

    public class WorldBossBattleLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int DamageDealt { get; set; }
        public int TurnsSurvived { get; set; }
        public List<Guid> CharactersUsed { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool WasLastHit { get; set; }
        public string ReplayId { get; set; }
    }

    public class WorldBossRankingEntry
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public long TotalDamage { get; set; }
        public int BestSingleDamage { get; set; }
        public int Rank { get; set; }
        public Guid AvatarId { get; set; }
        public Guid AvatarFrameId { get; set; }
        public int Level { get; set; }
    }
}