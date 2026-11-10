using System;
using System.Threading;
using System.Threading.Tasks;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GoddessRequiem.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly GoddessRequiemDbContext _context;
        private IDbContextTransaction _transaction;
        private bool _disposed;

        // Repositories
        private IUserRepository _users;
        private IUserCharacterRepository _userCharacters;
        private ICharacterRepository _characters;
        private ISkillRepository _skills;
        private IUserSkinRepository _userSkins;
        private IUserDyeRepository _userDyes;
        private IDyeTemplateRepository _dyeTemplates;
        private IEquipmentTemplateRepository _equipmentTemplates;
        private IUserEquipmentRepository _userEquipments;
        private IRelicTemplateRepository _relicTemplates;
        private IUserRelicRepository _userRelics;
        private IRuneTemplateRepository _runeTemplates;
        private IUserRuneRepository _userRunes;
        private IGachaBannerRepository _gachaBanners;
        private IGachaRateRepository _gachaRates;
        private IGachaPoolRepository _gachaPools;
        private IGachaPityRepository _gachaPities;
        private IGachaHistoryRepository _gachaHistories;
        private IBattleRepository _battles;
        private ICampaignProgressRepository _campaignProgress;
        private ICampaignStageRepository _campaignStages;
        private ITowerProgressRepository _towerProgress;
        private ITowerFloorRepository _towerFloors;
        private IArenaProfileRepository _arenaProfiles;
        private IArenaBattleRepository _arenaBattles;
        private IWorldBossRepository _worldBosses;
        private IWorldBossProgressRepository _worldBossProgress;
        private IWorldBossBattleRepository _worldBossBattles;
        private IDungeonRepository _dungeons;
        private IDungeonProgressRepository _dungeonProgress;
        private IExpeditionRepository _expeditions;
        private IUserExpeditionRepository _userExpeditions;
        private IQuestRepository _quests;
        private IUserQuestRepository _userQuests;
        private IGameEventRepository _gameEvents;
        private IEventProgressRepository _eventProgress;
        private IShopRepository _shops;
        private IUserShopDataRepository _userShopData;
        private IPurchaseProductRepository _purchaseProducts;
        private IUserPurchaseRepository _userPurchases;
        private IUserSubscriptionRepository _userSubscriptions;
        private IBattlePassRepository _battlePasses;
        private IUserBattlePassRepository _userBattlePasses;
        private IIdleRewardRepository _idleRewards;
        private INotificationRepository _notifications;
        private IScheduledNotificationRepository _scheduledNotifications;
        private INotificationSettingsRepository _notificationSettings;
        private IAnalyticsEventRepository _analyticsEvents;
        private IItemTemplateRepository _itemTemplates;
        private IUserItemRepository _userItems;
        private IUserCurrencyRepository _userCurrencies;
        private IUserDeviceRepository _userDevices;
        private IUserSessionRepository _userSessions;
        private IRefreshTokenRepository _refreshTokens;
        private IUserConsentRepository _userConsents;
        private IGameConfigRepository _gameConfigs;

        public UnitOfWork(GoddessRequiemDbContext context)
        {
            _context = context;
        }

        public IUserRepository Users => _users ??= new UserRepository(_context);
        public IUserCharacterRepository UserCharacters => _userCharacters ??= new UserCharacterRepository(_context);
        public ICharacterRepository Characters => _characters ??= new CharacterRepository(_context);
        public ISkillRepository Skills => _skills ??= new SkillRepository(_context);
        public IUserSkinRepository UserSkins => _userSkins ??= new UserSkinRepository(_context);
        public IUserDyeRepository UserDyes => _userDyes ??= new UserDyeRepository(_context);
        public IDyeTemplateRepository DyeTemplates => _dyeTemplates ??= new DyeTemplateRepository(_context);
        public IEquipmentTemplateRepository EquipmentTemplates => _equipmentTemplates ??= new EquipmentTemplateRepository(_context);
        public IUserEquipmentRepository UserEquipments => _userEquipments ??= new UserEquipmentRepository(_context);
        public IRelicTemplateRepository RelicTemplates => _relicTemplates ??= new RelicTemplateRepository(_context);
        public IUserRelicRepository UserRelics => _userRelics ??= new UserRelicRepository(_context);
        public IRuneTemplateRepository RuneTemplates => _runeTemplates ??= new RuneTemplateRepository(_context);
        public IUserRuneRepository UserRunes => _userRunes ??= new UserRuneRepository(_context);
        public IGachaBannerRepository GachaBanners => _gachaBanners ??= new GachaBannerRepository(_context);
        public IGachaRateRepository GachaRates => _gachaRates ??= new GachaRateRepository(_context);
        public IGachaPoolRepository GachaPools => _gachaPools ??= new GachaPoolRepository(_context);
        public IGachaPityRepository GachaPities => _gachaPities ??= new GachaPityRepository(_context);
        public IGachaHistoryRepository GachaHistories => _gachaHistories ??= new GachaHistoryRepository(_context);
        public IBattleRepository Battles => _battles ??= new BattleRepository(_context);
        public ICampaignProgressRepository CampaignProgress => _campaignProgress ??= new CampaignProgressRepository(_context);
        public ICampaignStageRepository CampaignStages => _campaignStages ??= new CampaignStageRepository(_context);
        public ITowerProgressRepository TowerProgress => _towerProgress ??= new TowerProgressRepository(_context);
        public ITowerFloorRepository TowerFloors => _towerFloors ??= new TowerFloorRepository(_context);
        public IArenaProfileRepository ArenaProfiles => _arenaProfiles ??= new ArenaProfileRepository(_context);
        public IArenaBattleRepository ArenaBattles => _arenaBattles ??= new ArenaBattleRepository(_context);
        public IWorldBossRepository WorldBosses => _worldBosses ??= new WorldBossRepository(_context);
        public IWorldBossProgressRepository WorldBossProgress => _worldBossProgress ??= new WorldBossProgressRepository(_context);
        public IWorldBossBattleRepository WorldBossBattles => _worldBossBattles ??= new WorldBossBattleRepository(_context);
        public IDungeonRepository Dungeons => _dungeons ??= new DungeonRepository(_context);
        public IDungeonProgressRepository DungeonProgress => _dungeonProgress ??= new DungeonProgressRepository(_context);
        public IExpeditionRepository Expeditions => _expeditions ??= new ExpeditionRepository(_context);
        public IUserExpeditionRepository UserExpeditions => _userExpeditions ??= new UserExpeditionRepository(_context);
        public IQuestRepository Quests => _quests ??= new QuestRepository(_context);
        public IUserQuestRepository UserQuests => _userQuests ??= new UserQuestRepository(_context);
        public IGameEventRepository GameEvents => _gameEvents ??= new GameEventRepository(_context);
        public IEventProgressRepository EventProgress => _eventProgress ??= new EventProgressRepository(_context);
        public IShopRepository Shops => _shops ??= new ShopRepository(_context);
        public IUserShopDataRepository UserShopData => _userShopData ??= new UserShopDataRepository(_context);
        public IPurchaseProductRepository PurchaseProducts => _purchaseProducts ??= new PurchaseProductRepository(_context);
        public IUserPurchaseRepository UserPurchases => _userPurchases ??= new UserPurchaseRepository(_context);
        public IUserSubscriptionRepository UserSubscriptions => _userSubscriptions ??= new UserSubscriptionRepository(_context);
        public IBattlePassRepository BattlePasses => _battlePasses ??= new BattlePassRepository(_context);
        public IUserBattlePassRepository UserBattlePasses => _userBattlePasses ??= new UserBattlePassRepository(_context);
        public IIdleRewardRepository IdleRewards => _idleRewards ??= new IdleRewardRepository(_context);
        public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
        public IScheduledNotificationRepository ScheduledNotifications => _scheduledNotifications ??= new ScheduledNotificationRepository(_context);
        public INotificationSettingsRepository NotificationSettings => _notificationSettings ??= new NotificationSettingsRepository(_context);
        public IAnalyticsEventRepository AnalyticsEvents => _analyticsEvents ??= new AnalyticsEventRepository(_context);
        public IItemTemplateRepository ItemTemplates => _itemTemplates ??= new ItemTemplateRepository(_context);
        public IUserItemRepository UserItems => _userItems ??= new UserItemRepository(_context);
        public IUserCurrencyRepository UserCurrencies => _userCurrencies ??= new UserCurrencyRepository(_context);
        public IUserDeviceRepository UserDevices => _userDevices ??= new UserDeviceRepository(_context);
        public IUserSessionRepository UserSessions => _userSessions ??= new UserSessionRepository(_context);
        public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
        public IUserConsentRepository UserConsents => _userConsents ??= new UserConsentRepository(_context);
        public IGameConfigRepository GameConfigs => _gameConfigs ??= new GameConfigRepository(_context);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync(cancellationToken);
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            await BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        public async Task ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            await BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action();
                await CommitTransactionAsync(cancellationToken);
                return result;
            }
            catch
            {
                await RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _transaction?.Dispose();
                _context?.Dispose();
                _disposed = true;
            }
        }
    }
}