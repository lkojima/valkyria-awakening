using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.iOS;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class NotificationManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private NotificationConfig _config;

        private NotificationSettings _settings;
        private readonly Dictionary<NotificationType, LocalNotification> _scheduledNotifications = new();

        public event System.Action<NotificationData> OnNotificationReceived;
        public event System.Action<NotificationSettings> OnSettingsChanged;

        public async UniTask InitializeAsync()
        {
            _settings = await LoadSettingsAsync();

            // Request permission
            await RequestPermissionAsync();

            // Register for remote notifications
            RegisterForRemoteNotifications();

            // Schedule default notifications
            await ScheduleDefaultNotificationsAsync();
        }

        public void Shutdown()
        {
            CancelAllNotifications();
        }

        public void OnGameStateChanged(GameState previous, GameState current) { }

        public async UniTask<bool> RequestPermissionAsync()
        {
#if UNITY_IOS
            var authStatus = await NotificationServices.RequestAuthorizationAsync(
                NotificationAuthorizationOption.Alert |
                NotificationAuthorizationOption.Badge |
                NotificationAuthorizationOption.Sound);
            return authStatus == AuthorizationStatus.Authorized;
#elif UNITY_ANDROID
            // Android 13+ requires runtime permission
            if (AndroidVersionCheck())
            {
                // Request permission via Android plugin
                return true;
            }
            return true;
#else
            return true;
#endif
        }

        public async UniTask ScheduleNotificationAsync(NotificationType type, TimeSpan delay, string title, string body, Dictionary<string, string> data = null)
        {
            if (!_settings.IsEnabled || !_settings.TypeSettings.TryGetValue(type, out var typeSetting) || !typeSetting.Enabled)
                return false;

            var fireTime = DateTime.Now.Add(delay);

            var notification = new LocalNotification
            {
                Type = type,
                Title = title,
                Body = body,
                FireTime = fireTime,
                Data = data ?? new Dictionary<string, string>(),
                RepeatInterval = typeSetting.RepeatInterval
            };

#if UNITY_IOS
            var iOSNotification = new iOSNotification
            {
                Title = title,
                Body = body,
                ShowInForeground = true,
                ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                CategoryIdentifier = type.ToString(),
                ThreadIdentifier = "game_notifications",
                Trigger = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = delay,
                    Repeats = typeSetting.RepeatInterval > TimeSpan.Zero
                }
            };

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    iOSNotification.Data[kvp.Key] = kvp.Value;
                }
            }

            NotificationServices.ScheduleNotification(iOSNotification);
#elif UNITY_ANDROID
            // Schedule via Android plugin
            ScheduleAndroidNotification(notification);
#endif

            _scheduledNotifications[type] = notification;
            return true;
        }

        public async UniTask CancelNotificationAsync(NotificationType type)
        {
#if UNITY_IOS
            NotificationServices.RemoveScheduledNotification(type.ToString());
#elif UNITY_ANDROID
            CancelAndroidNotification(type);
#endif

            _scheduledNotifications.Remove(type);
        }

        public async UniTask CancelAllNotificationsAsync()
        {
#if UNITY_IOS
            NotificationServices.RemoveAllScheduledNotifications();
#elif UNITY_ANDROID
            CancelAllAndroidNotifications();
#endif

            _scheduledNotifications.Clear();
        }

        public async UniTask<NotificationSettings> GetSettingsAsync()
        {
            return _settings;
        }

        public async UniTask UpdateSettingsAsync(NotificationSettings settings)
        {
            _settings = settings;
            await SaveSettingsAsync(settings);

            // Reschedule notifications based on new settings
            await RescheduleAllNotificationsAsync();

            OnSettingsChanged?.Invoke(settings);
        }

        public async UniTask HandleRemoteNotificationAsync(Dictionary<string, object> payload)
        {
            var notificationData = new NotificationData
            {
                Title = payload.GetValueOrDefault("title", "").ToString(),
                Body = payload.GetValueOrDefault("body", "").ToString(),
                Data = new Dictionary<string, string>(),
                ReceivedAt = DateTimeOffset.UtcNow
            };

            if (payload.TryGetValue("data", out var dataObj) && dataObj is Dictionary<string, object> data)
            {
                foreach (var kvp in data)
                {
                    notificationData.Data[kvp.Key] = kvp.Value.ToString();
                }
            }

            OnNotificationReceived?.Invoke(notificationData);

            // Handle deep link
            if (notificationData.Data.TryGetValue("deep_link", out var deepLink))
            {
                await HandleDeepLinkAsync(deepLink);
            }
        }

        private async UniTask ScheduleDefaultNotificationsAsync()
        {
            // Energy full notification
            if (_settings.TypeSettings.TryGetValue(NotificationType.EnergyFull, out var energySetting) && energySetting.Enabled)
            {
                // Would calculate based on energy regen rate
            }

            // Expedition complete
            if (_settings.TypeSettings.TryGetValue(NotificationType.ExpeditionComplete, out var expeditionSetting) && expeditionSetting.Enabled)
            {
                // Would check active expeditions
            }

            // Daily reset reminder
            if (_settings.TypeSettings.TryGetValue(NotificationType.DailyReset, out var dailySetting) && dailySetting.Enabled)
            {
                var nextReset = GetNextDailyReset();
                var delay = nextReset - DateTime.Now;
                if (delay > TimeSpan.Zero)
                {
                    await ScheduleNotificationAsync(NotificationType.DailyReset, delay,
                        "Daily Reset", "New daily quests and rewards are available!");
                }
            }
        }

        private async UniTask RescheduleAllNotificationsAsync()
        {
            await CancelAllNotificationsAsync();
            await ScheduleDefaultNotificationsAsync();
        }

        private DateTime GetNextDailyReset()
        {
            var now = DateTime.Now;
            var resetHour = _config.DailyResetHour;
            var resetTime = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0);

            if (resetTime <= now)
            {
                resetTime = resetTime.AddDays(1);
            }

            return resetTime;
        }

        private async UniTask HandleDeepLinkAsync(string deepLink)
        {
            // Parse deep link and navigate
            // e.g., "goddessrequiem://gacha/banner/123"
        }

        private async UniTask<NotificationSettings> LoadSettingsAsync()
        {
            var json = PlayerPrefs.GetString("notification_settings", "");
            if (!string.IsNullOrEmpty(json))
            {
                return JsonUtility.FromJson<NotificationSettings>(json);
            }

            return new NotificationSettings
            {
                IsEnabled = true,
                TypeSettings = new Dictionary<NotificationType, NotificationTypeSetting>
                {
                    { NotificationType.EnergyFull, new NotificationTypeSetting { Enabled = true } },
                    { NotificationType.ExpeditionComplete, new NotificationTypeSetting { Enabled = true } },
                    { NotificationType.QuestComplete, new NotificationTypeSetting { Enabled = true } },
                    { NotificationType.EventStart, new NotificationTypeSetting { Enabled = true } },
                    { NotificationType.EventEnd, new NotificationTypeSetting { Enabled = true } },
                    { NotificationType.DailyReset, new NotificationTypeSetting { Enabled = true, RepeatInterval = TimeSpan.FromDays(1) } },
                    { NotificationType.WeeklyReset, new NotificationTypeSetting { Enabled = true, RepeatInterval = TimeSpan.FromDays(7) } }
                }
            };
        }

        private async UniTask SaveSettingsAsync(NotificationSettings settings)
        {
            var json = JsonUtility.ToJson(settings);
            PlayerPrefs.SetString("notification_settings", json);
            PlayerPrefs.Save();
        }

        private void RegisterForRemoteNotifications()
        {
#if UNITY_IOS
            NotificationServices.RegisterForRemoteNotifications();
#endif
        }

        private bool AndroidVersionCheck()
        {
            // Check if Android 13+ (API 33+)
            return int.TryParse(SystemInfo.operatingSystem.Split(' ')[1].Split('.')[0], out int api) && api >= 33;
        }

        private void ScheduleAndroidNotification(LocalNotification notification)
        {
            // Would use Android plugin to schedule
        }

        private void CancelAndroidNotification(NotificationType type)
        {
            // Would use Android plugin to cancel
        }

        private void CancelAllAndroidNotifications()
        {
            // Would use Android plugin to cancel all
        }
    }

    [System.Serializable]
    public class NotificationConfig
    {
        public int DailyResetHour = 4;
        public bool EnableRemoteNotifications = true;
    }

    [System.Serializable]
    public class NotificationSettings
    {
        public bool IsEnabled = true;
        public Dictionary<NotificationType, NotificationTypeSetting> TypeSettings = new();
    }

    [System.Serializable]
    public class NotificationTypeSetting
    {
        public bool Enabled = true;
        public TimeSpan RepeatInterval = TimeSpan.Zero;
    }

    [System.Serializable]
    public class LocalNotification
    {
        public NotificationType Type;
        public string Title;
        public string Body;
        public DateTime FireTime;
        public Dictionary<string, string> Data;
        public TimeSpan RepeatInterval;
    }

    [System.Serializable]
    public class NotificationData
    {
        public string Title;
        public string Body;
        public Dictionary<string, string> Data;
        public DateTimeOffset ReceivedAt;
    }

    public enum NotificationType
    {
        EnergyFull = 1,
        ExpeditionComplete = 2,
        QuestComplete = 3,
        EventStart = 4,
        EventEnd = 5,
        FriendRequest = 6,
        GuildInvite = 7,
        GuildWar = 8,
        RankUp = 9,
        DailyReset = 10,
        WeeklyReset = 11,
        Maintenance = 12,
        Update = 13,
        RewardClaim = 14,
        SummonAvailable = 15,
        ShopRefresh = 16
    }
}