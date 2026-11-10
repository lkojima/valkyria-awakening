using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DG.Tweening;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Campaign;

namespace GoddessRequiem.Client.UI.Panels
{
    public class CampaignPanel : UIPanel
    {
        [Header("Chapter Selection")]
        [SerializeField] private RectTransform _chapterTabsContainer;
        [SerializeField] private GameObject _chapterTabPrefab;
        [SerializeField] private TMP_Dropdown _chapterDropdown;
        [SerializeField] private Button _prevChapterBtn;
        [SerializeField] private Button _nextChapterBtn;

        [Header("Stage List")]
        [SerializeField] private RectTransform _stageListContainer;
        [SerializeField] private GameObject _stageItemPrefab;
        [SerializeField] private ScrollRect _stageScrollRect;

        [Header("Stage Info (Selected)")]
        [SerializeField] private GameObject _stageInfoPanel;
        [SerializeField] private Image _stageBackgroundImage;
        [SerializeField] private TextMeshProUGUI _stageNameText;
        [SerializeField] private TextMeshProUGUI _stageDescriptionText;
        [SerializeField] private TextMeshProUGUI _recommendedPowerText;
        [SerializeField] private TextMeshProUGUI _staminaCostText;
        [SerializeField] private Image _staminaIcon;
        [SerializeField] private RectTransform _enemyPreviewContainer;
        [SerializeField] private GameObject _enemyPreviewPrefab;
        [SerializeField] private RectTransform _rewardsPreviewContainer;
        [SerializeField] private GameObject _rewardPreviewPrefab;
        [SerializeField] private Button _challengeBtn;
        [SerializeField] private Button _autoBattleBtn;
        [SerializeField] private Button _sweepBtn;
        [SerializeField] private TextMeshProUGUI _sweepCountText;
        [SerializeField] private Slider _sweepCountSlider;

        [Header("Progress")]
        [SerializeField] private Slider _chapterProgressSlider;
        [SerializeField] private TextMeshProUGUI _chapterProgressText;
        [SerializeField] private RectTransform _chapterRewardsContainer;
        [SerializeField] private GameObject _chapterRewardPrefab;

        [Header("Difficulty Tabs")]
        [SerializeField] private Button _normalDifficultyBtn;
        [SerializeField] private Button _hardDifficultyBtn;
        [SerializeField] private Button _hellDifficultyBtn;

        private CampaignSystem _campaignSystem;
        private List<CampaignChapter> _chapters = new();
        private CampaignChapter _selectedChapter;
        private CampaignStage _selectedStage;
        private Difficulty _currentDifficulty = Difficulty.Normal;

        protected override async UniTask InitializeComponentsAsync()
        {
            _campaignSystem = CampaignSystem.Instance;

            SetupButtonListeners();
            SubscribeToEvents();

            await RefreshChaptersAsync();
        }

        private void SetupButtonListeners()
        {
            _prevChapterBtn?.onClick.AddListener(OnPrevChapter);
            _nextChapterBtn?.onClick.AddListener(OnNextChapter);
            _chapterDropdown?.onValueChanged.AddListener(OnChapterDropdownChanged);

            _challengeBtn?.onClick.AddListener(OnChallengePressed);
            _autoBattleBtn?.onClick.AddListener(OnAutoBattlePressed);
            _sweepBtn?.onClick.AddListener(OnSweepPressed);
            _sweepCountSlider?.onValueChanged.AddListener(OnSweepCountChanged);

            _normalDifficultyBtn?.onClick.AddListener(() => SetDifficulty(Difficulty.Normal));
            _hardDifficultyBtn?.onClick.AddListener(() => SetDifficulty(Difficulty.Hard));
            _hellDifficultyBtn?.onClick.AddListener(() => SetDifficulty(Difficulty.Hell));
        }

        private void SubscribeToEvents()
        {
            if (_campaignSystem != null)
            {
                _campaignSystem.OnStageCleared += OnStageCleared;
                _campaignSystem.OnChapterRewardClaimed += OnChapterRewardClaimed;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_campaignSystem != null)
            {
                _campaignSystem.OnStageCleared -= OnStageCleared;
                _campaignSystem.OnChapterRewardClaimed -= OnChapterRewardClaimed;
            }
        }

        public override async UniTask ShowAsync()
        {
            await base.ShowAsync();
            await RefreshChaptersAsync();
        }

        private async UniTask RefreshChaptersAsync()
        {
            _chapters = await _campaignSystem.GetChaptersAsync();
            if (_chapters == null || _chapters.Count == 0) return;

            // Setup dropdown
            if (_chapterDropdown != null)
            {
                _chapterDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var chapter in _chapters)
                {
                    options.Add($"Capítulo {chapter.ChapterNumber}: {chapter.Name}");
                }
                _chapterDropdown.AddOptions(options);
            }

            // Setup chapter tabs
            SetupChapterTabs();

            // Select first available chapter
            var firstUnlocked = _chapters.Find(c => c.IsUnlocked);
            if (firstUnlocked != null)
            {
                await SelectChapterAsync(firstUnlocked);
            }
            else if (_chapters.Count > 0)
            {
                await SelectChapterAsync(_chapters[0]);
            }
        }

        private void SetupChapterTabs()
        {
            if (_chapterTabsContainer == null || _chapterTabPrefab == null) return;

            foreach (Transform child in _chapterTabsContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var chapter in _chapters)
            {
                var tab = Instantiate(_chapterTabPrefab, _chapterTabsContainer);
                var btn = tab.GetComponent<Button>();
                var text = tab.GetComponentInChildren<TextMeshProUGUI>();
                var lockIcon = tab.transform.Find("LockIcon")?.gameObject;
                var starContainer = tab.transform.Find("Stars")?.gameObject;

                if (text != null) text.text = chapter.ChapterNumber.ToString();
                if (lockIcon != null) lockIcon.SetActive(!chapter.IsUnlocked);
                if (starContainer != null)
                {
                    var stars = starContainer.GetComponentsInChildren<Image>();
                    for (int i = 0; i < stars.Length; i++)
                    {
                        stars[i].color = i < chapter.StarsEarned ? Color.yellow : Color.gray;
                    }
                }

                if (btn != null)
                {
                    var chapterCopy = chapter;
                    btn.onClick.AddListener(() => _ = SelectChapterAsync(chapterCopy));
                    btn.interactable = chapter.IsUnlocked;
                }
            }
        }

        private async UniTask SelectChapterAsync(CampaignChapter chapter)
        {
            _selectedChapter = chapter;

            if (_chapterDropdown != null)
            {
                _chapterDropdown.value = _chapters.IndexOf(chapter);
            }

            UpdateChapterProgress();
            UpdateChapterRewards();
            await RefreshStagesAsync();
        }

        private async UniTask RefreshStagesAsync()
        {
            if (_selectedChapter == null) return;

            var stages = await _campaignSystem.GetStagesAsync(_selectedChapter.ChapterId, _currentDifficulty);
            if (stages == null) return;

            if (_stageListContainer == null || _stageItemPrefab == null) return;

            foreach (Transform child in _stageListContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var stage in stages)
            {
                var item = Instantiate(_stageItemPrefab, _stageListContainer);
                var btn = item.GetComponent<Button>();
                var nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                var starContainer = item.transform.Find("Stars")?.gameObject;
                var lockIcon = item.transform.Find("LockIcon")?.gameObject;
                var completedIcon = item.transform.Find("CompletedIcon")?.gameObject;
                var recommendedPowerText = item.transform.Find("RecommendedPowerText")?.GetComponent<TextMeshProUGUI>();

                if (nameText != null) nameText.text = $"{stage.StageNumber}. {stage.Name}";
                if (recommendedPowerText != null) recommendedPowerText.text = $"Poder: {stage.RecommendedPower:N0}";
                if (lockIcon != null) lockIcon.SetActive(!stage.IsUnlocked);
                if (completedIcon != null) completedIcon.SetActive(stage.IsCompleted);

                if (starContainer != null)
                {
                    var stars = starContainer.GetComponentsInChildren<Image>();
                    for (int i = 0; i < stars.Length; i++)
                    {
                        stars[i].color = i < stage.StarsEarned ? Color.yellow : Color.gray;
                    }
                }

                if (btn != null)
                {
                    var stageCopy = stage;
                    btn.onClick.AddListener(() => SelectStage(stageCopy));
                    btn.interactable = stage.IsUnlocked;
                }
            }
        }

        private void SelectStage(CampaignStage stage)
        {
            _selectedStage = stage;
            _stageInfoPanel?.SetActive(true);
            UpdateStageInfo(stage);
        }

        private void UpdateStageInfo(CampaignStage stage)
        {
            if (_stageNameText != null) _stageNameText.text = $"{stage.StageNumber}. {stage.Name}";
            if (_stageDescriptionText != null) _stageDescriptionText.text = stage.Description;
            if (_recommendedPowerText != null) _recommendedPowerText.text = $"Poder Recomendado: {stage.RecommendedPower:N0}";
            if (_staminaCostText != null) _staminaCostText.text = stage.StaminaCost.ToString();

            // Load background
            if (_stageBackgroundImage != null && !string.IsNullOrEmpty(stage.BackgroundAssetPath))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(stage.BackgroundAssetPath);
                handle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded)
                        _stageBackgroundImage.sprite = op.Result;
                };
            }

            // Enemy preview
            UpdateEnemyPreview(stage);

            // Rewards preview
            UpdateRewardsPreview(stage);

            // Buttons
            bool canChallenge = stage.IsUnlocked;
            bool canSweep = stage.IsCompleted && stage.StarsEarned == 3;

            _challengeBtn?.gameObject.SetActive(canChallenge);
            _autoBattleBtn?.gameObject.SetActive(canChallenge);
            _sweepBtn?.gameObject.SetActive(canSweep);
            _sweepCountSlider?.gameObject.SetActive(canSweep);
            _sweepCountText?.gameObject.SetActive(canSweep);

            if (_sweepCountSlider != null)
            {
                _sweepCountSlider.maxValue = 10;
                _sweepCountSlider.value = 1;
                _sweepCountText.text = "1x";
            }
        }

        private void UpdateEnemyPreview(CampaignStage stage)
        {
            if (_enemyPreviewContainer == null || _enemyPreviewPrefab == null) return;

            foreach (Transform child in _enemyPreviewContainer)
            {
                Destroy(child.gameObject);
            }

            if (stage.EnemyPreview == null) return;

            foreach (var enemy in stage.EnemyPreview)
            {
                var item = Instantiate(_enemyPreviewPrefab, _enemyPreviewContainer);
                var nameText = item.GetComponentInChildren<TextMeshProUGUI>();
                var levelText = item.transform.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
                var elementIcon = item.transform.Find("ElementIcon")?.GetComponent<Image>();

                if (nameText != null) nameText.text = enemy.Name;
                if (levelText != null) levelText.text = $"Lv.{enemy.Level}";
                if (elementIcon != null) elementIcon.color = GetElementColor(enemy.Element);
            }
        }

        private void UpdateRewardsPreview(CampaignStage stage)
        {
            if (_rewardsPreviewContainer == null || _rewardPreviewPrefab == null) return;

            foreach (Transform child in _rewardsPreviewContainer)
            {
                Destroy(child.gameObject);
            }

            if (stage.FirstClearRewards != null)
            {
                foreach (var reward in stage.FirstClearRewards)
                {
                    var item = Instantiate(_rewardPreviewPrefab, _rewardsPreviewContainer);
                    var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();

                    if (reward.Type == RewardType.Currency)
                    {
                        if (System.Enum.TryParse<CurrencyType>(reward.ResourceId, out var currencyType))
                            itemUI.SetCurrencyData(currencyType, reward.Quantity);
                    }
                    else
                    {
                        itemUI.SetItemData(reward.ResourceId, reward.Quantity);
                    }

                    var firstClearLabel = item.transform.Find("FirstClearLabel")?.GetComponent<TextMeshProUGUI>();
                    if (firstClearLabel != null) firstClearLabel.text = "Primeira Vez";
                }
            }

            if (stage.DropRewards != null)
            {
                foreach (var reward in stage.DropRewards)
                {
                    var item = Instantiate(_rewardPreviewPrefab, _rewardsPreviewContainer);
                    var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();

                    if (reward.Type == RewardType.Currency)
                    {
                        if (System.Enum.TryParse<CurrencyType>(reward.ResourceId, out var currencyType))
                            itemUI.SetCurrencyData(currencyType, reward.Quantity);
                    }
                    else
                    {
                        itemUI.SetItemData(reward.ResourceId, reward.Quantity);
                    }

                    var dropRateLabel = item.transform.Find("DropRateLabel")?.GetComponent<TextMeshProUGUI>();
                    if (dropRateLabel != null) dropRateLabel.text = $"{reward.DropRate:P0}";
                }
            }
        }

        private void UpdateChapterProgress()
        {
            if (_selectedChapter == null) return;

            int completedStages = _selectedChapter.Stages?.FindAll(s => s.IsCompleted).Count ?? 0;
            int totalStages = _selectedChapter.Stages?.Count ?? 0;

            if (_chapterProgressSlider != null)
            {
                _chapterProgressSlider.maxValue = totalStages;
                _chapterProgressSlider.value = completedStages;
            }
            if (_chapterProgressText != null)
            {
                _chapterProgressText.text = $"{completedStages} / {totalStages} estágios";
            }
        }

        private void UpdateChapterRewards()
        {
            if (_selectedChapter == null || _chapterRewardsContainer == null || _chapterRewardPrefab == null) return;

            foreach (Transform child in _chapterRewardsContainer)
            {
                Destroy(child.gameObject);
            }

            if (_selectedChapter.ChapterRewards == null) return;

            foreach (var reward in _selectedChapter.ChapterRewards)
            {
                var item = Instantiate(_chapterRewardPrefab, _chapterRewardsContainer);
                var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();

                if (reward.Type == RewardType.Currency)
                {
                    if (System.Enum.TryParse<CurrencyType>(reward.ResourceId, out var currencyType))
                        itemUI.SetCurrencyData(currencyType, reward.Quantity);
                }
                else
                {
                    itemUI.SetItemData(reward.ResourceId, reward.Quantity);
                }

                var claimBtn = item.transform.Find("ClaimButton")?.GetComponent<Button>();
                var claimedIcon = item.transform.Find("ClaimedIcon")?.gameObject;

                if (claimBtn != null)
                {
                    claimBtn.interactable = !reward.IsClaimed && _selectedChapter.StarsEarned >= reward.RequiredStars;
                    claimBtn.onClick.AddListener(async () => await _campaignSystem.ClaimChapterRewardAsync(_selectedChapter.ChapterId, reward.RewardId));
                }
                if (claimedIcon != null) claimedIcon.SetActive(reward.IsClaimed);
            }
        }

        private void SetDifficulty(Difficulty difficulty)
        {
            _currentDifficulty = difficulty;
            UpdateDifficultyButtons();
            _ = RefreshStagesAsync();
        }

        private void UpdateDifficultyButtons()
        {
            _normalDifficultyBtn?.GetComponent<Image>().color = _currentDifficulty == Difficulty.Normal ? Color.yellow : Color.white;
            _hardDifficultyBtn?.GetComponent<Image>().color = _currentDifficulty == Difficulty.Hard ? Color.yellow : Color.white;
            _hellDifficultyBtn?.GetComponent<Image>().color = _currentDifficulty == Difficulty.Hell ? Color.yellow : Color.white;
        }

        private void OnPrevChapter()
        {
            if (_selectedChapter == null) return;
            int index = _chapters.IndexOf(_selectedChapter);
            if (index > 0)
            {
                _ = SelectChapterAsync(_chapters[index - 1]);
            }
        }

        private void OnNextChapter()
        {
            if (_selectedChapter == null) return;
            int index = _chapters.IndexOf(_selectedChapter);
            if (index < _chapters.Count - 1)
            {
                _ = SelectChapterAsync(_chapters[index + 1]);
            }
        }

        private void OnChapterDropdownChanged(int index)
        {
            if (index >= 0 && index < _chapters.Count)
            {
                _ = SelectChapterAsync(_chapters[index]);
            }
        }

        private async void OnChallengePressed()
        {
            if (_selectedStage == null) return;

            var canAfford = GameManager.Instance.Data.CanAffordCurrency(CurrencyType.Stamina, _selectedStage.StaminaCost);
            if (!canAfford)
            {
                UIManager.Instance.ShowToastAsync("Estamina insuficiente!", 2f, ToastType.Error);
                return;
            }

            var setup = await _campaignSystem.CreateBattleSetupAsync(_selectedStage.StageId, _currentDifficulty);
            if (setup != null)
            {
                setup.AutoMode = AutoBattleMode.Off;
                _uiManager?.OpenPanelAsync<BattlePanel>(panel => panel.StartBattleAsync(setup));
            }
        }

        private async void OnAutoBattlePressed()
        {
            if (_selectedStage == null) return;

            var canAfford = GameManager.Instance.Data.CanAffordCurrency(CurrencyType.Stamina, _selectedStage.StaminaCost);
            if (!canAfford)
            {
                UIManager.Instance.ShowToastAsync("Estamina insuficiente!", 2f, ToastType.Error);
                return;
            }

            var setup = await _campaignSystem.CreateBattleSetupAsync(_selectedStage.StageId, _currentDifficulty);
            if (setup != null)
            {
                setup.AutoMode = AutoBattleMode.Smart;
                _uiManager?.OpenPanelAsync<BattlePanel>(panel => panel.StartBattleAsync(setup));
            }
        }

        private async void OnSweepPressed()
        {
            if (_selectedStage == null) return;

            int count = Mathf.RoundToInt(_sweepCountSlider?.value ?? 1);
            var result = await _campaignSystem.SweepStageAsync(_selectedStage.StageId, _currentDifficulty, count);
            if (result != null)
            {
                UIManager.Instance.ShowToastAsync($"Varredura completa! {result.Rewards.Count} recompensas obtidas.", 2f, ToastType.Success);
                _ = RefreshStagesAsync();
            }
        }

        private void OnSweepCountChanged(float value)
        {
            if (_sweepCountText != null)
            {
                _sweepCountText.text = $"{Mathf.RoundToInt(value)}x";
            }
        }

        private void OnStageCleared(Guid stageId, int starsEarned)
        {
            _ = RefreshStagesAsync();
            UpdateChapterProgress();
        }

        private void OnChapterRewardClaimed(Guid chapterId, Guid rewardId)
        {
            UpdateChapterRewards();
        }

        private Color GetElementColor(Element element)
        {
            return element switch
            {
                Element.Fire => Color.red,
                Element.Water => Color.blue,
                Element.Nature => Color.green,
                Element.Light => Color.yellow,
                Element.Dark => new Color(0.5f, 0f, 1f),
                _ => Color.white
            };
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }
}