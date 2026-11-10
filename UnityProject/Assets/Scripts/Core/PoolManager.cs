using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class PoolManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private int _defaultPoolSize = 10;
        [SerializeField] private int _maxPoolSize = 100;
        [SerializeField] private bool _autoExpand = true;

        private readonly Dictionary<string, GameObjectPool> _pools = new();
        private readonly Dictionary<GameObject, string> _objectToPool = new();
        private Transform _poolRoot;

        public static PoolManager Instance { get; private set; }

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

        public async UniTask InitializeAsync()
        {
            _poolRoot = new GameObject("ObjectPools").transform;
            _poolRoot.SetParent(transform);

            // Pre-create common pools
            await PreloadCommonPoolsAsync();
        }

        public void Shutdown()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();
            _objectToPool.Clear();
        }

        public void OnGameStateChanged(GameState previous, GameState current) { }

        public GameObject Get(string poolName)
        {
            if (!_pools.TryGetValue(poolName, out var pool))
            {
                Debug.LogWarning($"[PoolManager] Pool not found: {poolName}");
                return null;
            }

            var obj = pool.Get();
            _objectToPool[obj] = poolName;
            return obj;
        }

        public async UniTask<GameObject> GetAsync(string poolName, Vector3 position, Quaternion rotation)
        {
            var obj = Get(poolName);
            if (obj != null)
            {
                obj.transform.SetPositionAndRotation(position, rotation);
                obj.SetActive(true);
            }
            return obj;
        }

        public void Return(GameObject obj)
        {
            if (!_objectToPool.TryGetValue(obj, out var poolName))
            {
                Debug.LogWarning($"[PoolManager] Object not from pool: {obj.name}");
                Destroy(obj);
                return;
            }

            if (_pools.TryGetValue(poolName, out var pool))
            {
                pool.Return(obj);
            }
            else
            {
                Destroy(obj);
            }

            _objectToPool.Remove(obj);
        }

        public void ReturnDelayed(GameObject obj, float delay)
        {
            StartCoroutine(ReturnDelayedCoroutine(obj, delay));
        }

        private System.Collections.IEnumerator ReturnDelayedCoroutine(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            Return(obj);
        }

        public void CreatePool(string poolName, GameObject prefab, int initialSize = -1, int maxSize = -1)
        {
            if (_pools.ContainsKey(poolName))
            {
                Debug.LogWarning($"[PoolManager] Pool already exists: {poolName}");
                return;
            }

            var pool = new GameObjectPool(prefab, _poolRoot,
                initialSize > 0 ? initialSize : _defaultPoolSize,
                maxSize > 0 ? maxSize : _maxPoolSize,
                _autoExpand);

            _pools[poolName] = pool;
        }

        public bool HasPool(string poolName)
        {
            return _pools.ContainsKey(poolName);
        }

        public int GetAvailableCount(string poolName)
        {
            return _pools.TryGetValue(poolName, out var pool) ? pool.AvailableCount : 0;
        }

        public int GetActiveCount(string poolName)
        {
            return _pools.TryGetValue(poolName, out var pool) ? pool.ActiveCount : 0;
        }

        public void ClearPool(string poolName)
        {
            if (_pools.TryGetValue(poolName, out var pool))
            {
                pool.Clear();
            }
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
        }

        private async UniTask PreloadCommonPoolsAsync()
        {
            var commonPools = new[]
            {
                ("vfx_hit", "vfx/hit"),
                ("vfx_crit", "vfx/crit"),
                ("vfx_heal", "vfx/heal"),
                ("vfx_buff", "vfx/buff"),
                ("vfx_debuff", "vfx/debuff"),
                ("damage_number", "ui/damage_number"),
                ("floating_text", "ui/floating_text"),
                ("projectile", "vfx/projectile"),
                ("summon_particle", "vfx/summon_particle"),
                ("level_up_effect", "vfx/level_up")
            };

            foreach (var (poolName, address) in commonPools)
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(address);
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    CreatePool(poolName, handle.Result);
                }
                else
                {
                    Debug.LogWarning($"[PoolManager] Failed to load pool prefab: {address}");
                }
            }
        }
    }

    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;
        private readonly bool _autoExpand;
        private readonly Stack<GameObject> _available = new();
        private readonly HashSet<GameObject> _active = new();

        public int AvailableCount => _available.Count;
        public int ActiveCount => _active.Count;
        public int TotalCount => _available.Count + _active.Count;

        public GameObjectPool(GameObject prefab, Transform parent, int initialSize, int maxSize, bool autoExpand)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;
            _autoExpand = autoExpand;

            // Pre-warm pool
            for (int i = 0; i < initialSize; i++)
            {
                CreateObject();
            }
        }

        public GameObject Get()
        {
            GameObject obj;

            if (_available.Count > 0)
            {
                obj = _available.Pop();
            }
            else if (_active.Count < _maxSize || _autoExpand)
            {
                obj = CreateObject();
            }
            else
            {
                // Pool exhausted, return oldest active object
                obj = GetOldestActive();
                if (obj != null)
                {
                    Return(obj);
                    obj = CreateObject();
                }
            }

            if (obj != null)
            {
                _active.Add(obj);
                obj.SetActive(true);
            }

            return obj;
        }

        public void Return(GameObject obj)
        {
            if (!_active.Remove(obj)) return;

            obj.SetActive(false);
            obj.transform.SetParent(_parent);

            // Reset object state
            var pooledObj = obj.GetComponent<IPooledObject>();
            pooledObj?.OnReturnToPool();

            if (_available.Count < _maxSize)
            {
                _available.Push(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        public void Clear()
        {
            foreach (var obj in _available)
            {
                Destroy(obj);
            }
            _available.Clear();

            foreach (var obj in _active)
            {
                Destroy(obj);
            }
            _active.Clear();
        }

        private GameObject CreateObject()
        {
            var obj = Instantiate(_prefab, _parent);
            obj.SetActive(false);

            var pooledObj = obj.GetComponent<IPooledObject>();
            pooledObj?.OnCreated();

            return obj;
        }

        private GameObject GetOldestActive()
        {
            foreach (var obj in _active)
            {
                return obj;
            }
            return null;
        }
    }

    public interface IPooledObject
    {
        void OnCreated();
        void OnReturnToPool();
        void OnGetFromPool();
    }
}