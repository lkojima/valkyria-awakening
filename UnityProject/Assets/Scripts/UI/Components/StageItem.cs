using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Campaign;

namespace GoddessRequiem.Client.UI.Components
{
    public class StageItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private GameObject _starContainer;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private GameObject _completedIcon;
        [SerializeField] private TextMeshProUGUI _recommendedPowerText;
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _selectedHighlight;

        private CampaignStage _stage;
        private System.Action<CampaignStage> _onSelect;

        public void Initialize(CampaignStage stage, System.Action<CampaignStage> onSelect)
        {
            _stage = stage;
            _onSelect = onSelect;

            _nameText.text = $"{stage.StageNumber}. {stage.Name}";
            _recommendedPowerText.text = $"Poder: {stage.RecommendedPower:N0}";
            _lockIcon?.SetActive(!stage.IsUnlocked);
            _completedIcon?.SetActive(stage.IsCompleted);
            _selectedHighlight?.SetActive(false);

            if (_starContainer != null)
            {
                var stars = _starContainer.GetComponentsInChildren<Image>();
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i].color = i < stage.StarsEarned ? Color.yellow : Color.gray;
                }
            }

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => _onSelect?.Invoke(_stage));
            _selectButton?.interactable = stage.IsUnlocked;
        }

        public void SetSelected(bool selected)
        {
            _selectedHighlight?.SetActive(selected);
        }

        public CampaignStage Stage => _stage;
    }
}