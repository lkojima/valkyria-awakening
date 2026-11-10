using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Shop;

namespace GoddessRequiem.Client.UI.Components
{
    public class ProductItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private Image _currencyIcon;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private GameObject _soldOutOverlay;
        [SerializeField] private GameObject _limitedTimeBadge;
        [SerializeField] private TextMeshProUGUI _limitText;
        [SerializeField] private Button _purchaseButton;

        private ShopProduct _product;
        private System.Action<ShopProduct> _onPurchase;

        public void Initialize(ShopProduct product, System.Action<ShopProduct> onPurchase)
        {
            _product = product;
            _onPurchase = onPurchase;

            _nameText.text = product.DisplayName;
            _priceText.text = product.Price.ToString("N0");
            _currencyIcon.sprite = GetCurrencyIcon(product.CurrencyType);

            if (!string.IsNullOrEmpty(product.IconAssetPath))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(product.IconAssetPath);
                handle.Completed += (op) => { if (op.Status == AsyncOperationStatus.Succeeded) _iconImage.sprite = op.Result; };
            }

            if (_quantityText != null)
            {
                if (product.MaxPurchaseLimit > 0)
                {
                    _quantityText.text = $"{product.CurrentPurchases} / {product.MaxPurchaseLimit}";
                }
                else
                {
                    _quantityText.text = "";
                }
            }

            _soldOutOverlay?.SetActive(product.IsSoldOut);

            bool isLimited = product.EndTime.HasValue && product.EndTime.Value > System.DateTime.UtcNow;
            _limitedTimeBadge?.SetActive(isLimited);
            if (_limitText != null && isLimited)
            {
                var remaining = product.EndTime.Value - System.DateTime.UtcNow;
                _limitText.text = remaining.Days > 0 ? $"{remaining.Days}d" : $"{remaining.Hours}h";
            }

            _purchaseButton?.onClick.RemoveAllListeners();
            _purchaseButton?.onClick.AddListener(() => _onPurchase?.Invoke(_product));
            _purchaseButton?.interactable = !product.IsSoldOut && product.CanPurchase;
        }

        public void Refresh(ShopProduct product)
        {
            Initialize(product, _onPurchase);
        }

        private Sprite GetCurrencyIcon(CurrencyType type)
        {
            // TODO: Load from Addressables
            return null;
        }

        public ShopProduct Product => _product;
    }
}