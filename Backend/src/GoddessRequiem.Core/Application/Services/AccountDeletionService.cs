using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoddessRequiem.Core.Application.Services
{
    public class AccountDeletionService : IAccountDeletionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AccountDeletionService> _logger;

        public AccountDeletionService(IUnitOfWork unitOfWork, ILogger<AccountDeletionService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> ProcessDeletionAsync(Guid userId)
        {
            var audit = new AccountDeletionAudit
            {
                UserId = userId,
                RequestedAt = DateTime.UtcNow,
                Status = AccountDeletionStatus.InProgress
            };

            try
            {
                _logger.LogInformation("Starting account deletion for user {UserId}", userId);

                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found for deletion", userId);
                    return false;
                }

                // 1. Revoke all refresh tokens
                var refreshTokens = await _unitOfWork.RefreshTokens.GetByUserIdAsync(userId);
                foreach (var token in refreshTokens)
                {
                    if (!token.IsRevoked)
                    {
                        token.Revoke("Account deletion");
                    }
                }
                audit.EntitiesDeleted["RefreshToken"] = refreshTokens.Count;

                // 2. Delete/Anonymize user characters
                var userCharacters = await _unitOfWork.UserCharacters.GetByUserIdAsync(userId);
                foreach (var uc in userCharacters)
                {
                    await _unitOfWork.UserCharacters.DeleteAsync(uc.Id);
                    audit.EntitiesDeleted["UserCharacter"] = audit.EntitiesDeleted.GetValueOrDefault("UserCharacter", 0) + 1;
                }

                // 3. Delete user equipment
                var userEquipment = await _unitOfWork.UserEquipments.GetByUserIdAsync(userId);
                foreach (var eq in userEquipment)
                {
                    await _unitOfWork.UserEquipments.DeleteAsync(eq.Id);
                    audit.EntitiesDeleted["UserEquipment"] = audit.EntitiesDeleted.GetValueOrDefault("UserEquipment", 0) + 1;
                }

                // 4. Delete user relics
                var userRelics = await _unitOfWork.UserRelics.GetByUserIdAsync(userId);
                foreach (var relic in userRelics)
                {
                    await _unitOfWork.UserRelics.DeleteAsync(relic.Id);
                    audit.EntitiesDeleted["UserRelic"] = audit.EntitiesDeleted.GetValueOrDefault("UserRelic", 0) + 1;
                }

                // 5. Delete user runes
                var userRunes = await _unitOfWork.UserRunes.GetByUserIdAsync(userId);
                foreach (var rune in userRunes)
                {
                    await _unitOfWork.UserRunes.DeleteAsync(rune.Id);
                    audit.EntitiesDeleted["UserRune"] = audit.EntitiesDeleted.GetValueOrDefault("UserRune", 0) + 1;
                }

                // 6. Delete user skins
                var userSkins = await _unitOfWork.UserSkins.GetByUserIdAsync(userId);
                foreach (var skin in userSkins)
                {
                    await _unitOfWork.UserSkins.DeleteAsync(skin.Id);
                    audit.EntitiesDeleted["UserSkin"] = audit.EntitiesDeleted.GetValueOrDefault("UserSkin", 0) + 1;
                }

                // 7. Delete gacha history
                var gachaHistory = await _unitOfWork.GachaHistories.GetByUserIdAsync(userId);
                foreach (var history in gachaHistory)
                {
                    await _unitOfWork.GachaHistories.DeleteAsync(history.Id);
                    audit.EntitiesDeleted["GachaHistory"] = audit.EntitiesDeleted.GetValueOrDefault("GachaHistory", 0) + 1;
                }

                // 8. Delete user gacha pity
                var userPity = await _unitOfWork.GachaPities.GetByUserIdAsync(userId);
                foreach (var pity in userPity)
                {
                    await _unitOfWork.GachaPities.DeleteAsync(pity.Id);
                    audit.EntitiesDeleted["UserGachaPity"] = audit.EntitiesDeleted.GetValueOrDefault("UserGachaPity", 0) + 1;
                }

                // 9. Delete battle records
                var battles = await _unitOfWork.Battles.GetByUserIdAsync(userId);
                foreach (var battle in battles)
                {
                    await _unitOfWork.Battles.DeleteAsync(battle.Id);
                    audit.EntitiesDeleted["Battle"] = audit.EntitiesDeleted.GetValueOrDefault("Battle", 0) + 1;
                }

                // 10. Delete campaign progress
                var campaignProgress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
                if (campaignProgress != null)
                {
                    await _unitOfWork.CampaignProgress.DeleteAsync(campaignProgress.Id);
                    audit.EntitiesDeleted["CampaignProgress"] = 1;
                }

                // 11. Delete tower progress
                var towerProgress = await _unitOfWork.TowerProgress.GetByUserIdAsync(userId);
                foreach (var tp in towerProgress)
                {
                    await _unitOfWork.TowerProgress.DeleteAsync(tp.Id);
                    audit.EntitiesDeleted["TowerProgress"] = audit.EntitiesDeleted.GetValueOrDefault("TowerProgress", 0) + 1;
                }

                // 12. Delete arena data
                var arenaInfo = await _unitOfWork.UserArena.GetByUserIdAsync(userId);
                if (arenaInfo != null)
                {
                    await _unitOfWork.UserArena.DeleteAsync(arenaInfo.Id);
                    audit.EntitiesDeleted["ArenaInfo"] = 1;
                }

                // 13. Delete world boss progress
                var wbProgress = await _unitOfWork.UserWorldBoss.GetByUserIdAsync(userId);
                foreach (var wp in wbProgress)
                {
                    await _unitOfWork.UserWorldBoss.DeleteAsync(wp.Id);
                    audit.EntitiesDeleted["WorldBossProgress"] = audit.EntitiesDeleted.GetValueOrDefault("WorldBossProgress", 0) + 1;
                }

                // 14. Delete dungeon progress
                var dungeonProgress = await _unitOfWork.UserDungeons.GetByUserIdAsync(userId);
                foreach (var dp in dungeonProgress)
                {
                    await _unitOfWork.UserDungeons.DeleteAsync(dp.Id);
                    audit.EntitiesDeleted["DungeonProgress"] = audit.EntitiesDeleted.GetValueOrDefault("DungeonProgress", 0) + 1;
                }

                // 15. Delete expedition data
                var userExpeditions = await _unitOfWork.UserExpeditions.GetByUserIdAsync(userId);
                foreach (var ue in userExpeditions)
                {
                    await _unitOfWork.UserExpeditions.DeleteAsync(ue.Id);
                    audit.EntitiesDeleted["UserExpedition"] = audit.EntitiesDeleted.GetValueOrDefault("UserExpedition", 0) + 1;
                }

                // 16. Delete quest data
                var userQuests = await _unitOfWork.UserQuests.GetByUserIdAsync(userId);
                foreach (var uq in userQuests)
                {
                    await _unitOfWork.UserQuests.DeleteAsync(uq.Id);
                    audit.EntitiesDeleted["UserQuest"] = audit.EntitiesDeleted.GetValueOrDefault("UserQuest", 0) + 1;
                }

                // 17. Delete event progress
                var eventProgress = await _unitOfWork.UserEvents.GetByUserIdAsync(userId);
                foreach (var ep in eventProgress)
                {
                    await _unitOfWork.UserEvents.DeleteAsync(ep.Id);
                    audit.EntitiesDeleted["EventProgress"] = audit.EntitiesDeleted.GetValueOrDefault("EventProgress", 0) + 1;
                }

                // 18. Delete shop purchases
                var shopPurchases = await _unitOfWork.PurchaseTransactions.GetByUserIdAsync(userId);
                foreach (var sp in shopPurchases)
                {
                    await _unitOfWork.PurchaseTransactions.DeleteAsync(sp.Id);
                    audit.EntitiesDeleted["ShopPurchase"] = audit.EntitiesDeleted.GetValueOrDefault("ShopPurchase", 0) + 1;
                }

                // 19. Delete idle rewards
                var idleRewards = await _unitOfWork.IdleRewards.GetByUserIdAsync(userId);
                if (idleRewards != null)
                {
                    await _unitOfWork.IdleRewards.DeleteAsync(idleRewards.Id);
                    audit.EntitiesDeleted["IdleReward"] = 1;
                }

                // 20. Delete user devices
                var userDevices = await _unitOfWork.UserDevices.GetByUserIdAsync(userId);
                foreach (var ud in userDevices)
                {
                    await _unitOfWork.UserDevices.DeleteAsync(ud.Id);
                    audit.EntitiesDeleted["UserDevice"] = audit.EntitiesDeleted.GetValueOrDefault("UserDevice", 0) + 1;
                }

                // 21. Delete user sessions
                var userSessions = await _unitOfWork.UserSessions.GetByUserIdAsync(userId);
                foreach (var us in userSessions)
                {
                    await _unitOfWork.UserSessions.DeleteAsync(us.Id);
                    audit.EntitiesDeleted["UserSession"] = audit.EntitiesDeleted.GetValueOrDefault("UserSession", 0) + 1;
                }

                // 22. Delete notifications
                var notifications = await _unitOfWork.Notifications.GetByUserIdAsync(userId);
                foreach (var n in notifications)
                {
                    await _unitOfWork.Notifications.DeleteAsync(n.Id);
                    audit.EntitiesDeleted["Notification"] = audit.EntitiesDeleted.GetValueOrDefault("Notification", 0) + 1;
                }

                // 23. Anonymize user record (LGPD compliance - keep minimal audit trail)
                user.Username = $"deleted_{userId.ToString()[..8]}";
                user.Email = $"deleted_{userId.ToString()[..8]}@goddessrequiem.local";
                user.DisplayName = "Deleted User";
                user.PasswordHash = "";
                user.Salt = "";
                user.DeviceId = "";
                user.AvatarId = "default";
                user.AvatarFrameId = "default";
                user.ChatBubbleId = "default";
                user.Settings.Clear();
                user.CustomData.Clear();
                user.CustomData["DeletionProcessedAt"] = DateTime.UtcNow;
                user.Status = AccountStatus.Deleted;

                await _unitOfWork.Users.UpdateAsync(user);
                audit.EntitiesDeleted["User"] = 1;

                // Save all changes
                await _unitOfWork.SaveChangesAsync();

                audit.ProcessedAt = DateTime.UtcNow;
                audit.Status = AccountDeletionStatus.Completed;
                audit.IsComplete = true;

                _logger.LogInformation("Account deletion completed for user {UserId}. Entities deleted: {Entities}",
                    userId, string.Join(", ", audit.EntitiesDeleted.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account deletion failed for user {UserId}", userId);
                audit.Status = AccountDeletionStatus.Failed;
                audit.Errors.Add(ex.Message);
                audit.ProcessedAt = DateTime.UtcNow;
                return false;
            }
        }

        public async Task<bool> CancelDeletionAsync(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null || user.Status != AccountStatus.Deleted)
                {
                    return false;
                }

                // Restore status to active (if within grace period)
                var requestedAt = user.CustomData.GetValueOrDefault("DeletionRequestedAt") as DateTime?;
                if (requestedAt.HasValue && (DateTime.UtcNow - requestedAt.Value).TotalDays <= 30)
                {
                    user.Status = AccountStatus.Active;
                    user.CustomData["DeletionCancelledAt"] = DateTime.UtcNow;
                    await _unitOfWork.Users.UpdateAsync(user);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Account deletion cancelled for user {UserId}", userId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel deletion for user {UserId}", userId);
                return false;
            }
        }

        public async Task<AccountDeletionAudit> GetAuditAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
            {
                return new AccountDeletionAudit { UserId = userId, Status = AccountDeletionStatus.Pending };
            }

            var requestedAt = user.CustomData.GetValueOrDefault("DeletionRequestedAt") as DateTime?;
            var processedAt = user.CustomData.GetValueOrDefault("DeletionProcessedAt") as DateTime?;
            var cancelledAt = user.CustomData.GetValueOrDefault("DeletionCancelledAt") as DateTime?;

            var status = user.Status == AccountStatus.Deleted
                ? (processedAt.HasValue ? AccountDeletionStatus.Completed : AccountDeletionStatus.InProgress)
                : (cancelledAt.HasValue ? AccountDeletionStatus.Cancelled : AccountDeletionStatus.Pending);

            return new AccountDeletionAudit
            {
                UserId = userId,
                RequestedAt = requestedAt ?? DateTime.MinValue,
                ProcessedAt = processedAt,
                CancelledAt = cancelledAt,
                Status = status,
                IsComplete = status == AccountDeletionStatus.Completed
            };
        }
    }
}