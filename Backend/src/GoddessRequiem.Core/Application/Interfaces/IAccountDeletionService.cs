using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoddessRequiem.Core.Application.Interfaces
{
    public interface IAccountDeletionService
    {
        Task<bool> ProcessDeletionAsync(Guid userId);
        Task<bool> CancelDeletionAsync(Guid userId);
        Task<AccountDeletionAudit> GetAuditAsync(Guid userId);
    }

    public class AccountDeletionAudit
    {
        public Guid UserId { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public AccountDeletionStatus Status { get; set; }
        public Dictionary<string, int> EntitiesDeleted { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool IsComplete { get; set; }
    }

    public enum AccountDeletionStatus
    {
        Pending = 1,
        InProgress = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5
    }
}