using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Shop;

namespace GoddessRequiem.Client.UI.Components
{
    public class CategoryTab : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _badge;
        [SerializeField] private TextMeshProUGUI _badgeText;
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _selectedHighlight;

        private ShopCategory _category;
        private System.Action<ShopCategory> _onSelect;

        public void Initialize(ShopCategory category, System.Action<ShopCategory> onSelect)
        {
            _category = category;
            _onSelect = onSelect;

            _nameText.text = category.DisplayName;

            if (!string.IsNullOrEmpty(category.IconAssetPath))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(category.IconAssetPath);
                handle.Completed += (op) => { if (op.Status == AsyncOperationStatus.Succeeded) _iconImage.sprite = op.Result; };
            }

            bool hasLimited = category.HasLimitedTimeOffers;
            _badge?.SetActive(hasLimited);
            if (_badgeText != null && hasLimited)
            {
                _badgeText.text = category.LimitedOfferCount.ToString();
            }

            _selectedHighlight?.SetActive(false);

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => _onSelect?.Invoke(_category));
        }

        public void SetSelected(bool selected)
        {
            _selectedHighlight?.SetActive(selected);
        }

        public ShopCategory Category => _category;
    }
}