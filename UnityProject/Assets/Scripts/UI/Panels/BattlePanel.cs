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
using GoddessRequiem.Client.Combat;
using GoddessRequiem.Client.Battle;

namespace GoddessRequiem.Client.UI.Panels
{
    public class BattlePanel : UIPanel
    {
        [Header("Battle UI")]
        [SerializeField] private BattleUI _battleUI;

        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI _stageNameText;
        [SerializeField] private TextMeshProUGUI _turnCounterText;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _autoBattleButton;
        [SerializeField] private TextMeshProUGUI _autoBattleText;
        [SerializeField] private Button _speedButton;
        [SerializeField] private TextMeshProUGUI _speedText;

        [Header("Result Overlay")]
        [SerializeField] private GameObject _resultOverlay;
        [SerializeField] private TextMeshProUGUI _resultTitleText;
        [SerializeField] private TextMeshProUGUI _resultDetailsText;
        [SerializeField] private RectTransform _resultRewardsContainer;
        [SerializeField] private GameObject _rewardItemPrefab;
        [SerializeField] private Button _resultNextButton;
        [SerializeField] private Button _resultRetryButton;
        [SerializeField] private Button _resultExitButton;

        [Header("Auto Battle Settings")]
        [SerializeField] private GameObject _autoBattleSettingsPopup;
        [SerializeField] private Button _autoOffBtn;
        [SerializeField] private Button _autoBasicBtn;
        [SerializeField] private Button _autoSmartBtn;
        [SerializeField] private Button _autoFullBtn;

        [Header("Speed Settings")]
        [SerializeField] private GameObject _speedSettingsPopup;
        [SerializeField] private Button _speed1xBtn;
        [SerializeField] private Button _speed2xBtn;
        [SerializeField] private Button _speed3xBtn;

        private BattleSystem _battleSystem;
        private BattleSetup _currentBattleSetup;
        private AutoBattleMode _currentAutoMode = AutoBattleMode.Off;
        private BattleSpeed _currentSpeed = BattleSpeed.Normal;

        protected override async UniTask InitializeComponentsAsync()
        {
            _battleSystem = BattleSystem.Instance;

            SetupButtonListeners();
            SubscribeToEvents();
        }

        private void SetupButtonListeners()
        {
            _pauseButton?.onClick.AddListener(OnPausePressed);
            _autoBattleButton?.onClick.AddListener(OnAutoBattlePressed);
            _speedButton?.onClick.AddListener(OnSpeedPressed);

            _resultNextButton?.onClick.AddListener(OnResultNextPressed);
            _resultRetryButton?.onClick.AddListener(OnResultRetryPressed);
            _resultExitButton?.onClick.AddListener(OnResultExitPressed);

            _autoOffBtn?.onClick.AddListener(() => SetAutoMode(AutoBattleMode.Off));
            _autoBasicBtn?.onClick.AddListener(() => SetAutoMode(AutoBattleMode.Basic));
            _autoSmartBtn?.onClick.AddListener(() => SetAutoMode(AutoBattleMode.Smart));
            _autoFullBtn?.onClick.AddListener(() => SetAutoMode(AutoBattleMode.Full));

            _speed1xBtn?.onClick.AddListener(() => SetSpeed(BattleSpeed.Normal));
            _speed2xBtn?.onClick.AddListener(() => SetSpeed(BattleSpeed.Fast));
            _speed3xBtn?.onClick.AddListener(() => SetSpeed(BattleSpeed.UltraFast));
        }

        private void SubscribeToEvents()
        {
            if (_battleSystem != null)
            {
                _battleSystem.OnBattleStarted += OnBattleStarted;
                _battleSystem.OnTurnStarted += OnTurnStarted;
                _battleSystem.OnActionExecuted += OnActionExecuted;
                _battleSystem.OnBattleEnded += OnBattleEnded;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_battleSystem != null)
            {
                _battleSystem.OnBattleStarted -= OnBattleStarted;
                _battleSystem.OnTurnStarted -= OnTurnStarted;
                _battleSystem.OnActionExecuted -= OnActionExecuted;
                _battleSystem.OnBattleEnded -= OnBattleEnded;
            }
        }

        public async UniTask StartBattleAsync(BattleSetup setup)
        {
            _currentBattleSetup = setup;
            _resultOverlay?.SetActive(false);

            if (_battleUI != null)
            {
                await _battleUI.InitializeAsync(setup.PlayerUnits, setup.EnemyUnits);
            }

            _stageNameText.text = setup.Mode.ToString();
            UpdateAutoBattleUI();
            UpdateSpeedUI();

            var result = await _battleSystem.StartBattleAsync(setup);
        }

        private void OnBattleStarted(BattleSetup setup)
        {
            _turnCounterText.text = "Turno 1";
        }

        private void OnTurnStarted(int turnNumber, BattleUnit activeUnit)
        {
            _turnCounterText.text = $"Turno {turnNumber}";

            if (_battleUI != null)
            {
                _battleUI.UpdateTurnCounter(turnNumber);
                _battleUI.HighlightActiveUnit(activeUnit);
            }
        }

        private void OnActionExecuted(BattleAction action)
        {
            if (_battleUI != null)
            {
                _battleUI.ShowActionResults(action);
            }
        }

        private async void OnBattleEnded(BattleResult result)
        {
            await ShowResultAsync(result);
        }

        private async UniTask ShowResultAsync(BattleResult result)
        {
            _resultOverlay?.SetActive(true);

            bool isVictory = result.Outcome == BattleOutcome.Victory;
            _resultTitleText.text = isVictory ? "VITÓRIA!" : "DERROTA";
            _resultTitleText.color = isVictory ? Color.green : Color.red;

            var details = $"Turnos: {result.Turns}\n";
            if (result.Rewards != null && result.Rewards.Count > 0)
            {
                details += "Recompensas:\n";
                foreach (var reward in result.Rewards)
                {
                    details += $"+{reward.Quantity} {reward.ResourceName}\n";
                }
            }
            _resultDetailsText.text = details;

            PopulateRewards(result.Rewards);

            _resultNextButton.gameObject.SetActive(isVictory && _currentBattleSetup.Mode == GameMode.Campaign);
            _resultRetryButton.gameObject.SetActive(!isVictory);
            _resultExitButton.gameObject.SetActive(true);

            // Animate in
            var canvasGroup = _resultOverlay.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = _resultOverlay.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            await canvasGroup.DOFade(1f, 0.3f).AsyncWaitForCompletion();
        }

        private void PopulateRewards(List<RewardDto> rewards)
        {
            if (_resultRewardsContainer == null || _rewardItemPrefab == null) return;

            foreach (Transform child in _resultRewardsContainer)
            {
                Destroy(child.gameObject);
            }

            if (rewards == null) return;

            foreach (var reward in rewards)
            {
                var item = Instantiate(_rewardItemPrefab, _resultRewardsContainer);
                var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();

                if (reward.Type == RewardType.Currency)
                {
                    if (System.Enum.TryParse<CurrencyType>(reward.ResourceId, out var currencyType))
                        itemUI.SetCurrencyData(currencyType, reward.Quantity);
                }
                else if (reward.Type == RewardType.CharacterFragment)
                {
                    itemUI.SetFragmentData(reward.ResourceId, reward.Quantity);
                }
                else
                {
                    itemUI.SetItemData(reward.ResourceId, reward.Quantity);
                }
            }
        }

        private void OnPausePressed()
        {
            if (_battleUI != null)
            {
                _battleUI.ShowPauseMenu();
            }
        }

        private void OnAutoBattlePressed()
        {
            _autoBattleSettingsPopup?.SetActive(true);
        }

        private void OnSpeedPressed()
        {
            _speedSettingsPopup?.SetActive(true);
        }

        private void SetAutoMode(AutoBattleMode mode)
        {
            _currentAutoMode = mode;
            _battleSystem?.SetAutoBattle(mode != AutoBattleMode.Off, mode);
            UpdateAutoBattleUI();
            _autoBattleSettingsPopup?.SetActive(false);
        }

        private void SetSpeed(BattleSpeed speed)
        {
            _currentSpeed = speed;
            _battleSystem?.SetBattleSpeed(speed);
            UpdateSpeedUI();
            _speedSettingsPopup?.SetActive(false);
        }

        private void UpdateAutoBattleUI()
        {
            if (_autoBattleText != null)
            {
                _autoBattleText.text = _currentAutoMode switch
                {
                    AutoBattleMode.Off => "Auto: OFF",
                    AutoBattleMode.Basic => "Auto: BÁSICO",
                    AutoBattleMode.Smart => "Auto: INTELIGENTE",
                    AutoBattleMode.Full => "Auto: COMPLETO",
                    _ => "Auto: OFF"
                };
            }
        }

        private void UpdateSpeedUI()
        {
            if (_speedText != null)
            {
                _speedText.text = _currentSpeed switch
                {
                    BattleSpeed.Normal => "1x",
                    BattleSpeed.Fast => "2x",
                    BattleSpeed.VeryFast => "3x",
                    _ => "1x"
                };
            }
        }

        private void OnResultNextPressed()
        {
            // Proceed to next stage
            _uiManager?.ClosePanelAsync(this);
            _uiManager?.OpenPanelAsync<UIPanel>("CampaignPanel");
        }

        private async void OnResultRetryPressed()
        {
            _resultOverlay?.SetActive(false);
            await StartBattleAsync(_currentBattleSetup);
        }

        private void OnResultExitPressed()
        {
            _uiManager?.ClosePanelAsync(this);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }
}