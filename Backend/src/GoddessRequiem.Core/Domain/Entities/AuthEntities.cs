using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class UserDevice : BaseEntity
    {
        public Guid UserId { get; private set; }
        public string DeviceId { get; private set; }
        public string DeviceModel { get; private set; }
        public string DeviceOs { get; private set; }
        public string AppVersion { get; private set; }
        public string PushToken { get; private set; }
        public bool IsPrimary { get; private set; }
        public DateTime LastUsedAt { get; private set; }
        public DateTime RegisteredAt { get; private set; }

        private UserDevice() { }

        public UserDevice(Guid userId, string deviceId, string deviceModel, string deviceOs, string appVersion)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            DeviceId = deviceId;
            DeviceModel = deviceModel;
            DeviceOs = deviceOs;
            AppVersion = appVersion;
            IsPrimary = false;
            LastUsedAt = DateTime.UtcNow;
            RegisteredAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateLastUsed(string appVersion = null)
        {
            LastUsedAt = DateTime.UtcNow;
            if (appVersion != null) AppVersion = appVersion;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPushToken(string token)
        {
            PushToken = token;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPrimary(bool isPrimary)
        {
            IsPrimary = isPrimary;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class UserSession : BaseEntity
    {
        public Guid UserId { get; private set; }
        public string SessionToken { get; private set; }
        public string RefreshToken { get; private set; }
        public string DeviceId { get; private set; }
        public string IpAddress { get; private set; }
        public string UserAgent { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime LastActivityAt { get; private set; }
        public DateTime? EndedAt { get; private set; }
        public string EndReason { get; private set; }

        private UserSession() { }

        public UserSession(Guid userId, string sessionToken, string refreshToken, string deviceId, string ipAddress, string userAgent)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            SessionToken = sessionToken;
            RefreshToken = refreshToken;
            DeviceId = deviceId;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            IsActive = true;
            StartedAt = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateActivity()
        {
            LastActivityAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void End(string reason)
        {
            IsActive = false;
            EndedAt = DateTime.UtcNow;
            EndReason = reason;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateRefreshToken(string newRefreshToken)
        {
            RefreshToken = newRefreshToken;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; private set; }
        public string Token { get; private set; }
        public string JwtId { get; private set; }
        public bool IsRevoked { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public string RevokedByIp { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        private RefreshToken() { }

        public RefreshToken(Guid userId, string token, string jwtId, DateTime expiresAt)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Token = token;
            JwtId = jwtId;
            IsRevoked = false;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Revoke(string revokedByIp)
        {
            IsRevoked = true;
            RevokedAt = DateTime.UtcNow;
            RevokedByIp = revokedByIp;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => !IsRevoked && !IsExpired;
    }

    public class Notification : BaseEntity
    {
        public Guid UserId { get; private set; }
        public NotificationType Type { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public Dictionary<string, string> Data { get; private set; } = new();
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }
        public bool IsSent { get; private set; }
        public DateTime? SentAt { get; private set; }
        public string ExternalId { get; private set; }

        private Notification() { }

        public Notification(Guid userId, NotificationType type, string title, string body, Dictionary<string, string> data = null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Type = type;
            Title = title;
            Body = body;
            Data = data ?? new Dictionary<string, string>();
            IsRead = false;
            IsSent = false;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkRead()
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkSent(string externalId = null)
        {
            IsSent = true;
            SentAt = DateTime.UtcNow;
            ExternalId = externalId;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class ScheduledNotification : BaseEntity
    {
        public Guid UserId { get; private set; }
        public NotificationType Type { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public Dictionary<string, string> Data { get; private set; } = new();
        public DateTime ScheduledAt { get; private set; }
        public bool IsProcessed { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public int RetryCount { get; private set; }
        public string LastError { get; private set; }

        private ScheduledNotification() { }

        public ScheduledNotification(Guid userId, NotificationType type, string title, string body, DateTime scheduledAt, Dictionary<string, string> data = null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Type = type;
            Title = title;
            Body = body;
            Data = data ?? new Dictionary<string, string>();
            ScheduledAt = scheduledAt;
            IsProcessed = false;
            RetryCount = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkProcessed()
        {
            IsProcessed = true;
            ProcessedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public void RecordError(string error)
        {
            RetryCount++;
            LastError = error;
            UpdatedAt = DateTime.UtcNow.
        }
    }

    public class NotificationSettings : BaseEntity
    {
        public Guid UserId { get; private set; }
        public bool EnablePush { get; private set; } = true;
        public bool EnableEmail { get; private set; } = false;
        public bool EnableInApp { get; private set; } = true;
        public Dictionary<NotificationType, bool> TypeEnabled { get; private set; } = new();
        public TimeSpan QuietHoursStart { get; private set; } = new TimeSpan(22, 0, 0);
        public TimeSpan QuietHoursEnd { get; private set; } = new TimeSpan(8, 0, 0);
        public string TimeZone { get; private set; } = "UTC";

        private NotificationSettings() { }

        public NotificationSettings(Guid userId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateTypeEnabled(NotificationType type, bool enabled)
        {
            TypeEnabled[type] = enabled;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetQuietHours(TimeSpan start, TimeSpan end)
        {
            QuietHoursStart = start;
            QuietHoursEnd = end;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetTimeZone(string timeZone)
        {
            TimeZone = timeZone;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsTypeEnabled(NotificationType type) => TypeEnabled.GetValueOrDefault(type, true);
    }
}