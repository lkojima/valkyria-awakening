# Goddess Requiem: Eternal Divas - Project State & Implementation Plan

**Last Updated:** 2026-11-09
**Phase:** MVP (Phase 1) - Core Systems Implementation
**Branch:** main (no git history yet)

---

## 📊 Executive Summary

| Metric | Status |
|--------|--------|
| **Backend Architecture** | ✅ Clean Architecture, EF Core, PostgreSQL, Redis, JWT, Rate Limiting, MediatR, FluentValidation |
| **Unity Architecture** | ✅ Systems Pattern, UniTask, Addressables, Firebase-ready, Netcode for GO |
| **Domain Models** | ✅ Complete (Character, Skill, Equipment, Relic, Rune, Skin, Gacha, Battle, Campaign, Tower, Arena, WorldBoss, Dungeon, Expedition, Quest, Event, Shop, IdleReward, User, Monetization) |
| **Core Services (Backend)** | ✅ Gacha, Battle, AutoBattle, IdleReward, Character, User - **Logic implemented, some stubs remain** |
| **Core Systems (Unity)** | ✅ GameManager, NetworkManager, DataManager, UIManager, CharacterSystem, IdleSystem, BattleSystem, GachaSystem, SaveManager - **Base implementations done** |
| **Missing Critical Visuals** | ✅ SummonAnimation, BattleUI done | ❌ **Panel Prefabs** (requires Unity Editor) |
| **Monetization/IAP** | ⚠️ Entities exist, Services incomplete |
| **LGPD/Account Deletion** | ✅ Complete (DELETE /account, cascade cleanup, consent, age verification) |
| **End-to-End Integration** | ⏳ Scripts ready, waiting for Panel Prefabs |
| **Script Integration (Sprint 2)** | ✅ BattleSystem↔BattleUI, GachaSystem↔SummonAnimation, IdleSystem↔IdleRewardPopupUI, All Systems have Instance pattern |

---

## ✅ What's Already Implemented (Solid Foundation)

### Backend (`.NET 8` - `GoddessRequiem.sln`)
```
GoddessRequiem.Core (Domain + Application)
├── Domain/Entities: 22 entities (Character, Skill, Equipment, Relic, Rune, Skin, UserCharacter, Battle, Gacha, Campaign, Tower, Arena, WorldBoss, Dungeon, Expedition, Quest, Event, Shop, IdleReward, User, UserDevice, UserSession)
├── Domain/Enums: 40+ enums (Rarity, Element, Class, Role, SkillType, BuffType, DebuffType, GameMode, SummonType, CurrencyType, etc.)
├── Domain/ValueObjects: CharacterStats, EquipmentSet, RelicSet, RuneSet, SetBonus, CurrencyAmount, Probability
├── Application/Services: GachaService, BattleService, AutoBattleService, IdleRewardService, CharacterService, UserService (+15 more stubs)
├── Application/Interfaces: IUnitOfWork, IGachaService, IBattleService, IAutoBattleService, IIdleRewardService, ICharacterService, etc.
└── Infrastructure
    ├── Data: GoddessRequiemDbContext (127 DbSets, full config, soft deletes, enum converters, JSON value objects)
    ├── Repositories: BaseRepository, EntityRepositories, UnitOfWork

GoddessRequiem.Api
├── Program.cs: JWT Auth, Rate Limiting (4 policies), CORS, Swagger, Health Checks, Serilog, MediatR, AutoMapper, FluentValidation
├── Controllers: Auth, Character, Gacha, Battle, Campaign, IdleReward, Equipment, Relic, Rune, Skin, Tower, Arena, WorldBoss, Dungeon, Expedition, Quest, Event, Shop, Monetization (17 controllers)
└── Middleware: ErrorHandling, RequestLogging, SecurityHeaders
```

### Unity (2022.3+ URP)
```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs          ✅ Systems orchestrator, state machine, session restore
│   ├── NetworkManager.cs       ✅ REST client, JWT, retry, queue, device login, session persistence
│   ├── DataManager.cs          ✅ Addressables catalog, cache, preload, network fallback
│   ├── UIManager.cs            ✅ Panel stack, popups, toasts, confirm dialogs, Addressables loading
│   ├── SaveManager.cs          ✅ Encrypted JSON saves, backups, memory cache
│   ├── AnalyticsManager.cs     ⚠️ Stub
│   ├── PoolManager.cs          ⚠️ Stub
│   ├── AudioManager.cs         ⚠️ Stub
│   ├── SceneManager.cs         ⚠️ Stub
│   └── NotificationManager.cs  ⚠️ Stub
├── Combat/
│   └── BattleSystem.cs         ✅ Turn-based, speed 1x/2x/3x, auto battle, elemental, status effects, seed-based RNG
├── Gacha/
│   └── GachaSystem.cs          ✅ Banners, summon, pity display, animation data, probability display
├── Idle/
│   └── IdleSystem.cs           ✅ Offline rewards, auto-claim, progress loop, local estimation
├── Character/
│   └── CharacterSystem.cs      ✅ Level up, ascend, skill upgrade, affinity, favorite/lock, asset loading
└── UI/ (Panels - PREFABS MISSING)
    ├── MainMenuPanel, CharacterPanel, BattlePanel, GachaPanel, ShopPanel, CampaignPanel, TowerPanel, ArenaPanel, WorldBossPanel, DungeonPanel, ExpeditionPanel, QuestPanel, EventPanel, SettingsPanel, ProfilePanel
```

---

## ❌ Critical Gaps (Blocking MVP)

| # | Component | Location | Description | Effort | Status |
|---|-----------|----------|-------------|--------|--------|
| **1** | **SummonAnimation** | `UnityProject/Assets/Scripts/Gacha/SummonAnimation.cs` | MonoBehaviour with Timeline/ParticleSystem for gacha reveal. Supports R/SR/SSR/UR variants, guaranteed animation, skip button, callback on complete. | 4-6h | ✅ **DONE** |
| **2** | **BattleUI** | `UnityProject/Assets/Scripts/Combat/BattleUI.cs` | MonoBehaviour: HP bars (player/enemy), turn order UI, skill buttons (basic/special/ultimate), auto battle toggle, speed selector (1x/2x/3x), pause button, action log. | 6-8h | ✅ **DONE** |
| **3** | **UI Panel Prefabs** | `Assets/UI/Panels/*.prefab` | 15 panels referenced in `UIManager.PreloadCommonPrefabsAsync()` - **5 PANEL SCRIPTS + 6 SUB-PREFAB SCRIPTS READY**. Prefabs need creation in Editor. | 8-12h | ⏳ **PENDING** (scripts ready, prefabs need creation in Editor) |
| **4** | **ToastUI / ConfirmDialogUI / IdleRewardPopupUI** | `Assets/Scripts/UI/Components/` | Referenced in `UIManager` - **MISSING**. | 3-4h | ✅ **DONE** |
| **5** | **BattleUnit MonoBehaviour** | `UnityProject/Assets/Scripts/Combat/BattleUnit.cs` | MonoBehaviour: Animator, SpriteRenderer, HP bar, status effect icons, position logic, skill animation triggers. | 3-4h | ✅ **DONE** |
| **6** | **SkillData / EquipmentSetup / RelicSetup / RuneSetup** | `UnityProject/Assets/Scripts/Combat/BattleData.cs` | Serializable classes for battle: SkillData, EquipmentSetup, RelicSetup, RuneSetup, UnitSetup, BattleSetup, BattleResult, BattleAction, ActionResult, RewardDto. | 2-3h | ✅ **DONE** |

---

## ⚠️ Important Gaps (Post-MVP / Phase 2-3)

| Area | Missing Items | Priority |
|------|---------------|----------|
| **Monetization/IAP** | `PurchaseService` (Google Play Billing validation), `MonetizationService` (battle pass, subscription, shop), Receipt verification endpoint | Phase 3 |
| **LGPD/Compliance** | `DELETE /account` endpoint, Data export, Consent management, Privacy policy URLs, Age verification flow | **Legal Required** |
| **Game Modes** | `CampaignService`, `TowerService`, `ArenaService`, `WorldBossService`, `DungeonService`, `ExpeditionService`, `QuestService`, `EventService`, `ShopService` - mostly stubs | Phase 2 |
| **Social/Guild** | Guild entities exist in DB but no services/controllers | Phase 4+ |
| **Push Notifications** | `NotificationService` backend, `NotificationManager` Unity, Firebase Cloud Messaging setup | Phase 2 |
| **Analytics** | `AnalyticsService` backend, `AnalyticsManager` Unity, Event tracking, Funnel analysis | Phase 2 |
| **Leaderboards** | `LeaderboardService` backend (Redis sorted sets) | Phase 2 |
| **LiveOps/Remote Config** | Banner/rate config from server, Feature flags, A/B testing framework | Phase 2 |

---

## 🎯 Implementation Plan - Phase 1 (MVP) - **Next 2-3 Weeks**

### Sprint 1: Visual Core (Week 1) - **BLOCKING**
| Task | File | Description | Depends On | Status |
|------|------|-------------|------------|--------|
| **1.1** | `SummonAnimation.cs` | MonoBehaviour with Timeline/ParticleSystem for gacha reveal. Supports R/SR/SSR/UR variants, guaranteed animation, skip button, callback on complete. | None | ✅ **DONE** |
| **1.2** | `BattleUI.cs` | MonoBehaviour: HP bars (player/enemy), turn order UI, skill buttons (basic/special/ultimate), auto battle toggle, speed selector (1x/2x/3x), pause button, action log. | BattleUnit | ✅ **DONE** |
| **1.3** | `BattleUnit.cs` | MonoBehaviour: Animator, SpriteRenderer, HP bar, status effect icons, position logic, skill animation triggers. | None | ✅ **DONE** |
| **1.4** | `SkillData.cs` + setups | Serializable classes for battle: SkillData, EquipmentSetup, RelicSetup, RuneSetup, UnitSetup. | None | ✅ **DONE** (in BattleData.cs) |
| **1.5** | `ToastUI.cs` / `ConfirmDialogUI.cs` / `IdleRewardPopupUI.cs` | UI components for UIManager. | UIManager | ✅ **DONE** |
| **1.6a** | `CharacterPanel.cs` | Character list with filters, detail view (level/ascension/skills/equipment/relic/runes/affinity/skins), favorite/lock. | CharacterSystem | ✅ **DONE** |
| **1.6b** | `BattlePanel.cs` | Battle wrapper: top bar, pause/auto/speed, result overlay, integrates BattleUI. | BattleUI, BattleSystem | ✅ **DONE** |
| **1.6c** | `GachaPanel.cs` | Banner selection, summon buttons (single/multi/ticket), pity display, history, probability, SummonAnimation integration. | GachaSystem | ✅ **DONE** |
| **1.6d** | `CampaignPanel.cs` | Chapter/stage selection, difficulty tabs, stage info (enemies/rewards), challenge/auto/sweep, chapter rewards. | CampaignSystem | ✅ **DONE** |
| **1.6e** | `ShopPanel.cs` | Category tabs, product grid, purchase confirm, pack details, IAP integration, refresh timer, battle pass/subscription. | ShopSystem, MonetizationSystem | ✅ **DONE** |
| **1.6f** | **Sub-prefab Scripts (6)** | CharacterListItem, StageItem, ProductItem, RewardItem, BannerTab, ChapterTab, CategoryTab - all component scripts for panel prefabs. | UIManager | ✅ **DONE** |
| **1.6g** | **Panel Prefabs (5 critical)** | MainMenuPanel, CharacterPanel, BattlePanel, GachaPanel, CampaignPanel - create prefabs in Editor with Addressable keys. | 1.6f | ⏳ **NEXT** |

### Sprint 2: Integration & Polish (Week 2) - **COMPLETE**
| Task | File | Description | Depends On | Status |
|------|------|-------------|------------|--------|
| **2.1** | `NetworkManager` fixes | Add request deduplication, better error codes, offline queue persistence | Sprint 1 | ✅ **DONE** |
| **2.2** | `DataManager` validation | Verify catalog loading, add version check, cache invalidation | Sprint 1 | ✅ **DONE** |
| **2.3** | `BattleSystem` + `BattleUI` integration | Wire events: OnTurnStart, OnTurnStarted, OnActionExecuted, OnBattleEnded, OnStateChanged → UI updates | 1.2, 1.3 | ✅ **DONE** |
| **2.4** | `GachaSystem` + `SummonAnimation` integration | Wire SummonResult → animation → callback → UI update | 1.1 | ✅ **DONE** |
| **2.5** | `IdleSystem` + UI | IdleRewardPopupUI shows on app resume, progress bar on main menu | 1.5 | ✅ **DONE** |
| **2.6** | Singleton Instances | Added Instance pattern to all systems (BattleSystem, GachaSystem, CharacterSystem, DataManager, PoolManager, NetworkManager, UIManager, IdleSystem) | Sprint 1 | ✅ **DONE** |
| **2.7** | Event Name Fixes | Fixed OnBannersUpdated vs OnBannerUpdated, BattleOutcome enum vs BattleResult | Sprint 1 | ✅ **DONE** |
| **2.8** | End-to-End Test Flow | Login → MainMenu → Gacha (summon) → CharacterPanel → Battle → Idle Claim | All above | ⏳ **NEXT** (after prefabs) |

### Sprint 3: Hardening & Compliance (Week 3)
| Task | File | Description | Depends On | Status |
|------|------|-------------|------------|--------|
| **3.1** | `AuthController.DELETE /account` | Account deletion endpoint with cascade cleanup, GDPR compliance | Backend | ✅ **DONE** |
| **3.2** | `AccountDeletionService` | Orchestrate: revoke tokens, anonymize PII, delete/soft-delete entities, schedule data purge | 3.1 | ✅ **DONE** |
| **3.3** | Privacy Policy / Terms URLs | Add to `GameConfig`, show on first login, store consent timestamp | 3.1 | ✅ **DONE** (GameConfig + GET /config/public) |
| **3.4** | Age Verification Gate | Popup on first launch, store `age_verified` claim in JWT | 3.1 | ✅ **DONE** (VerifyAgeAsync + POST /age-verification) |
| **3.5** | Consent Management | Record consent per type (ToS, Privacy, Marketing, Age), GET /consent | 3.1 | ✅ **DONE** (RecordConsentAsync + POST/GET /consent) |
| **3.6** | Rate Limit Tuning | Adjust policies based on load test: auth=10/min, gacha=50/min, api=100/min | Backend | ⏳ |
| **3.7** | Error Handling Polish | User-friendly error messages, retry logic, offline mode indicators | All | ⏳ |

---

## 📁 File Structure to Create

```
UnityProject/Assets/Scripts/
├── Gacha/
│   └── SummonAnimation.cs          [NEW - CRITICAL]
├── Combat/
│   ├── BattleUI.cs                 [NEW - CRITICAL]
│   ├── BattleUnit.cs               [NEW - CRITICAL]
│   ├── BattleSetup.cs              [EXISTS in BattleSystem - EXTRACT]
│   ├── SkillData.cs                [NEW]
│   ├── EquipmentSetup.cs           [NEW]
│   ├── RelicSetup.cs               [NEW]
│   └── RuneSetup.cs                [NEW]
├── UI/
│   ├── Components/
│   │   ├── ToastUI.cs              [NEW]
│   │   ├── ConfirmDialogUI.cs      [NEW]
│   │   └── IdleRewardPopupUI.cs    [NEW]
│   └── Panels/ (Prefabs - Addressables)
│       ├── MainMenuPanel.prefab
│       ├── CharacterPanel.prefab
│       ├── BattlePanel.prefab
│       ├── GachaPanel.prefab
│       ├── CampaignPanel.prefab
│       └── ... (10 more)
├── Core/
│   └── GameConfig.cs               [EXTRACT from GameManager]
Backend/src/GoddessRequiem.Api/
├── Controllers/
│   └── AccountController.cs        [NEW - DELETE endpoint]
└── Services/
    └── AccountDeletionService.cs   [NEW]
```

---

## 🔧 Technical Decisions & Conventions

| Area | Decision |
|------|----------|
| **Async** | UniTask everywhere (Unity), `async Task` (Backend) |
| **Serialization** | Newtonsoft.Json (Unity), System.Text.Json (Backend) |
| **Addressables** | Keys: `ui/panels/{PanelName}`, `data/{key}`, `characters/{id}/portrait` |
| **RNG** | Seed-based (battle), server-authoritative (gacha), `System.Random` with seed |
| **State** | GameManager.GameState enum, UIManager panel stack |
| **Errors** | `ApiResponse<T>` with Success/Data/Error/Code, user-facing messages in Portuguese |
| **Security** | JWT (15min access, 7d refresh), device ID header, rate limiting, HTTPS only |
| **LGPD** | `age_verified` claim, account deletion cascade, data minimization, consent logs |

---

## 🚀 Next Immediate Actions (Start Here)

### **TASK 1.6g: Create Panel Prefabs in Unity Editor** ⏳ **BLOCKING - REQUIRES UNITY EDITOR**

All 18 scripts are ready. You need to create 12 prefabs in Unity Editor with Addressable labels matching `UIManager.PreloadCommonPrefabsAsync()`:

#### **5 Panel Prefabs** (in `Assets/UI/Panels/`):
| Prefab Name | Script | Addressable Label |
|-------------|--------|-------------------|
| MainMenuPanel.prefab | MainMenuPanel | `ui/panels/MainMenuPanel` |
| CharacterPanel.prefab | CharacterPanel | `ui/panels/CharacterPanel` |
| BattlePanel.prefab | BattlePanel | `ui/panels/BattlePanel` |
| GachaPanel.prefab | GachaPanel | `ui/panels/GachaPanel` |
| CampaignPanel.prefab | CampaignPanel | `ui/panels/CampaignPanel` |

#### **7 Sub-Prefab Components** (in `Assets/UI/Prefabs/`):
| Prefab Name | Script | Addressable Label |
|-------------|--------|-------------------|
| CharacterListItem.prefab | CharacterListItem | `ui/prefabs/CharacterListItem` |
| StageItem.prefab | StageItem | `ui/prefabs/StageItem` |
| ProductItem.prefab | ProductItem | `ui/prefabs/ProductItem` |
| RewardItem.prefab | RewardItem | `ui/prefabs/RewardItem` |
| BannerTab.prefab | BannerTab | `ui/prefabs/BannerTab` |
| ChapterTab.prefab | ChapterTab | `ui/prefabs/ChapterTab` |
| CategoryTab.prefab | CategoryTab | `ui/prefabs/CategoryTab` |

#### **Additional Prefabs for UIManager** (in `Assets/UI/`):
| Prefab Name | Addressable Label |
|-------------|-------------------|
| LoadingScreen.prefab | `ui/LoadingScreen` |
| Popup.prefab | `ui/Popup` |
| Toast.prefab | `ui/Toast` (attach ToastUI) |
| ConfirmDialog.prefab | `ui/ConfirmDialog` (attach ConfirmDialogUI) |
| SummonAnimation.prefab | `ui/SummonAnimation` (attach SummonAnimation) |
| IdleRewardPopup.prefab | `ui/IdleRewardPopup` (attach IdleRewardPopupUI) |

**Steps in Unity Editor:**
1. Right-click `Assets/UI/Panels` → Create → UI → Panel → Rename to match above
2. Attach corresponding script component
3. In Inspector: Check "Addressable" → Set "Address" to the Addressable Label above
4. Assign all serialized references (Text, Buttons, Images, Containers, Prefabs)
5. Repeat for all 12 prefabs
6. Window → Asset Management → Addressables → Build → New Build → Default Build Script

**After prefabs built**, say "Continue from PROJECT_STATE.md" to begin Sprint 2 integration work.

---

## 📝 Context Preservation

**When you resume, tell me:** "Continue from PROJECT_STATE.md"

**I will:**
1. Read this file
2. Check what's been done (git status / file existence)
3. Continue from the next unchecked task

**Current Position:** Sprint 1 **ALL SCRIPTS COMPLETE** (18 scripts: 11 core + 7 sub-prefab components). **BLOCKED ON:** Task 1.6g - Panel Prefab Creation in Unity Editor (requires Unity Editor).

**Sprint 2 Integration - COMPLETE:**
- ✅ BattleSystem + BattleUI: Fixed event wiring (OnTurnStarted, OnBattleStarted, OnTurnStart, OnActionExecuted, OnBattleEnded, OnStateChanged), added ChangeState(), singleton Instance
- ✅ GachaSystem + SummonAnimation: Fixed PlaySummonAnimationAsync via UIManager.ShowSummonAnimationAsync, added singleton Instance, fixed OnBannersUpdated event name
- ✅ IdleSystem + UI: App focus handler triggers IdleRewardPopupUI via UIManager
- ✅ Singleton Pattern: Added to BattleSystem, GachaSystem, CharacterSystem, DataManager, PoolManager, NetworkManager, UIManager, IdleSystem
- ✅ BattleOutcome enum: Fixed naming conflict (BattleResult class vs BattleResult enum)
- ✅ NetworkManager: Request deduplication, offline queue persistence (24h expiry), better error codes, DeleteAccountAsync
- ✅ DataManager: Cache validation, version checking, size limits (100MB), LRU eviction
- ✅ BattlePanel: Fixed SetAutoMode (uses SetAutoBattle), BattleSpeed.VeryFast → UltraFast, BattleOutcome enum

**All Scripts Verified:**
- ✅ SummonAnimation.cs (Gacha reveal with variants, skip, particles)
- ✅ BattleUI.cs (Full battle interface: HP bars, turn order, skills, auto/speed/pause)
- ✅ BattleUnit.cs (MonoBehaviour with stats, status effects, animations, Live2D support)
- ✅ BattleData.cs (All serializable DTOs: SkillData, EquipmentSetup, RelicSetup, RuneSetup, UnitSetup, BattleSetup, BattleResult, etc.)
- ✅ ToastUI.cs (ToastUI, ConfirmDialogUI, IdleRewardPopupUI, RewardItemUI - all in one file)
- ✅ MainMenuPanel.cs (Character display, currency, menu buttons, idle progress)
- ✅ CharacterPanel.cs (List with filters, detail view, level/ascension/skills/equipment/relic/runes/affinity/skins)
- ✅ BattlePanel.cs (Wrapper integrating BattleUI, top bar, pause/auto/speed, result overlay)
- ✅ GachaPanel.cs (Banner selection, summon buttons, pity, history, probability, SummonAnimation)
- ✅ CampaignPanel.cs (Chapter/stage selection, difficulty, stage info, challenge/auto/sweep)
- ✅ ShopPanel.cs (Categories, product grid, purchase confirm, IAP, battle pass)
- ✅ CharacterListItem.cs, StageItem.cs, ProductItem.cs, RewardItem.cs, BannerTab.cs, ChapterTab.cs, CategoryTab.cs (7 sub-prefab components)
- ✅ NetworkManager.cs (Request deduplication, offline queue persistence, error codes, DeleteAccountAsync)
- ✅ DataManager.cs (Cache validation, version checking, size limits, LRU eviction)
- ✅ BattleSystem.cs (Fixed InitializeUnitsAsync to await BattleUI.InitializeAsync, added events + Instance)
- ✅ GachaSystem.cs (SummonAsync overload, UIManager.ShowSummonAnimationAsync integration, Instance)
- ✅ IdleSystem.cs (App focus changed handler for idle reward popup, Instance)
- ✅ UIManager.cs (ShowSummonAnimationAsync, _summonAnimationPrefab reference, Instance)

**Sprint 3 LGPD Compliance - COMPLETE:**
- ✅ IAccountDeletionService + AccountDeletionService (cascade cleanup of 23 entity types)
- ✅ IUserConsentRepository + UserConsentRepository (consent logging per type)
- ✅ IGameConfigRepository + GameConfigRepository (public config: privacy policy URL, terms URL)
- ✅ UserService: RecordConsentAsync, GetConsentStatusAsync, VerifyAgeAsync
- ✅ AuthController: POST /consent, GET /consent, POST /age-verification, GET /config/public
- ✅ UserConsent entity + GameConfig entity
- ✅ 30-day grace period with CancelDeletionAsync
- ✅ Anonymization (LGPD Art. 16) - keeps minimal audit trail

---

## 🧪 Test Checklist (Definition of Done for MVP)

- [ ] App launches → GameManager initializes all systems → MainMenuPanel shows
- [ ] Device login works → JWT stored → Session survives app restart
- [ ] GachaPanel opens → Banners load → Summon (single/multi) → SummonAnimation plays → Results show → Pity updates
- [ ] CharacterPanel opens → List owned characters → Level up / Ascend / Skill Upgrade / Affinity work
- [ ] CampaignPanel → Select stage → BattlePanel loads → BattleSystem runs → BattleUI shows → Auto Battle works → Victory → Rewards granted → Campaign progress saves
- [ ] App background 5min → Foreground → IdleRewardPopup shows → Claim → Resources added
- [ ] Settings → Account → Delete Account → Confirms → Data purged → Returns to login
- [ ] Memory stable 30min play (no leaks in Addressables, pools, caches)
- [ ] Build Android AAB → Installs on device → Runs 60fps on mid-range

---

*Generated by Claude Code on 2026-11-09. Update this file after each session.*