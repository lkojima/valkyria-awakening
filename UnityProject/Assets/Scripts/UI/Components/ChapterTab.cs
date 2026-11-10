using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Campaign;

namespace GoddessRequiem.Client.UI.Components
{
    public class ChapterTab : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _numberText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private GameObject _starContainer;
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _selectedHighlight;
        [SerializeField] private GameObject _newBadge;

        private CampaignChapter _chapter;
        private System.Action<CampaignChapter> _onSelect;

        public void Initialize(CampaignChapter chapter, System.Action<CampaignChapter> onSelect)
        {
            _chapter = chapter;
            _onSelect = onSelect;

            _numberText.text = chapter.ChapterNumber.ToString();
            _nameText.text = chapter.Name;
            _lockIcon?.SetActive(!chapter.IsUnlocked);
            _newBadge?.SetActive(chapter.IsNew);
            _selectedHighlight?.SetActive(false);

            if (_starContainer != null)
            {
                var stars = _starContainer.GetComponentsInChildren<Image>();
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i].color = i < chapter.StarsEarned ? Color.yellow : Color.gray;
                }
            }

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => _onSelect?.Invoke(_chapter));
            _selectButton?.interactable = chapter.IsUnlocked;
        }

        public void SetSelected(bool selected)
        {
            _selectedHighlight?.SetActive(selected);
        }

        public CampaignChapter Chapter => _chapter;
    }
}