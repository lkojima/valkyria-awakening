using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Quest : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public QuestType Type { get; private set; }
        public int RequiredPlayerLevel { get; private set; }
        public List<QuestObjective> Objectives { get; private set; } = new();
        public List<Reward> Rewards { get; private set; } = new();
        public DateTimeRange? AvailablePeriod { get; private set; }
        public int MaxCompletions { get; private set; } = 1;
        public int DailyResetHour { get; private set; } = 4;
        public int WeeklyResetDay { get; private set; } = 0; // Sunday
        public string IconPath { get; private set; }
        public bool IsActive { get; private set; }
        public List<Guid> PrerequisiteQuestIds { get; private set; } = new();
        public QuestChain Chain { get; private set; }

        private Quest() { }

        public Quest(
            string name,
            string description,
            QuestType type,
            int requiredLevel = 1)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            RequiredPlayerLevel = requiredLevel;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddObjective(QuestObjective objective)
        {
            Objectives.Add(objective);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRewards(List<Reward> rewards)
        {
            Rewards = rewards;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSchedule(DateTimeRange? period, int dailyResetHour = 4, int weeklyResetDay = 0)
        {
            AvailablePeriod = period;
            DailyResetHour = dailyResetHour;
            WeeklyResetDay = weeklyResetDay;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPrerequisites(List<Guid> questIds)
        {
            PrerequisiteQuestIds = questIds;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetChain(QuestChain chain)
        {
            Chain = chain;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsAvailableNow => IsActive && (!AvailablePeriod.HasValue || AvailablePeriod.Value.IsActive);
        public bool CanAccept(HashSet<Guid> completedQuests) => PrerequisiteQuestIds.All(q => completedQuests.Contains(q));
    }

    public class QuestObjective
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public QuestObjectiveType Type { get; set; }
        public string Description { get; set; }
        public Guid TargetId { get; set; } // Enemy, Item, Stage, etc.
        public int RequiredCount { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsHidden { get; set; }
        public bool IsOptional { get; set; }
    }

    public enum QuestObjectiveType
    {
        KillEnemies = 1,
        CollectItems = 2,
        ClearStage = 3,
        WinBattles = 4,
        UseSkill = 5,
        LevelUpCharacter = 6,
        AscendCharacter = 7,
        EnhanceEquipment = 8,
        SummonCharacters = 9,
        SpendCurrency = 10,
        Login = 11,
        CompleteExpedition = 12,
        ClearDungeon = 13,
        ReachArenaRank = 14,
        DealDamageWorldBoss = 15,
        UnlockCharacter = 16,
        ReadStory = 17,
        ShareGame = 18,
        WatchAd = 19,
        InviteFriend = 20,
        JoinGuild = 21
    }

    public class QuestChain
    {
        public Guid ChainId { get; set; }
        public int StepNumber { get; set; }
        public Guid NextQuestId { get; set; }
        public bool IsFinalStep { get; set; }
        public List<Reward> ChainCompletionRewards { get; set; } = new();
    }

    public class UserQuest : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid QuestId { get; private set; }
        public QuestStatus Status { get; private set; }
        public Dictionary<string, int> ObjectiveProgress { get; private set; } = new();
        public int CompletionCount { get; private set; }
        public DateTime AcceptedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        private UserQuest() { }

        public UserQuest(Guid userId, Guid questId, Quest quest)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            QuestId = questId;
            Status = QuestStatus.InProgress;
            AcceptedAt = DateTime.UtcNow;

            foreach (var obj in quest.Objectives)
            {
                ObjectiveProgress[obj.Id] = 0;
            }

            ExpiresAt = CalculateExpiry(quest);
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateProgress(string objectiveId, int amount)
        {
            if (Status != QuestStatus.InProgress) return;
            if (!ObjectiveProgress.ContainsKey(objectiveId)) return;

            ObjectiveProgress[objectiveId] = Math.Max(ObjectiveProgress[objectiveId], amount);
            CheckCompletion();
            UpdatedAt = DateTime.UtcNow;
        }

        public void IncrementProgress(string objectiveId, int amount = 1)
        {
            if (Status != QuestStatus.InProgress) return;
            if (!ObjectiveProgress.ContainsKey(objectiveId)) return;

            ObjectiveProgress[objectiveId] += amount;
            CheckCompletion();
            UpdatedAt = DateTime.UtcNow;
        }

        private void CheckCompletion()
        {
            // Would check against quest objectives
        }

        public void Complete()
        {
            if (Status == QuestStatus.InProgress)
            {
                Status = QuestStatus.Completed;
                CompletedAt = DateTime.UtcNow;
                CompletionCount++;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void Claim()
        {
            if (Status == QuestStatus.Completed)
            {
                Status = QuestStatus.Claimed;
                ClaimedAt = DateTime.UtcNow;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void Expire()
        {
            if (Status == QuestStatus.InProgress || Status == QuestStatus.Completed)
            {
                Status = QuestStatus.Expired;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void Reset(Quest quest)
        {
            Status = QuestStatus.InProgress;
            ObjectiveProgress.Clear();
            foreach (var obj in quest.Objectives)
            {
                ObjectiveProgress[obj.Id] = 0;
            }
            ExpiresAt = CalculateExpiry(quest);
            UpdatedAt = DateTime.UtcNow;
        }

        public int GetProgress(string objectiveId) => ObjectiveProgress.GetValueOrDefault(objectiveId, 0);
        public float GetProgressPercent(string objectiveId, int required) => required > 0 ? Math.Min(1f, (float)GetProgress(objectiveId) / required) : 1f;

        private DateTime CalculateExpiry(Quest quest)
        {
            return quest.Type switch
            {
                QuestType.Daily => DateTime.UtcNow.Date.AddHours(quest.DailyResetHour).AddDays(1),
                QuestType.Weekly => GetNextWeeklyReset(quest.WeeklyResetDay),
                QuestType.Monthly => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1),
                QuestType.Event => quest.AvailablePeriod?.End ?? DateTime.UtcNow.AddDays(7),
                _ => DateTime.MaxValue
            };
        }

        private DateTime GetNextWeeklyReset(int dayOfWeek)
        {
            var now = DateTime.UtcNow;
            var daysUntil = ((DayOfWeek)dayOfWeek - now.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7;
            return now.Date.AddDays(daysUntil);
        }
    }
}