using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;
using GoddessRequiem.Client.UI;

namespace GoddessRequiem.Client.Gacha
{
    public class GachaSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GachaConfig _config;

        [Header("Animation")]
        [SerializeField] private GameObject _summonAnimationPrefab;
        [SerializeField] private Transform _animationRoot;
        [SerializeField] private ParticleSystem _rarityParticles;

        private List<GachaBanner> _availableBanners = new();
        private Dictionary<Guid, UserGachaPity> _pityData = new();
        private GachaHistory _lastSummonResult;

        public IReadOnlyList<GachaBanner> AvailableBanners => _availableBanners;

        public event System.Action<List<GachaBanner>> OnBannersUpdated;
        public event System.Action<GachaHistory> OnSummonCompleted;
        public event System.Action<Guid, int> OnPityUpdated;

        public async UniTask InitializeAsync()
        {
            await RefreshBannersAsync();
        }

        public async UniTask RefreshBannersAsync()
        {
            var response = await GameManager.Instance.Network.GetAsync<List<GachaBanner>>("gacha/banners");
            if (response.Success && response.Data != null)
            {
                _availableBanners = response.Data;
                OnBannersUpdated?.Invoke(_availableBanners);
            }
        }

        public async UniTask<GachaBannerDetail> GetBannerDetailAsync(Guid bannerId)
        {
            var response = await GameManager.Instance.Network.GetAsync<GachaBannerDetail>($"gacha/banners/{bannerId}");
            return response.Success ? response.Data : null;
        }

        public async UniTask<SummonResult> SummonAsync(Guid bannerId, bool isMulti, CurrencyType currencyType, long amount)
        {
            var response = await GameManager.Instance.Network.PostAsync<SummonResult>("gacha/summon", new
            {
                bannerId = bannerId.ToString(),
                isMulti,
                currencyType = (int)currencyType,
                amount
            });

            if (response.Success && response.Data != null)
            {
                _lastSummonResult = response.Data.History;
                await PlaySummonAnimationAsync(response.Data);
                OnSummonCompleted?.Invoke(response.Data.History);

                // Update pity
                if (response.Data.PityStatus != null)
                {
                    _pityData[bannerId] = response.Data.PityStatus;
                    OnPityUpdated?.Invoke(bannerId, response.Data.PityStatus.PullsSinceLastSSR);
                }

                return response.Data;
            }

            return null;
        }

        public async UniTask<SummonResult> SummonAsync(Guid bannerId, SummonType summonType)
        {
            var response = await GameManager.Instance.Network.PostAsync<SummonResult>("gacha/summon", new
            {
                bannerId = bannerId.ToString(),
                summonType = (int)summonType
            });

            if (response.Success && response.Data != null)
            {
                _lastSummonResult = response.Data.History;
                await PlaySummonAnimationAsync(response.Data);
                OnSummonCompleted?.Invoke(response.Data.History);

                // Update pity
                if (response.Data.PityStatus != null)
                {
                    _pityData[bannerId] = response.Data.PityStatus;
                    OnPityUpdated?.Invoke(bannerId, response.Data.PityStatus.PullsSinceLastSSR);
                }

                return response.Data;
            }

            return null;
        }

        public async UniTask<List<GachaHistory>> GetHistoryAsync(int limit = 50)
        {
            var response = await GameManager.Instance.Network.GetAsync<List<GachaHistory>>($"gacha/history?limit={limit}");
            return response.Success ? response.Data : new List<GachaHistory>();
        }

        public async UniTask<UserGachaPity> GetPityStatusAsync(Guid bannerId)
        {
            if (_pityData.TryGetValue(bannerId, out var pity))
            {
                return pity;
            }

            var response = await GameManager.Instance.Network.GetAsync<UserGachaPity>($"gacha/pity/{bannerId}");
            if (response.Success && response.Data != null)
            {
                _pityData[bannerId] = response.Data;
                return response.Data;
            }

            return null;
        }

        private async UniTask PlaySummonAnimationAsync(SummonResult result)
        {
            // Use UIManager to show the summon animation properly
            await UIManager.Instance.ShowSummonAnimationAsync(result);
        }

        public Probability GetDisplayRate(Guid bannerId, Rarity rarity)
        {
            var banner = _availableBanners.Find(b => b.Id == bannerId);
            if (banner == null) return new Probability(0);

            var rate = banner.Rates.Find(r => r.Rarity == rarity);
            if (rate == null) return new Probability(0);

            // Apply pity modifier
            if (_pityData.TryGetValue(bannerId, out var pity))
            {
                return pity.GetModifiedRate(rarity, rate.BaseRate, banner.PitySystem);
            }

            return rate.BaseRate;
        }

        public static GachaSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    [System.Serializable]
    public class GachaConfig
    {
        public float AnimationSkipDelay = 0.5f;
        public bool AutoSkipDuplicates = false;
        public int MaxHistoryDisplayed = 100;
    }

    [System.Serializable]
    public class GachaBanner
    {
        public Guid Id;
        public string Name;
        public string Description;
        public SummonType Type;
        public DateTimeRange Period;
        public bool IsActive;
        public List<GachaRate> Rates;
        public PitySystem PitySystem;
        public List<GachaFeaturedCharacter> FeaturedCharacters;
        public CurrencyAmount SingleCost;
        public CurrencyAmount MultiCost;
        public int MultiCount;
        public string BannerImagePath;
        public string BackgroundVideoPath;
    }

    [System.Serializable]
    public class GachaBannerDetail : GachaBanner
    {
        public List<GachaRateDisplay> RateDisplays;
        public UserGachaPity UserPity;
    }

    [System.Serializable]
    public class GachaRate
    {
        public Rarity Rarity;
        public Probability BaseRate;
        public Probability DisplayRate;
        public string Description;
    }

    [System.Serializable]
    public class GachaRateDisplay
    {
        public Rarity Rarity;
        public Probability Rate;
        public List<CharacterDisplayInfo> Characters;
    }

    [System.Serializable]
    public class CharacterDisplayInfo
    {
        public Guid CharacterId;
        public string Name;
        public string IconPath;
        public bool IsFeatured;
        public Probability IndividualRate;
    }

    [System.Serializable]
    public class PitySystem
    {
        public int SoftPityThreshold;
        public int HardPityThreshold;
        public Rarity GuaranteedRarity;
        public float RateIncreasePerPull;
        public bool CarryOverBetweenBanners;
        public bool ResetOnFeaturedObtained;
    }

    [System.Serializable]
    public class GachaFeaturedCharacter
    {
        public Guid CharacterId;
        public Rarity Rarity;
        public float RateUpMultiplier;
        public bool IsRateUp;
    }

    [System.Serializable]
    public class UserGachaPity
    {
        public Guid UserId;
        public Guid BannerId;
        public int PullsSinceLastSSR;
        public int PullsSinceLastSR;
        public int TotalPullsOnBanner;
        public Dictionary<Rarity, int> RarityCounters;

        public Probability GetModifiedRate(Rarity rarity, Probability baseRate, PitySystem system)
        {
            if (rarity < Rarity.SSR) return baseRate;

            if (PullsSinceLastSSR >= system.HardPityThreshold)
            {
                return new Probability(1f);
            }

            if (PullsSinceLastSSR >= system.SoftPityThreshold)
            {
                var extraPulls = PullsSinceLastSSR - system.SoftPityThreshold;
                var bonusRate = extraPulls * system.RateIncreasePerPull;
                return new Probability(Mathf.Min(1f, baseRate.Value + bonusRate));
            }

            return baseRate;
        }
    }

    [System.Serializable]
    public class SummonResult
    {
        public GachaHistory History;
        public UserGachaPity PityStatus;
        public List<SummonAnimationData> AnimationData;
    }

    [System.Serializable]
    public class GachaHistory
    {
        public Guid Id;
        public Guid UserId;
        public Guid BannerId;
        public SummonType SummonType;
        public bool IsMulti;
        public List<GachaResult> Results;
        public CurrencyAmount Cost;
        public DateTime Timestamp;
        public string SessionId;
        public int PityCounterBefore;
        public int PityCounterAfter;
    }

    [System.Serializable]
    public class GachaResult
    {
        public Guid CharacterId;
        public Rarity Rarity;
        public bool IsNew;
        public int DuplicateCount;
        public bool IsFeatured;
        public int Position;
    }

    [System.Serializable]
    public class SummonAnimationData
    {
        public GachaResult Result;
        public float Delay;
        public string AnimationType;
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

    public enum Rarity
    {
        R = 1,
        SR = 2,
        SSR = 3,
        UR = 4
    }

    [System.Serializable]
    public class Probability
    {
        public float Value;

        public Probability(float value)
        {
            Value = Mathf.Clamp01(value);
        }

        public static implicit operator float(Probability p) => p.Value;
        public static implicit operator Probability(float f) => new Probability(f);

        public override string ToString() => $"{Value * 100:F4}%";
    }

    [System.Serializable]
    public class CurrencyAmount
    {
        public CurrencyType Type;
        public long Amount;
    }

    public enum CurrencyType
    {
        Gold = 1,
        Gem = 2,
        SummonTicket = 3,
        PremiumSummonTicket = 4,
        Experience = 19,
        CharacterExp = 20,
        Stamina = 22,
        Energy = 23
    }
}