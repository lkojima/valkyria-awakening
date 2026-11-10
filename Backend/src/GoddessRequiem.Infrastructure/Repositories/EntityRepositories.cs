using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Infrastructure.Data;

namespace GoddessRequiem.Infrastructure.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<User> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        }

        public async Task<User> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.DeviceId == deviceId, cancellationToken);
        }

        public async Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AnyAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AnyAsync(u => u.Username == username, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetByStatusAsync(AccountStatus status, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(u => u.Status == status).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetInactiveUsersAsync(DateTime since, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(u => u.LastLoginAt < since).ToListAsync(cancellationToken);
        }
    }

    public class UserCharacterRepository : BaseRepository<UserCharacter>, IUserCharacterRepository
    {
        public UserCharacterRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<UserCharacter> GetByUserAndCharacterAsync(Guid userId, Guid characterId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CharacterId == characterId, cancellationToken);
        }

        public async Task<IReadOnlyList<UserCharacter>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(uc => uc.UserId == userId).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserCharacter>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(uc => uc.UserId == userId && uc.IsFavorite).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserCharacter>> GetByRarityAsync(Guid userId, Rarity rarity, CancellationToken cancellationToken = default)
        {
            // Would need join with Character table
            var characterIds = await _context.Characters.Where(c => c.Rarity == rarity).Select(c => c.Id).ToListAsync(cancellationToken);
            return await _dbSet.Where(uc => uc.UserId == userId && characterIds.Contains(uc.CharacterId)).ToListAsync(cancellationToken);
        }

        public async Task<int> GetCharacterCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.CountAsync(uc => uc.UserId == userId, cancellationToken);
        }

        public async Task<bool> ExistsAsync(Guid userId, Guid characterId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AnyAsync(uc => uc.UserId == userId && uc.CharacterId == characterId, cancellationToken);
        }

        public async Task<UserCharacter> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(uc => uc.Id == id, cancellationToken);
        }
    }

    public class CharacterRepository : BaseRepository<Character>, ICharacterRepository
    {
        public CharacterRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<Character>> GetByRarityAsync(Rarity rarity, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(c => c.Rarity == rarity).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Character>> GetByElementAsync(Element element, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(c => c.Element == element).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Character>> GetObtainableAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(c => c.IsObtainable).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Character>> GetByClassAsync(CharacterClass classType, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(c => c.Class == classType).ToListAsync(cancellationToken);
        }
    }

    public class GachaBannerRepository : BaseRepository<GachaBanner>, IGachaBannerRepository
    {
        public GachaBannerRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<GachaBanner>> GetActiveAsync(DateTime now, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(b => b.StartAt <= now && b.EndAt >= now && b.IsActive).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<GachaBanner>> GetByTypeAsync(GachaBannerType type, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(b => b.Type == type).ToListAsync(cancellationToken);
        }

        public async Task<GachaBanner> GetNewPlayerBannerAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(b => b.IsNewPlayerBanner && b.IsActive, cancellationToken);
        }
    }

    public class GachaPityRepository : BaseRepository<GachaPity>, IGachaPityRepository
    {
        public GachaPityRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<GachaPity> GetByUserAndBannerAsync(Guid userId, Guid bannerId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(p => p.UserId == userId && p.BannerId == bannerId, cancellationToken);
        }

        public async Task<GachaPity> GetOrCreateAsync(Guid userId, Guid bannerId, CancellationToken cancellationToken = default)
        {
            var pity = await GetByUserAndBannerAsync(userId, bannerId, cancellationToken);
            if (pity == null)
            {
                pity = new GachaPity { UserId = userId, BannerId = bannerId };
                await AddAsync(pity, cancellationToken);
            }
            return pity;
        }
    }

    public class GachaHistoryRepository : BaseRepository<GachaHistory>, IGachaHistoryRepository
    {
        public GachaHistoryRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<GachaHistory>> GetByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
    }

    public class BattleRepository : BaseRepository<Battle>, IBattleRepository
    {
        public BattleRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<Battle>> GetByUserIdAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(b => b.UserId == userId)
                .OrderByDescending(b => b.StartedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Battle>> GetRecentByUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(b => b.UserId == userId)
                .OrderByDescending(b => b.StartedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<Battle> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        }
    }

    public class CampaignProgressRepository : BaseRepository<CampaignProgress>, ICampaignProgressRepository
    {
        public CampaignProgressRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<CampaignProgress> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);
        }

        public async Task<IReadOnlyList<CampaignStageClear>> GetClearedStagesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var progress = await GetByUserIdAsync(userId, cancellationToken);
            return progress?.ClearedStages ?? new List<CampaignStageClear>();
        }

        public async Task<CampaignStageClear> GetStageClearDataAsync(Guid userId, int stageId, CancellationToken cancellationToken = default)
        {
            var progress = await GetByUserIdAsync(userId, cancellationToken);
            return progress?.ClearedStages.FirstOrDefault(c => c.StageId == stageId);
        }

        public async Task AddStageClearAsync(CampaignStageClear clearData, CancellationToken cancellationToken = default)
        {
            var progress = await GetByUserIdAsync(userId, cancellationToken);
            if (progress != null)
            {
                progress.ClearedStages.Add(clearData);
                Update(progress);
            }
        }

        public async Task UpdateStageClearAsync(CampaignStageClear clearData, CancellationToken cancellationToken = default)
        {
            // Stage clear data is part of progress JSON, update parent
            var progress = await GetByUserIdAsync(clearData.UserId, cancellationToken);
            if (progress != null)
            {
                Update(progress);
            }
        }

        public async Task<IReadOnlyList<CampaignStageClear>> GetStarredStagesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var progress = await GetByUserIdAsync(userId, cancellationToken);
            return progress?.ClearedStages.Where(c => c.Stars > 0).ToList() ?? new List<CampaignStageClear>();
        }

        public async Task<IReadOnlyList<int>> GetClaimedChapterRewardsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var progress = await GetByUserIdAsync(userId, cancellationToken);
            return progress?.ClaimedChapterRewards ?? new List<int>();
        }

        public async Task<CampaignProgress.DungeonProgressData> GetChapterClearDataAsync(Guid userId, int chapter, CancellationToken cancellationToken = default)
        {
            // Would need to query stage clears for chapter
            return new CampaignProgress.DungeonProgressData();
        }
    }

    public class CampaignStageRepository : BaseRepository<CampaignStage>, ICampaignStageRepository
    {
        public CampaignStageRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<CampaignStage> GetByIdAsync(int stageId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(s => s.Id == stageId, cancellationToken);
        }

        public async Task<IReadOnlyList<CampaignStage>> GetByChapterAsync(int chapter, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(s => s.Chapter == chapter).OrderBy(s => s.StageNumber).ToListAsync(cancellationToken);
        }

        public async Task<CampaignStage> GetNextStageAsync(int chapter, int stageNumber, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(s => s.Chapter == chapter && s.StageNumber > stageNumber)
                .OrderBy(s => s.StageNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<CampaignStage> GetFirstStageOfChapterAsync(int chapter, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(s => s.Chapter == chapter)
                .OrderBy(s => s.StageNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    // Additional repositories for AccountDeletionService
    public class UserDeviceRepository : BaseRepository<UserDevice>, IUserDeviceRepository
    {
        public UserDeviceRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<UserDevice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
        }

        public async Task<UserDevice> GetByUserAndDeviceIdAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId, cancellationToken);
        }
    }

    public class UserSessionRepository : BaseRepository<UserSession>, IUserSessionRepository
    {
        public UserSessionRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<UserSession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        }

        public async Task<UserSession> GetActiveSessionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, cancellationToken);
        }
    }

    public class NotificationRepository : BaseRepository<Notification>, INotificationRepository
    {
        public NotificationRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Notification>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);
        }
    }

    public class PurchaseTransactionRepository : BaseRepository<PurchaseTransaction>, IPurchaseTransactionRepository
    {
        public PurchaseTransactionRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<PurchaseTransaction> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseTransaction>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PurchaseTransaction>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(p => p.Status == PurchaseStatus.Pending).ToListAsync(cancellationToken);
        }
    }

    public class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<RefreshToken> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(r => r.Token == token, cancellationToken);
        }

        public async Task<IReadOnlyList<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(r => r.UserId == userId).ToListAsync(cancellationToken);
        }

        public async Task<bool> RevokeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var tokens = await _dbSet.Where(r => r.UserId == userId && !r.IsRevoked).ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }
            return tokens.Count > 0;
        }
    }

    public class UserConsentRepository : BaseRepository<UserConsent>, IUserConsentRepository
    {
        public UserConsentRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<IReadOnlyList<UserConsent>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        }

        public async Task<UserConsent> GetByUserAndTypeAsync(Guid userId, ConsentType type, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(c => c.UserId == userId && c.Type == type, cancellationToken);
        }
    }

    public class GameConfigRepository : BaseRepository<GameConfig>, IGameConfigRepository
    {
        public GameConfigRepository(GoddessRequiemDbContext context) : base(context) { }

        public async Task<GameConfig> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
        }

        public async Task<IReadOnlyList<GameConfig>> GetPublicAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(c => c.IsPublic).ToListAsync(cancellationToken);
        }
    }
}