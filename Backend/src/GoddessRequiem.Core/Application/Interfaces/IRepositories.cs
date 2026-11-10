using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Application.Interfaces
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<T> GetByIdAsync(Guid id);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
    }

    // Additional repository interfaces needed for AccountDeletionService
    public interface IUserDeviceRepository : IRepository<UserDevice>
    {
        Task<IReadOnlyList<UserDevice>> GetByUserIdAsync(Guid userId);
        Task<UserDevice> GetByUserAndDeviceIdAsync(Guid userId, string deviceId);
    }

    public interface IUserSessionRepository : IRepository<UserSession>
    {
        Task<IReadOnlyList<UserSession>> GetByUserIdAsync(Guid userId);
        Task<UserSession> GetActiveSessionAsync(Guid userId);
    }

    public interface INotificationRepository : IRepository<Notification>
    {
        Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, int limit = 50);
        Task<IReadOnlyList<Notification>> GetUnreadAsync(Guid userId);
    }

    public interface IPurchaseTransactionRepository : IRepository<PurchaseTransaction>
    {
        Task<PurchaseTransaction> GetByTransactionIdAsync(string transactionId);
        Task<IReadOnlyList<PurchaseTransaction>> GetByUserIdAsync(Guid userId, int limit = 50);
        Task<IReadOnlyList<PurchaseTransaction>> GetPendingAsync();
    }

    public interface IRefreshTokenRepository : IRepository<RefreshToken>
    {
        Task<RefreshToken> GetByTokenAsync(string token);
        Task<IReadOnlyList<RefreshToken>> GetByUserIdAsync(Guid userId);
        Task<bool> RevokeAllByUserIdAsync(Guid userId);
    }

    public interface IUserConsentRepository : IRepository<UserConsent>
    {
        Task<IReadOnlyList<UserConsent>> GetByUserIdAsync(Guid userId);
        Task<UserConsent> GetByUserAndTypeAsync(Guid userId, ConsentType type);
    }

    public interface IGameConfigRepository : IRepository<GameConfig>
    {
        Task<GameConfig> GetByKeyAsync(string key);
        Task<IReadOnlyList<GameConfig>> GetPublicAsync();
    }

    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByUsernameAsync(string username);
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByDeviceIdAsync(string deviceId);
        Task<IReadOnlyList<User>> GetByStatusAsync(AccountStatus status);
        Task<IReadOnlyList<User>> GetActiveUsersAsync(DateTime since);
        Task<int> GetTotalUsersCountAsync();
    }

    public interface ICharacterRepository : IRepository<Character>
    {
        Task<IReadOnlyList<Character>> GetByRarityAsync(Rarity rarity);
        Task<IReadOnlyList<Character>> GetByElementAsync(Element element);
        Task<IReadOnlyList<Character>> GetByClassAsync(CharacterClass characterClass);
        Task<IReadOnlyList<Character>> GetObtainableAsync();
        Task<IReadOnlyList<Character>> GetLimitedAsync();
        Task<Character> GetByNameAsync(string name);
    }

    public interface IUserCharacterRepository : IRepository<UserCharacter>
    {
        Task<IReadOnlyList<UserCharacter>> GetByUserIdAsync(Guid userId);
        Task<UserCharacter> GetByUserAndCharacterAsync(Guid userId, Guid characterId);
        Task<IReadOnlyList<UserCharacter>> GetFavoritesAsync(Guid userId);
        Task<IReadOnlyList<UserCharacter>> GetLockedAsync(Guid userId);
        Task<int> GetCharacterCountAsync(Guid userId);
        Task<IReadOnlyList<UserCharacter>> GetByLevelRangeAsync(Guid userId, int minLevel, int maxLevel);
    }

    public interface IEquipmentRepository : IRepository<Equipment>
    {
        Task<IReadOnlyList<Equipment>> GetBySlotAsync(EquipmentSlot slot);
        Task<IReadOnlyList<Equipment>> GetByRarityAsync(EquipmentRarity rarity);
        Task<IReadOnlyList<Equipment>> GetBySetNameAsync(string setName);
        Task<IReadOnlyList<Equipment>> GetByLevelRequirementAsync(int maxLevel);
    }

    public interface IUserEquipmentRepository : IRepository<UserEquipment>
    {
        Task<IReadOnlyList<UserEquipment>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<UserEquipment>> GetEquippedAsync(Guid userId, Guid characterId);
        Task<UserEquipment> GetByIdAsync(Guid id);
        Task<int> GetCountByRarityAsync(Guid userId, EquipmentRarity rarity);
    }

    public interface IRelicRepository : IRepository<Relic>
    {
        Task<IReadOnlyList<Relic>> GetByTypeAsync(RelicType type);
        Task<IReadOnlyList<Relic>> GetBySetNameAsync(string setName);
    }

    public interface IUserRelicRepository : IRepository<UserRelic>
    {
        Task<IReadOnlyList<UserRelic>> GetByUserIdAsync(Guid userId);
        Task<UserRelic> GetEquippedAsync(Guid userId, Guid characterId);
    }

    public interface IRuneRepository : IRepository<Rune>
    {
        Task<IReadOnlyList<Rune>> GetBySlotAsync(RuneSlot slot);
        Task<IReadOnlyList<Rune>> GetByTypeAsync(RuneType type);
        Task<IReadOnlyList<Rune>> GetBySetNameAsync(string setName);
    }

    public interface IUserRuneRepository : IRepository<UserRune>
    {
        Task<IReadOnlyList<UserRune>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<UserRune>> GetEquippedAsync(Guid userId, Guid characterId);
    }

    public interface ISkinRepository : IRepository<Skin>
    {
        Task<IReadOnlyList<Skin>> GetByCharacterIdAsync(Guid characterId);
        Task<IReadOnlyList<Skin>> GetByRarityAsync(SkinRarity rarity);
        Task<IReadOnlyList<Skin>> GetAvailableNowAsync();
    }

    public interface IUserSkinRepository : IRepository<UserSkin>
    {
        Task<IReadOnlyList<UserSkin>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<UserSkin>> GetByCharacterIdAsync(Guid userId, Guid characterId);
        Task<UserSkin> GetEquippedAsync(Guid userId, Guid characterId);
    }

    public interface IGachaBannerRepository : IRepository<GachaBanner>
    {
        Task<IReadOnlyList<GachaBanner>> GetActiveAsync();
        Task<IReadOnlyList<GachaBanner>> GetByTypeAsync(SummonType type);
        Task<GachaBanner> GetCurrentRateUpAsync();
    }

    public interface IGachaHistoryRepository : IRepository<GachaHistory>
    {
        Task<IReadOnlyList<GachaHistory>> GetByUserIdAsync(Guid userId, int limit = 100);
        Task<IReadOnlyList<GachaHistory>> GetByBannerIdAsync(Guid bannerId);
        Task<GachaHistory> GetLatestByUserAsync(Guid userId);
    }

    public interface IUserGachaPityRepository : IRepository<UserGachaPity>
    {
        Task<UserGachaPity> GetByUserAndBannerAsync(Guid userId, Guid bannerId);
        Task<IReadOnlyList<UserGachaPity>> GetByUserIdAsync(Guid userId);
    }

    public interface IBattleRepository : IRepository<Battle>
    {
        Task<IReadOnlyList<Battle>> GetByUserIdAsync(Guid userId, int limit = 50);
        Task<IReadOnlyList<Battle>> GetByModeAsync(GameMode mode);
        Task<Battle> GetActiveBattleAsync(Guid userId);
    }

    public interface ICampaignRepository : IRepository<CampaignStage>
    {
        Task<IReadOnlyList<CampaignStage>> GetByChapterAsync(int chapter);
        Task<CampaignStage> GetByChapterAndStageAsync(int chapter, int stage);
        Task<IReadOnlyList<CampaignStage>> GetAvailableAsync(int playerLevel);
    }

    public interface IUserCampaignProgressRepository : IRepository<UserCampaignProgress>
    {
        Task<UserCampaignProgress> GetByUserIdAsync(Guid userId);
    }

    public interface ITowerRepository : IRepository<TowerFloor>
    {
        Task<IReadOnlyList<TowerFloor>> GetBySeasonAsync(int season);
        Task<TowerFloor> GetFloorAsync(int season, int floor);
    }

    public interface IUserTowerProgressRepository : IRepository<UserTowerProgress>
    {
        Task<UserTowerProgress> GetByUserAndSeasonAsync(Guid userId, int season);
    }

    public interface IArenaRepository : IRepository<ArenaSeason>
    {
        Task<ArenaSeason> GetCurrentSeasonAsync();
        Task<IReadOnlyList<ArenaSeason>> GetPastSeasonsAsync(int count);
    }

    public interface IUserArenaRepository : IRepository<UserArenaProfile>
    {
        Task<UserArenaProfile> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<UserArenaProfile>> GetTopPlayersAsync(int count);
        Task<IReadOnlyList<ArenaOpponent>> GetOpponentsAsync(Guid userId, int count);
    }

    public interface IWorldBossRepository : IRepository<WorldBoss>
    {
        Task<WorldBoss> GetActiveAsync();
        Task<IReadOnlyList<WorldBoss>> GetUpcomingAsync();
        Task<IReadOnlyList<WorldBoss>> GetHistoryAsync(int count);
    }

    public interface IUserWorldBossRepository : IRepository<UserWorldBossProgress>
    {
        Task<UserWorldBossProgress> GetByUserAndBossAsync(Guid userId, Guid bossId);
        Task<IReadOnlyList<WorldBossRankingEntry>> GetRankingAsync(Guid bossId, int count);
    }

    public interface IDungeonRepository : IRepository<Dungeon>
    {
        Task<IReadOnlyList<Dungeon>> GetAvailableAsync(int playerLevel);
        Task<IReadOnlyList<Dungeon>> GetByTypeAsync(DungeonType type);
    }

    public interface IUserDungeonRepository : IRepository<UserDungeonProgress>
    {
        Task<IReadOnlyList<UserDungeonProgress>> GetByUserIdAsync(Guid userId);
        Task<UserDungeonProgress> GetByUserAndDungeonAsync(Guid userId, Guid dungeonId);
    }

    public interface IExpeditionRepository : IRepository<Expedition>
    {
        Task<IReadOnlyList<Expedition>> GetAvailableAsync(int playerLevel);
    }

    public interface IUserExpeditionRepository : IRepository<UserExpedition>
    {
        Task<IReadOnlyList<UserExpedition>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<UserExpedition>> GetCompletedAsync(Guid userId);
        Task<IReadOnlyList<UserExpedition>> GetInProgressAsync(Guid userId);
    }

    public interface IQuestRepository : IRepository<Quest>
    {
        Task<IReadOnlyList<Quest>> GetByTypeAsync(QuestType type);
        Task<IReadOnlyList<Quest>> GetAvailableAsync(int playerLevel, HashSet<Guid> completedQuests);
        Task<Quest> GetByIdAsync(Guid id);
    }

    public interface IUserQuestRepository : IRepository<UserQuest>
    {
        Task<IReadOnlyList<UserQuest>> GetByUserIdAsync(Guid userId);
        Task<IReadOnlyList<UserQuest>> GetByStatusAsync(Guid userId, QuestStatus status);
        Task<IReadOnlyList<UserQuest>> GetActiveAsync(Guid userId);
        Task<UserQuest> GetByUserAndQuestAsync(Guid userId, Guid questId);
    }

    public interface IEventRepository : IRepository<GameEvent>
    {
        Task<IReadOnlyList<GameEvent>> GetActiveAsync();
        Task<IReadOnlyList<GameEvent>> GetUpcomingAsync();
        Task<IReadOnlyList<GameEvent>> GetByTypeAsync(EventType type);
    }

    public interface IUserEventRepository : IRepository<UserEventProgress>
    {
        Task<UserEventProgress> GetByUserAndEventAsync(Guid userId, Guid eventId);
        Task<IReadOnlyList<UserEventProgress>> GetByUserIdAsync(Guid userId);
    }

    public interface IShopRepository : IRepository<Shop>
    {
        Task<IReadOnlyList<Shop>> GetByTypeAsync(ShopType type);
        Task<IReadOnlyList<Shop>> GetAvailableAsync(int playerVipLevel);
    }

    public interface IUserShopRepository : IRepository<UserShopData>
    {
        Task<UserShopData> GetByUserAndShopAsync(Guid userId, Guid shopId);
        Task<IReadOnlyList<UserShopData>> GetByUserIdAsync(Guid userId);
    }

    public interface IPurchaseTransactionRepository : IRepository<PurchaseTransaction>
    {
        Task<PurchaseTransaction> GetByTransactionIdAsync(string transactionId);
        Task<IReadOnlyList<PurchaseTransaction>> GetByUserIdAsync(Guid userId, int limit = 50);
        Task<IReadOnlyList<PurchaseTransaction>> GetPendingAsync();
    }

    public interface IIdleRewardRepository : IRepository<IdleReward>
    {
        Task<IdleReward> GetByUserIdAsync(Guid userId);
    }

    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        ICharacterRepository Characters { get; }
        IUserCharacterRepository UserCharacters { get; }
        IEquipmentRepository Equipment { get; }
        IUserEquipmentRepository UserEquipment { get; }
        IRelicRepository Relics { get; }
        IUserRelicRepository UserRelics { get; }
        IRuneRepository Runes { get; }
        IUserRuneRepository UserRunes { get; }
        ISkinRepository Skins { get; }
        IUserSkinRepository UserSkins { get; }
        IGachaBannerRepository GachaBanners { get; }
        IGachaHistoryRepository GachaHistory { get; }
        IUserGachaPityRepository UserGachaPity { get; }
        IBattleRepository Battles { get; }
        ICampaignRepository CampaignStages { get; }
        IUserCampaignProgressRepository UserCampaignProgress { get; }
        ITowerRepository TowerFloors { get; }
        IUserTowerProgressRepository UserTowerProgress { get; }
        IArenaRepository ArenaSeasons { get; }
        IUserArenaRepository UserArena { get; }
        IWorldBossRepository WorldBosses { get; }
        IUserWorldBossRepository UserWorldBoss { get; }
        IDungeonRepository Dungeons { get; }
        IUserDungeonRepository UserDungeons { get; }
        IExpeditionRepository Expeditions { get; }
        IUserExpeditionRepository UserExpeditions { get; }
        IQuestRepository Quests { get; }
        IUserQuestRepository UserQuests { get; }
        IEventRepository Events { get; }
        IUserEventRepository UserEvents { get; }
        IShopRepository Shops { get; }
        IUserShopRepository UserShops { get; }
        IPurchaseTransactionRepository PurchaseTransactions { get; }
        IIdleRewardRepository IdleRewards { get; }
        IUserConsentRepository UserConsents { get; }
        IGameConfigRepository GameConfigs { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}