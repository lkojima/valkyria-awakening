using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using Newtonsoft.Json;
using GoddessRequiem.Core;
using System.IO;
using System.Linq;

namespace GoddessRequiem.Client.Core
{
    public class NetworkManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private string _baseUrl = "https://api.goddessrequiem.com/api/v1";
        [SerializeField] private float _requestTimeout = 30f;
        [SerializeField] private int _maxRetries = 3;
        [SerializeField] private float _retryDelay = 1f;
        [SerializeField] private bool _enableRequestDeduplication = true;
        [SerializeField] private float _deduplicationWindow = 0.5f;
        [SerializeField] private int _maxQueueSize = 100;
        [SerializeField] private int _maxQueueAgeHours = 24;

        private string _accessToken;
        private string _refreshToken;
        private long _tokenExpiresAt;
        private Guid _userId;
        private string _deviceId;
        private bool _isLoggedIn;

        private readonly Queue<QueuedRequest> _requestQueue = new();
        private bool _isProcessingQueue;
        private readonly Dictionary<string, UniTaskCompletionSource<ApiResponse>> _pendingRequests = new();
        private readonly Dictionary<string, (float timestamp, UniTaskCompletionSource<ApiResponse> tcs)> _deduplicationCache = new();

        public bool IsLoggedIn => _isLoggedIn && !IsTokenExpired;
        public Guid UserId => _userId;
        public string AccessToken => _accessToken;
        public bool IsTokenExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= _tokenExpiresAt;
        public int QueuedRequestCount => _requestQueue.Count;

        public event System.Action<SessionData> OnSessionRestored;
        public event System.Action OnTokenRefreshed;
        public event System.Action OnLoggedOut;
        public event System.Action<NetworkError> OnNetworkError;
        public event System.Action<int> OnQueueSizeChanged;

        public async UniTask InitializeAsync()
        {
            _deviceId = SystemInfo.deviceUniqueIdentifier;
            await LoadSavedSessionAsync();
            await LoadOfflineQueueAsync();
        }

        public void Shutdown()
        {
            SaveOfflineQueueAsync().Forget();
        }

        public void OnGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.InGame && _isLoggedIn && _requestQueue.Count > 0)
            {
                ProcessOfflineQueueAsync().Forget();
            }
        }

        public async UniTask<ApiResponse<T>> GetAsync<T>(string endpoint, Dictionary<string, string> queryParams = null)
        {
            return await SendRequestAsync<T>(HttpMethod.Get, endpoint, null, queryParams);
        }

        public async UniTask<ApiResponse<T>> PostAsync<T>(string endpoint, object body = null, Dictionary<string, string> queryParams = null)
        {
            return await SendRequestAsync<T>(HttpMethod.Post, endpoint, body, queryParams);
        }

        public async UniTask<ApiResponse<T>> PutAsync<T>(string endpoint, object body = null, Dictionary<string, string> queryParams = null)
        {
            return await SendRequestAsync<T>(HttpMethod.Put, endpoint, body, queryParams);
        }

        public async UniTask<ApiResponse<T>> DeleteAsync<T>(string endpoint, Dictionary<string, string> queryParams = null)
        {
            return await SendRequestAsync<T>(HttpMethod.Delete, endpoint, null, queryParams);
        }

        public async UniTask<ApiResponse<T>> DeleteAccountAsync<T>(string password)
        {
            return await SendRequestAsync<T>(HttpMethod.Delete, "account", new { password }, null);
        }

        private async UniTask<ApiResponse<T>> SendRequestAsync<T>(string method, string endpoint, object body, Dictionary<string, string> queryParams)
        {
            if (IsTokenExpired && _isLoggedIn)
            {
                await RefreshTokenAsync();
            }

            var url = BuildUrl(endpoint, queryParams);
            var cacheKey = $"{method}:{url}:{JsonConvert.SerializeObject(body)}";

            // Request deduplication for GET requests
            if (_enableRequestDeduplication && method == HttpMethod.Get)
            {
                if (_deduplicationCache.TryGetValue(cacheKey, out var cached))
                {
                    if (Time.time - cached.timestamp < _deduplicationWindow)
                    {
                        return await cached.tcs.Task as UniTask<ApiResponse<T>>;
                    }
                    else
                    {
                        _deduplicationCache.Remove(cacheKey);
                    }
                }
            }

            var request = new UnityWebRequest(url, method);
            request.timeout = (int)_requestTimeout;

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body);
                var bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();

            if (_isLoggedIn)
            {
                request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            }

            request.SetRequestHeader("X-Device-ID", _deviceId);
            request.SetRequestHeader("X-App-Version", Application.version);
            request.SetRequestHeader("X-Platform", GetPlatformString());

            var tcs = new UniTaskCompletionSource<ApiResponse<T>>();

            if (_enableRequestDeduplication && method == HttpMethod.Get)
            {
                _deduplicationCache[cacheKey] = (Time.time, tcs);
            }

            int attempt = 0;
            while (attempt <= _maxRetries)
            {
                try
                {
                    var operation = request.SendWebRequest();
                    await operation;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var responseText = request.downloadHandler.text;
                        var response = JsonConvert.DeserializeObject<ApiResponse<T>>(responseText);
                        tcs.TrySetResult(response);
                        return response;
                    }
                    else if (request.responseCode == 401 && _isLoggedIn)
                    {
                        var refreshed = await RefreshTokenAsync();
                        if (refreshed)
                        {
                            request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
                            attempt++;
                            continue;
                        }
                        else
                        {
                            await LogoutAsync();
                            var errorResponse = ApiResponse<T>.Error("Unauthorized", 401);
                            tcs.TrySetResult(errorResponse);
                            return errorResponse;
                        }
                    }
                    else if (IsRetryableError(request.responseCode))
                    {
                        // Network error or server error - retry
                        var error = $"HTTP {(int)request.responseCode}: {request.error}";
                        if (attempt < _maxRetries)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(_retryDelay * (attempt + 1)));
                            attempt++;
                            continue;
                        }
                        var errorResponse = ApiResponse<T>.Error(error, (int)request.responseCode);
                        tcs.TrySetResult(errorResponse);
                        OnNetworkError?.Invoke(new NetworkError { Endpoint = endpoint, ErrorCode = (int)request.responseCode, Message = error, IsRetryable = true });
                        return errorResponse;
                    }
                    else
                    {
                        // Client error (4xx) - don't retry
                        var error = $"HTTP {(int)request.responseCode}: {request.error}";
                        var errorResponse = ApiResponse<T>.Error(error, (int)request.responseCode);
                        tcs.TrySetResult(errorResponse);
                        OnNetworkError?.Invoke(new NetworkError { Endpoint = endpoint, ErrorCode = (int)request.responseCode, Message = error, IsRetryable = false });
                        return errorResponse;
                    }
                }
                catch (System.Exception ex)
                {
                    if (attempt < _maxRetries)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(_retryDelay * (attempt + 1)));
                        attempt++;
                        continue;
                    }
                    var errorResponse = ApiResponse<T>.Error(ex.Message, -1);
                    tcs.TrySetResult(errorResponse);
                    OnNetworkError?.Invoke(new NetworkError { Endpoint = endpoint, ErrorCode = -1, Message = ex.Message, IsRetryable = true });
                    return errorResponse;
                }
            }

            var maxRetriesResponse = ApiResponse<T>.Error("Max retries exceeded", -2);
            tcs.TrySetResult(maxRetriesResponse);
            return maxRetriesResponse;
        }

        private bool IsRetryableError(long responseCode)
        {
            // 0 = network error, 5xx = server errors, 429 = rate limited
            return responseCode == 0 || responseCode >= 500 || responseCode == 429;
        }

        private class QueuedRequest
        {
            public string Method;
            public string Endpoint;
            public string Body;
            public Dictionary<string, string> QueryParams;
            public string ResponseType;
            public UniTaskCompletionSource<object> Tcs;
            public int RetryCount;
            public DateTime QueuedAt;
        }

        private async UniTask ProcessOfflineQueueAsync()
        {
            if (_isProcessingQueue || _requestQueue.Count == 0) return;

            _isProcessingQueue = true;

            while (_requestQueue.Count > 0)
            {
                var queued = _requestQueue.Dequeue();

                // Skip if too old (older than max age)
                if (DateTime.UtcNow - queued.QueuedAt > TimeSpan.FromHours(_maxQueueAgeHours))
                {
                    queued.Tcs.TrySetException(new System.Exception("Request expired"));
                    OnQueueSizeChanged?.Invoke(_requestQueue.Count);
                    continue;
                }

                try
                {
                    var url = BuildUrl(queued.Endpoint, queued.QueryParams);
                    var request = new UnityWebRequest(url, queued.Method);
                    request.timeout = (int)_requestTimeout;

                    if (!string.IsNullOrEmpty(queued.Body))
                    {
                        var bodyRaw = System.Text.Encoding.UTF8.GetBytes(queued.Body);
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        request.SetRequestHeader("Content-Type", "application/json");
                    }

                    request.downloadHandler = new DownloadHandlerBuffer();

                    if (_isLoggedIn)
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
                    }

                    request.SetRequestHeader("X-Device-ID", _deviceId);
                    request.SetRequestHeader("X-App-Version", Application.version);
                    request.SetRequestHeader("X-Platform", GetPlatformString());

                    var operation = request.SendWebRequest();
                    await operation;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var responseText = request.downloadHandler.text;
                        var responseType = System.Type.GetType(queued.ResponseType);
                        var response = JsonConvert.DeserializeObject(responseText, responseType);
                        queued.Tcs.TrySetResult(response);
                    }
                    else if (request.responseCode == 401 && _isLoggedIn)
                    {
                        var refreshed = await RefreshTokenAsync();
                        if (refreshed)
                        {
                            // Re-queue with new token
                            _requestQueue.Enqueue(queued);
                        }
                        else
                        {
                            await LogoutAsync();
                            queued.Tcs.TrySetException(new System.Exception("Unauthorized"));
                        }
                    }
                    else if (IsRetryableError(request.responseCode))
                    {
                        // Re-queue for retry
                        queued.RetryCount++;
                        if (queued.RetryCount <= _maxRetries)
                        {
                            _requestQueue.Enqueue(queued);
                            await UniTask.Delay(TimeSpan.FromSeconds(_retryDelay * queued.RetryCount));
                        }
                        else
                        {
                            queued.Tcs.TrySetException(new System.Exception($"Failed after {_maxRetries} retries"));
                        }
                    }
                    else
                    {
                        // Non-retryable error
                        queued.Tcs.TrySetException(new System.Exception($"HTTP {(int)request.responseCode}: {request.error}"));
                    }
                }
                catch (System.Exception ex)
                {
                    queued.RetryCount++;
                    if (queued.RetryCount <= _maxRetries)
                    {
                        _requestQueue.Enqueue(queued);
                        await UniTask.Delay(TimeSpan.FromSeconds(_retryDelay * queued.RetryCount));
                    }
                    else
                    {
                        queued.Tcs.TrySetException(ex);
                    }
                }

                OnQueueSizeChanged?.Invoke(_requestQueue.Count);
            }

            _isProcessingQueue = false;
            await SaveOfflineQueueAsync();
        }

        public async UniTask<ApiResponse<T>> QueueRequestAsync<T>(string method, string endpoint, object body = null, Dictionary<string, string> queryParams = null)
        {
            // If online and logged in, send immediately
            if (IsLoggedIn && Application.internetReachability != NetworkReachability.NotReachable)
            {
                return await SendRequestAsync<T>(method, endpoint, body, queryParams);
            }

            // Check queue size limit
            if (_requestQueue.Count >= _maxQueueSize)
            {
                return ApiResponse<T>.Error("Request queue full", -3);
            }

            // Queue for later
            var tcs = new UniTaskCompletionSource<ApiResponse<T>>();
            var queued = new QueuedRequest
            {
                Method = method,
                Endpoint = endpoint,
                Body = body != null ? JsonConvert.SerializeObject(body) : null,
                QueryParams = queryParams,
                ResponseType = typeof(ApiResponse<T>).AssemblyQualifiedName,
                Tcs = new UniTaskCompletionSource<object>(),
                RetryCount = 0,
                QueuedAt = DateTime.UtcNow
            };

            queued.Tcs.Task.ContinueWith(t =>
            {
                if (t.IsFaulted) tcs.TrySetException(t.Exception);
                else tcs.TrySetResult(t.Result as ApiResponse<T>);
            });

            _requestQueue.Enqueue(queued);
            OnQueueSizeChanged?.Invoke(_requestQueue.Count);
            await SaveOfflineQueueAsync();

            return await tcs.Task;
        }

        private async UniTask SaveOfflineQueueAsync()
        {
            var queueData = _requestQueue.ToArray();
            var json = JsonConvert.SerializeObject(queueData);
            await SaveManager.Instance.SaveAsync("offline_queue", json);
        }

        private async UniTask LoadOfflineQueueAsync()
        {
            var json = await SaveManager.Instance.LoadAsync<string>("offline_queue");
            if (!string.IsNullOrEmpty(json))
            {
                var queueData = JsonConvert.DeserializeObject<QueuedRequest[]>(json);
                if (queueData != null)
                {
                    foreach (var req in queueData)
                    {
                        // Skip expired requests on load
                        if (DateTime.UtcNow - req.QueuedAt <= TimeSpan.FromHours(_maxQueueAgeHours))
                        {
                            _requestQueue.Enqueue(req);
                        }
                    }
                }
            }
            OnQueueSizeChanged?.Invoke(_requestQueue.Count);
        }

        public async UniTask<bool> LoginAsync(string username, string password)
        {
            var response = await PostAsync<LoginResponse>("auth/login", new
            {
                username,
                password,
                deviceId = _deviceId,
                platform = GetPlatformEnum()
            });

            if (response.Success && response.Data != null)
            {
                SetSession(response.Data);
                await SaveSessionAsync();
                return true;
            }

            return false;
        }

        public async UniTask<bool> LoginWithDeviceIdAsync()
        {
            var response = await PostAsync<LoginResponse>("auth/login/device", new
            {
                deviceId = _deviceId
            });

            if (response.Success && response.Data != null)
            {
                SetSession(response.Data);
                await SaveSessionAsync();
                return true;
            }

            return false;
        }

        public async UniTask<bool> RegisterAsync(string username, string email, string password)
        {
            var response = await PostAsync<LoginResponse>("auth/register", new
            {
                username,
                email,
                password,
                deviceId = _deviceId,
                platform = GetPlatformEnum()
            });

            if (response.Success && response.Data != null)
            {
                SetSession(response.Data);
                await SaveSessionAsync();
                return true;
            }

            return false;
        }

        public async UniTask<bool> RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
                return false;

            var response = await PostAsync<LoginResponse>("auth/refresh", new
            {
                refreshToken = _refreshToken
            });

            if (response.Success && response.Data != null)
            {
                SetSession(response.Data);
                await SaveSessionAsync();
                OnTokenRefreshed?.Invoke();
                return true;
            }

            await LogoutAsync();
            return false;
        }

        public async UniTask LogoutAsync()
        {
            if (_isLoggedIn)
            {
                await PostAsync<object>("auth/revoke-tokens", null);
            }

            ClearSession();
            await SaveManager.Instance.DeleteAsync("session");
            OnLoggedOut?.Invoke();
        }

        public async UniTask<ApiResponse<IdleRewardSummary>> GetIdleRewardsAsync()
        {
            return await GetAsync<IdleRewardSummary>("idle/status");
        }

        public async UniTask<ApiResponse<IdleRewardSummary>> ClaimIdleRewardsAsync()
        {
            return await PostAsync<IdleRewardSummary>("idle/claim");
        }

        public SessionData GetSessionData()
        {
            return new SessionData
            {
                AccessToken = _accessToken,
                RefreshToken = _refreshToken,
                ExpiresAt = _tokenExpiresAt,
                UserId = _userId,
                DeviceId = _deviceId
            };
        }

        public async UniTask RestoreSessionAsync(SessionData sessionData)
        {
            _accessToken = sessionData.AccessToken;
            _refreshToken = sessionData.RefreshToken;
            _tokenExpiresAt = sessionData.ExpiresAt;
            _userId = sessionData.UserId;
            _deviceId = sessionData.DeviceId;
            _isLoggedIn = true;

            if (IsTokenExpired)
            {
                await RefreshTokenAsync();
            }

            OnSessionRestored?.Invoke(sessionData);
        }

        private void SetSession(LoginResponse data)
        {
            _accessToken = data.AccessToken;
            _refreshToken = data.RefreshToken;
            _tokenExpiresAt = data.ExpiresAt;
            _userId = Guid.Parse(data.User.Id);
            _isLoggedIn = true;
        }

        private void ClearSession()
        {
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiresAt = 0;
            _userId = Guid.Empty;
            _isLoggedIn = false;
        }

        private string BuildUrl(string endpoint, Dictionary<string, string> queryParams)
        {
            var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

            if (queryParams != null && queryParams.Count > 0)
            {
                var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
                foreach (var kvp in queryParams)
                {
                    query[kvp.Key] = kvp.Value;
                }
                url += "?" + query.ToString();
            }

            return url;
        }

        private string GetPlatformString()
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#elif UNITY_STANDALONE_WIN
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "Mac";
#else
            return "Unknown";
#endif
        }

        private Platform GetPlatformEnum()
        {
#if UNITY_ANDROID
            return Platform.Android;
#elif UNITY_IOS
            return Platform.iOS;
#elif UNITY_STANDALONE_WIN
            return Platform.Windows;
#elif UNITY_STANDALONE_OSX
            return Platform.Mac;
#else
            return Platform.Web;
#endif
        }

        private async UniTask LoadSavedSessionAsync()
        {
            var sessionData = await SaveManager.Instance.LoadAsync<SessionData>("session");
            if (sessionData != null)
            {
                await RestoreSessionAsync(sessionData);
            }
        }

        private async UniTask SaveSessionAsync()
        {
            await SaveManager.Instance.SaveAsync("session", GetSessionData());
        }

        public static NetworkManager Instance { get; private set; }

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

    [System.Serializable]
    public class ApiResponse<T>
    {
        public bool Success;
        public T Data;
        public string Error;
        public int Code;

        public static ApiResponse<T> Error(string error, int code = 0)
        {
            return new ApiResponse<T> { Success = false, Error = error, Code = code };
        }

        public static ApiResponse<T> SuccessResult(T data)
        {
            return new ApiResponse<T> { Success = true, Data = data };
        }
    }

    // Non-generic version for internal use
    [System.Serializable]
    public class ApiResponse
    {
        public bool Success;
        public string Error;
        public int Code;
    }

    [System.Serializable]
    public class LoginResponse
    {
        public string AccessToken;
        public string RefreshToken;
        public long ExpiresAt;
        public UserDto User;
    }

    [System.Serializable]
    public class UserDto
    {
        public string Id;
        public string Username;
        public string Email;
        public string DisplayName;
        public int VipLevel;
        public int SubscriptionTier;
    }

    [System.Serializable]
    public class IdleRewardSummary
    {
        public int HoursAccumulated;
        public List<CurrencyAmount> Currencies;
        public List<ResourceQuantity> Materials;
        public List<ResourceQuantity> Equipment;
        public List<ResourceQuantity> CharacterFragments;
        public bool HasRewards => Currencies?.Count > 0 || Materials?.Count > 0 || Equipment?.Count > 0 || CharacterFragments?.Count > 0;
    }

    [System.Serializable]
    public class CurrencyAmount
    {
        public CurrencyType Type;
        public long Amount;
    }

    [System.Serializable]
    public class ResourceQuantity
    {
        public string ResourceId;
        public int Quantity;
    }

    [System.Serializable]
    public class SessionData
    {
        public string AccessToken;
        public string RefreshToken;
        public long ExpiresAt;
        public Guid UserId;
        public string DeviceId;
    }

    [System.Serializable]
    public class NetworkError
    {
        public string Endpoint;
        public int ErrorCode;
        public string Message;
        public bool IsRetryable;
    }

    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Delete
    }

    public enum Platform
    {
        Android = 1,
        iOS = 2,
        Windows = 3,
        Mac = 4,
        Web = 5
    }

    public enum CurrencyType
    {
        Gold = 1,
        Gem = 2,
        SummonTicket = 3,
        PremiumSummonTicket = 4,
        Experience = 19,
        CharacterExp = 20,
        Stamina = 22,
        Energy = 23
    }
}