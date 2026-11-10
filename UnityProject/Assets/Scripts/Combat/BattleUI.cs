using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DG.Tweening;
using GoddessRequiem.Client.Combat;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Combat
{
    public class BattleUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BattleUIConfig _config;

        [Header("Root References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _uiRoot;

        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI _turnCounterText;
        [SerializeField] private TextMeshProUGUI _stageNameText;
        [SerializeField] private Slider _battleSpeedSlider;
        [SerializeField] private TextMeshProUGUI _speedLabelText;

        [Header("Auto Battle Controls")]
        [SerializeField] private GameObject _autoBattlePanel;
        [SerializeField] private Button _autoBattleToggleBtn;
        [SerializeField] private TextMeshProUGUI _autoBattleLabelText;
        [SerializeField] private Button _autoBattleModeBtn;
        [SerializeField] private TextMeshProUGUI _autoModeLabelText;

        [Header("Speed Controls")]
        [SerializeField] private Button _speed1xBtn;
        [SerializeField] private Button _speed2xBtn;
        [SerializeField] private Button _speed3xBtn;

        [Header("Pause Menu")]
        [SerializeField] private Button _pauseBtn;
        [SerializeField] private GameObject _pauseMenuPanel;
        [SerializeField] private Button _resumeBtn;
        [SerializeField] private Button _quitBattleBtn;
        [SerializeField] private Button _settingsBtn;

        [Header("Player Units Area")]
        [SerializeField] private RectTransform _playerUnitsContainer;
        [SerializeField] private GameObject _unitFramePrefab;

        [Header("Enemy Units Area")]
        [SerializeField] private RectTransform _enemyUnitsContainer;
        [SerializeField] private GameObject _enemyFramePrefab;

        [Header("Turn Order")]
        [SerializeField] private RectTransform _turnOrderContainer;
        [SerializeField] private GameObject _turnOrderIconPrefab;

        [Header("Active Unit Highlight")]
        [SerializeField] private GameObject _activeUnitIndicator;

        [Header("Action Preview")]
        [SerializeField] private GameObject _actionPreviewPanel;
        [SerializeField] private TextMeshProUGUI _actionPreviewSkillName;
        [SerializeField] private TextMeshProUGUI _actionPreviewTargetInfo;
        [SerializeField] private Image _actionPreviewSkillIcon;

        [Header("Action Results")]
        [SerializeField] private GameObject _damageNumberPrefab;
        [SerializeField] private GameObject _healNumberPrefab;
        [SerializeField] private GameObject _missTextPrefab;
        [SerializeField] private GameObject _critEffectPrefab;
        [SerializeField] private RectTransform _floatingTextContainer;

        [Header("Battle Result")]
        [SerializeField] private GameObject _battleResultPanel;
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private GameObject _defeatPanel;
        [SerializeField] private GameObject _drawPanel;
        [SerializeField] private TextMeshProUGUI _resultTurnsText;
        [SerializeField] private RectTransform _rewardsContainer;
        [SerializeField] private GameObject _rewardItemPrefab;
        [SerializeField] private Button _resultContinueBtn;

        [Header("Skill Buttons (Manual Mode)")]
        [SerializeField] private RectTransform _skillButtonsContainer;
        [SerializeField] private GameObject _skillButtonPrefab;
        [SerializeField] private Button _basicAttackBtn;
        [SerializeField] private Button _specialSkill1Btn;
        [SerializeField] private Button _specialSkill2Btn;
        [SerializeField] private Button _ultimateSkillBtn;

        [Header("Target Selection")]
        [SerializeField] private GameObject _targetSelectionPanel;
        [SerializeField] private TextMeshProUGUI _targetInstructionText;
        [SerializeField] private Button _cancelTargetBtn;

        // State
        private BattleSystem _battleSystem;
        private List<BattleUnitUI> _playerUnitUIs = new();
        private List<BattleUnitUI> _enemyUnitUIs = new();
        private List<TurnOrderIcon> _turnOrderIcons = new();
        private BattleUnit _activeUnit;
        private BattleSpeed _currentSpeed = BattleSpeed.Normal;
        private AutoBattleMode _currentAutoMode = AutoBattleMode.Off;
        private bool _isAutoBattle = false;
        private bool _isPaused = false;
        private bool _isTargeting = false;
        private SkillData _selectedSkill;
        private System.Action<BattleAction> _onActionSelected;
        private System.Action _onPauseCallback;
        private System.Action _onResumeCallback;
        private System.Action _onQuitCallback;

        public async UniTask InitializeAsync(List<BattleUnit> playerUnits, List<BattleUnit> enemyUnits)
        {
            Initialize(playerUnits, enemyUnits);
            await UniTask.Yield(); // Allow one frame for UI setup
        }

        public void Initialize(List<BattleUnit> playerUnits, List<BattleUnit> enemyUnits)
        {
            _battleSystem = BattleSystem.Instance;
            _currentSpeed = _battleSystem._battleSpeed;
            _currentAutoMode = _battleSystem._autoMode;
            _isAutoBattle = _battleSystem._isAutoBattle;

            CreateUnitFrames(playerUnits, enemyUnits);
            CreateTurnOrderIcons(playerUnits, enemyUnits);
            SetupEventHandlers();
            UpdateSpeedUI();
            UpdateAutoBattleUI();
            UpdateSkillButtonsInteractable();

            _pauseMenuPanel?.SetActive(false);
            _battleResultPanel?.SetActive(false);
            _actionPreviewPanel?.SetActive(false);
            _targetSelectionPanel?.SetActive(false);
            _autoBattlePanel?.SetActive(true);

            _canvas.worldCamera = Camera.main;
        }

        private void CreateUnitFrames(List<BattleUnit> playerUnits, List<BattleUnit> enemyUnits)
        {
            // Clear existing
            foreach (var ui in _playerUnitUIs) if (ui != null) Destroy(ui.gameObject);
            foreach (var ui in _enemyUnitUIs) if (ui != null) Destroy(ui.gameObject);
            _playerUnitUIs.Clear();
            _enemyUnitUIs.Clear();

            // Player units (bottom)
            for (int i = 0; i < playerUnits.Count; i++)
            {
                var unit = playerUnits[i];
                var frame = Instantiate(_unitFramePrefab, _playerUnitsContainer);
                var unitUI = frame.GetComponent<BattleUnitUI>() ?? frame.AddComponent<BattleUnitUI>();
                unitUI.Initialize(unit, true, i, _config);
                _playerUnitUIs.Add(unitUI);
            }

            // Enemy units (top)
            for (int i = 0; i < enemyUnits.Count; i++)
            {
                var unit = enemyUnits[i];
                var frame = Instantiate(_enemyFramePrefab, _enemyUnitsContainer);
                var unitUI = frame.GetComponent<BattleUnitUI>() ?? frame.AddComponent<BattleUnitUI>();
                unitUI.Initialize(unit, false, i, _config);
                _enemyUnitUIs.Add(unitUI);
            }
        }

        private void CreateTurnOrderIcons(List<BattleUnit> playerUnits, List<BattleUnit> enemyUnits)
        {
            foreach (var icon in _turnOrderIcons) if (icon != null) Destroy(icon.gameObject);
            _turnOrderIcons.Clear();

            var allUnits = new List<BattleUnit>();
            allUnits.AddRange(playerUnits);
            allUnits.AddRange(enemyUnits);
            allUnits.Sort((a, b) => a.TurnOrder.CompareTo(b.TurnOrder));

            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                var iconObj = Instantiate(_turnOrderIconPrefab, _turnOrderContainer);
                var icon = iconObj.GetComponent<TurnOrderIcon>() ?? iconObj.AddComponent<TurnOrderIcon>();
                icon.Initialize(unit, i == 0);
                _turnOrderIcons.Add(icon);
            }
        }

        private void SetupEventHandlers()
        {
            // Auto Battle
            _autoBattleToggleBtn?.onClick.AddListener(ToggleAutoBattle);
            _autoBattleModeBtn?.onClick.AddListener(CycleAutoBattleMode);

            // Speed
            _speed1xBtn?.onClick.AddListener(() => SetBattleSpeed(BattleSpeed.Normal));
            _speed2xBtn?.onClick.AddListener(() => SetBattleSpeed(BattleSpeed.Fast));
            _speed3xBtn?.onClick.AddListener(() => SetBattleSpeed(BattleSpeed.UltraFast));

            // Pause
            _pauseBtn?.onClick.AddListener(OnPausePressed);
            _resumeBtn?.onClick.AddListener(OnResumePressed);
            _quitBattleBtn?.onClick.AddListener(OnQuitPressed);

            // Result
            _resultContinueBtn?.onClick.AddListener(OnResultContinue);

            // Target cancel
            _cancelTargetBtn?.onClick.AddListener(CancelTargeting);

            // BattleSystem events
            if (_battleSystem != null)
            {
                _battleSystem.OnBattleStarted += OnBattleStarted;
                _battleSystem.OnTurnStarted += OnTurnStarted;
                _battleSystem.OnTurnStart += OnTurnStart;
                _battleSystem.OnActionExecuted += OnActionExecuted;
                _battleSystem.OnBattleEnded += OnBattleEnded;
                _battleSystem.OnStateChanged += OnStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_battleSystem != null)
            {
                _battleSystem.OnBattleStarted -= OnBattleStarted;
                _battleSystem.OnTurnStarted -= OnTurnStarted;
                _battleSystem.OnTurnStart -= OnTurnStart;
                _battleSystem.OnActionExecuted -= OnActionExecuted;
                _battleSystem.OnBattleEnded -= OnBattleEnded;
                _battleSystem.OnStateChanged -= OnStateChanged;
            }
        }

        // ==================== Public API ====================

        public void UpdateTurnCounter(int turn)
        {
            if (_turnCounterText != null)
                _turnCounterText.text = $"Turno {turn}";
        }

        public void HighlightActiveUnit(BattleUnit unit)
        {
            _activeUnit = unit;

            // Update unit frames
            foreach (var ui in _playerUnitUIs)
                ui.SetActiveHighlight(ui.Unit == unit);
            foreach (var ui in _enemyUnitUIs)
                ui.SetActiveHighlight(ui.Unit == unit);

            // Update turn order
            for (int i = 0; i < _turnOrderIcons.Count; i++)
            {
                _turnOrderIcons[i].SetActive(i == 0);
            }

            // Move active indicator
            if (_activeUnitIndicator != null && unit != null)
            {
                var targetUI = unit.IsPlayerUnit
                    ? _playerUnitUIs.Find(u => u.Unit == unit)
                    : _enemyUnitUIs.Find(u => u.Unit == unit);

                if (targetUI != null)
                {
                    _activeUnitIndicator.transform.SetParent(targetUI.transform);
                    _activeUnitIndicator.transform.localPosition = Vector3.zero;
                    _activeUnitIndicator.SetActive(true);
                }
            }
        }

        public void ShowActionPreview(BattleAction action)
        {
            if (_actionPreviewPanel == null) return;

            _actionPreviewPanel.SetActive(true);
            _selectedSkill = action.Skill;

            if (_actionPreviewSkillName != null)
                _actionPreviewSkillName.text = action.Skill.Name;

            if (_actionPreviewTargetInfo != null)
            {
                var targetNames = string.Join(", ", action.Targets.ConvertAll(t => t.Name));
                _actionPreviewTargetInfo.text = $"Alvo: {targetNames}";
            }

            if (_actionPreviewSkillIcon != null && action.Skill.Icon != null)
                _actionPreviewSkillIcon.sprite = action.Skill.Icon;

            // Animate in
            var canvasGroup = _actionPreviewPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = _actionPreviewPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.2f);
        }

        public void ShowActionResults(BattleAction action)
        {
            _actionPreviewPanel?.SetActive(false);

            foreach (var result in action.Results)
            {
                ShowActionResult(result);
            }
        }

        public async UniTask ShowBattleResultAsync(BattleResult result)
        {
            _battleResultPanel?.SetActive(true);
            _autoBattlePanel?.SetActive(false);
            _pauseBtn?.gameObject.SetActive(false);
            _skillButtonsContainer?.gameObject.SetActive(false);

            bool isVictory = result.Outcome == BattleOutcome.Victory;
            _victoryPanel?.SetActive(isVictory);
            _defeatPanel?.SetActive(!isVictory && result.Outcome == BattleOutcome.Defeat);
            _drawPanel?.SetActive(result.Outcome == BattleOutcome.Draw);

            if (_resultTurnsText != null)
                _resultTurnsText.text = $"Turnos: {result.Turns}";

            // Show rewards
            if (isVictory && result.Rewards != null)
            {
                foreach (var reward in result.Rewards)
                {
                    var rewardObj = Instantiate(_rewardItemPrefab, _rewardsContainer);
                    var rewardUI = rewardObj.GetComponent<RewardItemUI>() ?? rewardObj.AddComponent<RewardItemUI>();
                    rewardUI.SetData(reward);
                    await rewardUI.AnimateInAsync();
                    await UniTask.Delay(100);
                }
            }

            // Animate panel in
            var canvasGroup = _battleResultPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = _battleResultPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            await canvasGroup.DOFade(1f, 0.5f).AsyncWaitForCompletion();
        }

        public void SetStageName(string name)
        {
            if (_stageNameText != null)
                _stageNameText.text = name;
        }

        // ==================== Event Handlers ====================

        private void OnTurnStart(BattleUnit unit)
        {
            HighlightActiveUnit(unit);
            UpdateTurnOrder();
            UpdateSkillButtonsInteractable();
        }

        private void OnActionExecuted(BattleAction action)
        {
            // Update HP bars
            foreach (var target in action.Targets)
            {
                var targetUI = target.IsPlayerUnit
                    ? _playerUnitUIs.Find(u => u.Unit == target)
                    : _enemyUnitUIs.Find(u => u.Unit == target);

                targetUI?.UpdateHPBar();
                targetUI?.UpdateMPBar();
                targetUI?.UpdateBuffsDebuffs();
            }

            // Update actor MP
            var actorUI = action.Actor.IsPlayerUnit
                ? _playerUnitUIs.Find(u => u.Unit == action.Actor)
                : _enemyUnitUIs.Find(u => u.Unit == action.Actor);
            actorUI?.UpdateMPBar();
        }

        private void OnBattleEnded(BattleResult result)
        {
            // Cleanup handled in ShowBattleResultAsync
        }

        private void OnBattleStarted(BattleSetup setup)
        {
            if (_stageNameText != null)
                _stageNameText.text = setup.Mode.ToString();
        }

        private void OnTurnStarted(int turnNumber, BattleUnit activeUnit)
        {
            UpdateTurnCounter(turnNumber);
            HighlightActiveUnit(activeUnit);
        }

        private void OnStateChanged(BattleState state)
        {
            switch (state)
            {
                case BattleState.Paused:
                    _isPaused = true;
                    _pauseMenuPanel?.SetActive(true);
                    Time.timeScale = 0f;
                    break;
                case BattleState.Playing:
                    if (_isPaused)
                    {
                        _isPaused = false;
                        _pauseMenuPanel?.SetActive(false);
                        Time.timeScale = 1f;
                    }
                    break;
                case BattleState.Ended:
                    _autoBattlePanel?.SetActive(false);
                    _pauseBtn?.gameObject.SetActive(false);
                    break;
            }
        }

        private void UpdateTurnOrder()
        {
            // Rotate turn order icons
            if (_turnOrderIcons.Count > 0)
            {
                var first = _turnOrderIcons[0];
                _turnOrderIcons.RemoveAt(0);
                _turnOrderIcons.Add(first);

                for (int i = 0; i < _turnOrderIcons.Count; i++)
                {
                    _turnOrderIcons[i].SetPosition(i);
                    _turnOrderIcons[i].SetActive(i == 0);
                }
            }
        }

        // ==================== Auto Battle Controls ====================

        private void ToggleAutoBattle()
        {
            _isAutoBattle = !_isAutoBattle;
            _battleSystem?.SetAutoBattle(_isAutoBattle, _currentAutoMode);
            UpdateAutoBattleUI();
        }

        private void CycleAutoBattleMode()
        {
            if (!_isAutoBattle) return;

            _currentAutoMode = _currentAutoMode switch
            {
                AutoBattleMode.Basic => AutoBattleMode.Smart,
                AutoBattleMode.Smart => AutoBattleMode.Full,
                AutoBattleMode.Full => AutoBattleMode.Basic,
                _ => AutoBattleMode.Smart
            };

            _battleSystem?.SetAutoBattle(true, _currentAutoMode);
            UpdateAutoBattleUI();
        }

        private void UpdateAutoBattleUI()
        {
            if (_autoBattleToggleBtn != null)
            {
                var colors = _autoBattleToggleBtn.colors;
                colors.normalColor = _isAutoBattle ? _config.AutoActiveColor : _config.AutoInactiveColor;
                _autoBattleToggleBtn.colors = colors;
            }

            if (_autoBattleLabelText != null)
                _autoBattleLabelText.text = _isAutoBattle ? "AUTO: ON" : "AUTO: OFF";

            if (_autoModeLabelText != null)
                _autoModeLabelText.text = _currentAutoMode.ToString().ToUpper();
        }

        // ==================== Speed Controls ====================

        private void SetBattleSpeed(BattleSpeed speed)
        {
            _currentSpeed = speed;
            _battleSystem?.SetBattleSpeed(speed);
            UpdateSpeedUI();
        }

        private void UpdateSpeedUI()
        {
            if (_speedLabelText != null)
                _speedLabelText.text = $"{_currentSpeed}x";

            // Update button highlights
            UpdateSpeedButtonHighlight(_speed1xBtn, BattleSpeed.Normal);
            UpdateSpeedButtonHighlight(_speed2xBtn, BattleSpeed.Fast);
            UpdateSpeedButtonHighlight(_speed3xBtn, BattleSpeed.UltraFast);
        }

        private void UpdateSpeedButtonHighlight(Button btn, BattleSpeed speed)
        {
            if (btn == null) return;
            var colors = btn.colors;
            colors.normalColor = _currentSpeed == speed ? _config.SpeedActiveColor : _config.SpeedInactiveColor;
            btn.colors = colors;
        }

        // ==================== Pause Menu ====================

        private void OnPausePressed()
        {
            _battleSystem?.ChangeState(BattleState.Paused);
            _onPauseCallback?.Invoke();
        }

        private void OnResumePressed()
        {
            _battleSystem?.ChangeState(BattleState.Playing);
            _onResumeCallback?.Invoke();
        }

        private void OnQuitPressed()
        {
            var confirmUI = UIManager.Instance.ShowConfirmDialogAsync(
                "Sair da Batalha",
                "Tem certeza que deseja sair? O progresso não será salvo.",
                "Sair", "Cancelar"
            ).ContinueWith(async (task) =>
            {
                if (task.Result)
                {
                    _battleSystem?.ChangeState(BattleState.Ended);
                    _onQuitCallback?.Invoke();
                }
            });
        }

        // ==================== Result ====================

        private void OnResultContinue()
        {
            _battleResultPanel?.SetActive(false);
            _battleSystem?.ChangeState(BattleState.Ended);
        }

        // ==================== Manual Mode: Skill Buttons ====================

        private void UpdateSkillButtonsInteractable()
        {
            if (!_isAutoBattle && _activeUnit != null && _activeUnit.IsPlayerUnit)
            {
                _skillButtonsContainer?.gameObject.SetActive(true);
                SetupSkillButtons(_activeUnit);
            }
            else
            {
                _skillButtonsContainer?.gameObject.SetActive(false);
            }
        }

        private void SetupSkillButtons(BattleUnit unit)
        {
            var skills = unit.GetAvailableSkills();

            _basicAttackBtn?.onClick.RemoveAllListeners();
            _basicAttackBtn?.onClick.AddListener(() => OnSkillSelected(unit.BasicSkill));
            _basicAttackBtn?.gameObject.SetActive(true);

            _specialSkill1Btn?.onClick.RemoveAllListeners();
            _specialSkill1Btn?.onClick.AddListener(() => OnSkillSelected(skills.Find(s => s.Type == SkillType.Special)));
            _specialSkill1Btn?.gameObject.SetActive(skills.Exists(s => s.Type == SkillType.Special));

            _specialSkill2Btn?.onClick.RemoveAllListeners();
            _specialSkill2Btn?.onClick.AddListener(() => OnSkillSelected(skills.Find(s => s.Type == SkillType.Special && s != skills.Find(s2 => s2.Type == SkillType.Special))));
            _specialSkill2Btn?.gameObject.SetActive(skills.Exists(s => s.Type == SkillType.Special));

            _ultimateSkillBtn?.onClick.RemoveAllListeners();
            _ultimateSkillBtn?.onClick.AddListener(() => OnSkillSelected(unit.UltimateSkill));
            _ultimateSkillBtn?.gameObject.SetActive(unit.CanUseUltimate);
            _ultimateSkillBtn?.interactable = unit.CanUseUltimate;

            // Update cooldown displays
            UpdateSkillCooldownDisplay();
        }

        private void UpdateSkillCooldownDisplay()
        {
            // TODO: Update cooldown text on each skill button
        }

        private void OnSkillSelected(SkillData skill)
        {
            if (skill == null) return;

            _selectedSkill = skill;

            if (skill.TargetType == SkillTargetType.SingleEnemy || skill.TargetType == SkillTargetType.SingleAlly)
            {
                StartTargetSelection(skill);
            }
            else
            {
                // Auto-target for AoE/Self
                var targets = SelectTargetsForSkill(skill);
                SubmitAction(new BattleAction
                {
                    Actor = _activeUnit,
                    Skill = skill,
                    Targets = targets
                });
            }
        }

        private void StartTargetSelection(SkillData skill)
        {
            _isTargeting = true;
            _targetSelectionPanel?.SetActive(true);
            _targetInstructionText.text = $"Selecione o alvo para {skill.Name}";

            // Highlight valid targets
            var validTargets = GetValidTargets(skill);
            foreach (var target in validTargets)
            {
                var targetUI = target.IsPlayerUnit
                    ? _playerUnitUIs.Find(u => u.Unit == target)
                    : _enemyUnitUIs.Find(u => u.Unit == target);
                targetUI?.SetTargetable(true);
                targetUI?.OnClick += () => OnTargetSelected(target);
            }
        }

        private void OnTargetSelected(BattleUnit target)
        {
            if (!_isTargeting) return;

            _isTargeting = false;
            _targetSelectionPanel?.SetActive(false);

            // Remove targetable highlights
            foreach (var ui in _playerUnitUIs) ui.SetTargetable(false);
            foreach (var ui in _enemyUnitUIs) ui.SetTargetable(false);

            SubmitAction(new BattleAction
            {
                Actor = _activeUnit,
                Skill = _selectedSkill,
                Targets = new List<BattleUnit> { target }
            });
        }

        private void CancelTargeting()
        {
            _isTargeting = false;
            _targetSelectionPanel?.SetActive(false);
            foreach (var ui in _playerUnitUIs) ui.SetTargetable(false);
            foreach (var ui in _enemyUnitUIs) ui.SetTargetable(false);
        }

        private List<BattleUnit> GetValidTargets(SkillData skill)
        {
            var targets = skill.TargetsEnemies ? _battleSystem.EnemyUnits : _battleSystem.PlayerUnits;
            return targets.FindAll(u => u.IsAlive);
        }

        private List<BattleUnit> SelectTargetsForSkill(SkillData skill)
        {
            var validTargets = GetValidTargets(skill);
            if (validTargets.Count == 0) return new List<BattleUnit>();

            return skill.TargetType switch
            {
                SkillTargetType.AllEnemies or SkillTargetType.AllAllies => validTargets,
                SkillTargetType.RandomEnemy or SkillTargetType.RandomAlly =>
                    new List<BattleUnit> { validTargets[Random.Range(0, validTargets.Count)] },
                SkillTargetType.LowestHPAlly =>
                    new List<BattleUnit> { validTargets.OrderBy(u => u.Stats.CurrentHP).First() },
                SkillTargetType.HighestHPEnemy =>
                    new List<BattleUnit> { validTargets.OrderByDescending(u => u.Stats.CurrentHP).First() },
                _ => new List<BattleUnit> { validTargets[0] }
            };
        }

        private void SubmitAction(BattleAction action)
        {
            _onActionSelected?.Invoke(action);
        }

        // ==================== Floating Numbers ====================

        private void ShowActionResult(ActionResult result)
        {
            var targetUI = result.Target.IsPlayerUnit
                ? _playerUnitUIs.Find(u => u.Unit == result.Target)
                : _enemyUnitUIs.Find(u => u.Unit == result.Target);

            if (targetUI == null) return;

            Vector3 spawnPos = targetUI.transform.position;

            if (result.IsDodged)
            {
                ShowFloatingText(_missTextPrefab, spawnPos, "ERROU!", Color.gray);
            }
            else if (result.IsDamage)
            {
                var color = result.IsCrit ? _config.CritColor : _config.DamageColor;
                var text = result.Value.ToString("N0");
                if (result.IsCrit) text = $"CRIT {text}!";
                ShowFloatingText(_damageNumberPrefab, spawnPos, text, color, result.IsCrit);
            }
            else if (result.IsHeal)
            {
                var text = $"+{result.Value:N0}";
                ShowFloatingText(_healNumberPrefab, spawnPos, text, _config.HealColor);
            }

            // Status effects
            foreach (var effect in result.AppliedEffects)
            {
                targetUI.ShowStatusEffectIcon(effect);
            }
        }

        private void ShowFloatingText(GameObject prefab, Vector3 position, string text, Color color, bool isCrit = false)
        {
            if (prefab == null) return;

            var obj = Instantiate(prefab, _floatingTextContainer);
            var floatingText = obj.GetComponent<FloatingTextUI>() ?? obj.AddComponent<FloatingTextUI>();
            floatingText.Initialize(text, color, isCrit);
            obj.transform.position = position;
        }

        // ==================== Callbacks ====================

        public async UniTask<BattleAction> WaitForPlayerActionAsync(BattleUnit unit)
        {
            var tcs = new UniTaskCompletionSource<BattleAction>();
            _onActionSelected = (action) => tcs.TrySetResult(action);

            // Show skill buttons
            _skillButtonsContainer?.gameObject.SetActive(true);
            SetupSkillButtons(unit);

            return await tcs.Task;
        }

        public void SetActionSelectedCallback(System.Action<BattleAction> callback)
        {
            _onActionSelected = callback;
        }

        public void SetPauseCallbacks(System.Action onPause, System.Action onResume, System.Action onQuit)
        {
            _onPauseCallback = onPause;
            _onResumeCallback = onResume;
            _onQuitCallback = onQuit;
        }
    }

    // ==================== Supporting Components ====================

    public class BattleUnitUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Slider _hpBar;
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private Slider _mpBar;
        [SerializeField] private TextMeshProUGUI _mpText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private RectTransform _buffsContainer;
        [SerializeField] private RectTransform _debuffsContainer;
        [SerializeField] private GameObject _buffIconPrefab;
        [SerializeField] private GameObject _activeHighlight;
        [SerializeField] private Button _clickButton;
        [SerializeField] private Image _elementIcon;

        private BattleUnit _unit;
        private bool _isPlayerUnit;
        private int _positionIndex;
        private BattleUIConfig _config;
        private System.Action<BattleUnit> _onClick;

        public BattleUnit Unit => _unit;

        public void Initialize(BattleUnit unit, bool isPlayer, int position, BattleUIConfig config)
        {
            _unit = unit;
            _isPlayerUnit = isPlayer;
            _positionIndex = position;
            _config = config;

            if (_nameText != null) _nameText.text = unit.Name;
            if (_portraitImage != null && unit.Portrait != null) _portraitImage.sprite = unit.Portrait;
            if (_elementIcon != null && unit.Stats.Element != Element.None) _elementIcon.sprite = GetElementSprite(unit.Stats.Element);

            UpdateHPBar();
            UpdateMPBar();
            UpdateBuffsDebuffs();

            _activeHighlight?.SetActive(false);

            if (_clickButton != null)
            {
                _clickButton.onClick.RemoveAllListeners();
                _clickButton.onClick.AddListener(() => _onClick?.Invoke(_unit));
            }
        }

        public void SetActiveHighlight(bool active)
        {
            _activeHighlight?.SetActive(active);
        }

        public void SetTargetable(bool targetable)
        {
            // Visual feedback for targetable units
            var outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.enabled = targetable;
            outline.effectColor = targetable ? _config.TargetHighlightColor : Color.clear;
        }

        public void OnClick(System.Action<BattleUnit> callback)
        {
            _onClick = callback;
        }

        public void UpdateHPBar()
        {
            if (_hpBar != null)
            {
                float targetValue = (float)_unit.Stats.CurrentHP / _unit.Stats.MaxHP;
                _hpBar.DOValue(targetValue, 0.3f).SetEase(Ease.OutQuad);
            }
            if (_hpText != null)
            {
                _hpText.text = $"{_unit.Stats.CurrentHP:N0} / {_unit.Stats.MaxHP:N0}";
            }
        }

        public void UpdateMPBar()
        {
            if (_mpBar != null)
            {
                float targetValue = (float)_unit.Stats.CurrentMP / _unit.Stats.MaxMP;
                _mpBar.DOValue(targetValue, 0.3f);
            }
            if (_mpText != null)
            {
                _mpText.text = $"{_unit.Stats.CurrentMP:N0} / {_unit.Stats.MaxMP:N0}";
            }
        }

        public void UpdateBuffsDebuffs()
        {
            ClearContainer(_buffsContainer);
            ClearContainer(_debuffsContainer);

            foreach (var buff in _unit.Buffs)
            {
                var icon = Instantiate(_buffIconPrefab, _buffsContainer);
                var buffUI = icon.GetComponent<BuffIconUI>() ?? icon.AddComponent<BuffIconUI>();
                buffUI.Initialize(buff, true);
            }

            foreach (var debuff in _unit.Debuffs)
            {
                var icon = Instantiate(_buffIconPrefab, _debuffsContainer);
                var debuffUI = icon.GetComponent<BuffIconUI>() ?? icon.AddComponent<BuffIconUI>();
                debuffUI.Initialize(debuff, false);
            }
        }

        public void ShowStatusEffectIcon(StatusEffectData effect)
        {
            var isBuff = IsBuff(effect.Type);
            var container = isBuff ? _buffsContainer : _debuffsContainer;
            var icon = Instantiate(_buffIconPrefab, container);
            var buffUI = icon.GetComponent<BuffIconUI>() ?? icon.AddComponent<BuffIconUI>();
            buffUI.Initialize(effect, isBuff);
        }

        private void ClearContainer(RectTransform container)
        {
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private Sprite GetElementSprite(Element element)
        {
            // TODO: Load from resources
            return null;
        }

        private bool IsBuff(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.AttackUp or StatusEffectType.DefenseUp or StatusEffectType.SpeedUp or
                StatusEffectType.CritRateUp or StatusEffectType.CritDamageUp or StatusEffectType.Shield or
                StatusEffectType.Invulnerability or StatusEffectType.Regeneration => true,
                _ => false
            };
        }
    }

    public class TurnOrderIcon : MonoBehaviour
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private GameObject _activeIndicator;
        [SerializeField] private TextMeshProUGUI _positionText;

        private BattleUnit _unit;

        public void Initialize(BattleUnit unit, bool isActive)
        {
            _unit = unit;
            if (_portraitImage != null && unit.Portrait != null) _portraitImage.sprite = unit.Portrait;
            _activeIndicator?.SetActive(isActive);
        }

        public void SetActive(bool active)
        {
            _activeIndicator?.SetActive(active);
        }

        public void SetPosition(int index)
        {
            if (_positionText != null) _positionText.text = (index + 1).ToString();
            transform.SetSiblingIndex(index);
        }
    }

    public class BuffIconUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _turnsText;
        [SerializeField] private Image _backgroundImage;

        public void Initialize(Buff buff, bool isBuff)
        {
            _iconImage.sprite = buff.Icon;
            _backgroundImage.color = isBuff ? Color.green : Color.red;
            _turnsText.text = buff.RemainingTurns.ToString();
        }

        public void Initialize(Debuff debuff, bool isBuff)
        {
            _iconImage.sprite = debuff.Icon;
            _backgroundImage.color = isBuff ? Color.green : Color.red;
            _turnsText.text = debuff.RemainingTurns.ToString();
        }

        public void Initialize(StatusEffectData effect, bool isBuff)
        {
            _iconImage.sprite = effect.Icon;
            _backgroundImage.color = isBuff ? Color.green : Color.red;
            _turnsText.text = effect.RemainingTurns.ToString();
        }
    }

    public class FloatingTextUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private float _floatHeight = 50f;
        [SerializeField] private float _duration = 1f;
        [SerializeField] private float _fadeDuration = 0.5f;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;

        public void Initialize(string text, Color color, bool isCrit = false)
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _text.text = text;
            _text.color = color;
            _text.fontSize = isCrit ? 36 : 28;
            _text.enableWordWrapping = false;

            Animate();
        }

        private async void Animate()
        {
            Vector2 startPos = _rectTransform.anchoredPosition;
            Vector2 endPos = startPos + Vector2.up * _floatHeight;

            // Float up
            var floatTween = _rectTransform.DOAnchorPos(endPos, _duration).SetEase(Ease.OutQuad);

            // Fade out
            await UniTask.Delay((int)((_duration - _fadeDuration) * 1000));
            _canvasGroup.DOFade(0f, _fadeDuration);

            await floatTween.AsyncWaitForCompletion();
            Destroy(gameObject);
        }
    }

    public class RewardItemUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _quantityText;

        public void SetData(RewardDto reward)
        {
            // TODO: Load icon from Addressables
            _nameText.text = reward.ResourceName;
            _quantityText.text = $"x{reward.Quantity:N0}";
        }

        public async UniTask AnimateInAsync()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            transform.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;

            await transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).AsyncWaitForCompletion();
            await canvasGroup.DOFade(1f, 0.2f).AsyncWaitForCompletion();
        }
    }

    [System.Serializable]
    public class BattleUIConfig
    {
        public Color AutoActiveColor = new Color(0.2f, 0.8f, 0.2f);
        public Color AutoInactiveColor = Color.gray;
        public Color SpeedActiveColor = Color.yellow;
        public Color SpeedInactiveColor = Color.white;
        public Color DamageColor = Color.red;
        public Color CritColor = new Color(1f, 0.8f, 0f);
        public Color HealColor = Color.green;
        public Color TargetHighlightColor = Color.yellow;
        public float HPBarAnimationDuration = 0.3f;
    }

    // StatusEffectType enum extension
    public enum StatusEffectType
    {
        AttackUp,
        DefenseUp,
        SpeedUp,
        CritRateUp,
        CritDamageUp,
        Shield,
        Invulnerability,
        Regeneration,
        Poison,
        Burn,
        Stun,
        Silence
    }

    public class StatusEffectData
    {
        public StatusEffectType Type;
        public Sprite Icon;
        public int RemainingTurns;
        public float Value;
    }
}

// DOTween async extension
namespace DG.Tweening
{
    public static class TweenExtensions
    {
        public static UniTask AsyncWaitForCompletion(this Tween tween)
        {
            var tcs = new UniTaskCompletionSource<bool>();
            tween.OnComplete(() => tcs.TrySetResult(true));
            return tcs.Task;
        }
    }
}