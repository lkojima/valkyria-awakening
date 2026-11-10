using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Character;

namespace GoddessRequiem.Client.UI.Components
{
    public class CharacterListItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private Image _rarityFrame;
        [SerializeField] private Image _elementIcon;
        [SerializeField] private GameObject _favoriteIcon;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private GameObject _newBadge;
        [SerializeField] private Button _selectButton;
        [SerializeField] private GameObject _selectedHighlight;

        private UserCharacterData _character;
        private CharacterData _charData;
        private System.Action<UserCharacterData, CharacterData> _onSelect;

        public void Initialize(UserCharacterData character, CharacterData charData, System.Action<UserCharacterData, CharacterData> onSelect)
        {
            _character = character;
            _charData = charData;
            _onSelect = onSelect;

            _nameText.text = charData.DisplayName;
            _levelText.text = $"Lv.{character.Level}";
            _rarityFrame.color = GetRarityColor(charData.Rarity);
            _favoriteIcon?.SetActive(character.IsFavorite);
            _lockIcon?.SetActive(character.IsLocked);
            _newBadge?.SetActive(IsNew(character));
            _selectedHighlight?.SetActive(false);

            _elementIcon.color = GetElementColor(charData.Element);

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => _onSelect?.Invoke(_character, _charData));
        }

        public void SetSelected(bool selected)
        {
            _selectedHighlight?.SetActive(selected);
        }

        public UserCharacterData Character => _character;
        public CharacterData CharData => _charData;

        private bool IsNew(UserCharacterData character)
        {
            return (System.DateTime.UtcNow - character.AcquiredAt).TotalHours < 24;
        }

        private Color GetRarityColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.R => Color.gray,
                Rarity.SR => Color.blue,
                Rarity.SSR => new Color(0.5f, 0f, 1f),
                Rarity.UR => Color.yellow,
                _ => Color.white
            };
        }

        private Color GetElementColor(Element element)
        {
            return element switch
            {
                Element.Fire => Color.red,
                Element.Water => Color.blue,
                Element.Nature => Color.green,
                Element.Light => Color.yellow,
                Element.Dark => new Color(0.5f, 0f, 1f),
                _ => Color.white
            };
        }
    }
}