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
using GoddessRequiem.Client.Shop;
using GoddessRequiem.Client.Monetization;

namespace GoddessRequiem.Client.UI.Panels
{
    public class ShopPanel : UIPanel
    {
        [Header("Category Tabs")]
        [SerializeField] private RectTransform _categoryTabsContainer;
        [SerializeField] private GameObject _categoryTabPrefab;
        [SerializeField] private TMP_Dropdown _categoryDropdown;

        [Header("Product List")]
        [SerializeField] private RectTransform _productGridContainer;
        [SerializeField] private GameObject _productItemPrefab;
        [SerializeField] private ScrollRect _productScrollRect;

        [Header("Currency Display")]
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _gemText;
        [SerializeField] private TextMeshProUGUI _staminaText;
        [SerializeField] private TextMeshProUGUI _energyText;
        [SerializeField] private TextMeshProUGUI _summonTicketText;
        [SerializeField] private TextMeshProUGUI _premiumTicketText;

        [Header("Refresh Timer")]
        [SerializeField] private TextMeshProUGUI _refreshTimerText;
        [SerializeField] private Button _manualRefreshBtn;
        [SerializeField] private TextMeshProUGUI _refreshCostText;

        [Header("Purchase Confirmation")]
        [SerializeField] private GameObject _purchaseConfirmPopup;
        [SerializeField] private TextMeshProUGUI _confirmProductNameText;
        [SerializeField] private TextMeshProUGUI _confirmPriceText;
        [SerializeField] private Image _confirmCurrencyIcon;
        [SerializeField] private TextMeshProUGUI _confirmQuantityText;
        [SerializeField] private Button _confirmPurchaseBtn;
        [SerializeField] private Button _cancelPurchaseBtn;

        [Header("Pack Details")]
        [SerializeField] private GameObject _packDetailsPopup;
        [SerializeField] private TextMeshProUGUI _packNameText;
        [SerializeField] private TextMeshProUGUI _packDescriptionText;
        [SerializeField] private RectTransform _packContentsContainer;
        [SerializeField] private GameObject _packContentItemPrefab;
        [SerializeField] private Button _packPurchaseBtn;
        [SerializeField] private TextMeshProUGUI _packPriceText;

        [Header("Subscription/Battle Pass")]
        [SerializeField] private Button _battlePassBtn;
        [SerializeField] private Button _subscriptionBtn;

        private ShopSystem _shopSystem;
        private MonetizationSystem _monetizationSystem;
        private List<ShopCategory> _categories = new();
        private ShopCategory _selectedCategory;
        private ShopProduct _selectedProduct;
        private ShopPack _selectedPack;
        private float _refreshTimer;

        protected override async UniTask InitializeComponentsAsync()
        {
            _shopSystem = ShopSystem.Instance;
            _monetizationSystem = MonetizationSystem.Instance;

            SetupButtonListeners();
            SubscribeToEvents();

            await RefreshCategoriesAsync();
            StartRefreshTimer();
        }

        private void SetupButtonListeners()
        {
            _manualRefreshBtn?.onClick.AddListener(OnManualRefreshPressed);
            _confirmPurchaseBtn?.onClick.AddListener(OnConfirmPurchase);
            _cancelPurchaseBtn?.onClick.AddListener(() => _purchaseConfirmPopup?.SetActive(false));
            _categoryDropdown?.onValueChanged.AddListener(OnCategoryDropdownChanged);
            _battlePassBtn?.onClick.AddListener(OnBattlePassPressed);
            _subscriptionBtn?.onClick.AddListener(OnSubscriptionPressed);
        }

        private void SubscribeToEvents()
        {
            if (_shopSystem != null)
            {
                _shopSystem.OnProductsRefreshed += OnProductsRefreshed;
                _shopSystem.OnProductPurchased += OnProductPurchased;
            }
            if (_monetizationSystem != null)
            {
                _monetizationSystem.OnPurchaseCompleted += OnIAPPurchaseCompleted;
                _monetizationSystem.OnPurchaseFailed += OnIAPPurchaseFailed;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_shopSystem != null)
            {
                _shopSystem.OnProductsRefreshed -= OnProductsRefreshed;
                _shopSystem.OnProductPurchased -= OnProductPurchased;
            }
            if (_monetizationSystem != null)
            {
                _monetizationSystem.OnPurchaseCompleted -= OnIAPPurchaseCompleted;
                _monetizationSystem.OnPurchaseFailed -= OnIAPPurchaseFailed;
            }
        }

        public override async UniTask ShowAsync()
        {
            await base.ShowAsync();
            await RefreshCategoriesAsync();
            UpdateCurrencies();
        }

        private async UniTask RefreshCategoriesAsync()
        {
            _categories = await _shopSystem.GetCategoriesAsync();
            if (_categories == null || _categories.Count == 0) return;

            // Setup dropdown
            if (_categoryDropdown != null)
            {
                _categoryDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var cat in _categories)
                {
                    options.Add(cat.DisplayName);
                }
                _categoryDropdown.AddOptions(options);
            }

            // Setup tabs
            SetupCategoryTabs();

            // Select first category
            if (_categories.Count > 0)
            {
                await SelectCategoryAsync(_categories[0]);
            }
        }

        private void SetupCategoryTabs()
        {
            if (_categoryTabsContainer == null || _categoryTabPrefab == null) return;

            foreach (Transform child in _categoryTabsContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var category in _categories)
            {
                var tab = Instantiate(_categoryTabPrefab, _categoryTabsContainer);
                var btn = tab.GetComponent<Button>();
                var text = tab.GetComponentInChildren<TextMeshProUGUI>();
                var icon = tab.transform.Find("Icon")?.GetComponent<Image>();
                var badge = tab.transform.Find("Badge")?.gameObject;
                var badgeText = badge?.GetComponentInChildren<TextMeshProUGUI>();

                if (text != null) text.text = category.DisplayName;
                if (icon != null && !string.IsNullOrEmpty(category.IconAssetPath))
                {
                    var handle = Addressables.LoadAssetAsync<Sprite>(category.IconAssetPath);
                    handle.Completed += (op) => { if (op.Status == AsyncOperationStatus.Succeeded) icon.sprite = op.Result; };
                }
                if (badge != null) badge.SetActive(category.HasLimitedTimeOffers);
                if (badgeText != null) badgeText.text = category.LimitedOfferCount.ToString();

                if (btn != null)
                {
                    var catCopy = category;
                    btn.onClick.AddListener(() => _ = SelectCategoryAsync(catCopy));
                }
            }
        }

        private async UniTask SelectCategoryAsync(ShopCategory category)
        {
            _selectedCategory = category;

            if (_categoryDropdown != null)
            {
                _categoryDropdown.value = _categories.IndexOf(category);
            }

            await RefreshProductsAsync();
        }

        private async UniTask RefreshProductsAsync()
        {
            if (_selectedCategory == null) return;

            var products = await _shopSystem.GetProductsAsync(_selectedCategory.CategoryId);
            if (products == null) return;

            if (_productGridContainer == null || _productItemPrefab == null) return;

            foreach (Transform child in _productGridContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var product in products)
            {
                var item = Instantiate(_productItemPrefab, _productGridContainer);
                var btn = item.GetComponent<Button>();
                var nameText = item.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                var priceText = item.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
                var currencyIcon = item.transform.Find("CurrencyIcon")?.GetComponent<Image>();
                var iconImage = item.transform.Find("Icon")?.GetComponent<Image>();
                var quantityText = item.transform.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
                var soldOutOverlay = item.transform.Find("SoldOutOverlay")?.gameObject;
                var limitedTimeBadge = item.transform.Find("LimitedTimeBadge")?.gameObject;
                var limitText = limitedTimeBadge?.GetComponentInChildren<TextMeshProUGUI>();

                if (nameText != null) nameText.text = product.DisplayName;
                if (priceText != null) priceText.text = product.Price.ToString("N0");
                if (currencyIcon != null) currencyIcon.sprite = GetCurrencyIcon(product.CurrencyType);
                if (iconImage != null && !string.IsNullOrEmpty(product.IconAssetPath))
                {
                    var handle = Addressables.LoadAssetAsync<Sprite>(product.IconAssetPath);
                    handle.Completed += (op) => { if (op.Status == AsyncOperationStatus.Succeeded) iconImage.sprite = op.Result; };
                }
                if (quantityText != null)
                {
                    if (product.MaxPurchaseLimit > 0)
                    {
                        quantityText.text = $"{product.CurrentPurchases} / {product.MaxPurchaseLimit}";
                    }
                    else
                    {
                        quantityText.text = "";
                    }
                }
                if (soldOutOverlay != null) soldOutOverlay.SetActive(product.IsSoldOut);
                if (limitedTimeBadge != null)
                {
                    bool isLimited = product.EndTime.HasValue && product.EndTime.Value > DateTime.UtcNow;
                    limitedTimeBadge.SetActive(isLimited);
                    if (limitText != null && isLimited)
                    {
                        var remaining = product.EndTime.Value - DateTime.UtcNow;
                        limitText.text = remaining.Days > 0 ? $"{remaining.Days}d" : $"{remaining.Hours}h";
                    }
                }

                if (btn != null)
                {
                    btn.interactable = !product.IsSoldOut && product.CanPurchase;
                    btn.onClick.RemoveAllListeners();
                    if (product.IsPack)
                    {
                        btn.onClick.AddListener(() => ShowPackDetails(product));
                    }
                    else
                    {
                        btn.onClick.AddListener(() => ShowPurchaseConfirm(product));
                    }
                }
            }
        }

        private void ShowPurchaseConfirm(ShopProduct product)
        {
            _selectedProduct = product;
            _purchaseConfirmPopup?.SetActive(true);

            if (_confirmProductNameText != null) _confirmProductNameText.text = product.DisplayName;
            if (_confirmPriceText != null) _confirmPriceText.text = product.Price.ToString("N0");
            if (_confirmCurrencyIcon != null) _confirmCurrencyIcon.sprite = GetCurrencyIcon(product.CurrencyType);
            if (_confirmQuantityText != null) _confirmQuantityText.text = product.Quantity > 1 ? $"x{product.Quantity}" : "";
        }

        private void ShowPackDetails(ShopProduct product)
        {
            _selectedPack = product.PackData;
            _packDetailsPopup?.SetActive(true);

            if (_packNameText != null) _packNameText.text = product.DisplayName;
            if (_packDescriptionText != null) _packDescriptionText.text = product.Description;
            if (_packPriceText != null) _packPriceText.text = product.Price.ToString("N0");

            if (_packContentsContainer != null && _packContentItemPrefab != null && _selectedPack != null)
            {
                foreach (Transform child in _packContentsContainer)
                {
                    Destroy(child.gameObject);
                }

                if (_selectedPack.Contents != null)
                {
                    foreach (var content in _selectedPack.Contents)
                    {
                        var item = Instantiate(_packContentItemPrefab, _packContentsContainer);
                        var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();

                        if (content.Type == RewardType.Currency)
                        {
                            if (System.Enum.TryParse<CurrencyType>(content.ResourceId, out var currencyType))
                                itemUI.SetCurrencyData(currencyType, content.Quantity);
                        }
                        else
                        {
                            itemUI.SetItemData(content.ResourceId, content.Quantity);
                        }

                        var probabilityText = item.transform.Find("ProbabilityText")?.GetComponent<TextMeshProUGUI>();
                        if (probabilityText != null && content.DropRate > 0)
                        {
                            probabilityText.text = $"{content.DropRate:P1}";
                        }
                    }
                }

                _packPurchaseBtn.onClick.RemoveAllListeners();
                _packPurchaseBtn.onClick.AddListener(() => ShowPurchaseConfirm(product));
            }
        }

        private async void OnConfirmPurchase()
        {
            if (_selectedProduct == null) return;

            var canAfford = GameManager.Instance.Data.CanAffordCurrency(_selectedProduct.CurrencyType, _selectedProduct.Price);
            if (!canAfford)
            {
                UIManager.Instance.ShowToastAsync("Moedas insuficientes!", 2f, ToastType.Error);
                return;
            }

            _confirmPurchaseBtn.interactable = false;

            if (_selectedProduct.IsIAP)
            {
                // Handle IAP purchase
                var result = await _monetizationSystem.PurchaseIAPAsync(_selectedProduct.IAPProductId);
                if (result)
                {
                    UIManager.Instance.ShowToastAsync("Compra realizada com sucesso!", 2f, ToastType.Success);
                }
                else
                {
                    UIManager.Instance.ShowToastAsync("Falha na compra. Tente novamente.", 2f, ToastType.Error);
                }
            }
            else
            {
                // Handle in-game currency purchase
                var result = await _shopSystem.PurchaseProductAsync(_selectedProduct.ProductId);
                if (result != null)
                {
                    UIManager.Instance.ShowToastAsync("Compra realizada com sucesso!", 2f, ToastType.Success);
                    UpdateCurrencies();
                }
                else
                {
                    UIManager.Instance.ShowToastAsync("Falha na compra. Tente novamente.", 2f, ToastType.Error);
                }
            }

            _purchaseConfirmPopup?.SetActive(false);
            _confirmPurchaseBtn.interactable = true;
            _selectedProduct = null;
        }

        private void OnManualRefreshPressed()
        {
            var refreshCost = _shopSystem.GetManualRefreshCost(_selectedCategory?.CategoryId ?? Guid.Empty);
            var canAfford = GameManager.Instance.Data.CanAffordCurrency(CurrencyType.Gem, refreshCost);

            if (!canAfford)
            {
                UIManager.Instance.ShowToastAsync("Gemas insuficientes para atualizar!", 2f, ToastType.Error);
                return;
            }

            _shopSystem.RefreshShopAsync(_selectedCategory?.CategoryId ?? Guid.Empty);
        }

        private void OnCategoryDropdownChanged(int index)
        {
            if (index >= 0 && index < _categories.Count)
            {
                _ = SelectCategoryAsync(_categories[index]);
            }
        }

        private void OnProductsRefreshed(Guid categoryId)
        {
            if (_selectedCategory != null && _selectedCategory.CategoryId == categoryId)
            {
                _ = RefreshProductsAsync();
            }
            UpdateRefreshTimer();
        }

        private void OnProductPurchased(Guid productId)
        {
            _ = RefreshProductsAsync();
            UpdateCurrencies();
        }

        private void OnIAPPurchaseCompleted(string productId, string receipt)
        {
            UIManager.Instance.ShowToastAsync("Compra IAP confirmada!", 2f, ToastType.Success);
            UpdateCurrencies();
        }

        private void OnIAPPurchaseFailed(string productId, string error)
        {
            UIManager.Instance.ShowToastAsync($"Falha na compra IAP: {error}", 3f, ToastType.Error);
        }

        private void OnBattlePassPressed()
        {
            _uiManager?.OpenPopupAsync<UIPanel>("BattlePassPopup");
        }

        private void OnSubscriptionPressed()
        {
            _uiManager?.OpenPopupAsync<UIPanel>("SubscriptionPopup");
        }

        private void UpdateCurrencies()
        {
            var currencies = GameManager.Instance.Data.GetCachedCurrencies();
            if (currencies != null)
            {
                _goldText?.text = currencies.GetValueOrDefault(CurrencyType.Gold, 0).ToString("N0");
                _gemText?.text = currencies.GetValueOrDefault(CurrencyType.Gem, 0).ToString("N0");
                _staminaText?.text = currencies.GetValueOrDefault(CurrencyType.Stamina, 0).ToString("N0");
                _energyText?.text = currencies.GetValueOrDefault(CurrencyType.Energy, 0).ToString("N0");
                _summonTicketText?.text = currencies.GetValueOrDefault(CurrencyType.SummonTicket, 0).ToString("N0");
                _premiumTicketText?.text = currencies.GetValueOrDefault(CurrencyType.PremiumSummonTicket, 0).ToString("N0");
            }
        }

        private void StartRefreshTimer()
        {
            // Update timer every second
            Observable.EveryUpdate()
                .Where(_ => _refreshTimer > 0)
                .Subscribe(_ => UpdateRefreshTimerUI());
        }

        private void UpdateRefreshTimer()
        {
            _refreshTimer = _shopSystem.GetNextAutoRefreshTime();
        }

        private void UpdateRefreshTimerUI()
        {
            if (_refreshTimer <= 0)
            {
                _refreshTimerText.text = "Atualizando...";
                return;
            }

            var ts = TimeSpan.FromSeconds(_refreshTimer);
            _refreshTimerText.text = ts.Days > 0 ? $"{ts.Days}d {ts.Hours}h" : $"{ts.Hours}h {ts.Minutes}m";
            _refreshTimer -= Time.deltaTime;
        }

        private Sprite GetCurrencyIcon(CurrencyType type)
        {
            // TODO: Load from Addressables
            return null;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }
}