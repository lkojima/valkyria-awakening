using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GoddessRequiem.Core.Application.Services
{
    public class GachaService : IGachaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GachaService> _logger;
        private readonly IRandomProvider _randomProvider;

        public GachaService(IUnitOfWork unitOfWork, ILogger<GachaService> logger, IRandomProvider randomProvider)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _randomProvider = randomProvider;
        }

        public async Task<IReadOnlyList<GachaBannerDto>> GetAvailableBannersAsync(Guid userId)
        {
            var banners = await _unitOfWork.GachaBanners.GetActiveAsync(DateTime.UtcNow);
            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            return banners.Select(b => MapToBannerDto(b, user)).ToList();
        }

        public async Task<GachaBannerDetailDto> GetBannerDetailAsync(Guid bannerId)
        {
            var banner = await _unitOfWork.GachaBanners.GetByIdAsync(bannerId);
            if (banner == null) return null;

            var rates = await _unitOfWork.GachaRates.GetByBannerIdAsync(bannerId);
            var pity = await _unitOfWork.GachaPities.GetByBannerIdAsync(bannerId);

            return new GachaBannerDetailDto
            {
                Id = banner.Id,
                Name = banner.Name,
                Description = banner.Description,
                Type = banner.Type,
                StartAt = banner.StartAt,
                EndAt = banner.EndAt,
                CostSingle = banner.CostSingle,
                CostMulti = banner.CostMulti,
                CurrencyType = banner.CurrencyType,
                FeaturedCharacters = banner.FeaturedCharacters.Select(MapToFeaturedDto).ToList(),
                RateUpCharacters = banner.RateUpCharacters.Select(id => id.ToString()).ToList(),
                GuaranteedRarity = banner.GuaranteedRarity,
                PityThreshold = banner.PityThreshold,
                SoftPityStart = banner.SoftPityStart,
                Rates = rates.Select(MapToRateDto).ToList(),
                CurrentPity = pity?.CurrentCount ?? 0,
                IsNewPlayerBanner = banner.IsNewPlayerBanner,
                MaxPullsNewPlayer = banner.MaxPullsNewPlayer
            };
        }

        public async Task<SummonResult> SummonAsync(Guid userId, Guid bannerId, bool isMulti, CurrencyAmount cost)
        {
            var banner = await _unitOfWork.GachaBanners.GetByIdAsync(bannerId);
            if (banner == null)
                throw new KeyNotFoundException("Banner not found");

            if (!banner.IsActive(DateTime.UtcNow))
                throw new InvalidOperationException("Banner is not active");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            // Check currency
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient currency");

            // Spend currency
            await SpendCurrencyAsync(userId, cost);

            var pullCount = isMulti ? 10 : 1;
            var results = new List<GachaResult>();
            var pity = await _unitOfWork.GachaPities.GetOrCreateAsync(userId, bannerId);

            for (int i = 0; i < pullCount; i++)
            {
                var result = await PerformSinglePullAsync(userId, banner, pity);
                results.Add(result);

                // Update pity
                pity.IncrementPull(result.Rarity, banner.PityThreshold, banner.SoftPityStart, banner.GuaranteedRarity);

                // Grant character/item
                await GrantRewardAsync(userId, result);
            }

            await _unitOfWork.GachaPities.UpdateAsync(pity);

            // Record history
            var history = new GachaHistory
            {
                UserId = userId,
                BannerId = bannerId,
                PullCount = pullCount,
                Results = results,
                CurrencySpent = cost,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.GachaHistories.AddAsync(history);

            await _unitOfWork.SaveChangesAsync();

            // Check for guaranteed SSR/UR
            var guaranteedHits = results.Where(r => r.IsGuaranteed).ToList();

            return new SummonResult
            {
                Results = results,
                UpdatedPity = new UserGachaPityDto
                {
                    BannerId = bannerId,
                    CurrentCount = pity.CurrentCount,
                    GuaranteedCount = pity.GuaranteedCount,
                    SoftPityActive = pity.IsInSoftPity,
                    GuaranteedRarity = pity.GuaranteedRarity
                },
                CurrencySpent = cost,
                GuaranteedHits = guaranteedHits.Select(MapToGuaranteedDto).ToList()
            };
        }

        public async Task<IReadOnlyList<GachaHistoryDto>> GetSummonHistoryAsync(Guid userId, int limit = 50)
        {
            var histories = await _unitOfWork.GachaHistories.GetByUserIdAsync(userId, limit);
            return histories.Select(MapToHistoryDto).ToList();
        }

        public async Task<UserGachaPityDto> GetPityStatusAsync(Guid userId, Guid bannerId)
        {
            var pity = await _unitOfWork.GachaPities.GetByUserAndBannerAsync(userId, bannerId);
            if (pity == null)
            {
                var banner = await _unitOfWork.GachaBanners.GetByIdAsync(bannerId);
                return new UserGachaPityDto
                {
                    BannerId = bannerId,
                    CurrentCount = 0,
                    GuaranteedCount = 0,
                    SoftPityActive = false,
                    GuaranteedRarity = banner?.GuaranteedRarity ?? Rarity.SSR
                };
            }

            return new UserGachaPityDto
            {
                BannerId = bannerId,
                CurrentCount = pity.CurrentCount,
                GuaranteedCount = pity.GuaranteedCount,
                SoftPityActive = pity.IsInSoftPity,
                GuaranteedRarity = pity.GuaranteedRarity
            };
        }

        public async Task<SummonAnimationData> GetSummonAnimationAsync(Guid bannerId, List<GachaResult> results)
        {
            var banner = await _unitOfWork.GachaBanners.GetByIdAsync(bannerId);
            var highestRarity = results.Max(r => r.Rarity);

            return new SummonAnimationData
            {
                BannerTheme = banner?.AnimationTheme ?? "default",
                Results = results.Select(MapToAnimationResult).ToList(),
                HighestRarity = highestRarity,
                HasGuaranteed = results.Any(r => r.IsGuaranteed),
                AnimationDuration = CalculateAnimationDuration(results),
                SkipTime = CalculateSkipTime(highestRarity)
            };
        }

        private async Task<GachaResult> PerformSinglePullAsync(Guid userId, GachaBanner banner, GachaPity pity)
        {
            var rates = await _unitOfWork.GachaRates.GetByBannerIdAsync(banner.Id);
            var effectiveRates = pity.CalculateEffectiveRates(rates);

            var roll = _randomProvider.NextDouble();
            var cumulative = 0.0;

            foreach (var rate in effectiveRates.OrderBy(r => r.Rarity))
            {
                cumulative += rate.Probability;
                if (roll <= cumulative)
                {
                    var reward = await SelectRewardAsync(banner, rate.Rarity, userId);
                    return new GachaResult
                    {
                        RewardType = reward.Type,
                        ResourceId = reward.ResourceId,
                        Rarity = rate.Rarity,
                        IsNew = reward.IsNew,
                        IsGuaranteed = pity.IsGuaranteedPull(rate.Rarity),
                        Quantity = reward.Quantity
                    };
                }
            }

            // Fallback to lowest rarity
            var fallbackReward = await SelectRewardAsync(banner, Rarity.R, userId);
            return new GachaResult
            {
                RewardType = fallbackReward.Type,
                ResourceId = fallbackReward.ResourceId,
                Rarity = Rarity.R,
                IsNew = fallbackReward.IsNew,
                IsGuaranteed = false,
                Quantity = fallbackReward.Quantity
            };
        }

        private async Task<GachaReward> SelectRewardAsync(GachaBanner banner, Rarity rarity, Guid userId)
        {
            var rateUpCharacters = banner.RateUpCharacters.ToList();
            var pool = await _unitOfWork.GachaPools.GetByRarityAsync(rarity, banner.Type);

            // Prioritize rate-up characters
            if (rarity >= Rarity.SR && rateUpCharacters.Any())
            {
                var rateUpPool = pool.Where(p => rateUpCharacters.Contains(p.CharacterId)).ToList();
                if (rateUpPool.Any())
                {
                    var selected = rateUpPool[_randomProvider.Next(rateUpPool.Count)];
                    var isNew = !(await _unitOfWork.UserCharacters.ExistsAsync(userId, selected.CharacterId));
                    return new GachaReward
                    {
                        Type = RewardType.Character,
                        ResourceId = selected.CharacterId,
                        IsNew = isNew,
                        Quantity = 1
                    };
                }
            }

            // Regular pool
            var regularPool = pool.Where(p => !rateUpCharacters.Contains(p.CharacterId)).ToList();
            if (!regularPool.Any())
                regularPool = pool;

            var reward = regularPool[_randomProvider.Next(regularPool.Count)];
            var isNewChar = !(await _unitOfWork.UserCharacters.ExistsAsync(userId, reward.CharacterId));

            return new GachaReward
            {
                Type = RewardType.Character,
                ResourceId = reward.CharacterId,
                IsNew = isNewChar,
                Quantity = 1
            };
        }

        private async Task GrantRewardAsync(Guid userId, GachaResult result)
        {
            switch (result.RewardType)
            {
                case RewardType.Character:
                    await _unitOfWork.UserCharacters.AddOrUpdateAsync(new UserCharacter(userId, result.ResourceId, DateTime.UtcNow));
                    break;
                case RewardType.Item:
                    await AddItemAsync(userId, result.ResourceId, result.Quantity);
                    break;
                case RewardType.Currency:
                    await AddCurrencyAsync(userId, result.ResourceId, result.Quantity);
                    break;
            }
        }

        // Mapping methods
        private GachaBannerDto MapToBannerDto(GachaBanner banner, User user) => new GachaBannerDto
        {
            Id = banner.Id,
            Name = banner.Name,
            Type = banner.Type,
            StartAt = banner.StartAt,
            EndAt = banner.EndAt,
            CostSingle = banner.CostSingle,
            CostMulti = banner.CostMulti,
            CurrencyType = banner.CurrencyType,
            FeaturedImage = banner.FeaturedImage,
            IsNewPlayerBanner = banner.IsNewPlayerBanner,
            RemainingPullsNewPlayer = banner.IsNewPlayerBanner && user != null
                ? Math.Max(0, banner.MaxPullsNewPlayer - (user.GachaNewPlayerPulls ?? 0))
                : 0
        };

        private FeaturedCharacterDto MapToFeaturedDto(GachaBannerFeaturedCharacter fc) => new FeaturedCharacterDto
        {
            CharacterId = fc.CharacterId,
            CharacterName = fc.CharacterName,
            Rarity = fc.Rarity,
            Element = fc.Element,
            Class = fc.Class,
            IsRateUp = fc.IsRateUp,
            PortraitAssetPath = fc.PortraitAssetPath
        };

        private GachaRateDto MapToRateDto(GachaRate rate) => new GachaRateDto
        {
            Rarity = rate.Rarity,
            BaseProbability = rate.BaseProbability,
            SoftPityBonus = rate.SoftPityBonus,
            MaxProbability = rate.MaxProbability
        };

        private GachaHistoryDto MapToHistoryDto(GachaHistory h) => new GachaHistoryDto
        {
            Id = h.Id,
            BannerId = h.BannerId,
            BannerName = h.BannerName,
            PullCount = h.PullCount,
            Results = h.Results.Select(MapToResultDto).ToList(),
            CurrencySpent = h.CurrencySpent,
            CreatedAt = h.CreatedAt
        };

        private GachaResultDto MapToResultDto(GachaResult r) => new GachaResultDto
        {
            RewardType = r.RewardType,
            ResourceId = r.ResourceId,
            ResourceName = r.ResourceName,
            Rarity = r.Rarity,
            IsNew = r.IsNew,
            IsGuaranteed = r.IsGuaranteed,
            Quantity = r.Quantity
        };

        private GuaranteedHitDto MapToGuaranteedDto(GachaResult r) => new GuaranteedHitDto
        {
            Rarity = r.Rarity,
            CharacterId = r.ResourceId,
            CharacterName = r.ResourceName
        };

        private SummonAnimationResult MapToAnimationResult(GachaResult r) => new SummonAnimationResult
        {
            RewardType = r.RewardType,
            ResourceId = r.ResourceId,
            Rarity = r.Rarity,
            IsNew = r.IsNew,
            IsGuaranteed = r.IsGuaranteed,
            AnimationVariant = GetAnimationVariant(r.Rarity, r.IsGuaranteed)
        };

        private string GetAnimationVariant(Rarity rarity, bool isGuaranteed)
        {
            if (isGuaranteed) return "guaranteed";
            return rarity switch
            {
                Rarity.UR => "ur",
                Rarity.SSR => "ssr",
                Rarity.SR => "sr",
                _ => "r"
            };
        }

        private float CalculateAnimationDuration(List<GachaResult> results)
        {
            var maxRarity = results.Max(r => r.Rarity);
            return maxRarity switch
            {
                Rarity.UR => 8f,
                Rarity.SSR => 6f,
                Rarity.SR => 4f,
                _ => 2f
            };
        }

        private float CalculateSkipTime(Rarity rarity) => rarity switch
        {
            Rarity.UR => 3f,
            Rarity.SSR => 2f,
            _ => 1f
        };

        // Placeholder implementations
        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task AddItemAsync(Guid userId, Guid itemId, int quantity) { }
        private async Task AddCurrencyAsync(Guid userId, Guid currencyId, int quantity) { }
    }
}