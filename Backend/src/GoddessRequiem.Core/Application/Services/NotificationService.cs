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
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<NotificationService> _logger;
        private readonly IPushNotificationProvider _pushProvider;

        public NotificationService(IUnitOfWork unitOfWork, ILogger<NotificationService> logger, IPushNotificationProvider pushProvider)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _pushProvider = pushProvider;
        }

        public async Task<bool> SendPushNotificationAsync(Guid userId, string title, string body, Dictionary<string, string> data)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            var settings = await GetSettingsAsync(userId);
            if (!settings.IsEnabled) return false;

            var deviceTokens = await _unitOfWork.UserDevices.GetPushTokensAsync(userId);
            if (!deviceTokens.Any()) return false;

            foreach (var token in deviceTokens)
            {
                try
                {
                    await _pushProvider.SendAsync(token, title, body, data);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send push notification to user {UserId}", userId);
                }
            }

            // Log notification
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                Type = NotificationType.Generic,
                Title = title,
                Body = body,
                Data = data,
                SentAt = DateTime.UtcNow,
                Status = NotificationStatus.Sent
            });

            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ScheduleNotificationAsync(Guid userId, NotificationType type, TimeSpan delay)
        {
            var settings = await GetSettingsAsync(userId);
            if (!settings.IsEnabled) return false;

            var typeSetting = settings.TypeSettings.GetValueOrDefault(type);
            if (typeSetting == null || !typeSetting.Enabled) return false;

            var fireTime = DateTime.UtcNow.Add(delay);

            var scheduled = new ScheduledNotification
            {
                UserId = userId,
                Type = type,
                FireAt = fireTime,
                RepeatInterval = typeSetting.RepeatInterval,
                Status = ScheduledNotificationStatus.Pending
            };

            await _unitOfWork.ScheduledNotifications.AddAsync(scheduled);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CancelNotificationAsync(Guid userId, NotificationType type)
        {
            var notifications = await _unitOfWork.ScheduledNotifications.GetByUserAndTypeAsync(userId, type);

            foreach (var n in notifications)
            {
                n.Status = ScheduledNotificationStatus.Cancelled;
                await _unitOfWork.ScheduledNotifications.UpdateAsync(n);
            }

            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<NotificationSettingsDto> GetSettingsAsync(Guid userId)
        {
            var settings = await _unitOfWork.NotificationSettings.GetByUserIdAsync(userId);
            if (settings == null)
            {
                settings = new NotificationSettings
                {
                    UserId = userId,
                    IsEnabled = true,
                    TypeSettings = GetDefaultTypeSettings()
                };
                await _unitOfWork.NotificationSettings.AddAsync(settings);
                await _unitOfWork.SaveChangesAsync();
            }

            return MapToDto(settings);
        }

        public async Task<bool> UpdateSettingsAsync(Guid userId, NotificationSettingsDto settings)
        {
            var existing = await _unitOfWork.NotificationSettings.GetByUserIdAsync(userId);
            if (existing == null)
            {
                existing = new NotificationSettings { UserId = userId };
                await _unitOfWork.NotificationSettings.AddAsync(existing);
            }

            existing.IsEnabled = settings.IsEnabled;
            existing.TypeSettings = settings.TypeSettings.ToDictionary(
                kvp => kvp.Key,
                kvp => new NotificationTypeSetting { Enabled = kvp.Value.Enabled, RepeatInterval = kvp.Value.RepeatInterval }
            );
            existing.QuietHoursStart = settings.QuietHoursStart;
            existing.QuietHoursEnd = settings.QuietHoursEnd;
            existing.TimeZone = settings.TimeZone;

            await _unitOfWork.NotificationSettings.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync();

            // Reschedule notifications based on new settings
            await RescheduleNotificationsAsync(userId);

            return true;
        }

        public async Task ProcessScheduledNotificationsAsync()
        {
            var now = DateTime.UtcNow;
            var dueNotifications = await _unitOfWork.ScheduledNotifications.GetDueAsync(now);

            foreach (var notification in dueNotifications)
            {
                try
                {
                    await SendScheduledNotificationAsync(notification);

                    if (notification.RepeatInterval > TimeSpan.Zero)
                    {
                        notification.FireAt = notification.FireAt.Add(notification.RepeatInterval);
                        notification.Status = ScheduledNotificationStatus.Pending;
                    }
                    else
                    {
                        notification.Status = ScheduledNotificationStatus.Sent;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send scheduled notification {NotificationId}", notification.Id);
                    notification.Status = ScheduledNotificationStatus.Failed;
                }

                await _unitOfWork.ScheduledNotifications.UpdateAsync(notification);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task SendScheduledNotificationAsync(ScheduledNotification notification)
        {
            var title = GetNotificationTitle(notification.Type);
            var body = GetNotificationBody(notification.Type);

            await SendPushNotificationAsync(notification.UserId, title, body, new Dictionary<string, string>
            {
                { "type", notification.Type.ToString() },
                { "scheduled", "true" }
            });
        }

        private async Task RescheduleNotificationsAsync(Guid userId)
        {
            // Cancel existing
            var existing = await _unitOfWork.ScheduledNotifications.GetPendingByUserAsync(userId);
            foreach (var n in existing)
            {
                n.Status = ScheduledNotificationStatus.Cancelled;
                await _unitOfWork.ScheduledNotifications.UpdateAsync(n);
            }

            // Schedule new based on settings
            var settings = await GetSettingsAsync(userId);

            // Energy full
            if (settings.TypeSettings.GetValueOrDefault(NotificationType.EnergyFull)?.Enabled == true)
            {
                // Would calculate based on current energy
            }

            // Daily reset
            if (settings.TypeSettings.GetValueOrDefault(NotificationType.DailyReset)?.Enabled == true)
            {
                var nextReset = GetNextDailyReset(settings.TimeZone);
                var delay = nextReset - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await ScheduleNotificationAsync(userId, NotificationType.DailyReset, delay);
                }
            }
        }

        private DateTime GetNextDailyReset(string timeZone)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var resetHour = 4; // 4 AM local time
            var resetTime = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0);

            if (resetTime <= now)
            {
                resetTime = resetTime.AddDays(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(resetTime, tz);
        }

        private string GetNotificationTitle(NotificationType type) => type switch
        {
            NotificationType.EnergyFull => "Energy Full!",
            NotificationType.ExpeditionComplete => "Expedition Complete",
            NotificationType.QuestComplete => "Quest Completed",
            NotificationType.EventStart => "New Event Started",
            NotificationType.EventEnd => "Event Ending Soon",
            NotificationType.DailyReset => "Daily Reset",
            NotificationType.WeeklyReset => "Weekly Reset",
            NotificationType.Maintenance => "Maintenance Notice",
            NotificationType.Update => "Game Update Available",
            NotificationType.RewardClaim => "Rewards Available",
            NotificationType.SummonAvailable => "Free Summon Available",
            NotificationType.ShopRefresh => "Shop Refreshed",
            _ => "Goddess Requiem"
        };

        private string GetNotificationBody(NotificationType type) => type switch
        {
            NotificationType.EnergyFull => "Your energy has fully recovered. Jump back into battle!",
            NotificationType.ExpeditionComplete => "Your expedition has returned with rewards!",
            NotificationType.QuestComplete => "A quest has been completed. Claim your rewards!",
            NotificationType.EventStart => "A new event has begun. Don't miss out!",
            NotificationType.EventEnd => "Event ending soon. Claim your rewards!",
            NotificationType.DailyReset => "New daily quests and rewards are available!",
            NotificationType.WeeklyReset => "Weekly rankings have reset. Climb to the top!",
            NotificationType.Maintenance => "Scheduled maintenance in progress.",
            NotificationType.Update => "A new version is available. Update now!",
            NotificationType.RewardClaim => "You have unclaimed rewards waiting!",
            NotificationType.SummonAvailable => "A free summon is waiting for you!",
            NotificationType.ShopRefresh => "The shop has new items!",
            _ => "Check out what's new in Goddess Requiem!"
        };

        private Dictionary<NotificationType, NotificationTypeSetting> GetDefaultTypeSettings() => new()
        {
            { NotificationType.EnergyFull, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.ExpeditionComplete, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.QuestComplete, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.EventStart, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.EventEnd, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.DailyReset, new NotificationTypeSetting { Enabled = true, RepeatInterval = TimeSpan.FromDays(1) } },
            { NotificationType.WeeklyReset, new NotificationTypeSetting { Enabled = true, RepeatInterval = TimeSpan.FromDays(7) } },
            { NotificationType.Maintenance, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.Update, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.RewardClaim, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.SummonAvailable, new NotificationTypeSetting { Enabled = true } },
            { NotificationType.ShopRefresh, new NotificationTypeSetting { Enabled = true } }
        };

        private NotificationSettingsDto MapToDto(NotificationSettings s) => new NotificationSettingsDto
        {
            IsEnabled = s.IsEnabled,
            TypeSettings = s.TypeSettings.ToDictionary(
                kvp => kvp.Key,
                kvp => new NotificationTypeSettingDto { Enabled = kvp.Value.Enabled, RepeatInterval = kvp.Value.RepeatInterval }
            ),
            QuietHoursStart = s.QuietHoursStart,
            QuietHoursEnd = s.QuietHoursEnd,
            TimeZone = s.TimeZone
        };
    }
}