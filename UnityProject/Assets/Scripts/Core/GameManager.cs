using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private GameConfig _config;
        [SerializeField] private bool _autoInitialize = true;

        [Header("Managers")]
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private DataManager _dataManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private AudioManager _audioManager;
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private SceneManager _sceneManager;
        [SerializeField] private AnalyticsManager _analyticsManager;
        [SerializeField] private NotificationManager _notificationManager;
        [SerializeField] private SaveManager _saveManager;

        public GameConfig Config => _config;
        public NetworkManager Network => _networkManager;
        public DataManager Data => _dataManager;
        public UIManager UI => _uiManager;
        public AudioManager Audio => _audioManager;
        public PoolManager Pool => _poolManager;
        public SceneManager Scene => _sceneManager;
        public AnalyticsManager Analytics => _analyticsManager;
        public NotificationManager Notifications => _notificationManager;
        public SaveManager Save => _saveManager;

        public bool IsInitialized { get; private set; }
        public GameState CurrentState { get; private set; }

        private readonly List<IGameSystem> _systems = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private async void Start()
        {
            if (_autoInitialize)
            {
                await InitializeAsync();
            }
        }

        public async UniTask InitializeAsync()
        {
            if (IsInitialized) return;

            ChangeState(GameState.Initializing);

            try
            {
                // Initialize core systems
                await _saveManager.InitializeAsync();
                await _dataManager.InitializeAsync();
                await _poolManager.InitializeAsync();
                await _audioManager.InitializeAsync();
                await _networkManager.InitializeAsync();
                await _uiManager.InitializeAsync();
                await _sceneManager.InitializeAsync();
                await _analyticsManager.InitializeAsync();
                await _notificationManager.InitializeAsync();

                // Register all systems
                RegisterSystems();

                // Initialize all systems
                foreach (var system in _systems)
                {
                    await system.InitializeAsync();
                }

                // Check for existing session
                await TryRestoreSessionAsync();

                IsInitialized = true;
                ChangeState(GameState.Ready);

                // Load main menu or game scene
                await _sceneManager.LoadSceneAsync(SceneType.MainMenu);

                Debug.Log("[GameManager] Initialization complete");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameManager] Initialization failed: {ex}");
                ChangeState(GameState.Error);
                throw;
            }
        }

        private void RegisterSystems()
        {
            _systems.AddRange(new IGameSystem[]
            {
                _networkManager,
                _dataManager,
                _uiManager,
                _audioManager,
                _poolManager,
                _sceneManager,
                _analyticsManager,
                _notificationManager,
                _saveManager
            });
        }

        private async UniTask TryRestoreSessionAsync()
        {
            var sessionData = await _saveManager.LoadAsync<SessionData>("session");
            if (sessionData != null && !sessionData.IsExpired)
            {
                await _networkManager.RestoreSessionAsync(sessionData);
            }
        }

        public void ChangeState(GameState newState)
        {
            var previousState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(previousState, newState);

            foreach (var system in _systems)
            {
                system.OnGameStateChanged(previousState, newState);
            }
        }

        public event System.Action<GameState, GameState> OnStateChanged;

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                OnAppPaused();
            }
            else
            {
                OnAppResumed();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                OnAppPaused();
            }
            else
            {
                OnAppResumed();
            }
        }

        private async void OnAppPaused()
        {
            await _saveManager.SaveAsync("session", _networkManager.GetSessionData());
            _analyticsManager.TrackSessionEnd();
        }

        private async void OnAppResumed()
        {
            await _networkManager.RefreshSessionAsync();
            _analyticsManager.TrackSessionStart();
            await CheckIdleRewardsAsync();
        }

        private async UniTask CheckIdleRewardsAsync()
        {
            if (_networkManager.IsLoggedIn)
            {
                var result = await _networkManager.GetIdleRewardsAsync();
                if (result.HasRewards)
                {
                    _uiManager.ShowIdleRewardPopup(result);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var system in _systems)
            {
                system.Shutdown();
            }
        }
    }

    public enum GameState
    {
        Uninitialized,
        Initializing,
        Ready,
        Loading,
        InGame,
        Paused,
        Error
    }

    public interface IGameSystem
    {
        UniTask InitializeAsync();
        void Shutdown();
        void OnGameStateChanged(GameState previous, GameState current);
    }

    [System.Serializable]
    public class SessionData
    {
        public string AccessToken;
        public string RefreshToken;
        public long ExpiresAt;
        public Guid UserId;
        public string DeviceId;

        public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt;
    }

    [System.Serializable]
    public class GameConfig
    {
        public string ApiBaseUrl = "https://api.goddessrequiem.com/api/v1";
        public string CdnBaseUrl = "https://cdn.goddessrequiem.com";
        public int MaxFramerate = 60;
        public bool EnableDebugLogs = true;
        public float AutoSaveInterval = 30f;
        public int MaxConcurrentDownloads = 4;
        public bool UseAssetBundles = true;
        public string DefaultLanguage = "pt-BR";
        public bool EnableTelemetry = true;
    }
}