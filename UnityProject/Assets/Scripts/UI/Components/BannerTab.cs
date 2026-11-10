using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Gacha;

namespace GoddessRequiem.Client.UI.Components
{
    public class BannerTab : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _newBadge;
        [SerializeField] private GameObject _endingSoonBadge;
        [SerializeField] private TextMeshProUGUI _timeRemainingText;
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _selectedHighlight;

        private GachaBanner _banner;
        private System.Action<GachaBanner> _onSelect;

        public void Initialize(GachaBanner banner, System.Action<GachaBanner> onSelect)
        {
            _banner = banner;
            _onSelect = onSelect;

            _nameText.text = banner.Name;

            if (!string.IsNullOrEmpty(banner.IconAssetPath))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(banner.IconAssetPath);
                handle.Completed += (op) => { if (op.Status == AsyncOperationStatus.Succeeded) _iconImage.sprite = op.Result; };
            }

            _newBadge?.SetActive(banner.IsNew);
            _endingSoonBadge?.SetActive(banner.IsEndingSoon);
            _selectedHighlight?.SetActive(false);

            UpdateTimeRemaining();

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => _onSelect?.Invoke(_banner));
        }

        public void SetSelected(bool selected)
        {
            _selectedHighlight?.SetActive(selected);
        }

        private void UpdateTimeRemaining()
        {
            if (_timeRemainingText != null && _banner.EndTime > System.DateTime.UtcNow)
            {
                var remaining = _banner.EndTime - System.DateTime.UtcNow;
                _timeRemainingText.text = remaining.Days > 0 ? $"{remaining.Days}d" : $"{remaining.Hours}h {remaining.Minutes}m";
            }
        }

        public GachaBanner Banner => _banner;
    }
}