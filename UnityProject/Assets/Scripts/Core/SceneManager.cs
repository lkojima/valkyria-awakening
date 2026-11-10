using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class SceneManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private SceneConfig _config;

        private SceneType _currentScene;
        private SceneType _previousScene;
        private AsyncOperation _loadingOperation;
        private float _loadingProgress;
        private bool _isLoading;

        public SceneType CurrentScene => _currentScene;
        public SceneType PreviousScene => _previousScene;
        public float LoadingProgress => _loadingProgress;
        public bool IsLoading => _isLoading;

        public event System.Action<SceneType, SceneType> OnSceneChanged;
        public event System.Action<float> OnLoadingProgress;

        public async UniTask InitializeAsync()
        {
            _currentScene = (SceneType)UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

            // Preload common scenes
            await PreloadScenesAsync();
        }

        public void Shutdown() { }

        public void OnGameStateChanged(GameState previous, GameState current) { }

        public async UniTask LoadSceneAsync(SceneType sceneType, LoadSceneMode mode = LoadSceneMode.Single, bool showLoadingScreen = true)
        {
            if (_isLoading) return;

            _isLoading = true;
            _previousScene = _currentScene;
            _loadingProgress = 0f;

            GameManager.Instance.UI.ShowLoadingScreen($"Loading {sceneType}...");

            try
            {
                var sceneName = sceneType.ToString();

                _loadingOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);
                _loadingOperation.allowSceneActivation = false;

                // Wait for loading to reach 90%
                while (_loadingOperation.progress < 0.9f)
                {
                    _loadingProgress = _loadingOperation.progress;
                    OnLoadingProgress?.Invoke(_loadingProgress);
                    await UniTask.Yield();
                }

                _loadingProgress = 0.9f;
                OnLoadingProgress?.Invoke(_loadingProgress);

                // Allow scene activation
                _loadingOperation.allowSceneActivation = true;

                // Wait for scene to fully load
                while (!_loadingOperation.isDone)
                {
                    await UniTask.Yield();
                }

                _loadingProgress = 1f;
                OnLoadingProgress?.Invoke(_loadingProgress);

                _currentScene = sceneType;
                OnSceneChanged?.Invoke(_previousScene, _currentScene);

                // Initialize scene-specific systems
                await InitializeSceneAsync(sceneType);
            }
            finally
            {
                _isLoading = false;
                GameManager.Instance.UI.HideLoadingScreen();
            }
        }

        public async UniTask UnloadSceneAsync(SceneType sceneType)
        {
            var sceneName = sceneType.ToString();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);

            if (scene.isLoaded)
            {
                await UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
            }
        }

        public async UniTask LoadAdditiveAsync(SceneType sceneType)
        {
            await LoadSceneAsync(sceneType, LoadSceneMode.Additive, false);
        }

        public T FindObjectOfType<T>() where T : Object
        {
            return FindObjectOfType<T>();
        }

        public T[] FindObjectsOfType<T>() where T : Object
        {
            return FindObjectsOfType<T>();
        }

        private async UniTask InitializeSceneAsync(SceneType sceneType)
        {
            // Scene-specific initialization
            switch (sceneType)
            {
                case SceneType.MainMenu:
                    await InitializeMainMenuAsync();
                    break;
                case SceneType.Battle:
                    await InitializeBattleAsync();
                    break;
                case SceneType.Gacha:
                    await InitializeGachaAsync();
                    break;
            }
        }

        private async UniTask InitializeMainMenuAsync()
        {
            // Play main menu music
            await GameManager.Instance.Audio.PlayMusicAsync("main_menu");

            // Show main menu UI
            await GameManager.Instance.UI.OpenPanelAsync<MainMenuPanel>("MainMenuPanel");
        }

        private async UniTask InitializeBattleAsync()
        {
            // Battle scene initialization handled by BattleSystem
        }

        private async UniTask InitializeGachaAsync()
        {
            // Play gacha music
            await GameManager.Instance.Audio.PlayMusicAsync("gacha");

            // Show gacha UI
            await GameManager.Instance.UI.OpenPanelAsync<GachaPanel>("GachaPanel");
        }

        private async UniTask PreloadScenesAsync()
        {
            // Preload critical scenes
            var criticalScenes = new[]
            {
                SceneType.MainMenu,
                SceneType.Battle,
                SceneType.Gacha
            };

            // Scenes are loaded via build settings, no need to preload with Addressables
        }
    }

    public enum SceneType
    {
        Splash = 0,
        MainMenu = 1,
        Battle = 2,
        Gacha = 3,
        Character = 4,
        Campaign = 5,
        Tower = 6,
        Arena = 7,
        WorldBoss = 8,
        Dungeon = 8,
        Expedition = 9,
        Quest = 10,
        Event = 11,
        Shop = 12,
        Settings = 13,
        Profile = 14,
        Guild = 15,
        Social = 16
    }

    [System.Serializable]
    public class SceneConfig
    {
        public float LoadingScreenMinTime = 1f;
        public bool ShowTipsDuringLoading = true;
    }
}