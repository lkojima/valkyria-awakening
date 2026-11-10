using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; private set; }
        public string Email { get; private set; }
        public string DisplayName { get; private set; }
        public string PasswordHash { get; private set; }
        public string Salt { get; private set; }
        public AccountStatus Status { get; private set; }
        public Platform Platform { get; private set; }
        public string DeviceId { get; private set; }
        public Language Language { get; private set; }
        public DateTime? LastLoginAt { get; private set; }
        public DateTime? LastLogoutAt { get; private set; }
        public DateTime? EmailVerifiedAt { get; private set; }
        public bool IsEmailVerified { get; private set; }
        public int LoginStreak { get; private set; }
        public DateTime? LastDailyClaimAt { get; private set; }
        public int VipLevel { get; private set; }
        public long VipExperience { get; private set; }
        public SubscriptionTier SubscriptionTier { get; private set; }
        public DateTime? SubscriptionExpiresAt { get; private set; }
        public string AvatarId { get; private set; }
        public string AvatarFrameId { get; private set; }
        public string ChatBubbleId { get; private set; }
        public long TotalPlayTimeSeconds { get; private set; }
        public int TotalLogins { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public Dictionary<string, object> Settings { get; private set; } = new();
        public Dictionary<string, object> CustomData { get; private set; } = new();

        private User() { }

        public User(
            string username,
            string email,
            string displayName,
            string passwordHash,
            string salt,
            Platform platform,
            string deviceId,
            Language language = Language.Portuguese)
        {
            Id = Guid.NewGuid();
            Username = username.ToLowerInvariant();
            Email = email.ToLowerInvariant();
            DisplayName = displayName;
            PasswordHash = passwordHash;
            Salt = salt;
            Status = AccountStatus.Active;
            Platform = platform;
            DeviceId = deviceId;
            Language = language;
            IsEmailVerified = false;
            LoginStreak = 0;
            VipLevel = 0;
            VipExperience = 0;
            SubscriptionTier = SubscriptionTier.None;
            AvatarId = "default";
            AvatarFrameId = "default";
            ChatBubbleId = "default";
            TotalPlayTimeSeconds = 0;
            TotalLogins = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateLogin(DateTime loginTime)
        {
            LastLoginAt = loginTime;
            TotalLogins++;

            // Check streak
            if (LastDailyClaimAt.HasValue)
            {
                var daysDiff = (loginTime.Date - LastDailyClaimAt.Value.Date).Days;
                if (daysDiff == 1)
                {
                    LoginStreak++;
                }
                else if (daysDiff > 1)
                {
                    LoginStreak = 1;
                }
            }
            else
            {
                LoginStreak = 1;
            }

            LastDailyClaimAt = loginTime;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateLogout(DateTime logoutTime)
        {
            LastLogoutAt = logoutTime;
            if (LastLoginAt.HasValue)
            {
                TotalPlayTimeSeconds += (long)(logoutTime - LastLoginAt.Value).TotalSeconds;
            }
            UpdatedAt = DateTime.UtcNow;
        }

        public void VerifyEmail()
        {
            IsEmailVerified = true;
            EmailVerifiedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ChangeDisplayName(string newName)
        {
            DisplayName = newName;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ChangeLanguage(Language language)
        {
            Language = language;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddVipExperience(long amount, List<VipLevelConfig> vipConfigs)
        {
            VipExperience += amount;

            var newLevel = vipConfigs
                .Where(c => VipExperience >= c.RequiredExperience)
                .Max(c => (int?)c.Level) ?? 0;

            if (newLevel > VipLevel)
            {
                VipLevel = newLevel;
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSubscription(SubscriptionTier tier, DateTime expiresAt)
        {
            SubscriptionTier = tier;
            SubscriptionExpiresAt = expiresAt;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAvatar(string avatarId, string frameId = null, string chatBubbleId = null)
        {
            AvatarId = avatarId;
            if (frameId != null) AvatarFrameId = frameId;
            if (chatBubbleId != null) ChatBubbleId = chatBubbleId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateSetting(string key, object value)
        {
            Settings[key] = value;
            UpdatedAt = DateTime.UtcNow.
        }

        public T GetSetting<T>(string key, T defaultValue = default)
        {
            if (Settings.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }

        public bool IsSubscriptionActive => SubscriptionTier != SubscriptionTier.None
            && (!SubscriptionExpiresAt.HasValue || SubscriptionExpiresAt > DateTime.UtcNow);

        public void Suspend(string reason)
        {
            Status = AccountStatus.Suspended;
            CustomData["SuspensionReason"] = reason;
            CustomData["SuspendedAt"] = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Ban(string reason)
        {
            Status = AccountStatus.Banned;
            CustomData["BanReason"] = reason;
            CustomData["BannedAt"] = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public void RequestDeletion()
        {
            Status = AccountStatus.Deleted;
            CustomData["DeletionRequestedAt"] = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }
    }

    public class VipLevelConfig
    {
        public int Level { get; set; }
        public long RequiredExperience { get; set; }
        public List<VipBenefit> Benefits { get; set; } = new();
    }

    public class VipBenefit
    {
        public VipBenefitType Type { get; set; }
        public float Value { get; set; }
        public string Description { get; set; }
    }

    public enum VipBenefitType
    {
        StaminaCapIncrease = 1,
        GoldBonus = 2,
        ExperienceBonus = 3,
        DropRateBonus = 4,
        AutoBattleSlots = 5,
        FreeSummonsDaily = 6,
        ShopRefreshes = 7,
        InventorySlots = 8,
        CharacterSlots = 9,
        EquipmentSlots = 10,
        SkipTickets = 11,
        ExclusiveCosmetics = 12,
        PrioritySupport = 13
    }
}