using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Newtonsoft.Json;
using GoddessRequiem.Core;
using System.Linq;

namespace GoddessRequiem.Client.Core
{
    public class DataManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private bool _preloadCriticalData = true;
        [SerializeField] private string _dataCatalogAddress = "data/catalog";
        [SerializeField] private string _catalogVersionKey = "data_catalog_version";
        [SerializeField] private int _maxCacheAgeHours = 168; // 1 week
        [SerializeField] private long _maxCacheSizeBytes = 100 * 1024 * 1024; // 100 MB

        private Dictionary<string, CachedData> _dataCache = new();
        private Dictionary<string, UniTaskCompletionSource<object>> _loadingTasks = new();
        private DataCatalog _catalog;
        private string _currentCatalogVersion;

        public event System.Action<string, object> OnDataLoaded;
        public event System.Action<string> OnDataUnloaded;
        public event System.Action<string> OnCacheCleared;

        public async UniTask InitializeAsync()
        {
            await LoadCatalogAsync();
            await ValidateCatalogVersionAsync();

            if (_preloadCriticalData)
            {
                await PreloadCriticalDataAsync();
            }
        }

        public void Shutdown()
        {
            _dataCache.Clear();
            _loadingTasks.Clear();
            _catalog = null;
        }

        public void OnGameStateChanged(GameState previous, GameState current) { }

        public async UniTask<T> LoadAsync<T>(string key) where T : class
        {
            // Check cache first
            if (_dataCache.TryGetValue(key, out var cached) && cached.Data is T typed && !cached.IsExpired)
            {
                cached.LastAccessed = DateTime.UtcNow;
                return typed;
            }

            // Check if already loading
            if (_loadingTasks.TryGetValue(key, out var existingTask))
            {
                var result = await existingTask.Task;
                return result as T;
            }

            var tcs = new UniTaskCompletionSource<object>();
            _loadingTasks[key] = tcs;

            try
            {
                var data = await LoadDataInternalAsync<T>(key);
                CacheData(key, data);
                tcs.TrySetResult(data);
                OnDataLoaded?.Invoke(key, data);
                return data;
            }
            catch (System.Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
            finally
            {
                _loadingTasks.Remove(key);
            }
        }

        public void SetData<T>(string key, T data)
        {
            CacheData(key, data);
            OnDataLoaded?.Invoke(key, data);
        }

        public bool TryGetData<T>(string key, out T data) where T : class
        {
            if (_dataCache.TryGetValue(key, out var cached) && cached.Data is T typed && !cached.IsExpired)
            {
                data = typed;
                cached.LastAccessed = DateTime.UtcNow;
                return true;
            }
            data = null;
            return false;
        }

        public void Unload(string key)
        {
            if (_dataCache.Remove(key))
            {
                OnDataUnloaded?.Invoke(key);
                Resources.UnloadUnusedAssets();
            }
        }

        public void ClearCache()
        {
            _dataCache.Clear();
            Resources.UnloadUnusedAssets();
            OnCacheCleared?.Invoke("all");
        }

        public void InvalidateCache(string key)
        {
            if (_dataCache.Remove(key))
            {
                OnDataUnloaded?.Invoke(key);
            }
        }

        public async UniTask InvalidateExpiredCacheAsync()
        {
            var expiredKeys = _dataCache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _dataCache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                Resources.UnloadUnusedAssets();
                OnCacheCleared?.Invoke($"expired:{expiredKeys.Count}");
            }

            // Check cache size
            await EnforceCacheSizeLimitAsync();
        }

        public long GetCacheSize()
        {
            // Approximate size calculation
            long size = 0;
            foreach (var kvp in _dataCache)
            {
                if (kvp.Value.Data is string str)
                {
                    size += System.Text.Encoding.UTF8.GetByteCount(str);
                }
                else
                {
                    size += 1024; // Rough estimate for objects
                }
            }
            return size;
        }

        private void CacheData(string key, object data)
        {
            var cached = new CachedData
            {
                Data = data,
                CachedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                MaxAge = TimeSpan.FromHours(_maxCacheAgeHours)
            };
            _dataCache[key] = cached;
        }

        private async UniTask LoadCatalogAsync()
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>(_dataCatalogAddress);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _catalog = JsonConvert.DeserializeObject<DataCatalog>(handle.Result.text);
                Addressables.Release(handle);
            }
            else
            {
                Debug.LogError($"[DataManager] Failed to load catalog: {handle.OperationException}");
            }
        }

        private async UniTask ValidateCatalogVersionAsync()
        {
            _currentCatalogVersion = PlayerPrefs.GetString(_catalogVersionKey, "");

            if (_catalog != null && _catalog.Version != _currentCatalogVersion)
            {
                // Version mismatch - clear cache and update
                Debug.Log($"[DataManager] Catalog version changed: {_currentCatalogVersion} -> {_catalog.Version}");
                ClearCache();
                _currentCatalogVersion = _catalog.Version;
                PlayerPrefs.SetString(_catalogVersionKey, _currentCatalogVersion);
                PlayerPrefs.Save();
            }
            else if (_catalog == null)
            {
                Debug.LogWarning("[DataManager] No catalog loaded, using network fallback");
            }
        }

        private async UniTask PreloadCriticalDataAsync()
        {
            var criticalKeys = new[]
            {
                "characters",
                "skills",
                "equipment",
                "relics",
                "runes",
                "gacha_banners",
                "campaign_stages",
                "tower_floors",
                "dungeons",
                "expeditions",
                "quests",
                "shops",
                "vip_config",
                "idle_config"
            };

            var tasks = new List<UniTask>();
            foreach (var key in criticalKeys)
            {
                tasks.Add(LoadAsync<object>(key));
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask<T> LoadDataInternalAsync<T>(string key)
        {
            // Try local Addressables first
            var localPath = $"data/{key}";
            var handle = Addressables.LoadAssetAsync<TextAsset>(localPath);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var data = JsonConvert.DeserializeObject<T>(handle.Result.text);
                Addressables.Release(handle);
                return data;
            }

            // Fallback to network if logged in
            if (GameManager.Instance?.Network?.IsLoggedIn == true)
            {
                var response = await GameManager.Instance.Network.GetAsync<T>($"data/{key}");
                if (response.Success)
                {
                    return response.Data;
                }
            }

            // Return default
            return default;
        }

        private async UniTask EnforceCacheSizeLimitAsync()
        {
            var currentSize = GetCacheSize();
            if (currentSize <= _maxCacheSizeBytes) return;

            // Remove least recently used items until under limit
            var sortedByAccess = _dataCache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ToList();

            foreach (var kvp in sortedByAccess)
            {
                _dataCache.Remove(kvp.Key);
                currentSize = GetCacheSize();
                if (currentSize <= _maxCacheSizeBytes) break;
            }

            Resources.UnloadUnusedAssets();
        }

        public static DataManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    [System.Serializable]
    public class CachedData
    {
        public object Data;
        public DateTime CachedAt;
        public DateTime LastAccessed;
        public TimeSpan MaxAge;

        public bool IsExpired => DateTime.UtcNow - CachedAt > MaxAge;
    }

    [System.Serializable]
    public class DataCatalog
    {
        public List<DataEntry> Entries;
        public string Version;
        public long LastUpdated;
    }

    [System.Serializable]
    public class DataEntry
    {
        public string Key;
        public string Address;
        public long Size;
        public string Hash;
        public bool IsCritical;
        public List<string> Dependencies;
    }
}