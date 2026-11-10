using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;

namespace GoddessRequiem.Infrastructure.Data
{
    public class GoddessRequiemDbContext : DbContext
    {
        public GoddessRequiemDbContext(DbContextOptions<GoddessRequiemDbContext> options) : base(options) { }

        // Users & Auth
        public DbSet<User> Users => Set<User>();
        public DbSet<UserDevice> UserDevices => Set<UserDevice>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        // Characters
        public DbSet<Character> Characters => Set<Character>();
        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<UserCharacter> UserCharacters => Set<UserCharacter>();
        public DbSet<CharacterSkin> CharacterSkins => Set<CharacterSkin>();
        public DbSet<UserSkin> UserSkins => Set<UserSkin>();
        public DbSet<UserDye> UserDyes => Set<UserDye>();
        public DbSet<DyeTemplate> DyeTemplates => Set<DyeTemplate>();

        // Equipment
        public DbSet<EquipmentTemplate> EquipmentTemplates => Set<EquipmentTemplate>();
        public DbSet<UserEquipment> UserEquipments => Set<UserEquipment>();
        public DbSet<RelicTemplate> RelicTemplates => Set<RelicTemplate>();
        public DbSet<UserRelic> UserRelics => Set<UserRelic>();
        public DbSet<RuneTemplate> RuneTemplates => Set<RuneTemplate>();
        public DbSet<UserRune> UserRunes => Set<UserRune>();

        // Gacha
        public DbSet<GachaBanner> GachaBanners => Set<GachaBanner>();
        public DbSet<GachaRate> GachaRates => Set<GachaRate>();
        public DbSet<GachaPool> GachaPools => Set<GachaPool>();
        public DbSet<GachaPity> GachaPities => Set<GachaPity>();
        public DbSet<GachaHistory> GachaHistories => Set<GachaHistory>();

        // Battle
        public DbSet<Battle> Battles => Set<Battle>();
        public DbSet<BattleActionLog> BattleActionLogs => Set<BattleActionLog>();

        // Campaign
        public DbSet<CampaignChapter> CampaignChapters => Set<CampaignChapter>();
        public DbSet<CampaignStage> CampaignStages => Set<CampaignStage>();
        public DbSet<CampaignProgress> CampaignProgress => Set<CampaignProgress>();
        public DbSet<CampaignStageClear> CampaignStageClears => Set<CampaignStageClear>();

        // Tower
        public DbSet<TowerFloor> TowerFloors => Set<TowerFloor>();
        public DbSet<TowerProgress> TowerProgress => Set<TowerProgress>();
        public DbSet<TowerFloorClear> TowerFloorClears => Set<TowerFloorClear>();
        public DbSet<TowerStarReward> TowerStarRewards => Set<TowerStarReward>();
        public DbSet<TowerSeasonReward> TowerSeasonRewards => Set<TowerSeasonReward>();

        // Arena
        public DbSet<ArenaProfile> ArenaProfiles => Set<ArenaProfile>();
        public DbSet<ArenaBattle> ArenaBattles => Set<ArenaBattle>();

        // World Boss
        public DbSet<WorldBoss> WorldBosses => Set<WorldBoss>();
        public DbSet<WorldBossProgress> WorldBossProgress => Set<WorldBossProgress>();
        public DbSet<WorldBossBattle> WorldBossBattles => Set<WorldBossBattle>();

        // Dungeon
        public DbSet<Dungeon> Dungeons => Set<Dungeon>();
        public DbSet<DungeonTier> DungeonTiers => Set<DungeonTier>();
        public DbSet<DungeonProgress> DungeonProgress => Set<DungeonProgress>();

        // Expedition
        public DbSet<Expedition> Expeditions => Set<Expedition>();
        public DbSet<UserExpedition> UserExpeditions => Set<UserExpedition>();

        // Quest
        public DbSet<Quest> Quests => Set<Quest>();
        public DbSet<QuestObjective> QuestObjectives => Set<QuestObjective>();
        public DbSet<UserQuest> UserQuests => Set<UserQuest>();

        // Events
        public DbSet<GameEvent> GameEvents => Set<GameEvent>();
        public DbSet<EventStage> EventStages => Set<EventStage>();
        public DbSet<EventQuest> EventQuests => Set<EventQuest>();
        public DbSet<EventShop> EventShops => Set<EventShop>();
        public DbSet<EventMilestone> EventMilestones => Set<EventMilestone>();
        public DbSet<EventProgress> EventProgress => Set<EventProgress>();

        // Shop
        public DbSet<Shop> Shops => Set<Shop>();
        public DbSet<ShopItem> ShopItems => Set<ShopItem>();
        public DbSet<UserShopData> UserShopData => Set<UserShopData>();

        // Monetization
        public DbSet<PurchaseProduct> PurchaseProducts => Set<PurchaseProduct>();
        public DbSet<UserPurchase> UserPurchases => Set<UserPurchase>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<BattlePass> BattlePasses => Set<BattlePass>();
        public DbSet<BattlePassReward> BattlePassRewards => Set<BattlePassReward>();
        public DbSet<UserBattlePass> UserBattlePasses => Set<UserBattlePass>();

        // Idle Rewards
        public DbSet<IdleReward> IdleRewards => Set<IdleReward>();

        // Notifications
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<ScheduledNotification> ScheduledNotifications => Set<ScheduledNotification>();
        public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();

        // Analytics
        public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();

        // Items & Inventory
        public DbSet<ItemTemplate> ItemTemplates => Set<ItemTemplate>();
        public DbSet<UserItem> UserItems => Set<UserItem>();
        public DbSet<UserCurrency> UserCurrencies => Set<UserCurrency>();

        // Guild/Social (future)
        // public DbSet<Guild> Guilds => Set<Guild>();
        // public DbSet<GuildMember> GuildMembers => Set<GuildMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all configurations from assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Global query filters for soft deletes
            modelBuilder.Entity<User>().HasQueryFilter(u => u.Status != AccountStatus.Deleted);
            modelBuilder.Entity<UserCharacter>().HasQueryFilter(uc => !uc.IsDeleted);
            modelBuilder.Entity<UserEquipment>().HasQueryFilter(ue => !ue.IsDeleted);
            modelBuilder.Entity<UserRelic>().HasQueryFilter(ur => !ur.IsDeleted);
            modelBuilder.Entity<UserRune>().HasQueryFilter(ur => !ur.IsDeleted);
            modelBuilder.Entity<UserSkin>().HasQueryFilter(us => !us.IsDeleted);

            // Value converters for enums
            var enumConverter = new ValueConverter<Enum, int>(v => Convert.ToInt32(v), v => (Enum)Enum.ToObject(typeof(Enum), v));

            // Configure all enum properties to use int storage
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType.IsEnum)
                    {
                        property.SetValueConverter(new EnumToIntConverter(property.ClrType));
                    }
                }
            }

            // Value object converters
            modelBuilder.Entity<User>()
                .Property(u => u.Settings)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<string, object>());

            modelBuilder.Entity<UserCharacter>()
                .Property(uc => uc.Stats)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<EquipmentStatType, long>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<EquipmentStatType, long>());

            modelBuilder.Entity<UserEquipment>()
                .Property(ue => ue.Stats)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<EquipmentStat>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<EquipmentStat>());

            modelBuilder.Entity<UserRelic>()
                .Property(ur => ur.MainStat)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<EquipmentStat>(v, (System.Text.Json.JsonSerializerOptions)null));

            modelBuilder.Entity<UserRelic>()
                .Property(ur => ur.SubStats)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<EquipmentStat>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<EquipmentStat>());

            modelBuilder.Entity<UserRune>()
                .Property(ur => ur.Stats)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<EquipmentStat>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<EquipmentStat>());

            modelBuilder.Entity<UserSkin>()
                .Property(us => us.DyeSlots)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<UserSkinDyeSlot>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<UserSkinDyeSlot>());

            modelBuilder.Entity<CampaignProgress>()
                .Property(cp => cp.AutoProgressConfig)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<AutoProgressConfig>(v, (System.Text.Json.JsonSerializerOptions)null));

            modelBuilder.Entity<CampaignProgress>()
                .Property(cp => cp.ClearedStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<CampaignStageClear>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<CampaignStageClear>());

            modelBuilder.Entity<TowerProgress>()
                .Property(tp => tp.ClearedFloors)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<TowerFloorClear>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<TowerFloorClear>());

            modelBuilder.Entity<DungeonProgress>()
                .Property(dp => dp.Dungeons)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, DungeonProgress.DungeonProgressData>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<int, DungeonProgress.DungeonProgressData>());

            modelBuilder.EventProgress()
                .Property(ep => ep.StageProgress)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, EventStageProgress>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<Guid, EventStageProgress>());

            modelBuilder.EventProgress()
                .Property(ep => ep.QuestProgress)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, EventQuestProgress>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<Guid, EventQuestProgress>());

            modelBuilder.Entity<EventProgress>()
                .Property(ep => ep.ClaimedMilestones)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<int>());

            modelBuilder.Entity<EventProgress>()
                .Property(ep => ep.ClaimedQuestRewards)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<Guid>());

            modelBuilder.Entity<EventProgress>()
                .Property(ep => ep.ShopPurchases)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, int>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<Guid, int>());

            modelBuilder.Entity<UserShopData>()
                .Property(usd => usd.Shops)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, UserShopData.ShopData>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<Guid, UserShopData.ShopData>());

            modelBuilder.Entity<UserQuest>()
                .Property(uq => uq.ObjectiveProgress)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<string, int>());

            modelBuilder.Entity<UserBattlePass>()
                .Property(ubp => ubp.ClaimedFreeRewards)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<int>());

            modelBuilder.Entity<UserBattlePass>()
                .Property(ubp => ubp.ClaimedPremiumRewards)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<int>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<int>());

            // Configure owned types
            modelBuilder.Entity<User>().OwnsOne(u => u.CustomData, cd =>
            {
                cd.ToJson();
            });

            modelBuilder.Entity<Battle>().OwnsMany(b => b.FieldEffects);
            modelBuilder.Entity<Battle>().OwnsMany(b => b.TurnOrder);

            // Indexes
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Status);
            modelBuilder.Entity<User>().HasIndex(u => u.LastLoginAt);

            modelBuilder.Entity<UserCharacter>().HasIndex(uc => new { uc.UserId, uc.CharacterId }).IsUnique();
            modelBuilder.Entity<UserCharacter>().HasIndex(uc => uc.UserId);
            modelBuilder.Entity<UserCharacter>().HasIndex(uc => uc.IsFavorite);
            modelBuilder.Entity<UserCharacter>().HasIndex(uc => uc.IsLocked);

            modelBuilder.Entity<UserEquipment>().HasIndex(ue => new { ue.UserId, ue.IsEquipped });
            modelBuilder.Entity<UserEquipment>().HasIndex(ue => ue.EquippedCharacterId);

            modelBuilder.Entity<UserRelic>().HasIndex(ur => new { ur.UserId, ur.IsEquipped });
            modelBuilder.Entity<UserRelic>().HasIndex(ur => ur.EquippedCharacterId);

            modelBuilder.Entity<UserRune>().HasIndex(ur => new { ur.UserId, ur.IsEquipped });
            modelBuilder.Entity<UserRune>().HasIndex(ur => ur.EquippedCharacterId);

            modelBuilder.Entity<GachaPity>().HasIndex(gp => new { gp.UserId, gp.BannerId }).IsUnique();

            modelBuilder.Entity<CampaignProgress>().HasIndex(cp => cp.UserId).IsUnique();
            modelBuilder.Entity<TowerProgress>().HasIndex(tp => new { tp.UserId, tp.Season }).IsUnique();
            modelBuilder.Entity<ArenaProfile>().HasIndex(ap => ap.UserId).IsUnique();
            modelBuilder.Entity<WorldBossProgress>().HasIndex(wbp => new { wbp.UserId, wbp.BossId }).IsUnique();
            modelBuilder.Entity<DungeonProgress>().HasIndex(dp => dp.UserId).IsUnique();
            modelBuilder.Entity<EventProgress>().HasIndex(ep => new { ep.UserId, ep.EventId }).IsUnique();
            modelBuilder.Entity<UserBattlePass>().HasIndex(ubp => new { ubp.UserId, ubp.Season }).IsUnique();

            modelBuilder.Entity<RefreshToken>().HasIndex(rt => rt.Token).IsUnique();
            modelBuilder.Entity<RefreshToken>().HasIndex(rt => rt.UserId);
            modelBuilder.Entity<RefreshToken>().HasIndex(rt => rt.ExpiresAt);

            modelBuilder.Entity<ScheduledNotification>().HasIndex(sn => sn.FireAt);
            modelBuilder.Entity<ScheduledNotification>().HasIndex(sn => sn.Status);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Update timestamps
            var entries = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }

            // Handle soft deletes
            var deletedEntries = ChangeTracker.Entries<ISoftDelete>()
                .Where(e => e.State == EntityState.Deleted);

            foreach (var entry in deletedEntries)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    public class EnumToIntConverter : ValueConverter
    {
        public EnumToIntConverter(Type enumType) : base(
            enumType,
            typeof(int),
            v => Convert.ToInt32(v),
            v => Enum.ToObject(enumType, v))
        {
        }
    }
}