using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Shop : BaseEntity
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public ShopType Type { get; private set; }
        public List<ShopItem> Items { get; private set; } = new()
        public DateTimeRange? AvailablePeriod { get; private set; }
        public int RefreshIntervalHours { get; private set; } = 6
        public CurrencyAmount RefreshCost { get; private set; }
        public int MaxRefreshesPerDay { get; private set; } = 5
        public List<int> RequiredVipLevels { get; private set; } = new()
        public bool IsActive { get; private set; }

        private Shop() { }

        public Shop(string name, string description, ShopType type)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Type = type;
            IsActive = true;
            RefreshCost = new CurrencyAmount(CurrencyType.Gem, 50);
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddItem(ShopItem item)
        {
            Items.Add(item);
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSchedule(DateTimeRange? period, int refreshHours = 6, CurrencyAmount refreshCost = null, int maxRefreshes = 5)
        {
            AvailablePeriod = period;
            RefreshIntervalHours = refreshHours;
            if (refreshCost != null) RefreshCost = refreshCost;
            MaxRefreshesPerDay = maxRefreshes;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetVipRequirements(List<int> vipLevels)
        {
            RequiredVipLevels = vipLevels;
            UpdatedAt = DateTime.UtcNow;
        }

        public List<ShopItem> GetAvailableItems(int playerVipLevel, DateTime currentTime)
        {
            if (!IsActive) return new();
            if (AvailablePeriod.HasValue && !AvailablePeriod.Value.Contains(currentTime)) return new();
            if (RequiredVipLevels.Any() && !RequiredVipLevels.Contains(playerVipLevel)) return new();

            return Items.Where(i => i.IsAvailable(currentTime)).ToList();
        }
    }

    public class ShopItem
    {
        public Guid Id { get; set; } = Guid.NewGuid()
        public string Name { get; set; }
        public string Description { get; set; }
        public RewardType RewardType { get; set; }
        public Guid ResourceId { get; set; }
        public int Quantity { get; set; }
        public CurrencyAmount Price { get; set; }
        public int Stock { get; set; } = -1 // -1 = unlimited
        public int MaxPurchasesPerUser { get; set; } = -1
        public int MaxPurchasesPerRefresh { get; set; } = -1
        public DateTimeRange? AvailablePeriod { get; set; }
        public List<int> RequiredVipLevels { get; set; } = new()
        public int SortOrder { get; set; }
        public bool IsFeatured { get; set; }
        public string Tag { get; set; } // "New", "Hot", "Limited", "Discount"
        public float DiscountPercent { get; set; } = 0
        public DateTimeRange? DiscountPeriod { get; set; }

        public CurrencyAmount GetCurrentPrice(DateTime currentTime)
        {
            if (DiscountPercent > 0 && DiscountPeriod.HasValue && DiscountPeriod.Value.Contains(currentTime))
            {
                var discounted = Price.Amount * (1 - DiscountPercent / 100f);
                return new CurrencyAmount(Price.Type, (long)Math.Ceiling(discounted));
            }
            return Price;
        }

        public bool IsAvailable(DateTime currentTime)
        {
            if (AvailablePeriod.HasValue && !AvailablePeriod.Value.Contains(currentTime))
                return false;

            if (Stock >= 0 && Stock <= 0)
                return false;

            return true;
        }

        public bool CanPurchase(int vipLevel, int userPurchases, int userPurchasesThisRefresh)
        {
            if (RequiredVipLevels.Any() && !RequiredVipLevels.Contains(vipLevel))
                return false;
            if (MaxPurchasesPerUser >= 0 && userPurchases >= MaxPurchasesPerUser)
                return false;
            if (MaxPurchasesPerRefresh >= 0 && userPurchasesThisRefresh >= MaxPurchasesPerRefresh)
                return false;
            return true;
        }
    }

    public class UserShopData : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid ShopId { get; private set; }
        public Dictionary<Guid, int> PurchaseCounts { get; private set; } = new()
        public Dictionary<Guid, int> RefreshPurchaseCounts { get; private set; } = new()
        public int RefreshesUsedToday { get; private set; }
        public DateTime LastRefreshAt { get; private set; }
        public DateTime LastResetAt { get; private set; }

        private UserShopData() { }

        public UserShopData(Guid userId, Guid shopId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            ShopId = shopId;
            LastResetAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RecordPurchase(Guid itemId, int quantity = 1)
        {
            CheckDailyReset();

            if (!PurchaseCounts.ContainsKey(itemId))
                PurchaseCounts[itemId] = 0;
            PurchaseCounts[itemId] += quantity;

            if (!RefreshPurchaseCounts.ContainsKey(itemId))
                RefreshPurchaseCounts[itemId] = 0;
            RefreshPurchaseCounts[itemId] += quantity;

            UpdatedAt = DateTime.UtcNow;
        }

        public void Refresh()
        {
            CheckDailyReset();
            RefreshesUsedToday++;
            LastRefreshAt = DateTime.UtcNow;
            RefreshPurchaseCounts.Clear();
            UpdatedAt = DateTime.UtcNow.
        }

        public int GetPurchaseCount(Guid itemId) => PurchaseCounts.GetValueOrDefault(itemId, 0)
        public int GetRefreshPurchaseCount(Guid itemId) => RefreshPurchaseCounts.GetValueOrDefault(itemId, 0)

        public bool CanRefresh(int maxRefreshes, CurrencyAmount cost, Func<CurrencyAmount, bool> trySpend)
        {
            CheckDailyReset();
            if (RefreshesUsedToday >= maxRefreshes) return false;
            return trySpend(cost);
        }

        private void CheckDailyReset()
        {
            if (LastResetAt.Date < DateTime.UtcNow.Date)
            {
                RefreshesUsedToday = 0;
                LastResetAt = DateTime.UtcNow;
                UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    public class PurchaseTransaction : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid ShopId { get; private set; }
        public Guid ItemId { get; private set; }
        public int Quantity { get; private set; }
        public CurrencyAmount PricePaid { get; private set; }
        public CurrencyAmount OriginalPrice { get; private set; }
        public PurchaseStatus Status { get; private set; }
        public string TransactionId { get; private set; } // External (Google Play) transaction ID
        public string ReceiptData { get; private set; }
        public DateTime PurchasedAt { get; private set; }
        public DateTime? VerifiedAt { get; private set; }
        public string FailureReason { get; private set; }

        private PurchaseTransaction() { }

        public PurchaseTransaction(
            Guid userId,
            Guid shopId,
            Guid itemId,
            int quantity,
            CurrencyAmount pricePaid,
            CurrencyAmount originalPrice,
            string transactionId,
            string receiptData)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            ShopId = shopId;
            ItemId = itemId;
            Quantity = quantity;
            PricePaid = pricePaid;
            OriginalPrice = originalPrice;
            Status = PurchaseStatus.Pending;
            TransactionId = transactionId;
            ReceiptData = receiptData;
            PurchasedAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkVerified()
        {
            Status = PurchaseStatus.Verified;
            VerifiedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public void MarkCompleted()
        {
            Status = PurchaseStatus.Completed;
            UpdatedAt = DateTime.UtcNow.
        }

        public void MarkFailed(string reason)
        {
            Status = PurchaseStatus.Failed;
            FailureReason = reason;
            UpdatedAt = DateTime.UtcNow.
        }

        public void MarkRefunded()
        {
            Status = PurchaseStatus.Refunded;
            UpdatedAt = DateTime.UtcNow.
        }
    }
}