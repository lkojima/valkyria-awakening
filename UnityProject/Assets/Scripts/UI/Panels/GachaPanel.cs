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
using GoddessRequiem.Client.Gacha;
using GoddessRequiem.Client.UI;

namespace GoddessRequiem.Client.UI.Panels
{
    public class GachaPanel : UIPanel
    {
        [Header("Banner Selection")]
        [SerializeField] private RectTransform _bannerTabsContainer;
        [SerializeField] private GameObject _bannerTabPrefab;
        [SerializeField] private TMP_Dropdown _bannerDropdown;

        [Header("Banner Info")]
        [SerializeField] private Image _bannerBackgroundImage;
        [SerializeField] private TextMeshProUGUI _bannerNameText;
        [SerializeField] private TextMeshProUGUI _bannerDescriptionText;
        [SerializeField] private TextMeshProUGUI _bannerEndTimeText;
        [SerializeField] private RectTransform _rateUpCharactersContainer;
        [SerializeField] private GameObject _rateUpCharacterPrefab;

        [Header("Summon Buttons")]
        [SerializeField] private Button _singleSummonBtn;
        [SerializeField] private TextMeshProUGUI _singleSummonCostText;
        [SerializeField] private Image _singleSummonCurrencyIcon;
        [SerializeField] private Button _multiSummonBtn;
        [SerializeField] private TextMeshProUGUI _multiSummonCostText;
        [SerializeField] private Image _multiSummonCurrencyIcon;
        [SerializeField] private Button _ticketSummonBtn;
        [SerializeField] private TextMeshProUGUI _ticketCountText;

        [Header("Pity Display")]
        [SerializeField] private Slider _pityProgressBar;
        [SerializeField] private TextMeshProUGUI _pityText;
        [SerializeField] private TextMeshProUGUI _softPityText;
        [SerializeField] private TextMeshProUGUI _guaranteedText;

        [Header("History")]
        [SerializeField] private Button _historyBtn;
        [SerializeField] private GameObject _historyPopup;
        [SerializeField] private RectTransform _historyContainer;
        [SerializeField] private GameObject _historyItemPrefab;

        [Header("Probability")]
        [SerializeField] private Button _probabilityBtn;
        [SerializeField] private GameObject _probabilityPopup;
        [SerializeField] private RectTransform _probabilityContainer;
        [SerializeField] private GameObject _probabilityItemPrefab;

        [Header("Animation")]
        [SerializeField] private SummonAnimation _summonAnimation;

        private GachaSystem _gachaSystem;
        private GachaBanner _currentBanner;
        private List<GachaBanner> _banners = new();

        protected override async UniTask InitializeComponentsAsync()
        {
            _gachaSystem = GachaSystem.Instance;

            SetupButtonListeners();
            SubscribeToEvents();

            await RefreshBannersAsync();
        }

        private void SetupButtonListeners()
        {
            _singleSummonBtn?.onClick.AddListener(OnSingleSummonPressed);
            _multiSummonBtn?.onClick.AddListener(OnMultiSummonPressed);
            _ticketSummonBtn?.onClick.AddListener(OnTicketSummonPressed);
            _historyBtn?.onClick.AddListener(OnHistoryPressed);
            _probabilityBtn?.onClick.AddListener(OnProbabilityPressed);
            _bannerDropdown?.onValueChanged.AddListener(OnBannerChanged);
        }

        private void SubscribeToEvents()
        {
            if (_gachaSystem != null)
            {
                _gachaSystem.OnBannersUpdated += OnBannerUpdated;
                _gachaSystem.OnPityUpdated += OnPityUpdated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_gachaSystem != null)
            {
                _gachaSystem.OnBannersUpdated -= OnBannerUpdated;
                _gachaSystem.OnPityUpdated -= OnPityUpdated;
            }
        }

        public override async UniTask ShowAsync()
        {
            await base.ShowAsync();
            await RefreshBannersAsync();
        }

        private async UniTask RefreshBannersAsync()
        {
            _banners = await _gachaSystem.GetActiveBannersAsync();
            if (_banners == null || _banners.Count == 0) return;

            // Setup dropdown
            if (_bannerDropdown != null)
            {
                _bannerDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var banner in _banners)
                {
                    options.Add(banner.Name);
                }
                _bannerDropdown.AddOptions(options);
            }

            // Select first banner
            if (_banners.Count > 0)
            {
                await SelectBannerAsync(_banners[0]);
            }
        }

        private async UniTask SelectBannerAsync(GachaBanner banner)
        {
            _currentBanner = banner;

            if (_bannerNameText != null) _bannerNameText.text = banner.Name;
            if (_bannerDescriptionText != null) _bannerDescriptionText.text = banner.Description;
            if (_bannerEndTimeText != null) _bannerEndTimeText.text = $"Termina em: {banner.EndTime:dd/MM/yyyy HH:mm}";

            // Load banner background
            if (_bannerBackgroundImage != null && !string.IsNullOrEmpty(banner.BackgroundAssetPath))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(banner.BackgroundAssetPath);
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _bannerBackgroundImage.sprite = handle.Result;
                }
            }

            // Update rate-up characters
            UpdateRateUpCharacters(banner.RateUpCharacters);

            // Update summon costs
            UpdateSummonCosts();

            // Update pity display
            UpdatePityDisplay();

            // Subscribe to banner updates
            _gachaSystem?.SubscribeToBanner(banner.BannerId);
        }

        private void UpdateRateUpCharacters(List<RateUpCharacter> rateUpChars)
        {
            if (_rateUpCharactersContainer == null || _rateUpCharacterPrefab == null) return;

            foreach (Transform child in _rateUpCharactersContainer)
            {
                Destroy(child.gameObject);
            }

            if (rateUpChars == null) return;

            foreach (var rateUp in rateUpChars)
            {
                var item = Instantiate(_rateUpCharacterPrefab, _rateUpCharactersContainer);
                var charData = _gachaSystem.GetCharacterData(rateUp.CharacterId);
                if (charData != null)
                {
                    var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                    if (texts.Length > 0)
                    {
                        texts[0].text = $"{charData.DisplayName} (Rate-Up!)";
                    }
                    var img = item.GetComponentInChildren<Image>();
                    if (img != null && charData.Rarity == Rarity.UR)
                    {
                        img.color = Color.yellow;
                    }
                }
            }
        }

        private void UpdateSummonCosts()
        {
            if (_currentBanner == null) return;

            if (_singleSummonCostText != null) _singleSummonCostText.text = _currentBanner.SingleSummonCost.ToString("N0");
            if (_multiSummonCostText != null) _multiSummonCostText.text = _currentBanner.MultiSummonCost.ToString("N0");

            // Update currency icons
            if (_singleSummonCurrencyIcon != null)
            {
                _singleSummonCurrencyIcon.sprite = GetCurrencyIcon(_currentBanner.CurrencyType);
            }
            if (_multiSummonCurrencyIcon != null)
            {
                _multiSummonCurrencyIcon.sprite = GetCurrencyIcon(_currentBanner.CurrencyType);
            }

            // Ticket count
            if (_ticketCountText != null)
            {
                var tickets = GameManager.Instance.Data.GetCachedCurrencies()?[CurrencyType.SummonTicket] ?? 0;
                _ticketCountText.text = $"{tickets}";
            }
        }

        private void UpdatePityDisplay()
        {
            if (_currentBanner == null) return;

            var pityInfo = _gachaSystem.GetPityInfo(_currentBanner.BannerId);
            if (pityInfo == null) return;

            if (_pityProgressBar != null)
            {
                _pityProgressBar.maxValue = pityInfo.HardPity;
                _pityProgressBar.value = pityInfo.CurrentPity;
            }
            if (_pityText != null)
            {
                _pityText.text = $"Pity: {pityInfo.CurrentPity} / {pityInfo.HardPity}";
            }
            if (_softPityText != null)
            {
                _softPityText.text = $"Soft Pity: {pityInfo.SoftPityStart} ({pityInfo.SoftPityRate:P0} rate)";
            }
            if (_guaranteedText != null)
            {
                _guaranteedText.text = pityInfo.IsGuaranteedNext ? "PRÓXIMO É GARANTIDO!" : "";
            }
        }

        private async void OnSingleSummonPressed()
        {
            if (_currentBanner == null) return;

            var canAfford = GameManager.Instance.Data.CanAffordCurrency(_currentBanner.CurrencyType, _currentBanner.SingleSummonCost);
            if (!canAfford)
            {
                UIManager.Instance.ShowToastAsync("Moedas insuficientes!", 2f, ToastType.Error);
                return;
            }

            var result = await _gachaSystem.SummonAsync(_currentBanner.BannerId, SummonType.Single);
            if (result != null)
            {
                await PlaySummonAnimationAsync(result);
            }
        }

        private async void OnMultiSummonPressed()
        {
            if (_currentBanner == null) return;

            var canAfford = GameManager.Instance.Data.CanAffordCurrency(_currentBanner.CurrencyType, _currentBanner.MultiSummonCost);
            if (!canAfford)
            {
                UIManager.Instance.ShowToastAsync("Moedas insuficientes!", 2f, ToastType.Error);
                return;
            }

            var result = await _gachaSystem.SummonAsync(_currentBanner.BannerId, SummonType.Multi);
            if (result != null)
            {
                await PlaySummonAnimationAsync(result);
            }
        }

        private async void OnTicketSummonPressed()
        {
            if (_currentBanner == null) return;

            var tickets = GameManager.Instance.Data.GetCachedCurrencies()?[CurrencyType.SummonTicket] ?? 0;
            if (tickets <= 0)
            {
                UIManager.Instance.ShowToastAsync("Sem tickets de invocação!", 2f, ToastType.Error);
                return;
            }

            var result = await _gachaSystem.SummonAsync(_currentBanner.BannerId, SummonType.Ticket);
            if (result != null)
            {
                await PlaySummonAnimationAsync(result);
            }
        }

        private async UniTask PlaySummonAnimationAsync(SummonResult result)
        {
            if (_summonAnimation != null)
            {
                await _summonAnimation.PlayAsync(result);
            }

            UpdatePityDisplay();
            UpdateSummonCosts();

            UIManager.Instance.ShowToastAsync($"Invocação realizada! {result.Results.Count} itens obtidos.", 2f, ToastType.Success);
        }

        private void OnHistoryPressed()
        {
            _historyPopup?.SetActive(true);
            PopulateHistory();
        }

        private void PopulateHistory()
        {
            if (_historyContainer == null || _historyItemPrefab == null) return;

            foreach (Transform child in _historyContainer)
            {
                Destroy(child.gameObject);
            }

            var history = _gachaSystem.GetSummonHistory(_currentBanner?.BannerId ?? Guid.Empty);
            if (history == null) return;

            foreach (var entry in history)
            {
                var item = Instantiate(_historyItemPrefab, _historyContainer);
                var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = entry.Timestamp.ToString("dd/MM/yyyy HH:mm");
                    texts[1].text = $"{entry.SummonType}: {entry.Results.Count} itens";
                }
            }
        }

        private void OnProbabilityPressed()
        {
            _probabilityPopup?.SetActive(true);
            PopulateProbability();
        }

        private void PopulateProbability()
        {
            if (_probabilityContainer == null || _probabilityItemPrefab == null) return;

            foreach (Transform child in _probabilityContainer)
            {
                Destroy(child.gameObject);
            }

            var probabilities = _gachaSystem.GetProbabilities(_currentBanner?.BannerId ?? Guid.Empty);
            if (probabilities == null) return;

            foreach (var prob in probabilities)
            {
                var item = Instantiate(_probabilityItemPrefab, _probabilityContainer);
                var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = prob.Rarity.ToString();
                    texts[1].text = $"{prob.BaseRate:P2} (Base) → {prob.CurrentRate:P2} (Atual)";
                }
            }
        }

        private void OnBannerChanged(int index)
        {
            if (index >= 0 && index < _banners.Count)
            {
                _ = SelectBannerAsync(_banners[index]);
            }
        }

        private void OnBannerUpdated(Guid bannerId)
        {
            if (_currentBanner != null && _currentBanner.BannerId == bannerId)
            {
                UpdatePityDisplay();
                UpdateSummonCosts();
            }
            _ = RefreshBannersAsync();
        }

        private void OnPityUpdated(Guid bannerId, PityInfo pityInfo)
        {
            if (_currentBanner != null && _currentBanner.BannerId == bannerId)
            {
                UpdatePityDisplay();
            }
        }

        private Sprite GetCurrencyIcon(CurrencyType type)
        {
            // TODO: Load from Addressables
            return null;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_currentBanner != null)
            {
                _gachaSystem?.UnsubscribeFromBanner(_currentBanner.BannerId);
            }
        }
    }
}