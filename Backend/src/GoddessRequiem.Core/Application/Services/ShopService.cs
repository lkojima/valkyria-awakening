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
    public class ShopService : IShopService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ShopService> _logger;

        public ShopService(IUnitOfWork unitOfWork, ILogger<ShopService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ShopDto>> GetAvailableShopsAsync(Guid userId)
        {
            var shops = await _unitOfWork.Shops.GetActiveAsync(DateTime.UtcNow);
            var userShopData = await _unitOfWork.UserShopData.GetByUserIdAsync(userId);

            return shops.Select(s => MapToDto(s, userShopData)).ToList();
        }

        public async Task<ShopDetailDto> GetShopDetailAsync(Guid userId, Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) return null;

            var userShopData = await _unitOfWork.UserShopData.GetByUserIdAsync(userId);
            var shopData = userShopData?.Shops.GetValueOrDefault(shopId);

            return new ShopDetailDto
            {
                Id = shop.Id,
                Name = shop.Name,
                Type = shop.Type,
                RefreshInterval = shop.RefreshInterval,
                NextRefreshAt = shopData?.NextRefreshAt ?? shop.GetNextRefreshTime(DateTime.UtcNow),
                FreeRefreshesRemaining = shopData?.FreeRefreshesRemaining ?? shop.FreeRefreshesPerDay,
                RefreshCost = shop.RefreshCost,
                Items = shop.Items.Select(i => MapToItemDto(i, shopData)).ToList(),
                CurrencyType = shop.CurrencyType
            };
        }

        public async Task<ShopPurchaseResult> PurchaseAsync(Guid userId, Guid shopId, Guid itemId, int quantity)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null)
                throw new KeyNotFoundException("Shop not found");

            var item = shop.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
                throw new KeyNotFoundException("Item not found");

            var userShopData = await _unitOfWork.UserShopData.GetByUserIdAsync(userId);
            if (userShopData == null)
            {
                userShopData = new UserShopData { UserId = userId };
                await _unitOfWork.UserShopData.AddAsync(userShopData);
            }

            var shopData = userShopData.GetOrCreateShopData(shopId);

            // Check stock
            if (item.Stock > 0 && shopData.PurchasedItems.GetValueOrDefault(itemId, 0) + quantity > item.Stock)
                throw new InvalidOperationException("Insufficient stock");

            // Check purchase limit
            if (item.PurchaseLimit > 0 && shopData.PurchasedItems.GetValueOrDefault(itemId, 0) + quantity > item.PurchaseLimit)
                throw new InvalidOperationException("Purchase limit exceeded");

            // Check currency
            var totalCost = new CurrencyAmount(shop.CurrencyType, item.Price * quantity);
            var canAfford = await CheckCurrencyAsync(userId, totalCost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient currency");

            // Spend currency
            await SpendCurrencyAsync(userId, totalCost);

            // Grant item
            var rewards = new List<Reward>
            {
                new Reward { Type = item.RewardType, ResourceId = item.RewardId, Quantity = item.Quantity * quantity, IsGuaranteed = true }
            };
            await GrantRewardsAsync(userId, rewards);

            // Update purchase count
            shopData.PurchasedItems[itemId] = shopData.PurchasedItems.GetValueOrDefault(itemId, 0) + quantity;
            shopData.LastPurchaseAt = DateTime.UtcNow;

            await _unitOfWork.UserShopData.UpdateAsync(userShopData);
            await _unitOfWork.SaveChangesAsync();

            return new ShopPurchaseResult
            {
                Success = true,
                ItemId = itemId,
                Quantity = quantity,
                CurrencySpent = totalCost,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                RemainingStock = item.Stock > 0 ? item.Stock - shopData.PurchasedItems[itemId] : -1
            };
        }

        public async Task<RefreshResult> RefreshShopAsync(Guid userId, Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null)
                throw new KeyNotFoundException("Shop not found");

            var userShopData = await _unitOfWork.UserShopData.GetByUserIdAsync(userId);
            if (userShopData == null)
            {
                userShopData = new UserShopData { UserId = userId };
                await _unitOfWork.UserShopData.AddAsync(userShopData);
            }

            var shopData = userShopData.GetOrCreateShopData(shopId);

            // Check if can refresh
            if (shopData.FreeRefreshesRemaining > 0)
            {
                shopData.FreeRefreshesRemaining--;
            }
            else
            {
                var canAfford = await CheckCurrencyAsync(userId, shop.RefreshCost);
                if (!canAfford)
                    throw new InvalidOperationException("Insufficient currency for refresh");

                await SpendCurrencyAsync(userId, shop.RefreshCost);
            }

            // Refresh items
            shopData.RefreshItems(shop);
            shopData.NextRefreshAt = shop.GetNextRefreshTime(DateTime.UtcNow);

            await _unitOfWork.UserShopData.UpdateAsync(userShopData);
            await _unitOfWork.SaveChangesAsync();

            var updatedShop = await GetShopDetailAsync(userId, shopId);

            return new RefreshResult
            {
                Success = true,
                Items = updatedShop.Items,
                NextRefreshAt = shopData.NextRefreshAt,
                FreeRefreshesRemaining = shopData.FreeRefreshesRemaining
            };
        }

        private ShopDto MapToDto(Shop s, UserShopData userData)
        {
            var shopData = userData?.Shops.GetValueOrDefault(s.Id);
            return new ShopDto
            {
                Id = s.Id,
                Name = s.Name,
                Type = s.Type,
                CurrencyType = s.CurrencyType,
                NextRefreshAt = shopData?.NextRefreshAt ?? s.GetNextRefreshTime(DateTime.UtcNow),
                FreeRefreshesRemaining = shopData?.FreeRefreshesRemaining ?? s.FreeRefreshesPerDay
            };
        }

        private ShopItemDto MapToItemDto(ShopItem item, UserShopData.ShopData shopData)
        {
            var purchased = shopData?.PurchasedItems.GetValueOrDefault(item.Id, 0) ?? 0;
            return new ShopItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                RewardType = item.RewardType,
                RewardId = item.RewardId,
                Quantity = item.Quantity,
                Price = item.Price,
                CurrencyType = item.CurrencyType,
                Stock = item.Stock,
                Purchased = purchased,
                PurchaseLimit = item.PurchaseLimit,
                RemainingStock = item.Stock > 0 ? item.Stock - purchased : -1,
                IsLimited = item.PurchaseLimit > 0 || item.Stock > 0,
                DisplayOrder = item.DisplayOrder
            };
        }

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private RewardDto MapToRewardDto(Reward r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
    }
}