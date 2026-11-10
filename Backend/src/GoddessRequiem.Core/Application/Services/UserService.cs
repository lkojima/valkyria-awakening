using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GoddessRequiem.Core.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;

        private readonly LoginRewardConfig _loginRewardConfig;
        private readonly VipConfig _vipConfig;

        public UserService(
            IUnitOfWork unitOfWork,
            IAuthService authService,
            INotificationService notificationService,
            IAnalyticsService analyticsService,
            ILogger<UserService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _notificationService = notificationService;
            _analyticsService = analyticsService;
            _logger = logger;
            _configuration = configuration;

            _loginRewardConfig = _configuration.GetSection("LoginRewards").Get<LoginRewardConfig>() ?? new LoginRewardConfig();
            _vipConfig = _configuration.GetSection("Vip").Get<VipConfig>() ?? new VipConfig();
        }

        public async Task<UserProfileDto> GetProfileAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            var vipInfo = await GetVipInfoAsync(userId);

            return new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Status = user.Status,
                Language = user.Language,
                VipLevel = user.VipLevel,
                SubscriptionTier = user.SubscriptionTier,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                LoginStreak = user.LoginStreak,
                TotalPlayTimeSeconds = user.TotalPlayTimeSeconds,
                Settings = user.Settings,
                VipInfo = vipInfo
            };
        }

        public async Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 20)
                return false;

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            user.ChangeDisplayName(displayName);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateLanguageAsync(Guid userId, Language language)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            user.ChangeLanguage(language);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateSettingsAsync(Guid userId, Dictionary<string, object> settings)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            foreach (var kvp in settings)
            {
                user.UpdateSetting(kvp.Key, kvp.Value);
            }

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<LoginRewardResult> ClaimDailyLoginAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return new LoginRewardResult { Success = false, Error = "User not found" };

            var now = DateTime.UtcNow;
            var today = now.Date;

            // Check if already claimed today
            if (user.LastDailyClaimAt.HasValue && user.LastDailyClaimAt.Value.Date == today)
            {
                return new LoginRewardResult { Success = false, Error = "Already claimed today" };
            }

            // Calculate streak
            int streakDay = 1;
            if (user.LastDailyClaimAt.HasValue)
            {
                var daysDiff = (today - user.LastDailyClaimAt.Value.Date).Days;
                if (daysDiff == 1)
                {
                    streakDay = user.LoginStreak + 1;
                }
                else if (daysDiff > 1)
                {
                    streakDay = 1;
                }
                else
                {
                    return new LoginRewardResult { Success = false, Error = "Already claimed today" };
                }
            }

            // Get rewards for streak day
            var rewards = GetLoginRewards(streakDay);
            var isBonusDay = _loginRewardConfig.BonusDays.Contains(streakDay % 30);

            if (isBonusDay)
            {
                rewards.AddRange(_loginRewardConfig.BonusRewards);
            }

            // Grant rewards
            foreach (var reward in rewards)
            {
                await GrantRewardAsync(userId, reward);
            }

            // Update user
            user.UpdateLogin(now);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Track analytics
            await _analyticsService.TrackEventAsync("daily_login_claimed", new Dictionary<string, object>
            {
                { "streak_day", streakDay },
                { "is_bonus_day", isBonusDay },
                { "rewards_count", rewards.Count }
            });

            // Schedule next notification
            await _notificationService.ScheduleNotificationAsync(userId, NotificationType.DailyReset, TimeSpan.FromHours(24), "Daily Login", "Claim your daily reward!");

            return new LoginRewardResult
            {
                Success = true,
                Rewards = rewards,
                StreakDay = streakDay,
                IsBonusDay = isBonusDay
            };
        }

        public async Task<VipInfoDto> GetVipInfoAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            var currentConfig = _vipConfig.Levels.FirstOrDefault(l => l.Level == user.VipLevel) ?? new VipLevelConfig();
            var nextConfig = _vipConfig.Levels.FirstOrDefault(l => l.Level == user.VipLevel + 1);

            var benefits = new List<VipBenefitDto>();
            foreach (var benefit in currentConfig.Benefits)
            {
                benefits.Add(new VipBenefitDto
                {
                    Type = benefit.Type,
                    Value = benefit.Value,
                    Description = benefit.Description,
                    IsUnlocked = true
                });
            }

            // Add next level benefits as locked
            if (nextConfig != null)
            {
                foreach (var benefit in nextConfig.Benefits)
                {
                    benefits.Add(new VipBenefitDto
                    {
                        Type = benefit.Type,
                        Value = benefit.Value,
                        Description = benefit.Description,
                        IsUnlocked = false
                    });
                }
            }

            return new VipInfoDto
            {
                CurrentLevel = user.VipLevel,
                CurrentExp = user.VipExperience,
                ExpToNextLevel = nextConfig?.RequiredExperience ?? 0,
                Benefits = benefits,
                NextLevels = _vipConfig.Levels.Where(l => l.Level > user.VipLevel).Take(5).ToList()
            };
        }

        public async Task<bool> DeleteAccountAsync(Guid userId, string password)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            // Verify password (would use auth service)
            var isValid = await _authService.VerifyPasswordAsync(userId, password);
            if (!isValid) return false;

            // Mark for deletion (GDPR compliance - 30 day grace period)
            user.RequestDeletion();
            user.CustomData["DeletionGracePeriodDays"] = 30;
            user.CustomData["DeletionReason"] = "User requested";

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Schedule actual deletion
            await ScheduleAccountDeletionAsync(userId);

            await _analyticsService.TrackEventAsync("account_deletion_requested", new Dictionary<string, object>
            {
                { "user_id", userId.ToString() }
            });

            return true;
        }

        public async Task<AccountDeletionStatus> GetDeletionStatusAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (user.Status != AccountStatus.Deleted)
            {
                return new AccountDeletionStatus { IsScheduled = false };
            }

            var scheduledAt = user.CustomData.GetValueOrDefault("DeletionRequestedAt") as DateTime?;
            var gracePeriod = user.CustomData.GetValueOrDefault("DeletionGracePeriodDays") as int? ?? 30;

            return new AccountDeletionStatus
            {
                IsScheduled = true,
                ScheduledAt = scheduledAt,
                ExpiresAt = scheduledAt?.AddDays(gracePeriod),
                CanCancel = true
            };
        }

        private List<Reward> GetLoginRewards(int day)
        {
            var rewards = new List<Reward>();

            // Base daily reward
            rewards.Add(new Reward
            {
                Type = RewardType.Currency,
                ResourceId = Guid.Parse("11111111-1111-1111-1111-111111111111"), // Gold
                Quantity = _loginRewardConfig.BaseGold + (day * _loginRewardConfig.GoldPerDay),
                IsGuaranteed = true
            });

            // Day-specific rewards
            if (_loginRewardConfig.DayRewards.TryGetValue(day, out var dayRewards))
            {
                rewards.AddRange(dayRewards);
            }

            // Weekly bonus (every 7 days)
            if (day % 7 == 0)
            {
                rewards.AddRange(_loginRewardConfig.WeeklyRewards);
            }

            // Monthly bonus (every 30 days)
            if (day % 30 == 0)
            {
                rewards.AddRange(_loginRewardConfig.MonthlyRewards);
            }

            return rewards;
        }

        private async Task GrantRewardAsync(Guid userId, Reward reward)
        {
            switch (reward.Type)
            {
                case RewardType.Currency:
                    await AddCurrencyAsync(userId, reward.ResourceId, reward.Quantity);
                    break;
                case RewardType.Item:
                    await AddItemAsync(userId, reward.ResourceId, reward.Quantity);
                    break;
                case RewardType.Character:
                    await _unitOfWork.UserCharacters.AddAsync(new UserCharacter(userId, reward.ResourceId, DateTime.UtcNow));
                    break;
                case RewardType.Experience:
                    await AddExperienceAsync(userId, reward.Quantity);
                    break;
            }
        }

        private async Task AddCurrencyAsync(Guid userId, Guid currencyId, int amount)
        {
            // Implementation would update user currency balances
            await Task.CompletedTask;
        }

        private async Task AddItemAsync(Guid userId, Guid itemId, int quantity)
        {
            // Implementation would add to user inventory
            await Task.CompletedTask;
        }

        private async Task AddExperienceAsync(Guid userId, int amount)
        {
            // Implementation would add to player experience
            await Task.CompletedTask;
        }

        private async Task ScheduleAccountDeletionAsync(Guid userId)
        {
            // Would schedule a background job for actual deletion after grace period
            await Task.CompletedTask;
        }

        public async Task<bool> RecordConsentAsync(Guid userId, ConsentType type, bool accepted, string version)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            var existing = await _unitOfWork.UserConsents.GetByUserAndTypeAsync(userId, type);
            var ipAddress = "unknown"; // Would get from context

            if (existing != null)
            {
                existing.Update(accepted, version, ipAddress);
                await _unitOfWork.UserConsents.UpdateAsync(existing);
            }
            else
            {
                var consent = new UserConsent(userId, type, accepted, version, ipAddress);
                await _unitOfWork.UserConsents.AddAsync(consent);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<ConsentRecordDto> GetConsentStatusAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            var consents = await _unitOfWork.UserConsents.GetByUserIdAsync(userId);
            var consentDict = consents.ToDictionary(c => c.Type, c => new ConsentEntry
            {
                Accepted = c.Accepted,
                AcceptedAt = c.AcceptedAt,
                Version = c.Version,
                IpAddress = c.IpAddress
            });

            var isAgeVerified = user.CustomData.GetValueOrDefault("AgeVerified") as bool? ?? false;
            var ageVerifiedAt = user.CustomData.GetValueOrDefault("AgeVerifiedAt") as DateTime?;
            var privacyVersion = user.CustomData.GetValueOrDefault("PrivacyPolicyVersion") as string;
            var termsVersion = user.CustomData.GetValueOrDefault("TermsVersion") as string;

            return new ConsentRecordDto
            {
                UserId = userId,
                Consents = consentDict,
                IsAgeVerified = isAgeVerified,
                AgeVerifiedAt = ageVerifiedAt,
                PrivacyPolicyVersion = privacyVersion,
                TermsVersion = termsVersion
            };
        }

        public async Task<bool> VerifyAgeAsync(Guid userId, bool isAdult)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            user.CustomData["AgeVerified"] = isAdult;
            user.CustomData["AgeVerifiedAt"] = DateTime.UtcNow;

            if (isAdult)
            {
                // Add age_verified claim would be handled by auth service
                await _authService.RevokeAllTokensAsync(userId); // Force re-login with new claims
            }

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await _analyticsService.TrackEventAsync("age_verification", new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "is_adult", isAdult }
            });

            return true;
        }
    }

    public class LoginRewardConfig
    {
        public int BaseGold { get; set; } = 1000;
        public int GoldPerDay { get; set; } = 50;
        public List<int> BonusDays { get; set; } = new() { 7, 14, 21, 30 };
        public Dictionary<int, List<Reward>> DayRewards { get; set; } = new();
        public List<Reward> WeeklyRewards { get; set; } = new();
        public List<Reward> MonthlyRewards { get; set; } = new();
        public List<Reward> BonusRewards { get; set; } = new();
    }

    public class VipConfig
    {
        public List<VipLevelConfig> Levels { get; set; } = new();
    }
}