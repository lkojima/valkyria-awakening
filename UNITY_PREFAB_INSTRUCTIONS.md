# Unity Prefab Creation Instructions

## ⚠️ CRITICAL: Must be done in Unity Editor

All 18 scripts are ready. You need to create **12 prefabs** with correct Addressable labels.

---

## Prefabs to Create (12 Total)

### 5 Panel Prefabs → `Assets/UI/Panels/`
| Prefab Name | Script | Addressable Label |
|-------------|--------|-------------------|
| `MainMenuPanel.prefab` | `MainMenuPanel` | `ui/panels/MainMenuPanel` |
| `CharacterPanel.prefab` | `CharacterPanel` | `ui/panels/CharacterPanel` |
| `BattlePanel.prefab` | `BattlePanel` | `ui/panels/BattlePanel` |
| `GachaPanel.prefab` | `GachaPanel` | `ui/panels/GachaPanel` |
| `CampaignPanel.prefab` | `CampaignPanel` | `ui/panels/CampaignPanel` |

### 7 Sub-Prefab Components → `Assets/UI/Prefabs/`
| Prefab Name | Script | Addressable Label |
|-------------|--------|-------------------|
| `CharacterListItem.prefab` | `CharacterListItem` | `ui/prefabs/CharacterListItem` |
| `StageItem.prefab` | `StageItem` | `ui/prefabs/StageItem` |
| `ProductItem.prefab` | `ProductItem` | `ui/prefabs/ProductItem` |
| `RewardItem.prefab` | `RewardItem` | `ui/prefabs/RewardItem` |
| `BannerTab.prefab` | `BannerTab` | `ui/prefabs/BannerTab` |
| `ChapterTab.prefab` | `ChapterTab` | `ui/prefabs/ChapterTab` |
| `CategoryTab.prefab` | `CategoryTab` | `ui/prefabs/CategoryTab` |

---

## Step-by-Step in Unity Editor

### 1. Create Folder Structure
```
Assets/
├── UI/
│   ├── Panels/      (for 5 panel prefabs)
│   └── Prefabs/     (for 7 sub-prefab components)
```

### 2. For Each Prefab:
1. Right-click folder → **Create → UI → Panel**
2. Rename to exact name above (e.g., `MainMenuPanel`)
3. **Add Component** → Search for matching script (e.g., `MainMenuPanel`)
4. In Inspector, **set all serialized references**:
   - Buttons, Texts, Images, ScrollViews, Grids
   - Child prefab references (e.g., MainMenuPanel needs CharacterListItem prefab reference)
5. With prefab selected → **Inspector → Addressables** → **New Label** → Enter exact label (e.g., `ui/panels/MainMenuPanel`)
6. **Save as Prefab** (drag to folder)

### 3. Critical Serialized References to Assign:

**MainMenuPanel** needs:
- `_characterDisplay` (RectTransform for character sprite/Live2D)
- `_playerNameText`, `_playerLevelText`, `_vipLevelText`
- `_gemText`, `_goldText`, `_staminaText`, `_energyText`, `_ticketText`
- `_idleProgressBar`, `_idleTimeText`, `_claimIdleButton`
- 14 menu buttons: `_gachaButton`, `_characterButton`, `_campaignButton`, `_towerButton`, `_arenaButton`, `_worldBossButton`, `_dungeonButton`, `_expeditionButton`, `_questButton`, `_eventButton`, `_shopButton`, `_settingsButton`, `_profileButton`, `_mailButton`
- `_characterListItemPrefab` → drag `CharacterListItem` prefab here

**CharacterPanel** needs:
- `_characterListParent` (Transform for grid)
- `_searchInput` (TMP_InputField)
- Filter dropdowns: `_rarityFilter`, `_elementFilter`, `_classFilter`
- `_characterListItemPrefab` → drag `CharacterListItem` prefab
- Detail view: `_detailRoot`, `_portraitImage`, `_nameText`, `_rarityText`, `_elementText`, `_classText`, `_levelText`, `_ascensionText`, `_expSlider`, `_expText`, `_hpText`, `_atkText`, `_defText`, `_spdText`, `_critRateText`, `_critDmgText`
- Skill slots array: `_skillSlots[4]` (each has icon, name, level, upgrade button)
- Equipment slots: `_equipmentSlots[6]`
- Relic slot: `_relicSlot`
- Rune slots: `_runeSlots[4]`
- Affinity: `_affinitySlider`, `_affinityLevelText`, `_affinityExpText`
- Buttons: `_favoriteButton`, `_lockButton`, `_skinButton`, `_closeButton`

**BattlePanel** needs:
- `_battleUI` (reference to BattleUI component on same GameObject)
- Top bar: `_turnCounterText`, `_autoBattleDropdown`, `_speedDropdown`, `_pauseButton`
- `_resultOverlay` (GameObject for victory/defeat)
- Result texts: `_resultTitleText`, `_rewardsText`, `_continueButton`, `_retryButton`

**GachaPanel** needs:
- Banner tabs parent: `_bannerTabsParent`
- `_bannerTabPrefab` → drag `BannerTab` prefab
- Summon buttons: `_singleSummonButton`, `_multiSummonButton`, `_ticketSummonButton`
- Currency costs: `_singleCostText`, `_multiCostText`
- Pity display: `_pityCounterText`, `_pityProgressBar`, `_pityInfoText`
- History: `_historyButton`, `_historyPanel`, `_historyItemPrefab`
- Probability: `_probabilityButton`, `_probabilityPanel`
- `_summonAnimationPrefab` → drag `SummonAnimation` prefab (from Gacha folder)

**CampaignPanel** needs:
- Chapter tabs: `_chapterTabsParent`, `_chapterTabPrefab` → `ChapterTab`
- Stage list: `_stageListParent`, `_stageItemPrefab` → `StageItem`
- Difficulty tabs: `_difficultyTabs` (Normal/Hard/VeryHard)
- Stage info: `_stageInfoPanel`, `_enemyListParent`, `_rewardListParent`
- Buttons: `_challengeButton`, `_autoButton`, `_sweepButton`
- Chapter rewards: `_chapterRewardsPanel`, `_claimChapterRewardButton`

**ShopPanel** needs:
- Category tabs: `_categoryTabsParent`, `_categoryTabPrefab` → `CategoryTab`
- Product grid: `_productGridParent`, `_productItemPrefab` → `ProductItem`
- Refresh timer: `_refreshTimerText`, `_refreshButton`
- Pack detail: `_packDetailPanel`
- Battle pass: `_battlePassButton`
- Subscription: `_subscriptionButton`

**Sub-prefabs** (CharacterListItem, StageItem, ProductItem, RewardItem, BannerTab, ChapterTab, CategoryTab):
- Each needs their UI references assigned per their script's `[SerializeField]` fields
- Keep them simple: Image + Text + Button as needed

---

## 4. Build Addressables
1. **Window → Asset Management → Addressables → Groups**
2. Verify all 12 prefabs show with correct labels
3. **Build → New Build → Default Build Script**
4. Wait for completion → Check `Assets/AddressableAssetsData/` has bundles

---

## 5. Verify in UIManager
The `UIManager.PreloadCommonPrefabsAsync()` loads these labels:
```csharp
"MainMenuPanel", "CharacterPanel", "BattlePanel", "GachaPanel", 
"ShopPanel", "CampaignPanel", "TowerPanel", "ArenaPanel", 
"WorldBossPanel", "DungeonPanel", "ExpeditionPanel", "QuestPanel", 
"EventPanel", "SettingsPanel", "ProfilePanel"
```

**Only the first 6 are critical for MVP.** The other 9 can be empty placeholder panels for now.

---

## After Prefabs Built
Run the game → Should reach MainMenuPanel without errors.
Then say: **"Continue from PROJECT_STATE.md"** to begin Sprint 2 integration.