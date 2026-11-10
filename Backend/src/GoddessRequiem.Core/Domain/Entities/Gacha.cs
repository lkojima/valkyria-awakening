using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class GachaBanner : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public SummonType Type { get; private set; }
        public DateTimeRange Period { get; private set; }
        public bool IsActive { get; private set; }
        public int SortOrder { get; private set; }
        public string BannerImagePath { get; private set; }
        public string BackgroundVideoPath { get; private set; }
        public string BackgroundMusicPath { get; private set; }

        // Rates
        public List<GachaRate> Rates { get; private set; } = new();
        public PitySystem PitySystem { get; private set; }

        // Featured Characters
        public List<GachaFeaturedCharacter> FeaturedCharacters { get; private set; } = new();

        // Costs
        public CurrencyAmount SingleCost { get; private set; }
        public CurrencyAmount MultiCost { get; private set; }
        public int MultiCount { get; private set; } = 10;

        // Requirements
        public int RequiredPlayerLevel { get; private set; } = 1;
        public List<Guid> RequiredQuestCompletion { get; private set; } = new();

        // Limits
        public int? DailyLimit { get; private set; }
        public int? WeeklyLimit { get; private set; }
        public int? TotalLimit { get; private set; }

        private GachaBanner() { }

        public GachaBanner(
            string name,
            string description,
            SummonType type,
            DateTimeRange period,
            CurrencyAmount singleCost,
            CurrencyAmount multiCost,
            int multiCount = 10,
            string bannerImage = "",
            string bgVideo = "",
            string bgMusic = "")
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            Period = period;
            IsActive = true;
            SingleCost = singleCost;
            MultiCost = multiCost;
            MultiCount = multiCount;
            BannerImagePath = bannerImage;
            BackgroundVideoPath = bgVideo;
            BackgroundMusicPath = bgMusic;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRates(List<GachaRate> rates)
        {
            var total = rates.Sum(r => r.BaseRate.Value);
            if (Math.Abs(total - 1f) > 0.0001f)
                throw new ArgumentException("Rates must sum to 100%");

            Rates = rates;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPitySystem(PitySystem pity)
        {
            PitySystem = pity;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddFeaturedCharacter(Guid characterId, Rarity rarity, float rateUpMultiplier = 1f)
        {
            FeaturedCharacters.Add(new GachaFeaturedCharacter
            {
                CharacterId = characterId,
                Rarity = rarity,
                RateUpMultiplier = rateUpMultiplier
            });
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRequirements(int playerLevel, List<Guid> quests)
        {
            RequiredPlayerLevel = playerLevel;
            RequiredQuestCompletion = quests;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetLimits(int? daily, int? weekly, int? total)
        {
            DailyLimit = daily;
            WeeklyLimit = weekly;
            TotalLimit = total;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsAvailableNow => IsActive && Period.IsActive;
        public bool IsAvailableForPlayer(int playerLevel, HashSet<Guid> completedQuests)
        {
            if (!IsAvailableNow) return false;
            if (playerLevel < RequiredPlayerLevel) return false;
            if (RequiredQuestCompletion.Any(q => !completedQuests.Contains(q))) return false;
            return true;
        }
    }

    public class GachaRate
    {
        public Rarity Rarity { get; set; }
        public Probability BaseRate { get; set; }
        public Probability DisplayRate { get; set; } // After pity modifications
        public string Description { get; set; }
    }

    public class PitySystem
    {
        public int SoftPityThreshold { get; set; } // e.g., 75 pulls
        public int HardPityThreshold { get; set; } // e.g., 90 pulls
        public Rarity GuaranteedRarity { get; set; } = Rarity.SSR;
        public float RateIncreasePerPull { get; set; } // After soft pity
        public bool CarryOverBetweenBanners { get; set; }
        public bool ResetOnFeaturedObtained { get; set; }
        public Dictionary<Rarity, int> PityCounters { get; set; } = new();
    }

    public class GachaFeaturedCharacter
    {
        public Guid CharacterId { get; set; }
        public Rarity Rarity { get; set; }
        public float RateUpMultiplier { get; set; } = 1f;
        public bool IsRateUp { get; set; }
    }

    public class GachaHistory : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid BannerId { get; private set; }
        public SummonType SummonType { get; private set; }
        public bool IsMulti { get; private set; }
        public List<GachaResult> Results { get; private set; } = new();
        public CurrencyAmount Cost { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string SessionId { get; private set; }
        public int PityCounterBefore { get; private set; }
        public int PityCounterAfter { get; private set; }

        private GachaHistory() { }

        public GachaHistory(
            Guid userId,
            Guid bannerId,
            SummonType summonType,
            bool isMulti,
            List<GachaResult> results,
            CurrencyAmount cost,
            string sessionId,
            int pityBefore,
            int pityAfter)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            BannerId = bannerId;
            SummonType = summonType;
            IsMulti = isMulti;
            Results = results;
            Cost = cost;
            Timestamp = DateTime.UtcNow;
            SessionId = sessionId;
            PityCounterBefore = pityBefore;
            PityCounterAfter = pityAfter;
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class GachaResult
    {
        public Guid CharacterId { get; set; }
        public Rarity Rarity { get; set; }
        public bool IsNew { get; set; }
        public int DuplicateCount { get; set; } // For fragment conversion
        public bool IsFeatured { get; set; }
        public int Position { get; set; } // 1-10 for multi
    }

    public class UserGachaPity : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid BannerId { get; private set; }
        public int PullsSinceLastSSR { get; private set; }
        public int PullsSinceLastSR { get; private set; }
        public int TotalPullsOnBanner { get; private set; }
        public DateTime LastPullAt { get; private set; }
        public Dictionary<Rarity, int> RarityCounters { get; private set; } = new();

        private UserGachaPity() { }

        public UserGachaPity(Guid userId, Guid bannerId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            BannerId = bannerId;
            PullsSinceLastSSR = 0;
            PullsSinceLastSR = 0;
            TotalPullsOnBanner = 0;
            LastPullAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
        }

        public void RecordPull(Rarity rarity, PitySystem pitySystem)
        {
            TotalPullsOnBanner++;
            LastPullAt = DateTime.UtcNow;

            foreach (var r in Enum.GetValues<Rarity>())
            {
                if (!RarityCounters.ContainsKey(r)) RarityCounters[r] = 0;
                RarityCounters[r]++;
            }

            if (rarity >= Rarity.SSR)
            {
                PullsSinceLastSSR = 0;
            }
            else
            {
                PullsSinceLastSSR++;
            }

            if (rarity >= Rarity.SR)
            {
                PullsSinceLastSR = 0;
            }
            else
            {
                PullsSinceLastSR++;
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public void ResetForRarity(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.SSR:
                case Rarity.UR:
                    PullsSinceLastSSR = 0;
                    break;
                case Rarity.SR:
                    PullsSinceLastSR = 0;
                    break;
            }
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsSoftPity(Rarity rarity, PitySystem system)
        {
            return rarity >= Rarity.SSR && PullsSinceLastSSR >= system.SoftPityThreshold;
        }

        public bool IsHardPity(Rarity rarity, PitySystem system)
        {
            return rarity >= Rarity.SSR && PullsSinceLastSSR >= system.HardPityThreshold;
        }

        public Probability GetModifiedRate(Rarity rarity, Probability baseRate, PitySystem system)
        {
            if (rarity < Rarity.SSR) return baseRate;

            if (IsHardPity(rarity, system))
            {
                return new Probability(1f); // Guaranteed
            }

            if (IsSoftPity(rarity, system))
            {
                var extraPulls = PullsSinceLastSSR - system.SoftPityThreshold;
                var bonusRate = extraPulls * system.RateIncreasePerPull;
                return new Probability(Math.Min(1f, baseRate.Value + bonusRate));
            }

            return baseRate;
        }
    }
}