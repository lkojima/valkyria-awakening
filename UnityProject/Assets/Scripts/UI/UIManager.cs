using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using TMPro;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Gacha;

namespace GoddessRequiem.Client.UI
{
    public class UIManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private GraphicRaycaster _raycaster;
        [SerializeField] private float _transitionDuration = 0.3f;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject _loadingScreenPrefab;
        [SerializeField] private GameObject _popupPrefab;
        [SerializeField] private GameObject _toastPrefab;
        [SerializeField] private GameObject _confirmDialogPrefab;
        [SerializeField] private GameObject _summonAnimationPrefab;

        private readonly Stack<UIPanel> _panelStack = new();
        private readonly Dictionary<string, UIPanel> _panelCache = new();
        private readonly Dictionary<string, GameObject> _prefabCache = new();
        private UIPanel _currentPanel;
        private GameObject _loadingScreen;
        private bool _isTransitioning;

        public UIPanel CurrentPanel => _currentPanel;
        public Canvas MainCanvas => _mainCanvas;

        public event System.Action<UIPanel> OnPanelOpened;
        public event System.Action<UIPanel> OnPanelClosed;

        public async UniTask InitializeAsync()
        {
            _loadingScreen = Instantiate(_loadingScreenPrefab, _mainCanvas.transform);
            _loadingScreen.SetActive(false);

            await PreloadCommonPrefabsAsync();
        }

        public void Shutdown()
        {
            CloseAllPanels();
            _panelCache.Clear();
            _prefabCache.Clear();
        }

        public void OnGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Loading)
            {
                ShowLoadingScreen();
            }
            else if (previous == GameState.Loading)
            {
                HideLoadingScreen();
            }
        }

        public async UniTask<T> OpenPanelAsync<T>(string panelName, object data = null) where T : UIPanel
        {
            if (_isTransitioning) await WaitForTransitionAsync();

            _isTransitioning = true;

            // Close current panel if it's not a popup
            if (_currentPanel != null && !_currentPanel.IsPopup)
            {
                await ClosePanelAsync(_currentPanel);
            }

            var panel = await GetOrCreatePanelAsync<T>(panelName);
            panel.transform.SetAsLastSibling();

            if (data != null)
            {
                panel.SetData(data);
            }

            await panel.ShowAsync();
            _panelStack.Push(panel);
            _currentPanel = panel;

            OnPanelOpened?.Invoke(panel);
            _isTransitioning = false;

            return panel;
        }

        public async UniTask<T> OpenPopupAsync<T>(string panelName, object data = null) where T : UIPanel
        {
            var panel = await GetOrCreatePanelAsync<T>(panelName);
            panel.transform.SetAsLastSibling();

            if (data != null)
            {
                panel.SetData(data);
            }

            await panel.ShowAsync();
            _panelStack.Push(panel);

            OnPanelOpened?.Invoke(panel);

            return panel;
        }

        public async UniTask ClosePanelAsync(UIPanel panel = null)
        {
            if (_isTransitioning) await WaitForTransitionAsync();

            panel ??= _currentPanel;
            if (panel == null) return;

            _isTransitioning = true;

            await panel.HideAsync();

            if (_panelStack.Count > 0 && _panelStack.Peek() == panel)
            {
                _panelStack.Pop();
            }

            if (_currentPanel == panel)
            {
                _currentPanel = _panelStack.Count > 0 ? _panelStack.Peek() : null;
            }

            if (!panel.IsPersistent)
            {
                ReturnPanelToPool(panel);
            }

            OnPanelClosed?.Invoke(panel);
            _isTransitioning = false;
        }

        public void CloseAllPanels()
        {
            while (_panelStack.Count > 0)
            {
                var panel = _panelStack.Pop();
                panel.HideImmediate();
                if (!panel.IsPersistent)
                {
                    ReturnPanelToPool(panel);
                }
            }
            _currentPanel = null;
        }

        public async UniTask<T> GetOrCreatePanelAsync<T>(string panelName) where T : UIPanel
        {
            if (_panelCache.TryGetValue(panelName, out var cached) && cached is T typed)
            {
                return typed;
            }

            var prefab = await GetPanelPrefabAsync(panelName);
            var instance = Instantiate(prefab, _mainCanvas.transform);
            var panel = instance.GetComponent<T>();

            panel.Initialize(this);
            _panelCache[panelName] = panel;

            return panel;
        }

        private async UniTask<GameObject> GetPanelPrefabAsync(string panelName)
        {
            if (_prefabCache.TryGetValue(panelName, out var cached))
            {
                return cached;
            }

            var handle = Addressables.LoadAssetAsync<GameObject>($"ui/panels/{panelName}");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _prefabCache[panelName] = handle.Result;
                return handle.Result;
            }

            throw new System.Exception($"Failed to load panel prefab: {panelName}");
        }

        private async UniTask PreloadCommonPrefabsAsync()
        {
            var commonPanels = new[]
            {
                "MainMenuPanel",
                "CharacterPanel",
                "BattlePanel",
                "GachaPanel",
                "ShopPanel",
                "CampaignPanel",
                "TowerPanel",
                "ArenaPanel",
                "WorldBossPanel",
                "DungeonPanel",
                "ExpeditionPanel",
                "QuestPanel",
                "EventPanel",
                "SettingsPanel",
                "ProfilePanel"
            };

            var tasks = new List<UniTask>();
            foreach (var panel in commonPanels)
            {
                tasks.Add(GetPanelPrefabAsync(panel));
            }

            await UniTask.WhenAll(tasks);
        }

        private void ReturnPanelToPool(UIPanel panel)
        {
            panel.gameObject.SetActive(false);
        }

        public void ShowLoadingScreen(string message = null)
        {
            _loadingScreen.SetActive(true);
            var text = _loadingScreen.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null && message != null)
            {
                text.text = message;
            }
        }

        public void HideLoadingScreen()
        {
            _loadingScreen.SetActive(false);
        }

        public async UniTask ShowToastAsync(string message, float duration = 2f, ToastType type = ToastType.Info)
        {
            var toast = Instantiate(_toastPrefab, _mainCanvas.transform);
            var toastComponent = toast.GetComponent<ToastUI>();
            await toastComponent.ShowAsync(message, duration, type);
            Destroy(toast, duration + 1f);
        }

        public async UniTask<bool> ShowConfirmDialogAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            var dialog = Instantiate(_confirmDialogPrefab, _mainCanvas.transform);
            var dialogComponent = dialog.GetComponent<ConfirmDialogUI>();
            var result = await dialogComponent.ShowAsync(title, message, confirmText, cancelText);
            Destroy(dialog);
            return result;
        }

        public async UniTask ShowSummonAnimationAsync(SummonResult result)
        {
            var animObj = Instantiate(_summonAnimationPrefab, _mainCanvas.transform);
            var animation = animObj.GetComponent<SummonAnimation>();
            await animation.PlayAsync(result);
            Destroy(animObj);
        }

        public async UniTask ShowIdleRewardPopup(IdleRewardSummary summary)
        {
            await OpenPopupAsync<IdleRewardPopupUI>("IdleRewardPopup", summary);
        }

        private async UniTask WaitForTransitionAsync()
        {
            while (_isTransitioning)
            {
                await UniTask.Yield();
            }
        }

        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    public abstract class UIPanel : MonoBehaviour
    {
        protected UIManager _uiManager;
        public virtual bool IsPopup => false;
        public virtual bool IsPersistent => false;

        public virtual void Initialize(UIManager uiManager)
        {
            _uiManager = uiManager;
            gameObject.SetActive(false);
        }

        public virtual void SetData(object data) { }

        public virtual async UniTask ShowAsync()
        {
            gameObject.SetActive(true);
            await AnimateShowAsync();
        }

        public virtual async UniTask HideAsync()
        {
            await AnimateHideAsync();
            gameObject.SetActive(false);
        }

        public virtual void HideImmediate()
        {
            gameObject.SetActive(false);
        }

        protected virtual async UniTask AnimateShowAsync()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;

                float elapsed = 0;
                float duration = 0.2f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
                    await UniTask.Yield();
                }
                canvasGroup.alpha = 1;
            }
        }

        protected virtual async UniTask AnimateHideAsync()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                float elapsed = 0;
                float duration = 0.15f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / duration);
                    await UniTask.Yield();
                }
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    [System.Serializable]
    public class IdleRewardSummary
    {
        public int HoursAccumulated;
        public List<CurrencyAmount> Currencies;
        public List<ResourceQuantity> Materials;
        public List<ResourceQuantity> Equipment;
        public List<ResourceQuantity> CharacterFragments;
    }
}