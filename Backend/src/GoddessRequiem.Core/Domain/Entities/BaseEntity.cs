using System;

namespace GoddessRequiem.Core.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; protected set; }
        public DateTime CreatedAt { get; protected set; }
        public DateTime UpdatedAt { get; protected set; }
        public DateTime? DeletedAt { get; protected set; }
        public bool IsDeleted { get; protected set; }
        public int Version { get; protected set; } = 1;

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkAsDeleted()
        {
            DeletedAt = DateTime.UtcNow;
            IsDeleted = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void IncrementVersion()
        {
            Version++;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public abstract class BaseEntity<TId> : BaseEntity where TId : notnull
    {
        public new TId Id { get; protected set; }
    }
}