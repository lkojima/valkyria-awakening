using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(string username, string email, string password, Platform platform, string deviceId, Language language);
        Task<AuthResult> LoginAsync(string username, string password, string deviceId);
        Task<AuthResult> LoginWithDeviceIdAsync(string deviceId);
        Task<AuthResult> RefreshTokenAsync(string refreshToken);
        Task<bool> VerifyEmailAsync(Guid userId, string token);
        Task<bool> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task RevokeAllTokensAsync(Guid userId);
    }

    public interface IUserService
    {
        Task<UserProfileDto> GetProfileAsync(Guid userId);
        Task<bool> UpdateDisplayNameAsync(Guid userId, string displayName);
        Task<bool> UpdateLanguageAsync(Guid userId, Language language);
        Task<bool> UpdateSettingsAsync(Guid userId, Dictionary<string, object> settings);
        Task<LoginRewardResult> ClaimDailyLoginAsync(Guid userId);
        Task<VipInfoDto> GetVipInfoAsync(Guid userId);
        Task<bool> DeleteAccountAsync(Guid userId, string password);
        Task<AccountDeletionStatus> GetDeletionStatusAsync(Guid userId);
        Task<bool> RecordConsentAsync(Guid userId, ConsentType type, bool accepted, string version);
        Task<ConsentRecordDto> GetConsentStatusAsync(Guid userId);
        Task<bool> VerifyAgeAsync(Guid userId, bool isAdult);
    }

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

    public enum ConsentType
    {
        TermsOfService = 1,
        PrivacyPolicy = 2,
        DataProcessing = 3,
        Marketing = 4,
        AgeVerification = 5,
        ThirdPartySharing = 6
    }

    public class ConsentRecordDto
    {
        public Guid UserId { get; set; }
        public Dictionary<ConsentType, ConsentEntry> Consents { get; set; } = new();
        public bool IsAgeVerified { get; set; }
        public DateTime? AgeVerifiedAt { get; set; }
        public string PrivacyPolicyVersion { get; set; }
        public string TermsVersion { get; set; }
    }

    public class ConsentEntry
    {
        public bool Accepted { get; set; }
        public DateTime AcceptedAt { get; set; }
        public string Version { get; set; }
        public string IpAddress { get; set; }
    }

    public interface ICharacterService
    {
        Task<CharacterDetailDto> GetCharacterAsync(Guid characterId);
        Task<IReadOnlyList<CharacterSummaryDto>> GetAllCharactersAsync(Rarity? rarity = null, Element? element = null);
        Task<UserCharacterDto> GetUserCharacterAsync(Guid userId, Guid characterId);
        Task<IReadOnlyList<UserCharacterDto>> GetUserCharactersAsync(Guid userId);
        Task<UserCharacterDto> AcquireCharacterAsync(Guid userId, Guid characterId, AcquisitionSource source);
        Task<LevelUpResult> LevelUpCharacterAsync(Guid userId, Guid characterId, int targetLevel, CurrencyAmount cost);
        Task<AscensionResult> AscendCharacterAsync(Guid userId, Guid characterId);
        Task<SkillUpgradeResult> UpgradeSkillAsync(Guid userId, Guid characterId, SkillType skillType);
        Task<LimitBreakResult> LimitBreakAsync(Guid userId, Guid characterId);
        Task<bool> ToggleFavoriteAsync(Guid userId, Guid characterId);
        Task<bool> ToggleLockAsync(Guid userId, Guid characterId);
        Task<CharacterStatsDto> CalculateStatsAsync(Guid userId, Guid characterId);
        Task<AffinityResult> AddAffinityExpAsync(Guid userId, Guid characterId, long amount);
    }

    public interface IGachaService
    {
        Task<IReadOnlyList<GachaBannerDto>> GetAvailableBannersAsync(Guid userId);
        Task<GachaBannerDetailDto> GetBannerDetailAsync(Guid bannerId);
        Task<SummonResult> SummonAsync(Guid userId, Guid bannerId, bool isMulti, CurrencyAmount cost);
        Task<IReadOnlyList<GachaHistoryDto>> GetSummonHistoryAsync(Guid userId, int limit = 50);
        Task<UserGachaPityDto> GetPityStatusAsync(Guid userId, Guid bannerId);
        Task<SummonAnimationData> GetSummonAnimationAsync(Guid bannerId, List<GachaResult> results);
    }

    public interface IBattleService
    {
        Task<BattleStartResult> StartBattleAsync(Guid userId, GameMode mode, int stageId, List<Guid> characterIds, BattleSpeed speed, AutoBattleMode autoMode);
        Task<BattleActionResult> ProcessActionAsync(Guid battleId, BattleAction action);
        Task<BattleActionResult> AutoBattleTurnAsync(Guid battleId);
        Task<BattleEndResult> EndBattleAsync(Guid battleId, bool surrender = false);
        Task<BattleStateDto> GetBattleStateAsync(Guid battleId);
        Task<IReadOnlyList<BattleReplayDto>> GetBattleReplaysAsync(Guid userId, int limit = 10);
    }

    public interface IAutoBattleService
    {
        Task<AutoBattleConfig> GetConfigAsync(Guid userId);
        Task UpdateConfigAsync(Guid userId, AutoBattleConfig config);
        Task<AutoBattleDecision> GetNextActionAsync(BattleStateDto state, List<UserCharacterDto> characters);
        Task<bool> ShouldUseUltimateAsync(BattleUnitDto unit, BattleStateDto state);
        Task<List<Guid>> GetPriorityTargetsAsync(BattleStateDto state, CharacterRole role);
    }

    public interface ICampaignService
    {
        Task<CampaignProgressDto> GetProgressAsync(Guid userId);
        Task<StageDetailDto> GetStageDetailAsync(Guid userId, int stageId);
        Task<StageClearResult> ClearStageAsync(Guid userId, int stageId, BattleResultData result);
        Task<ChapterRewardResult> ClaimChapterRewardAsync(Guid userId, int chapter);
        Task<AutoProgressResult> StartAutoProgressAsync(Guid userId, AutoProgressConfig config);
        Task StopAutoProgressAsync(Guid userId);
        Task<AutoProgressStatus> GetAutoProgressStatusAsync(Guid userId);
    }

    public interface IIdleRewardService
    {
        Task<IdleRewardStatusDto> GetStatusAsync(Guid userId);
        Task<IdleRewardSummary> ClaimRewardsAsync(Guid userId);
        Task<bool> SetAutoClaimAsync(Guid userId, bool enabled);
        Task<bool> SetMaxAccumulationAsync(Guid userId, int hours);
        Task<IdleRewardConfigDto> GetConfigAsync();
    }

    public interface IEquipmentService
    {
        Task<UserEquipmentDto> GetEquipmentAsync(Guid equipmentId);
        Task<IReadOnlyList<UserEquipmentDto>> GetUserEquipmentAsync(Guid userId);
        Task<EnhanceResult> EnhanceEquipmentAsync(Guid userId, Guid equipmentId);
        Task<EquipResult> EquipAsync(Guid userId, Guid characterId, Guid equipmentId, EquipmentSlot slot);
        Task<EquipResult> UnequipAsync(Guid userId, Guid characterId, EquipmentSlot slot);
        Task<SellResult> SellEquipmentAsync(Guid userId, List<Guid> equipmentIds);
        Task<AutoEquipResult> AutoEquipAsync(Guid userId, Guid characterId);
    }

    public interface IRelicService
    {
        Task<UserRelicDto> GetRelicAsync(Guid relicId);
        Task<IReadOnlyList<UserRelicDto>> GetUserRelicsAsync(Guid userId);
        Task<EnhanceResult> EnhanceRelicAsync(Guid userId, Guid relicId);
        Task<EquipResult> EquipAsync(Guid userId, Guid characterId, Guid relicId);
        Task<EquipResult> UnequipAsync(Guid userId, Guid characterId);
    }

    public interface IRuneService
    {
        Task<UserRuneDto> GetRuneAsync(Guid runeId);
        Task<IReadOnlyList<UserRuneDto>> GetUserRunesAsync(Guid userId);
        Task<EnhanceResult> EnhanceRuneAsync(Guid userId, Guid runeId);
        Task<EquipResult> EquipAsync(Guid userId, Guid characterId, Guid runeId, int slot);
        Task<EquipResult> UnequipAsync(Guid userId, Guid characterId, int slot);
    }

    public interface ISkinService
    {
        Task<IReadOnlyList<SkinDto>> GetAvailableSkinsAsync(Guid characterId);
        Task<UserSkinDto> GetUserSkinAsync(Guid userId, Guid skinId);
        Task<IReadOnlyList<UserSkinDto>> GetUserSkinsAsync(Guid userId, Guid characterId);
        Task<PurchaseResult> PurchaseSkinAsync(Guid userId, Guid skinId, CurrencyAmount price);
        Task<EquipResult> EquipSkinAsync(Guid userId, Guid characterId, Guid? skinId);
        Task<DyeResult> DyeSkinAsync(Guid userId, Guid skinId, SkinDyeOption option);
    }

    public interface ITowerService
    {
        Task<TowerProgressDto> GetProgressAsync(Guid userId);
        Task<TowerFloorDto> GetFloorDetailAsync(Guid userId, int floor);
        Task<FloorClearResult> ClearFloorAsync(Guid userId, int floor, BattleResultData result);
        Task<RewardResult> ClaimFloorRewardAsync(Guid userId, int floor);
        Task<RewardResult> ClaimStarRewardAsync(Guid userId, int stars);
        Task<SeasonResetResult> ResetSeasonAsync(Guid userId);
    }

    public interface IArenaService
    {
        Task<ArenaInfoDto> GetArenaInfoAsync(Guid userId);
        Task<IReadOnlyList<ArenaOpponentDto>> GetOpponentsAsync(Guid userId);
        Task<BattleStartResult> StartBattleAsync(Guid userId, Guid opponentId, bool isRevenge);
        Task<ArenaBattleResult> ProcessBattleResultAsync(Guid userId, BattleResultData result);
        Task<RewardResult> ClaimDailyRewardAsync(Guid userId);
        Task<RewardResult> ClaimWeeklyRewardAsync(Guid userId);
        Task<RewardResult> ClaimSeasonRewardAsync(Guid userId);
        Task<RefreshResult> RefreshOpponentsAsync(Guid userId);
        Task<DefenseTeamResult> SetDefenseTeamAsync(Guid userId, List<Guid> characterIds);
    }

    public interface IWorldBossService
    {
        Task<WorldBossInfoDto> GetActiveBossAsync();
        Task<WorldBossProgressDto> GetProgressAsync(Guid userId);
        Task<BattleStartResult> StartBattleAsync(Guid userId, bool useFreeEntry);
        Task<WorldBossBattleResult> SubmitDamageAsync(Guid userId, long damage, BattleResultData result);
        Task<RewardResult> ClaimDamageRewardAsync(Guid userId, int thresholdIndex);
        Task<RewardResult> ClaimParticipationRewardAsync(Guid userId);
        Task<RewardResult> ClaimRankingRewardAsync(Guid userId);
        Task<IReadOnlyList<WorldBossRankingEntry>> GetRankingAsync(int count);
    }

    public interface IDungeonService
    {
        Task<IReadOnlyList<DungeonDto>> GetAvailableDungeonsAsync(Guid userId);
        Task<DungeonDetailDto> GetDungeonDetailAsync(Guid userId, Guid dungeonId);
        Task<DungeonClearResult> ClearDungeonAsync(Guid userId, Guid dungeonId, int tier, BattleResultData result);
        Task<RewardResult> ClaimStarRewardsAsync(Guid userId, Guid dungeonId, int tier);
    }

    public interface IExpeditionService
    {
        Task<IReadOnlyList<ExpeditionDto>> GetAvailableExpeditionsAsync(Guid userId);
        Task<ExpeditionStartResult> StartExpeditionAsync(Guid userId, Guid expeditionId, List<Guid> characterIds, bool autoRepeat, int maxRepeats);
        Task<ExpeditionCompleteResult> CompleteExpeditionAsync(Guid userId, Guid userExpeditionId);
        Task<ClaimResult> ClaimExpeditionRewardsAsync(Guid userId, Guid userExpeditionId);
        Task<IReadOnlyList<UserExpeditionDto>> GetActiveExpeditionsAsync(Guid userId);
        Task<IReadOnlyList<UserExpeditionDto>> GetCompletedExpeditionsAsync(Guid userId);
        Task<bool> CancelExpeditionAsync(Guid userId, Guid userExpeditionId);
    }

    public interface IQuestService
    {
        Task<IReadOnlyList<UserQuestDto>> GetActiveQuestsAsync(Guid userId);
        Task<IReadOnlyList<UserQuestDto>> GetAvailableQuestsAsync(Guid userId);
        Task<QuestAcceptResult> AcceptQuestAsync(Guid userId, Guid questId);
        Task<QuestProgressResult> UpdateProgressAsync(Guid userId, Guid questId, string objectiveId, int amount);
        Task<ClaimResult> ClaimQuestRewardAsync(Guid userId, Guid questId);
        Task<DailyQuestResult> RefreshDailyQuestsAsync(Guid userId);
    }

    public interface IEventService
    {
        Task<IReadOnlyList<GameEventDto>> GetActiveEventsAsync(Guid userId);
        Task<EventProgressDto> GetEventProgressAsync(Guid userId, Guid eventId);
        Task<EventStageClearResult> ClearEventStageAsync(Guid userId, Guid eventId, Guid stageId, BattleResultData result);
        Task<EventQuestProgressResult> UpdateEventQuestProgressAsync(Guid userId, Guid eventId, Guid questId, Dictionary<string, int> progress);
        Task<ClaimResult> ClaimEventQuestRewardAsync(Guid userId, Guid eventId, Guid questId);
        Task<ClaimResult> ClaimMilestoneRewardAsync(Guid userId, Guid eventId, int pointsThreshold);
        Task<ShopPurchaseResult> PurchaseFromEventShopAsync(Guid userId, Guid eventId, Guid shopId, Guid itemId, int quantity);
    }

    public interface IShopService
    {
        Task<IReadOnlyList<ShopDto>> GetAvailableShopsAsync(Guid userId);
        Task<ShopDetailDto> GetShopDetailAsync(Guid userId, Guid shopId);
        Task<ShopPurchaseResult> PurchaseAsync(Guid userId, Guid shopId, Guid itemId, int quantity);
        Task<RefreshResult> RefreshShopAsync(Guid userId, Guid shopId);
    }

    public interface IMonetizationService
    {
        Task<IReadOnlyList<PurchaseProductDto>> GetProductsAsync(Guid userId);
        Task<PurchaseValidationResult> ValidatePurchaseAsync(Guid userId, string productId, string purchaseToken, string receiptData);
        Task<SubscriptionStatus> GetSubscriptionStatusAsync(Guid userId);
        Task<bool> ProcessSubscriptionRenewalAsync(Guid userId, string purchaseToken);
        Task<BattlePassInfoDto> GetBattlePassInfoAsync(Guid userId);
        Task<BattlePassRewardResult> ClaimBattlePassRewardAsync(Guid userId, int tier, bool isPremium);
        Task<PurchaseHistoryDto> GetPurchaseHistoryAsync(Guid userId, int limit = 50);
    }

    public interface INotificationService
    {
        Task<bool> SendPushNotificationAsync(Guid userId, string title, string body, Dictionary<string, string> data);
        Task<bool> ScheduleNotificationAsync(Guid userId, NotificationType type, TimeSpan delay);
        Task<bool> CancelNotificationAsync(Guid userId, NotificationType type);
        Task<NotificationSettingsDto> GetSettingsAsync(Guid userId);
        Task<bool> UpdateSettingsAsync(Guid userId, NotificationSettingsDto settings);
    }

    public interface IAnalyticsService
    {
        Task TrackEventAsync(string eventName, Dictionary<string, object> parameters);
        Task TrackPurchaseAsync(Guid userId, string productId, decimal price, string currency);
        Task TrackProgressionAsync(Guid userId, string milestone, Dictionary<string, object> data);
        Task TrackRetentionAsync(Guid userId, int day);
        Task TrackErrorAsync(string error, Dictionary<string, object> context);
    }

    public interface ILeaderboardService
    {
        Task UpdateScoreAsync(string leaderboardId, Guid userId, long score, Dictionary<string, object> metadata);
        Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string leaderboardId, int count);
        Task<LeaderboardEntry> GetRankAsync(string leaderboardId, Guid userId);
        Task<IReadOnlyList<LeaderboardEntry>> GetAroundAsync(string leaderboardId, Guid userId, int range);
    }

    // DTOs
    public class AuthResult
    {
        public bool Success { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; }
        public string Error { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public AccountStatus Status { get; set; }
        public Language Language { get; set; }
        public int VipLevel { get; set; }
        public SubscriptionTier SubscriptionTier { get; set; }
    }

    public class UserProfileDto : UserDto
    {
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int LoginStreak { get; set; }
        public long TotalPlayTimeSeconds { get; set; }
        public Dictionary<string, object> Settings { get; set; }
    }

    public class LoginRewardResult
    {
        public bool Success { get; set; }
        public List<Reward> Rewards { get; set; } = new();
        public int StreakDay { get; set; }
        public bool IsBonusDay { get; set; }
    }

    public class VipInfoDto
    {
        public int CurrentLevel { get; set; }
        public long CurrentExp { get; set; }
        public long ExpToNextLevel { get; set; }
        public List<VipBenefitDto> Benefits { get; set; } = new();
        public List<VipLevelConfig> NextLevels { get; set; } = new();
    }

    public class VipBenefitDto
    {
        public VipBenefitType Type { get; set; }
        public float Value { get; set; }
        public string Description { get; set; }
        public bool IsUnlocked { get; set; }
    }

    public class AccountDeletionStatus
    {
        public bool IsScheduled { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool CanCancel { get; set; }
    }

    // More DTOs would be defined here...
}