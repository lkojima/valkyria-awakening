using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class GameEvent : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public EventType Type { get; private set; }
        public DateTimeRange Period { get; private set; }
        public int RequiredPlayerLevel { get; private set; }
        public string BannerImagePath { get; private set; }
        public string BackgroundPath { get; private set; }
        public string MusicPath { get; private set; }
        public List<EventStage> Stages { get; private set; } = new();
        public List<EventQuest> Quests { get; private set; } = new();
        public List<EventShop> Shops { get; private set; } = new();
        public List<EventReward> MilestoneRewards { get; private set; } = new();
        public List<EventReward> RankingRewards { get; private set; } = new()
        public EventCurrency Currency { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsVisible { get; private set; }
        public int SortOrder { get; private set; }

        private GameEvent() { }

        public GameEvent(
            string name,
            string description,
            EventType type,
            DateTimeRange period,
            int requiredLevel = 1)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            Period = period;
            RequiredPlayerLevel = requiredLevel;
            IsActive = true;
            IsVisible = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddStage(EventStage stage)
        {
            Stages.Add(stage);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddQuest(EventQuest quest)
        {
            Quests.Add(quest);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddShop(EventShop shop)
        {
            Shops.Add(shop);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddMilestoneReward(EventReward reward)
        {
            MilestoneRewards.Add(reward);
            MilestoneRewards.Sort((a, b) => a.RequiredPoints.CompareTo(b.RequiredPoints));
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddRankingReward(EventReward reward)
        {
            RankingRewards.Add(reward);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetCurrency(EventCurrency currency)
        {
            Currency = currency;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsEventActive => IsActive && Period.IsActive;
        public bool IsEventUpcoming => IsActive && Period.IsFuture;
        public bool IsEventEnded => Period.IsPast;
    }

    public enum EventType
    {
        Story = 1,
        Seasonal = 2,
        Collaboration = 3,
        Anniversary = 4,
        NewCharacter = 5,
        Challenge = 6,
        Raid = 7,
        Festival = 8,
        LoginBonus = 9,
        SummonRateUp = 10
    }

    public class EventStage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }
        public int StageNumber { get; set; }
        public int RequiredPlayerLevel { get; set; }
        public int StaminaCost { get; set; }
        public List<WaveConfig> Waves { get; set; } = new();
        public List<Reward> FirstClearRewards { get; set; } = new();
        public List<Reward> RepeatRewards { get; set; } = new();
        public List<EventDrop> EventDrops { get; set; } = new();
        public int EventPointsReward { get; set; }
        public bool IsChallenge { get; set; }
        public int MaxEntriesPerDay { get; set; } = 0; // 0 = unlimited
    }

    public class EventDrop
    {
        public Guid ResourceId { get; set; }
        public int MinQuantity { get; set; }
        public int MaxQuantity { get; set; }
        public float DropRate { get; set; }
        public int RequiredStars { get; set; }
    }

    public class EventQuest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }
        public QuestType Type { get; set; }
        public List<QuestObjective> Objectives { get; set; } = new();
        public List<Reward> Rewards { get; set; } = new();
        public int EventPointsReward { get; set; }
        public DateTimeRange? AvailablePeriod { get; set; }
        public int MaxCompletions { get; set; } = 1;
        public bool IsRepeatable { get; set; }
    }

    public class EventShop
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public List<EventShopItem> Items { get; set; } = new();
        public DateTimeRange? AvailablePeriod { get; set; }
        public int RefreshIntervalHours { get; set; } = 0; // 0 = no refresh
        public CurrencyAmount RefreshCost { get; set; }
    }

    public class EventShopItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public RewardType RewardType { get; set; }
        public Guid ResourceId { get; set; }
        public int Quantity { get; set; }
        public CurrencyAmount Price { get; set; }
        public int Stock { get; set; } = -1; // -1 = unlimited
        public int MaxPurchasesPerUser { get; set; } = -1;
        public int RequiredEventPoints { get; set; }
        public bool IsLimited { get; set; }
    }

    public class EventReward
    {
        public RewardType Type { get; set; }
        public Guid ResourceId { get; set; }
        public int Quantity { get; set; }
        public int RequiredPoints { get; set; } // For milestones
        public int MinRank { get; set; } // For rankings
        public int MaxRank { get; set; }
        public bool IsTopReward { get; set; }
    }

    public class EventCurrency
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public CurrencyType BaseCurrency { get; set; } = CurrencyType.EventCurrency;
        public int MaxCap { get; set; } = 999999;
        public DateTime ExpiresAt { get; set; }
        public bool ConvertAfterEvent { get; set; }
        public CurrencyAmount ConversionRate { get; set; }
    }

    public class UserEventProgress : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid EventId { get; private set; }
        public long EventPoints { get; private set; }
        public long TotalEventPointsEarned { get; private set; }
        public int CurrentRank { get; private set; }
        public Dictionary<Guid, EventStageProgress> StageProgress { get; private set; } = new();
        public Dictionary<Guid, EventQuestProgress> QuestProgress { get; private set; } = new();
        public List<int> ClaimedMilestones { get; private set; } = new();
        public bool ClaimedRankingReward { get; private set; }
        public long EventCurrencyBalance { get; private set; }
        public DateTime LastUpdatedAt { get; private set; }

        private UserEventProgress() { }

        public UserEventProgress(Guid userId, Guid eventId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            EventId = eventId;
            EventPoints = 0;
            TotalEventPointsEarned = 0;
            CurrentRank = 0;
            EventCurrencyBalance = 0;
            LastUpdatedAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddEventPoints(long points)
        {
            EventPoints += points;
            TotalEventPointsEarned += points;
            LastUpdatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddEventCurrency(long amount)
        {
            EventCurrencyBalance += amount;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SpendEventCurrency(long amount)
        {
            if (EventCurrencyBalance >= amount)
            {
                EventCurrencyBalance -= amount;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateStageProgress(Guid stageId, int stars, int entriesUsed)
        {
            if (!StageProgress.ContainsKey(stageId))
                StageProgress[stageId] = new EventStageProgress { StageId = stageId };

            var progress = StageProgress[stageId];
            progress.BestStars = Math.Max(progress.BestStars, stars);
            progress.EntriesUsed += entriesUsed;
            progress.LastClearedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateQuestProgress(Guid questId, Dictionary<string, int> objectiveProgress, QuestStatus status)
        {
            if (!QuestProgress.ContainsKey(questId))
                QuestProgress[questId] = new EventQuestProgress { QuestId = questId };

            var progress = QuestProgress[questId];
            progress.ObjectiveProgress = objectiveProgress;
            progress.Status = status;
            if (status == QuestStatus.Completed)
                progress.CompletedAt = DateTime.UtcNow;
            if (status == QuestStatus.Claimed)
                progress.ClaimedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ClaimMilestone(int pointsThreshold)
        {
            if (!ClaimedMilestones.Contains(pointsThreshold))
            {
                ClaimedMilestones.Add(pointsThreshold);
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateRank(int rank)
        {
            CurrentRank = rank;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class EventStageProgress
    {
        public Guid StageId { get; set; }
        public int BestStars { get; set; }
        public int EntriesUsed { get; set; }
        public DateTime? LastClearedAt { get; set; }
    }

    public class EventQuestProgress
    {
        public Guid QuestId { get; set; }
        public Dictionary<string, int> ObjectiveProgress { get; set; } = new();
        public QuestStatus Status { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
    }
}