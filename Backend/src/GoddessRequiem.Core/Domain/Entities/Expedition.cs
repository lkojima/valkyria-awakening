using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Expedition : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public ExpeditionType Type { get; private set; }
        public int RequiredPlayerLevel { get; private set; }
        public int RequiredCharacters { get; private set; } = 1;
        public int MaxCharacters { get; private set; } = 5;
        public TimeSpan Duration { get; private set; }
        public List<ExpeditionReward> BaseRewards { get; private set; } = new();
        public List<ExpeditionReward> BonusRewards { get; private set; } = new();
        public List<ExpeditionRequirement> Requirements { get; private set; } = new();
        public List<ExpeditionRisk> Risks { get; private set; } = new();
        public string BackgroundPath { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeRange? AvailablePeriod { get; private set; }

        private Expedition() { }

        public Expedition(
            string name,
            string description,
            ExpeditionType type,
            int requiredLevel,
            TimeSpan duration,
            int requiredCharacters = 1,
            int maxCharacters = 5)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            RequiredPlayerLevel = requiredLevel;
            RequiredCharacters = requiredCharacters;
            MaxCharacters = maxCharacters;
            Duration = duration;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddReward(ExpeditionReward reward)
        {
            BaseRewards.Add(reward);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddBonusReward(ExpeditionReward reward)
        {
            BonusRewards.Add(reward);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddRequirement(ExpeditionRequirement requirement)
        {
            Requirements.Add(requirement);
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddRisk(ExpeditionRisk risk)
        {
            Risks.Add(risk);
            UpdatedAt = DateTime.UtcNow;
        }

        public ExpeditionResult CalculateResult(List<UserCharacter> characters, ServerTimestamp completionTime)
        {
            var totalPower = characters.Sum(c => c.Level * (1 + c.AscensionLevel * 0.1f));
            var successRate = CalculateSuccessRate(characters);
            var isSuccess = Random.Shared.NextDouble() < successRate;

            var rewards = new List<ExpeditionRewardResult>();

            // Base rewards
            foreach (var reward in BaseRewards)
            {
                var qty = CalculateRewardQuantity(reward, totalPower, isSuccess);
                if (qty > 0)
                    rewards.Add(new ExpeditionRewardResult { Reward = reward, Quantity = qty });
            }

            // Bonus rewards (if high success chance)
            if (successRate > 0.8)
            {
                foreach (var reward in BonusRewards)
                {
                    var qty = CalculateRewardQuantity(reward, totalPower, true);
                    if (qty > 0)
                        rewards.Add(new ExpeditionRewardResult { Reward = reward, Quantity = qty, IsBonus = true });
                }
            }

            // Apply risks
            var incidents = new List<ExpeditionIncident>();
            foreach (var risk in Risks)
            {
                if (Random.Shared.NextDouble() < risk.Probability)
                {
                    incidents.Add(new ExpeditionIncident { Risk = risk, Description = risk.Description });
                    // Apply penalty
                    if (risk.PenaltyType == ExpeditionPenalty.ReduceRewards)
                    {
                        rewards.ForEach(r => r.Quantity = (int)(r.Quantity * (1 - risk.PenaltyValue)));
                    }
                }
            }

            return new ExpeditionResult
            {
                IsSuccess = isSuccess,
                Rewards = rewards,
                Incidents = incidents,
                CompletedAt = completionTime.Value,
                TotalPower = totalPower,
                SuccessRate = successRate
            };
        }

        private double CalculateSuccessRate(List<UserCharacter> characters)
        {
            var baseRate = 0.5;
            var powerBonus = Math.Min(0.4, characters.Sum(c => c.Level) * 0.001);
            var synergyBonus = CalculateSynergyBonus(characters) * 0.1;
            var requirementBonus = Requirements.All(r => r.IsMet(characters)) ? 0.1 : 0;

            return Math.Min(0.99, baseRate + powerBonus + synergyBonus + requirementBonus);
        }

        private double CalculateSynergyBonus(List<UserCharacter> characters)
        {
            var elements = characters.Select(c => c.Element).Distinct().Count();
            var classes = characters.Select(c => c.Class).Distinct().Count();
            return (elements + classes) / 10.0;
        }

        private int CalculateRewardQuantity(ExpeditionReward reward, float totalPower, bool isSuccess)
        {
            var baseQty = reward.BaseQuantity;
            var powerMultiplier = 1f + totalPower * 0.0001f;
            var successMultiplier = isSuccess ? 1f : 0.3f;
            var randomFactor = 0.8f + (float)Random.Shared.NextDouble() * 0.4f;

            return (int)(baseQty * powerMultiplier * successMultiplier * randomFactor * reward.DropRate);
        }
    }

    public enum ExpeditionType
    {
        Exploration = 1,
        ResourceGathering = 2,
        MonsterHunting = 3,
        RuinInvestigation = 4,
        TradeMission = 5,
        RescueMission = 6,
        SecretMission = 7,
        Event = 8
    }

    public class ExpeditionReward
    {
        public RewardType Type { get; set; }
        public Guid ResourceId { get; set; }
        public int BaseQuantity { get; set; }
        public float DropRate { get; set; } = 1f;
        public bool IsGuaranteed { get; set; }
        public string Condition { get; set; }
    }

    public class ExpeditionRequirement
    {
        public ExpeditionRequirementType Type { get; set; }
        public Guid RequiredId { get; set; } // Character, Element, Class, etc.
        public int MinLevel { get; set; }
        public int MinAscension { get; set; }
        public int Count { get; set; } = 1;
        public string Description { get; set; }

        public bool IsMet(List<UserCharacter> characters)
        {
            return Type switch
            {
                ExpeditionRequirementType.Element => characters.Count(c => c.Element == (Element)RequiredId) >= Count,
                ExpeditionRequirementType.Class => characters.Count(c => c.Class == (CharacterClass)RequiredId) >= Count,
                ExpeditionRequirementType.SpecificCharacter => characters.Any(c => c.CharacterId == RequiredId),
                ExpeditionRequirementType.MinLevel => characters.Count(c => c.Level >= MinLevel) >= Count,
                ExpeditionRequirementType.MinAscension => characters.Count(c => c.AscensionLevel >= MinAscension) >= Count,
                ExpeditionRequirementType.Role => characters.Count(c => c.Role == (CharacterRole)RequiredId) >= Count,
                _ => true
            };
        }
    }

    public enum ExpeditionRequirementType
    {
        Element = 1,
        Class = 2,
        SpecificCharacter = 3,
        MinLevel = 4,
        MinAscension = 5,
        Role = 6,
        Rarity = 7
    }

    public class ExpeditionRisk
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public float Probability { get; set; }
        public ExpeditionPenalty PenaltyType { get; set; }
        public float PenaltyValue { get; set; } // 0.1 = 10% reduction
        public bool CanBeMitigated { get; set; }
        public List<ExpeditionRequirement> MitigationRequirements { get; set; } = new();
    }

    public enum ExpeditionPenalty
    {
        ReduceRewards = 1,
        LoseCharacter = 2, // Character injured, unavailable for time
        DamageEquipment = 3,
        LoseCurrency = 4,
        ExtendDuration = 5
    }

    public class ExpeditionResult
    {
        public bool IsSuccess { get; set; }
        public List<ExpeditionRewardResult> Rewards { get; set; } = new();
        public List<ExpeditionIncident> Incidents { get; set; } = new();
        public DateTime CompletedAt { get; set; }
        public float TotalPower { get; set; }
        public double SuccessRate { get; set; }
    }

    public class ExpeditionRewardResult
    {
        public ExpeditionReward Reward { get; set; }
        public int Quantity { get; set; }
        public bool IsBonus { get; set; }
    }

    public class ExpeditionIncident
    {
        public ExpeditionRisk Risk { get; set; }
        public string Description { get; set; }
    }

    public class UserExpedition : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid ExpeditionId { get; private set; }
        public List<Guid> CharacterIds { get; private set; } = new();
        public DateTime StartedAt { get; private set; }
        public DateTime EndsAt { get; private set; }
        public ExpeditionStatus Status { get; private set; }
        public ExpeditionResult Result { get; private set; }
        public bool IsAutoRepeat { get; private set; }
        public int RepeatCount { get; private set; }
        public int MaxRepeats { get; private set; }

        private UserExpedition() { }

        public UserExpedition(Guid userId, Guid expeditionId, List<Guid> characterIds, TimeSpan duration, bool autoRepeat = false, int maxRepeats = 0)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            ExpeditionId = expeditionId;
            CharacterIds = characterIds;
            StartedAt = DateTime.UtcNow;
            EndsAt = DateTime.UtcNow.Add(duration);
            Status = ExpeditionStatus.InProgress;
            IsAutoRepeat = autoRepeat;
            MaxRepeats = maxRepeats;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Complete(ExpeditionResult result)
        {
            Result = result;
            Status = ExpeditionStatus.Completed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Claim()
        {
            Status = ExpeditionStatus.Claimed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Cancel()
        {
            Status = ExpeditionStatus.Cancelled;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsCompleted => DateTime.UtcNow >= EndsAt;
        public TimeSpan TimeRemaining => EndsAt > DateTime.UtcNow ? EndsAt - DateTime.UtcNow : TimeSpan.Zero;
        public float Progress => Math.Min(1f, (float)(DateTime.UtcNow - StartedAt).TotalSeconds / (EndsAt - StartedAt).TotalSeconds);
    }

    public enum ExpeditionStatus
    {
        InProgress = 1,
        Completed = 2,
        Claimed = 3,
        Cancelled = 4,
        Failed = 5
    }
}