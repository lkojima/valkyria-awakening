using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GoddessRequiem.Core;
using GoddessRequiem.Client.UI;

namespace GoddessRequiem.Client.Idle
{
    public class IdleSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private IdleConfig _config;

        private IdleRewardStatus _currentStatus;
        private float _nextUpdateTime;
        private bool _isClaiming;

        public IdleRewardStatus CurrentStatus => _currentStatus;
        public bool IsAutoClaimEnabled => _currentStatus?.IsAutoClaimEnabled ?? false;
        public int MaxAccumulationHours => _currentStatus?.MaxAccumulationHours ?? 12;

        public event System.Action<IdleRewardStatus> OnStatusUpdated;
        public event System.Action<IdleRewardSummary> OnRewardsClaimed;
        public event System.Action<float> OnProgressUpdated;

        public async UniTask InitializeAsync()
        {
            await RefreshStatusAsync();

            // Subscribe to app focus events for idle reward popup
            Application.focusChanged += OnApplicationFocusChanged;

            // Start periodic updates
            _ = PeriodicUpdateLoopAsync();
        }

        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus && GameManager.Instance?.CurrentState == GameState.InGame)
            {
                // Check for idle rewards on app resume
                CheckAndShowIdleRewardsAsync().Forget();
            }
        }

        private async UniTask CheckAndShowIdleRewardsAsync()
        {
            await RefreshStatusAsync();

            if (_currentStatus?.HasRewards == true)
            {
                var summary = new IdleRewardSummary
                {
                    HoursAccumulated = _currentStatus.HoursAccumulated,
                    Currencies = _currentStatus.Currencies,
                    Materials = _currentStatus.Materials,
                    Equipment = _currentStatus.Equipment,
                    CharacterFragments = _currentStatus.CharacterFragments,
                    ClaimedAt = DateTimeOffset.UtcNow
                };

                await UIManager.Instance.ShowIdleRewardPopup(summary);
            }
        }

        public async UniTask<IdleRewardStatus> RefreshStatusAsync()
        {
            var response = await GameManager.Instance.Network.GetIdleRewardsAsync();
            if (response.Success && response.Data != null)
            {
                _currentStatus = new IdleRewardStatus
                {
                    HoursAccumulated = response.Data.HoursAccumulated,
                    Currencies = response.Data.Currencies,
                    Materials = response.Data.Materials,
                    Equipment = response.Data.Equipment,
                    CharacterFragments = response.Data.CharacterFragments,
                    HasRewards = response.Data.HasRewards,
                    LastClaimTime = DateTimeOffset.UtcNow.AddHours(-response.Data.HoursAccumulated),
                    NextClaimTime = DateTimeOffset.UtcNow.AddHours(MaxAccumulationHours - response.Data.HoursAccumulated)
                };

                OnStatusUpdated?.Invoke(_currentStatus);
            }

            return _currentStatus;
        }

        public async UniTask<IdleRewardSummary> ClaimRewardsAsync()
        {
            if (_isClaiming) return null;

            _isClaiming = true;

            var response = await GameManager.Instance.Network.ClaimIdleRewardsAsync();

            _isClaiming = false;

            if (response.Success && response.Data != null)
            {
                var summary = new IdleRewardSummary
                {
                    HoursAccumulated = response.Data.HoursAccumulated,
                    Currencies = response.Data.Currencies,
                    Materials = response.Data.Materials,
                    Equipment = response.Data.Equipment,
                    CharacterFragments = response.Data.CharacterFragments,
                    ClaimedAt = DateTimeOffset.UtcNow
                };

                // Refresh status after claim
                await RefreshStatusAsync();

                OnRewardsClaimed?.Invoke(summary);

                return summary;
            }

            return null;
        }

        public async UniTask SetAutoClaimAsync(bool enabled)
        {
            var response = await GameManager.Instance.Network.PostAsync<object>("idle/auto-claim", new { enabled });
            if (response.Success)
            {
                await RefreshStatusAsync();
            }
        }

        public async UniTask SetMaxAccumulationHoursAsync(int hours)
        {
            hours = Mathf.Clamp(hours, 1, 24);
            var response = await GameManager.Instance.Network.PostAsync<object>("idle/max-accumulation", new { hours });
            if (response.Success)
            {
                await RefreshStatusAsync();
            }
        }

        private async UniTask PeriodicUpdateLoopAsync()
        {
            while (true)
            {
                await UniTask.Delay(30000); // Update every 30 seconds

                if (GameManager.Instance.CurrentState == GameState.InGame)
                {
                    // Calculate estimated progress locally
                    UpdateLocalProgress();
                }
            }
        }

        private void UpdateLocalProgress()
        {
            if (_currentStatus == null) return;

            var elapsed = DateTimeOffset.UtcNow - _currentStatus.LastClaimTime;
            var hours = Mathf.Min((float)elapsed.TotalHours, MaxAccumulationHours);
            var progress = hours / MaxAccumulationHours;

            OnProgressUpdated?.Invoke(progress);

            // Auto-claim if enabled and maxed
            if (_currentStatus.IsAutoClaimEnabled && hours >= MaxAccumulationHours)
            {
                _ = ClaimRewardsAsync();
            }
        }

        public float GetProgressPercent()
        {
            if (_currentStatus == null) return 0f;

            var elapsed = DateTimeOffset.UtcNow - _currentStatus.LastClaimTime;
            var hours = Mathf.Min((float)elapsed.TotalHours, MaxAccumulationHours);
            return hours / MaxAccumulationHours;
        }

        public string GetTimeUntilNextClaim()
        {
            if (_currentStatus == null) return "00:00:00";

            var remaining = _currentStatus.NextClaimTime - DateTimeOffset.UtcNow;
            if (remaining.TotalSeconds <= 0) return "Ready";

            return $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        private void OnDestroy()
        {
            Application.focusChanged -= OnApplicationFocusChanged;
        }

        public static IdleSystem Instance { get; private set; }

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
    public class IdleConfig
    {
        public float UpdateInterval = 30f;
        public bool ShowNotifications = true;
    }

    [System.Serializable]
    public class IdleRewardStatus
    {
        public int HoursAccumulated;
        public int MaxAccumulationHours = 12;
        public List<CurrencyAmount> Currencies;
        public List<ResourceQuantity> Materials;
        public List<ResourceQuantity> Equipment;
        public List<ResourceQuantity> CharacterFragments;
        public bool HasRewards;
        public bool IsAutoClaimEnabled;
        public DateTimeOffset LastClaimTime;
        public DateTimeOffset NextClaimTime;
    }

    [System.Serializable]
    public class IdleRewardSummary
    {
        public int HoursAccumulated;
        public List<CurrencyAmount> Currencies;
        public List<ResourceQuantity> Materials;
        public List<ResourceQuantity> Equipment;
        public List<ResourceQuantity> CharacterFragments;
        public DateTimeOffset ClaimedAt;
    }
}