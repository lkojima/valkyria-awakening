using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class UserConsent : BaseEntity
    {
        public Guid UserId { get; private set; }
        public ConsentType Type { get; private set; }
        public bool Accepted { get; private set; }
        public string Version { get; private set; }
        public string IpAddress { get; private set; }
        public DateTime AcceptedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public string RevokedByIp { get; private set; }

        private UserConsent() { }

        public UserConsent(Guid userId, ConsentType type, bool accepted, string version, string ipAddress)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Type = type;
            Accepted = accepted;
            Version = version;
            IpAddress = ipAddress;
            AcceptedAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Revoke(string ipAddress)
        {
            Accepted = false;
            RevokedAt = DateTime.UtcNow;
            RevokedByIp = ipAddress;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Update(bool accepted, string version, string ipAddress)
        {
            Accepted = accepted;
            Version = version;
            IpAddress = ipAddress;
            AcceptedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public class GameConfig : BaseEntity
    {
        public string Key { get; private set; }
        public string Value { get; private set; }
        public string Description { get; private set; }
        public bool IsPublic { get; private set; }

        private GameConfig() { }

        public GameConfig(string key, string value, string description, bool isPublic = false)
        {
            Id = Guid.NewGuid();
            Key = key;
            Value = value;
            Description = description;
            IsPublic = isPublic;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow.
        }

        public void UpdateValue(string value)
        {
            Value = value;
            UpdatedAt = DateTime.UtcNow.
        }
    }
}