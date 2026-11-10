using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Character;
using GoddessRequiem.Client.Gacha;
using GoddessRequiem.Client.Idle;

namespace GoddessRequiem.Client.UI.Panels
{
    public class MainMenuPanel : UIPanel
    {
        [Header("References")]
        [SerializeField] private RectTransform _characterDisplayRoot;
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _playerLevelText;
        [SerializeField] private Slider _expSlider;
        [SerializeField] private TextMeshProUGUI _energyText;
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _gemText;
        [SerializeField] private TextMeshProUGUI _staminaText;

        [Header("Menu Buttons")]
        [SerializeField] private Button _campaignBtn;
        [SerializeField] private Button _gachaBtn;
        [SerializeField] private Button _charactersBtn;
        [SerializeField] private Button _shopBtn;
        [SerializeField] private Button _questsBtn;
        [SerializeField] private Button _eventsBtn;
        [SerializeField] private Button _arenaBtn;
        [SerializeField] private Button _towerBtn;
        [SerializeField] private Button _worldBossBtn;
        [SerializeField] private Button _dungeonBtn;
        [SerializeField] private Button _expeditionBtn;
        [SerializeField] private Button _settingsBtn;
        [SerializeField] private Button _profileBtn;
        [SerializeField] private Button _mailBtn;
        [SerializeField] private Button _friendsBtn;

        [Header("Idle Progress")]
        [SerializeField] private GameObject _idleProgressContainer;
        [SerializeField] private Slider _idleProgressSlider;
        [SerializeField] private TextMeshProUGUI _idleTimeText;
        [SerializeField] private Button _idleClaimBtn;

        [Header("Character Display")]
        [SerializeField] private Image _mainCharacterImage;
        [SerializeField] private Animator _mainCharacterAnimator;

        private CharacterSystem _characterSystem;
        private IdleSystem _idleSystem;
        private GameObject _currentCharacterModel;

        public override async UniTask ShowAsync()
        {
            await base.ShowAsync();
            await RefreshUIAsync();
            StartIdleProgressUpdates();
        }

        public override async UniTask HideAsync()
        {
            StopIdleProgressUpdates();
            if (_currentCharacterModel != null)
            {
                Addressables.ReleaseInstance(_currentCharacterModel);
                _currentCharacterModel = null;
            }
            await base.HideAsync();
        }

        protected override async UniTask InitializeComponentsAsync()
        {
            _characterSystem = CharacterSystem.Instance;
            _idleSystem = IdleSystem.Instance;

            SetupButtonListeners();
            SubscribeToEvents();

            // Load main character display
            await LoadMainCharacterDisplayAsync();
        }

        private void SetupButtonListeners()
        {
            _campaignBtn?.onClick.AddListener(() => OpenPanel("CampaignPanel"));
            _gachaBtn?.onClick.AddListener(() => OpenPanel("GachaPanel"));
            _charactersBtn?.onClick.AddListener(() => OpenPanel("CharacterPanel"));
            _shopBtn?.onClick.AddListener(() => OpenPanel("ShopPanel"));
            _questsBtn?.onClick.AddListener(() => OpenPanel("QuestPanel"));
            _eventsBtn?.onClick.AddListener(() => OpenPanel("EventPanel"));
            _arenaBtn?.onClick.AddListener(() => OpenPanel("ArenaPanel"));
            _towerBtn?.onClick.AddListener(() => OpenPanel("TowerPanel"));
            _worldBossBtn?.onClick.AddListener(() => OpenPanel("WorldBossPanel"));
            _dungeonBtn?.onClick.AddListener(() => OpenPanel("DungeonPanel"));
            _expeditionBtn?.onClick.AddListener(() => OpenPanel("ExpeditionPanel"));
            _settingsBtn?.onClick.AddListener(() => OpenPopup("SettingsPanel"));
            _profileBtn?.onClick.AddListener(() => OpenPopup("ProfilePanel"));
            _mailBtn?.onClick.AddListener(() => OpenPopup("MailPanel"));
            _friendsBtn?.onClick.AddListener(() => OpenPopup("FriendsPanel"));

            _idleClaimBtn?.onClick.AddListener(OnIdleClaimPressed);
        }

        private void SubscribeToEvents()
        {
            if (_characterSystem != null)
            {
                _characterSystem.OnCharacterUpdated += OnCharacterUpdated;
            }
            if (_idleSystem != null)
            {
                _idleSystem.OnStatusUpdated += OnIdleStatusUpdated;
                _idleSystem.OnRewardsClaimed += OnIdleRewardsClaimed;
                _idleSystem.OnProgressUpdated += OnIdleProgressUpdated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_characterSystem != null)
            {
                _characterSystem.OnCharacterUpdated -= OnCharacterUpdated;
            }
            if (_idleSystem != null)
            {
                _idleSystem.OnStatusUpdated -= OnIdleStatusUpdated;
                _idleSystem.OnRewardsClaimed -= OnIdleRewardsClaimed;
                _idleSystem.OnProgressUpdated -= OnIdleProgressUpdated;
            }
        }

        private async UniTask RefreshUIAsync()
        {
            var user = GameManager.Instance.Network.GetCurrentUser();
            if (user != null)
            {
                _playerNameText.text = user.DisplayName;
                _playerLevelText.text = $"Lv.{user.Level}";
                _expSlider.value = (float)user.CurrentExp / user.ExpToNextLevel;
            }

            UpdateCurrencies();
            await UpdateIdleProgressUI();
        }

        private void UpdateCurrencies()
        {
            var currencies = GameManager.Instance.Data.GetCachedCurrencies();
            if (currencies != null)
            {
                _goldText.text = currencies.GetValueOrDefault(CurrencyType.Gold, 0).ToString("N0");
                _gemText.text = currencies.GetValueOrDefault(CurrencyType.Gem, 0).ToString("N0");
                _staminaText.text = currencies.GetValueOrDefault(CurrencyType.Stamina, 0).ToString("N0");
                _energyText.text = currencies.GetValueOrDefault(CurrencyType.Energy, 0).ToString("N0");
            }
        }

        private async UniTask LoadMainCharacterDisplayAsync()
        {
            var mainChar = _characterSystem?.GetMainDisplayCharacter();
            if (mainChar == null) return;

            var assets = await _characterSystem.LoadCharacterAssetsAsync(mainChar.CharacterId);
            if (assets != null && assets.FullBody != null)
            {
                if (_mainCharacterImage != null)
                {
                    _mainCharacterImage.sprite = assets.FullBody;
                    _mainCharacterImage.SetNativeSize();
                }
            }
            else if (assets?.Live2DModel != null)
            {
                _currentCharacterModel = Instantiate(assets.Live2DModel, _characterDisplayRoot);
                _currentCharacterModel.transform.localPosition = Vector3.zero;
            }
        }

        private async UniTask UpdateIdleProgressUI()
        {
            if (_idleSystem == null) return;

            var status = await _idleSystem.RefreshStatusAsync();
            if (status != null)
            {
                _idleProgressContainer.SetActive(status.HasRewards);
                _idleProgressSlider.value = status.HasRewards ? 1f : _idleSystem.GetProgressPercent();
                _idleTimeText.text = status.HasRewards ? "Disponível!" : _idleSystem.GetTimeUntilNextClaim();
                _idleClaimBtn.interactable = status.HasRewards;
            }
        }

        private void OnCharacterUpdated(Guid characterId)
        {
            _ = RefreshUIAsync();
        }

        private void OnIdleStatusUpdated(IdleRewardStatus status)
        {
            _ = UpdateIdleProgressUI();
        }

        private void OnIdleRewardsClaimed(IdleRewardSummary summary)
        {
            _ = UpdateIdleProgressUI();
            UpdateCurrencies();
        }

        private void OnIdleProgressUpdated(float progress)
        {
            if (_idleProgressSlider != null)
                _idleProgressSlider.value = progress;
        }

        private void OnIdleClaimPressed()
        {
            _idleSystem?.ClaimRewardsAsync();
        }

        private void StartIdleProgressUpdates()
        {
            // Already handled by IdleSystem's periodic loop
        }

        private void StopIdleProgressUpdates()
        {
            UnsubscribeFromEvents();
        }

        private void OpenPanel(string panelName)
        {
            _uiManager?.OpenPanelAsync<UIPanel>(panelName);
        }

        private void OpenPopup(string panelName)
        {
            _uiManager?.OpenPopupAsync<UIPanel>(panelName);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }
}