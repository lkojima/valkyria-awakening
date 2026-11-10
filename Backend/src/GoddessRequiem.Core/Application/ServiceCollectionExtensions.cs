using Microsoft.Extensions.DependencyInjection;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Application.Services;
using GoddessRequiem.Infrastructure.Repositories;
using GoddessRequiem.Infrastructure.Data;

namespace GoddessRequiem.Core.Application
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Auth
            services.AddScoped<IAuthService, AuthService>();

            // User
            services.AddScoped<IUserService, UserService>();

            // Character
            services.AddScoped<ICharacterService, CharacterService>();

            // Gacha
            services.AddScoped<IGachaService, GachaService>();

            // Battle
            services.AddScoped<IBattleService, BattleService>();
            services.AddScoped<IAutoBattleService, AutoBattleService>();

            // Campaign
            services.AddScoped<ICampaignService, CampaignService>();

            // Idle
            services.AddScoped<IIdleRewardService, IdleRewardService>();

            // Equipment/Relic/Rune/Skin
            services.AddScoped<IEquipmentService, EquipmentService>();
            services.AddScoped<IRelicService, RelicService>();
            services.AddScoped<IRuneService, RuneService>();
            services.AddScoped<ISkinService, SkinService>();

            // Game Modes
            services.AddScoped<ITowerService, TowerService>();
            services.AddScoped<IArenaService, ArenaService>();
            services.AddScoped<IWorldBossService, WorldBossService>();
            services.AddScoped<IDungeonService, DungeonService>();
            services.AddScoped<IExpeditionService, ExpeditionService>();
            services.AddScoped<IQuestService, QuestService>();
            services.AddScoped<IEventService, EventService>();

            // Shop & Monetization
            services.AddScoped<IShopService, ShopService>();
            services.AddScoped<IMonetizationService, MonetizationService>();

            // Cross-cutting
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddScoped<ILeaderboardService, LeaderboardService>();

            // Account Deletion (LGPD)
            services.AddScoped<IAccountDeletionService, AccountDeletionService>();

            return services;
        }

        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Repositories
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ICharacterRepository, CharacterRepository>();
            services.AddScoped<IUserCharacterRepository, UserCharacterRepository>();
            services.AddScoped<ISkillRepository, SkillRepository>();
            services.AddScoped<ISkinRepository, SkinRepository>();
            services.AddScoped<IUserSkinRepository, UserSkinRepository>();
            services.AddScoped<IUserDyeRepository, UserDyeRepository>();
            services.AddScoped<IDyeTemplateRepository, DyeTemplateRepository>();
            services.AddScoped<IEquipmentTemplateRepository, EquipmentTemplateRepository>();
            services.AddScoped<IUserEquipmentRepository, UserEquipmentRepository>();
            services.AddScoped<IRelicTemplateRepository, RelicTemplateRepository>();
            services.AddScoped<IUserRelicRepository, UserRelicRepository>();
            services.AddScoped<IRuneTemplateRepository, RuneTemplateRepository>();
            services.AddScoped<IUserRuneRepository, UserRuneRepository>();
            services.AddScoped<IGachaBannerRepository, GachaBannerRepository>();
            services.AddScoped<IGachaRateRepository, GachaRateRepository>();
            services.AddScoped<IGachaPoolRepository, GachaPoolRepository>();
            services.AddScoped<IGachaPityRepository, GachaPityRepository>();
            services.AddScoped<IGachaHistoryRepository, GachaHistoryRepository>();
            services.AddScoped<IBattleRepository, BattleRepository>();
            services.AddScoped<ICampaignProgressRepository, CampaignProgressRepository>();
            services.AddScoped<ICampaignStageRepository, CampaignStageRepository>();
            services.AddScoped<ITowerProgressRepository, TowerProgressRepository>();
            services.AddScoped<ITowerFloorRepository, TowerFloorRepository>();
            services.AddScoped<IArenaProfileRepository, ArenaProfileRepository>();
            services.AddScoped<IArenaBattleRepository, ArenaBattleRepository>();
            services.AddScoped<IWorldBossRepository, WorldBossRepository>();
            services.AddScoped<IWorldBossProgressRepository, WorldBossProgressRepository>();
            services.AddScoped<IWorldBossBattleRepository, WorldBossBattleRepository>();
            services.AddScoped<IDungeonRepository, DungeonRepository>();
            services.AddScoped<IDungeonProgressRepository, DungeonProgressRepository>();
            services.AddScoped<IExpeditionRepository, ExpeditionRepository>();
            services.AddScoped<IUserExpeditionRepository, UserExpeditionRepository>();
            services.AddScoped<IQuestRepository, QuestRepository>();
            services.AddScoped<IUserQuestRepository, UserQuestRepository>();
            services.AddScoped<IGameEventRepository, GameEventRepository>();
            services.AddScoped<IEventProgressRepository, EventProgressRepository>();
            services.AddScoped<IShopRepository, ShopRepository>();
            services.AddScoped<IUserShopDataRepository, UserShopDataRepository>();
            services.AddScoped<IPurchaseProductRepository, PurchaseProductRepository>();
            services.AddScoped<IUserPurchaseRepository, UserPurchaseRepository>();
            services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
            services.AddScoped<IBattlePassRepository, BattlePassRepository>();
            services.AddScoped<IUserBattlePassRepository, UserBattlePassRepository>();
            services.AddScoped<IIdleRewardRepository, IdleRewardRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IScheduledNotificationRepository, ScheduledNotificationRepository>();
            services.AddScoped<INotificationSettingsRepository, NotificationSettingsRepository>();
            services.AddScoped<IAnalyticsEventRepository, AnalyticsEventRepository>();
            services.AddScoped<IItemTemplateRepository, ItemTemplateRepository>();
            services.AddScoped<IUserItemRepository, UserItemRepository>();
            services.AddScoped<IUserCurrencyRepository, UserCurrencyRepository>();
            services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
            services.AddScoped<IUserSessionRepository, UserSessionRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IUserConsentRepository, UserConsentRepository>();
            services.AddScoped<IGameConfigRepository, GameConfigRepository>();

            // Database
            services.AddDbContext<GoddessRequiemDbContext>();

            return services;
        }

        public static IServiceCollection AddGameServices(this IServiceCollection services)
        {
            // Game-specific singleton services
            services.AddSingleton<GameConfig>();
            services.AddSingleton<BattleConfig>();
            services.AddSingleton<GachaConfig>();
            services.AddSingleton<IdleConfig>();

            return services;
        }
    }
}