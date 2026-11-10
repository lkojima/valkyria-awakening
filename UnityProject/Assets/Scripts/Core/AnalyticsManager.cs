using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class AnalyticsManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private AnalyticsConfig _config;
        [SerializeField] private bool _enableUnityAnalytics = true;
        [SerializeField] private bool _enableFirebaseAnalytics = true;
        [SerializeField] private bool _enableCustomAnalytics = true;

        private Queue<AnalyticsEvent> _eventQueue = new();
        private bool _isProcessingQueue;
        private float _lastFlushTime;
        private readonly HashSet<string> _trackedFirstTimes = new();

        public async UniTask InitializeAsync()
        {
            if (_enableUnityAnalytics)
            {
                InitializeUnityAnalytics();
            }

            if (_enableFirebaseAnalytics)
            {
                await InitializeFirebaseAnalyticsAsync();
            }

            // Start periodic flush
            _ = PeriodicFlushLoopAsync();
        }

        public void Shutdown()
        {
            FlushQueue();
        }

        public void OnGameStateChanged(GameState previous, GameState current)
        {
            TrackEvent("game_state_changed", new Dictionary<string, object>
            {
                { "previous_state", previous.ToString() },
                { "current_state", current.ToString() }
            });
        }

        public void TrackEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!_config.EnableTracking) return;

            var evt = new AnalyticsEvent
            {
                Name = eventName,
                Parameters = parameters ?? new Dictionary<string, object>(),
                Timestamp = DateTimeOffset.UtcNow,
                SessionId = GameManager.Instance.Network.GetSessionData()?.UserId.ToString() ?? "unknown"
            };

            // Add common parameters
            evt.Parameters["app_version"] = Application.version;
            evt.Parameters["platform"] = Application.platform.ToString();
            evt.Parameters["device_model"] = SystemInfo.deviceModel;
            evt.Parameters["os_version"] = SystemInfo.operatingSystem;

            _eventQueue.Enqueue(evt);

            // Send to Unity Analytics
            if (_enableUnityAnalytics)
            {
                UnityEngine.Analytics.AnalyticsEvent.Custom(eventName, evt.Parameters);
            }

            // Send to Firebase
            if (_enableFirebaseAnalytics)
            {
                Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName,
                    ConvertToFirebaseParameters(evt.Parameters));
            }
        }

        public void TrackFirstTime(string eventName, Dictionary<string, object> parameters = null)
        {
            if (_trackedFirstTimes.Contains(eventName)) return;

            _trackedFirstTimes.Add(eventName);
            TrackEvent($"first_{eventName}", parameters);
        }

        public void TrackProgression(string milestone, Dictionary<string, object> data = null)
        {
            var parameters = data ?? new Dictionary<string, object>();
            parameters["milestone"] = milestone;
            parameters["player_level"] = GetPlayerLevel();
            parameters["vip_level"] = GetVipLevel();
            parameters["total_playtime"] = GetTotalPlayTime();

            TrackEvent("progression", parameters);
        }

        public void TrackPurchase(string productId, decimal price, string currency, Dictionary<string, object> data = null)
        {
            var parameters = data ?? new Dictionary<string, object>();
            parameters["product_id"] = productId;
            parameters["price"] = (double)price;
            parameters["currency"] = currency;
            parameters["player_level"] = GetPlayerLevel();

            TrackEvent("purchase", parameters);

            // Unity IAP tracking
            if (_enableUnityAnalytics)
            {
                UnityEngine.Analytics.AnalyticsEvent.Transaction(productId, (double)price, currency, null, null);
            }
        }

        public void TrackRetention(int day)
        {
            TrackEvent($"retention_day_{day}", new Dictionary<string, object>
            {
                { "day", day },
                { "player_level", GetPlayerLevel() }
            });
        }

        public void TrackError(string error, Dictionary<string, object> context = null)
        {
            var parameters = context ?? new Dictionary<string, object>();
            parameters["error_message"] = error;
            parameters["stack_trace"] = System.Environment.StackTrace;

            TrackEvent("error", parameters);
        }

        public void TrackSessionStart()
        {
            TrackEvent("session_start", new Dictionary<string, object>
            {
                { "player_level", GetPlayerLevel() },
                { "total_sessions", GetTotalSessions() }
            });
        }

        public void TrackSessionEnd()
        {
            TrackEvent("session_end", new Dictionary<string, object>
            {
                { "session_duration", GetSessionDuration() },
                { "player_level", GetPlayerLevel() }
            });
        }

        public void TrackGachaSummon(Guid bannerId, bool isMulti, List<GachaResult> results, CurrencyAmount cost)
        {
            var parameters = new Dictionary<string, object>
            {
                { "banner_id", bannerId.ToString() },
                { "is_multi", isMulti },
                { "cost_type", cost.Type.ToString() },
                { "cost_amount", cost.Amount },
                { "results_count", results.Count }
            };

            // Track rarity distribution
            var rarityCounts = new Dictionary<string, int>();
            foreach (var result in results)
            {
                var rarity = result.Rarity.ToString();
                rarityCounts[rarity] = rarityCounts.GetValueOrDefault(rarity, 0) + 1;
            }
            parameters["rarity_distribution"] = rarityCounts;

            TrackEvent("gacha_summon", parameters);
        }

        public void TrackBattle(GameMode mode, BattleResult result, int turns, Dictionary<string, object> data = null)
        {
            var parameters = data ?? new Dictionary<string, object>();
            parameters["mode"] = mode.ToString();
            parameters["result"] = result.ToString();
            parameters["turns"] = turns;
            parameters["player_level"] = GetPlayerLevel();

            TrackEvent("battle", parameters);
        }

        private async UniTask InitializeFirebaseAnalyticsAsync()
        {
            try
            {
                // Firebase Analytics initialization would go here
                // await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
                Debug.Log("[AnalyticsManager] Firebase Analytics initialized");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AnalyticsManager] Firebase Analytics init failed: {ex.Message}");
            }
        }

        private void InitializeUnityAnalytics()
        {
            UnityEngine.Analytics.AnalyticsEvent.GameStart();
            Debug.Log("[AnalyticsManager] Unity Analytics initialized");
        }

        private Firebase.Analytics.Parameter[] ConvertToFirebaseParameters(Dictionary<string, object> parameters)
        {
            var list = new List<Firebase.Analytics.Parameter>();
            foreach (var kvp in parameters)
            {
                if (kvp.Value is string s)
                    list.Add(new Firebase.Analytics.Parameter(kvp.Key, s));
                else if (kvp.Value is int i)
                    list.Add(new Firebase.Analytics.Parameter(kvp.Key, i));
                else if (kvp.Value is long l)
                    list.Add(new Firebase.Analytics.Parameter(kvp.Key, l));
                else if (kvp.Value is double d)
                    list.Add(new Firebase.Analytics.Parameter(kvp.Key, d));
                else if (kvp.Value is float f)
                    list.Add(new Firebase.Analytics.Parameter(kvp.Key, f));
                else if (kvp.Value is bool b)
                    list.Add(new Firebase.Analytics.Parameter(kvp.Key, b));
            }
            return list.ToArray();
        }

        private async UniTask PeriodicFlushLoopAsync()
        {
            while (true)
            {
                await UniTask.Delay(30000); // Flush every 30 seconds
                FlushQueue();
            }
        }

        private void FlushQueue()
        {
            if (_config.EnableCustomAnalytics && _eventQueue.Count > 0)
            {
                var events = new List<AnalyticsEvent>();
                while (_eventQueue.Count > 0)
                {
                    events.Add(_eventQueue.Dequeue());
                }

                _ = SendBatchAsync(events);
            }

            _lastFlushTime = Time.time;
        }

        private async UniTask SendBatchAsync(List<AnalyticsEvent> events)
        {
            try
            {
                await GameManager.Instance.Network.PostAsync<object>("analytics/batch", new { events });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AnalyticsManager] Failed to send analytics batch: {ex.Message}");
                // Re-queue failed events
                foreach (var evt in events)
                {
                    _eventQueue.Enqueue(evt);
                }
            }
        }

        private int GetPlayerLevel()
        {
            // Would get from player data
            return 1;
        }

        private int GetVipLevel()
        {
            return 0;
        }

        private long GetTotalPlayTime()
        {
            return 0;
        }

        private int GetTotalSessions()
        {
            return 0;
        }

        private float GetSessionDuration()
        {
            return Time.time - _lastFlushTime;
        }
    }

    [System.Serializable]
    public class AnalyticsConfig
    {
        public bool EnableTracking = true;
        public int MaxQueueSize = 1000;
        public float FlushInterval = 30f;
    }

    [System.Serializable]
    public class AnalyticsEvent
    {
        public string Name;
        public Dictionary<string, object> Parameters;
        public DateTimeOffset Timestamp;
        public string SessionId;
    }
}