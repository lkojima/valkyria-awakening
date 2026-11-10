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
    public class MonetizationService : IMonetizationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MonetizationService> _logger;
        private readonly IGooglePlayBillingService _billingService;

        public MonetizationService(IUnitOfWork unitOfWork, ILogger<MonetizationService> logger, IGooglePlayBillingService billingService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _billingService = billingService;
        }

        public async Task<IReadOnlyList<PurchaseProductDto>> GetProductsAsync(Guid userId)
        {
            var products = await _unitOfWork.PurchaseProducts.GetActiveAsync();
            var userPurchases = await _unitOfWork.UserPurchases.GetByUserIdAsync(userId);

            return products.Select(p => MapToProductDto(p, userPurchases)).ToList();
        }

        public async Task<PurchaseValidationResult> ValidatePurchaseAsync(Guid userId, string productId, string purchaseToken, string receiptData)
        {
            // Verify with Google Play Billing
            var verificationResult = await _billingService.VerifyPurchaseAsync(productId, purchaseToken, receiptData);
            if (!verificationResult.IsValid)
            {
                _logger.LogWarning("Purchase verification failed for user {UserId}: {Error}", userId, verificationResult.ErrorMessage);
                return new PurchaseValidationResult { Success = false, Error = verificationResult.ErrorMessage };
            }

            // Check if already processed
            var existing = await _unitOfWork.UserPurchases.GetByPurchaseTokenAsync(purchaseToken);
            if (existing != null)
            {
                return new PurchaseValidationResult { Success = false, Error = "Purchase already processed" };
            }

            var product = await _unitOfWork.PurchaseProducts.GetByProductIdAsync(productId);
            if (product == null)
            {
                return new PurchaseValidationResult { Success = false, Error = "Product not found" };
            }

            // Record purchase
            var purchase = new UserPurchase
            {
                UserId = userId,
                ProductId = product.Id,
                ProductType = product.Type,
                PurchaseToken = purchaseToken,
                OrderId = verificationResult.OrderId,
                Price = verificationResult.Price,
                Currency = verificationResult.Currency,
                PurchaseTime = verificationResult.PurchaseTime,
                Status = PurchaseStatus.Completed,
                Payload = receiptData
            };

            await _unitOfWork.UserPurchases.AddAsync(purchase);

            // Grant rewards based on product type
            await GrantProductRewardsAsync(userId, product, verificationResult);

            // Handle subscriptions
            if (product.Type == ProductType.Subscription)
            {
                await ProcessSubscriptionAsync(userId, product, verificationResult);
            }

            await _unitOfWork.SaveChangesAsync();

            // Track analytics
            await TrackPurchaseAsync(userId, productId, verificationResult.Price, verificationResult.Currency);

            return new PurchaseValidationResult
            {
                Success = true,
                ProductId = productId,
                ProductType = product.Type,
                Rewards = product.Rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<SubscriptionStatus> GetSubscriptionStatusAsync(Guid userId)
        {
            var subscriptions = await _unitOfWork.UserSubscriptions.GetActiveByUserIdAsync(userId);

            return new SubscriptionStatus
            {
                ActiveSubscriptions = subscriptions.Select(MapToSubscriptionDto).ToList(),
                HasActiveSubscription = subscriptions.Any()
            };
        }

        public async Task<bool> ProcessSubscriptionRenewalAsync(Guid userId, string purchaseToken)
        {
            var verificationResult = await _billingService.VerifyPurchaseAsync("", purchaseToken, "");
            if (!verificationResult.IsValid)
                return false;

            var subscription = await _unitOfWork.UserSubscriptions.GetByPurchaseTokenAsync(purchaseToken);
            if (subscription == null)
                return false;

            // Update subscription
            subscription.ExpiryDate = verificationResult.ExpiryTime;
            subscription.Status = SubscriptionStatus.Active;
            subscription.LastRenewalAt = DateTime.UtcNow;
            subscription.AutoRenew = verificationResult.AutoRenewing;

            await _unitOfWork.UserSubscriptions.UpdateAsync(subscription);

            // Grant renewal rewards
            var product = await _unitOfWork.PurchaseProducts.GetByIdAsync(subscription.ProductId);
            if (product != null)
            {
                await GrantProductRewardsAsync(userId, product, verificationResult);
            }

            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<BattlePassInfoDto> GetBattlePassInfoAsync(Guid userId)
        {
            var currentSeason = GetCurrentBattlePassSeason();
            var battlePass = await _unitOfWork.BattlePasses.GetBySeasonAsync(currentSeason);
            if (battlePass == null) return null;

            var userPass = await _unitOfWork.UserBattlePasses.GetByUserAndSeasonAsync(userId, currentSeason);
            if (userPass == null)
            {
                userPass = new UserBattlePass { UserId = userId, Season = currentSeason };
                await _unitOfWork.UserBattlePasses.AddAsync(userPass);
                await _unitOfWork.SaveChangesAsync();
            }

            var claimedFree = userPass.ClaimedFreeRewards;
            var claimedPremium = userPass.ClaimedPremiumRewards;

            return new BattlePassInfoDto
            {
                Season = currentSeason,
                EndAt = battlePass.EndAt,
                Level = userPass.Level,
                Experience = userPass.Experience,
                ExperienceToNextLevel = CalculateExpToNextLevel(userPass.Level),
                IsPremium = userPass.IsPremium,
                ClaimedFreeRewards = claimedFree,
                ClaimedPremiumRewards = claimedPremium,
                FreeRewards = battlePass.FreeRewards.Select((r, i) => MapToBattlePassRewardDto(r, i + 1, claimedFree.Contains(i + 1), false)).ToList(),
                PremiumRewards = battlePass.PremiumRewards.Select((r, i) => MapToBattlePassRewardDto(r, i + 1, claimedPremium.Contains(i + 1), true)).ToList(),
                PremiumPrice = battlePass.PremiumPrice
            };
        }

        public async Task<BattlePassRewardResult> ClaimBattlePassRewardAsync(Guid userId, int tier, bool isPremium)
        {
            var currentSeason = GetCurrentBattlePassSeason();
            var userPass = await _unitOfWork.UserBattlePasses.GetByUserAndSeasonAsync(userId, currentSeason);
            if (userPass == null)
                throw new KeyNotFoundException("Battle pass not found");

            var battlePass = await _unitOfWork.BattlePasses.GetBySeasonAsync(currentSeason);
            if (battlePass == null)
                throw new KeyNotFoundException("Battle pass season not found");

            var claimedList = isPremium ? userPass.ClaimedPremiumRewards : userPass.ClaimedFreeRewards;
            var rewardsList = isPremium ? battlePass.PremiumRewards : battlePass.FreeRewards;

            if (tier < 1 || tier > rewardsList.Count)
                throw new ArgumentException("Invalid tier");

            if (claimedList.Contains(tier))
                throw new InvalidOperationException("Reward already claimed");

            if (userPass.Level < tier)
                throw new InvalidOperationException("Tier not unlocked");

            if (isPremium && !userPass.IsPremium)
                throw new InvalidOperationException("Premium battle pass required");

            var reward = rewardsList[tier - 1];
            await GrantRewardsAsync(userId, reward.Rewards);

            claimedList.Add(tier);

            await _unitOfWork.UserBattlePasses.UpdateAsync(userPass);
            await _unitOfWork.SaveChangesAsync();

            return new BattlePassRewardResult
            {
                Success = true,
                Tier = tier,
                IsPremium = isPremium,
                Rewards = reward.Rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<PurchaseHistoryDto> GetPurchaseHistoryAsync(Guid userId, int limit = 50)
        {
            var purchases = await _unitOfWork.UserPurchases.GetByUserIdAsync(userId, limit);

            return new PurchaseHistoryDto
            {
                Purchases = purchases.Select(MapToPurchaseHistoryItemDto).ToList()
            };
        }

        public async Task AddBattlePassExpAsync(Guid userId, int exp)
        {
            var currentSeason = GetCurrentBattlePassSeason();
            var userPass = await _unitOfWork.UserBattlePasses.GetByUserAndSeasonAsync(userId, currentSeason);
            if (userPass == null) return;

            var oldLevel = userPass.Level;
            userPass.Experience += exp;

            // Check for level ups
            while (userPass.Experience >= CalculateExpToNextLevel(userPass.Level))
            {
                userPass.Experience -= CalculateExpToNextLevel(userPass.Level);
                userPass.Level++;
            }

            if (userPass.Level > oldLevel)
            {
                // Grant level up rewards
                var battlePass = await _unitOfWork.BattlePasses.GetBySeasonAsync(currentSeason);
                for (int level = oldLevel + 1; level <= userPass.Level; level++)
                {
                    var freeReward = battlePass.FreeRewards.FirstOrDefault(r => r.Tier == level);
                    if (freeReward != null && !userPass.ClaimedFreeRewards.Contains(level))
                    {
                        await GrantRewardsAsync(userId, freeReward.Rewards);
                    }

                    if (userPass.IsPremium)
                    {
                        var premiumReward = battlePass.PremiumRewards.FirstOrDefault(r => r.Tier == level);
                        if (premiumReward != null && !userPass.ClaimedPremiumRewards.Contains(level))
                        {
                            await GrantRewardsAsync(userId, premiumReward.Rewards);
                        }
                    }
                }
            }

            await _unitOfWork.UserBattlePasses.UpdateAsync(userPass);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> PurchaseBattlePassPremiumAsync(Guid userId)
        {
            var currentSeason = GetCurrentBattlePassSeason();
            var battlePass = await _unitOfWork.BattlePasses.GetBySeasonAsync(currentSeason);
            if (battlePass == null) return false;

            var userPass = await _unitOfWork.UserBattlePasses.GetByUserAndSeasonAsync(userId, currentSeason);
            if (userPass == null)
            {
                userPass = new UserBattlePass { UserId = userId, Season = currentSeason };
                await _unitOfWork.UserBattlePasses.AddAsync(userPass);
            }

            if (userPass.IsPremium) return true;

            var cost = new CurrencyAmount(CurrencyType.PremiumCurrency, battlePass.PremiumPrice);
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford) return false;

            await SpendCurrencyAsync(userId, cost);

            userPass.IsPremium = true;
            userPass.PremiumPurchasedAt = DateTime.UtcNow;

            // Grant immediate premium rewards for unlocked tiers
            foreach (var reward in battlePass.PremiumRewards.Where(r => r.Tier <= userPass.Level))
            {
                if (!userPass.ClaimedPremiumRewards.Contains(reward.Tier))
                {
                    await GrantRewardsAsync(userId, reward.Rewards);
                }
            }

            await _unitOfWork.UserBattlePasses.UpdateAsync(userPass);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        private int GetCurrentBattlePassSeason() => (int)((DateTime.UtcNow - new DateTime(2024, 1, 1)).TotalDays / 28) + 1;

        private int CalculateExpToNextLevel(int level) => 1000 + (level * 100);

        private async Task GrantProductRewardsAsync(Guid userId, PurchaseProduct product, PurchaseVerificationResult verification)
        {
            foreach (var reward in product.Rewards)
            {
                await GrantRewardAsync(userId, reward);
            }

            // Bonus for first purchase
            if (product.Type == ProductType.Consumable || product.Type == ProductType.NonConsumable)
            {
                var firstPurchase = await _unitOfWork.UserPurchases.IsFirstPurchaseAsync(userId, product.Id);
                if (firstPurchase && product.FirstPurchaseBonus != null)
                {
                    await GrantRewardAsync(userId, product.FirstPurchaseBonus);
                }
            }
        }

        private async Task ProcessSubscriptionAsync(Guid userId, PurchaseProduct product, PurchaseVerificationResult verification)
        {
            var subscription = new UserSubscription
            {
                UserId = userId,
                ProductId = product.Id,
                PurchaseToken = verification.PurchaseToken,
                StartDate = verification.PurchaseTime,
                ExpiryDate = verification.ExpiryTime,
                Status = SubscriptionStatus.Active,
                AutoRenew = verification.AutoRenewing,
                Platform = Platform.Android
            };

            await _unitOfWork.UserSubscriptions.AddAsync(subscription);

            // Grant subscription benefits
            if (product.SubscriptionBenefits != null)
            {
                foreach (var benefit in product.SubscriptionBenefits)
                {
                    await ApplySubscriptionBenefitAsync(userId, benefit);
                }
            }
        }

        private async Task ApplySubscriptionBenefitAsync(Guid userId, SubscriptionBenefit benefit)
        {
            switch (benefit.Type)
            {
                case SubscriptionBenefitType.DailyRewards:
                    // Would set up daily reward multiplier
                    break;
                case SubscriptionBenefitType.InventorySlots:
                    // Would increase inventory
                    break;
                case SubscriptionBenefitType.EnergyRegen:
                    // Would increase energy regen
                    break;
            }
        }

        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards)
        {
            foreach (var reward in rewards)
            {
                await GrantRewardAsync(userId, reward);
            }
        }

        private async Task GrantRewardAsync(Guid userId, Reward reward) { }
        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task TrackPurchaseAsync(Guid userId, string productId, decimal price, string currency) { }

        private PurchaseProductDto MapToProductDto(PurchaseProduct p, List<UserPurchase> userPurchases)
        {
            var userPurchase = userPurchases.FirstOrDefault(up => up.ProductId == p.Id);
            var purchaseCount = userPurchases.Count(up => up.ProductId == p.Id);

            return new PurchaseProductDto
            {
                Id = p.Id,
                ProductId = p.ProductId,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type,
                Price = p.Price,
                Currency = p.Currency,
                CurrencyType = p.CurrencyType,
                Rewards = p.Rewards.Select(MapToRewardDto).ToList(),
                IsPurchased = userPurchase != null,
                PurchaseCount = purchaseCount,
                PurchaseLimit = p.PurchaseLimit,
                RemainingPurchases = p.PurchaseLimit > 0 ? p.PurchaseLimit - purchaseCount : -1,
                Tags = p.Tags
            };
        }

        private SubscriptionDto MapToSubscriptionDto(UserSubscription s) => new SubscriptionDto
        {
            ProductId = s.ProductId,
            PurchaseToken = s.PurchaseToken,
            StartDate = s.StartDate,
            ExpiryDate = s.ExpiryDate,
            Status = s.Status,
            AutoRenew = s.AutoRenew,
            Platform = s.Platform
        };

        private BattlePassRewardDto MapToBattlePassRewardDto(BattlePassReward r, int tier, bool claimed, bool isPremium) => new BattlePassRewardDto
        {
            Tier = tier,
            IsPremium = isPremium,
            Rewards = r.Rewards.Select(MapToRewardDto).ToList(),
            Claimed = claimed,
            Unlocked = true // Would check level
        };

        private RewardDto MapToRewardDto(Reward r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };

        private PurchaseHistoryItemDto MapToPurchaseHistoryItemDto(UserPurchase p) => new PurchaseHistoryItemDto
        {
            ProductId = p.ProductId,
            ProductName = p.ProductId.ToString(), // Would get actual name
            Type = p.ProductType,
            Price = p.Price,
            Currency = p.Currency,
            PurchaseTime = p.PurchaseTime,
            Status = p.Status
        };
    }
}