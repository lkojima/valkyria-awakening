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
using GoddessRequiem.Client.Character;

namespace GoddessRequiem.Client.UI.Panels
{
    public class CharacterPanel : UIPanel
    {
        [Header("Character List")]
        [SerializeField] private RectTransform _characterListContainer;
        [SerializeField] private GameObject _characterListItemPrefab;
        [SerializeField] private ScrollRect _characterListScroll;
        [SerializeField] private TMP_Dropdown _rarityFilterDropdown;
        [SerializeField] private TMP_Dropdown _elementFilterDropdown;
        [SerializeField] private TMP_Dropdown _classFilterDropdown;
        [SerializeField] private TMP_InputField _searchInputField;

        [Header("Character Detail")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Image _fullBodyImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _rarityText;
        [SerializeField] private TextMeshProUGUI _elementText;
        [SerializeField] private TextMeshProUGUI _classText;
        [SerializeField] private TextMeshProUGUI _roleText;
        [SerializeField] private Slider _levelSlider;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _expText;
        [SerializeField] private Button _levelUpBtn;
        [SerializeField] private Button _autoLevelUpBtn;
        [SerializeField] private TextMeshProUGUI _ascensionText;
        [SerializeField] private Button _ascendBtn;
        [SerializeField] private RectTransform _ascensionMaterialsContainer;
        [SerializeField] private GameObject _materialItemPrefab;

        [Header("Skills")]
        [SerializeField] private RectTransform _skillsContainer;
        [SerializeField] private GameObject _skillSlotPrefab;
        [SerializeField] private Button _basicSkillBtn;
        [SerializeField] private Button _specialSkill1Btn;
        [SerializeField] private Button _specialSkill2Btn;
        [SerializeField] private Button _ultimateSkillBtn;

        [Header("Equipment")]
        [SerializeField] private RectTransform _equipmentSlotsContainer;
        [SerializeField] private GameObject _equipmentSlotPrefab;

        [Header("Relic & Runes")]
        [SerializeField] private RectTransform _relicSlotContainer;
        [SerializeField] private RectTransform _runeSlotsContainer;
        [SerializeField] private GameObject _relicSlotPrefab;
        [SerializeField] private GameObject _runeSlotPrefab;

        [Header("Affinity")]
        [SerializeField] private Slider _affinitySlider;
        [SerializeField] private TextMeshProUGUI _affinityLevelText;
        [SerializeField] private TextMeshProUGUI _affinityExpText;
        [SerializeField] private RectTransform _affinityRewardsContainer;
        [SerializeField] private GameObject _affinityRewardPrefab;

        [Header("Skins")]
        [SerializeField] private Button _skinsBtn;
        [SerializeField] private RectTransform _skinPreviewContainer;
        [SerializeField] private GameObject _skinPreviewItemPrefab;

        [Header("Actions")]
        [SerializeField] private Button _favoriteBtn;
        [SerializeField] private Button _lockBtn;
        [SerializeField] private Button _closeDetailBtn;

        [Header("Stats")]
        [SerializeField] private RectTransform _statsContainer;
        [SerializeField] private GameObject _statRowPrefab;

        private CharacterSystem _characterSystem;
        private List<CharacterListItemUI> _listItems = new();
        private UserCharacterData _selectedCharacter;
        private CharacterData _selectedCharData;
        private Rarity _currentRarityFilter = Rarity.R;
        private Element _currentElementFilter = Element.None;
        private CharacterClass _currentClassFilter = CharacterClass.Warrior;
        private string _searchText = "";

        protected override async UniTask InitializeComponentsAsync()
        {
            _characterSystem = CharacterSystem.Instance;

            SetupFilterDropdowns();
            SetupButtonListeners();
            SubscribeToEvents();

            await RefreshCharacterListAsync();
        }

        private void SetupFilterDropdowns()
        {
            _rarityFilterDropdown?.ClearOptions();
            _rarityFilterDropdown?.AddOptions(new List<string> { "Todas", "R", "SR", "SSR", "UR" });
            _rarityFilterDropdown?.onValueChanged.AddListener(OnRarityFilterChanged);

            _elementFilterDropdown?.ClearOptions();
            _elementFilterDropdown?.AddOptions(new List<string> { "Todos", "Fogo", "Água", "Natureza", "Luz", "Trevas" });
            _elementFilterDropdown?.onValueChanged.AddListener(OnElementFilterChanged);

            _classFilterDropdown?.ClearOptions();
            _classFilterDropdown?.AddOptions(new List<string> { "Todas", "Guerreiro", "Mago", "Arqueiro", "Assassino", "Tanque", "Suporte", "Curandeiro", "Invocador", "Bardo", "Paladino" });
            _classFilterDropdown?.onValueChanged.AddListener(OnClassFilterChanged);

            _searchInputField?.onValueChanged.AddListener(OnSearchChanged);
        }

        private void SetupButtonListeners()
        {
            _levelUpBtn?.onClick.AddListener(OnLevelUpPressed);
            _autoLevelUpBtn?.onClick.AddListener(OnAutoLevelUpPressed);
            _ascendBtn?.onClick.AddListener(OnAscendPressed);
            _basicSkillBtn?.onClick.AddListener(() => OnSkillUpgradePressed(SkillType.Basic));
            _specialSkill1Btn?.onClick.AddListener(() => OnSkillUpgradePressed(SkillType.Special));
            _specialSkill2Btn?.onClick.AddListener(() => OnSkillUpgradePressed(SkillType.Special)); // Second special
            _ultimateSkillBtn?.onClick.AddListener(() => OnSkillUpgradePressed(SkillType.Ultimate));
            _favoriteBtn?.onClick.AddListener(OnFavoriteToggled);
            _lockBtn?.onClick.AddListener(OnLockToggled);
            _closeDetailBtn?.onClick.AddListener(CloseDetail);
            _skinsBtn?.onClick.AddListener(OnSkinsPressed);
        }

        private void SubscribeToEvents()
        {
            if (_characterSystem != null)
            {
                _characterSystem.OnCharacterAcquired += OnCharacterAcquired;
                _characterSystem.OnCharacterUpdated += OnCharacterUpdated;
                _characterSystem.OnCharacterLeveledUp += OnCharacterLeveledUp;
                _characterSystem.OnCharacterAscended += OnCharacterAscended;
                _characterSystem.OnSkinChanged += OnSkinChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_characterSystem != null)
            {
                _characterSystem.OnCharacterAcquired -= OnCharacterAcquired;
                _characterSystem.OnCharacterUpdated -= OnCharacterUpdated;
                _characterSystem.OnCharacterLeveledUp -= OnCharacterLeveledUp;
                _characterSystem.OnCharacterAscended -= OnCharacterAscended;
                _characterSystem.OnSkinChanged -= OnSkinChanged;
            }
        }

        public override async UniTask ShowAsync()
        {
            await base.ShowAsync();
            await RefreshCharacterListAsync();
        }

        private async UniTask RefreshCharacterListAsync()
        {
            ClearList();

            var characters = _characterSystem?.GetAllUserCharacters();
            if (characters == null) return;

            var filtered = FilterCharacters(characters);

            foreach (var character in filtered)
            {
                var charData = _characterSystem.GetCharacterData(character.CharacterId);
                if (charData == null) continue;

                var itemObj = Instantiate(_characterListItemPrefab, _characterListContainer);
                var itemUI = itemObj.GetComponent<CharacterListItemUI>() ?? itemObj.AddComponent<CharacterListItemUI>();
                itemUI.Initialize(character, charData, OnCharacterSelected);
                _listItems.Add(itemUI);
            }

            // Select first if none selected
            if (_selectedCharacter == null && _listItems.Count > 0)
            {
                _listItems[0].Select();
            }
        }

        private List<UserCharacterData> FilterCharacters(List<UserCharacterData> characters)
        {
            var result = new List<UserCharacterData>();

            foreach (var character in characters)
            {
                var charData = _characterSystem.GetCharacterData(character.CharacterId);
                if (charData == null) continue;

                // Rarity filter
                if (_rarityFilterDropdown.value > 0 && charData.Rarity != (Rarity)_rarityFilterDropdown.value)
                    continue;

                // Element filter
                if (_elementFilterDropdown.value > 0 && charData.Element != (Element)_elementFilterDropdown.value)
                    continue;

                // Class filter
                if (_classFilterDropdown.value > 0 && charData.Class != (CharacterClass)_classFilterDropdown.value)
                    continue;

                // Search filter
                if (!string.IsNullOrEmpty(_searchText) &&
                    !charData.DisplayName.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(character);
            }

            return result;
        }

        private void ClearList()
        {
            foreach (var item in _listItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _listItems.Clear();
        }

        private void OnCharacterSelected(UserCharacterData character, CharacterData charData)
        {
            _selectedCharacter = character;
            _selectedCharData = charData;
            ShowDetail(character, charData);
        }

        private async void ShowDetail(UserCharacterData character, CharacterData charData)
        {
            _detailPanel?.SetActive(true);

            // Basic info
            _nameText.text = charData.DisplayName;
            _rarityText.text = charData.Rarity.ToString();
            _rarityText.color = GetRarityColor(charData.Rarity);
            _elementText.text = charData.Element.ToString();
            _classText.text = charData.Class.ToString();
            _roleText.text = charData.Role.ToString();

            // Level
            _levelText.text = $"Lv.{character.Level} / {charData.MaxLevel}";
            _levelSlider.maxValue = charData.MaxLevel;
            _levelSlider.value = character.Level;
            _expText.text = $"EXP: {character.Experience:N0}";

            // Ascension
            _ascensionText.text = $"+{character.AscensionLevel} / {charData.MaxAscension}";

            // Load portrait
            var assets = await _characterSystem.LoadCharacterAssetsAsync(character.CharacterId);
            if (assets != null)
            {
                if (_portraitImage != null && assets.Portrait != null)
                    _portraitImage.sprite = assets.Portrait;
                if (_fullBodyImage != null && assets.FullBody != null)
                    _fullBodyImage.sprite = assets.FullBody;
            }

            // Skills
            UpdateSkillsUI(character, charData);

            // Equipment
            UpdateEquipmentUI(character);

            // Relic & Runes
            UpdateRelicRunesUI(character);

            // Affinity
            UpdateAffinityUI(character, charData);

            // Stats
            UpdateStatsUI(character);

            // Favorite/Lock state
            UpdateFavoriteLockUI(character);
        }

        private void UpdateSkillsUI(UserCharacterData character, CharacterData charData)
        {
            var skills = new[]
            {
                new { Skill = charData.BasicSkillId, Level = character.Skill1Level, Button = _basicSkillBtn, Type = SkillType.Basic },
                new { Skill = charData.SpecialSkill1Id, Level = character.Skill2Level, Button = _specialSkill1Btn, Type = SkillType.Special },
                new { Skill = charData.SpecialSkill2Id, Level = character.Skill3Level, Button = _specialSkill2Btn, Type = SkillType.Special },
                new { Skill = charData.UltimateSkillId, Level = character.UltimateLevel, Button = _ultimateSkillBtn, Type = SkillType.Ultimate }
            };

            foreach (var skillInfo in skills)
            {
                if (skillInfo.Button != null)
                {
                    var skillText = skillInfo.Button.GetComponentInChildren<TextMeshProUGUI>();
                    if (skillText != null)
                        skillText.text = $"{skillInfo.Type}: Lv.{skillInfo.Level}";
                }
            }
        }

        private void UpdateEquipmentUI(UserCharacterData character)
        {
            // TODO: Load equipped equipment from backend
        }

        private void UpdateRelicRunesUI(UserCharacterData character)
        {
            // TODO: Load equipped relic/runes from backend
        }

        private void UpdateAffinityUI(UserCharacterData character, CharacterData charData)
        {
            _affinitySlider.maxValue = charData.MaxAffinityLevel;
            _affinitySlider.value = character.AffinityLevel;
            _affinityLevelText.text = $"Afinidade Lv.{character.AffinityLevel}";
            _affinityExpText.text = $"EXP: {character.AffinityExperience:N0}";
        }

        private void UpdateStatsUI(UserCharacterData character)
        {
            ClearContainer(_statsContainer);

            var stats = _characterSystem.CalculateStatsAsync(character.CharacterId).Result;
            if (stats == null) return;

            var statRows = new[]
            {
                ("HP", stats.HP),
                ("Ataque", stats.Attack),
                ("Defesa", stats.Defense),
                ("Velocidade", stats.Speed),
                ("Taxa Crítica", $"{stats.CritRate:P1}"),
                ("Dano Crítico", $"{stats.CritDamage:F1}x"),
                ("Resistência", $"{stats.Resistance:P1}"),
                ("Precisão", $"{stats.Accuracy:P1}")
            };

            foreach (var (name, value) in statRows)
            {
                var row = Instantiate(_statRowPrefab, _statsContainer);
                var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = name;
                    texts[1].text = value.ToString();
                }
            }
        }

        private void UpdateFavoriteLockUI(UserCharacterData character)
        {
            if (_favoriteBtn != null)
            {
                var img = _favoriteBtn.GetComponent<Image>();
                img.color = character.IsFavorite ? Color.yellow : Color.white;
            }
            if (_lockBtn != null)
            {
                var img = _lockBtn.GetComponent<Image>();
                img.color = character.IsLocked ? Color.red : Color.white;
            }
        }

        private async void OnLevelUpPressed()
        {
            if (_selectedCharacter == null) return;

            var result = await _characterSystem.LevelUpAsync(_selectedCharacter.CharacterId, _selectedCharacter.Level + 1);
            if (result != null)
            {
                UIManager.Instance.ShowToastAsync($"Subiu para Lv.{result.NewLevel}!", 2f, ToastType.Success);
            }
        }

        private async void OnAutoLevelUpPressed()
        {
            if (_selectedCharacter == null) return;

            var charData = _selectedCharData;
            var result = await _characterSystem.LevelUpAsync(_selectedCharacter.CharacterId, charData.MaxLevel);
            if (result != null)
            {
                UIManager.Instance.ShowToastAsync($"Max Level atingido: Lv.{result.NewLevel}!", 2f, ToastType.Success);
            }
        }

        private async void OnAscendPressed()
        {
            if (_selectedCharacter == null) return;

            var result = await _characterSystem.AscendAsync(_selectedCharacter.CharacterId);
            if (result != null)
            {
                UIManager.Instance.ShowToastAsync($"Ascensão +{result.NewAscensionLevel}!", 2f, ToastType.Success);
            }
        }

        private async void OnSkillUpgradePressed(SkillType type)
        {
            if (_selectedCharacter == null) return;

            var result = await _characterSystem.UpgradeSkillAsync(_selectedCharacter.CharacterId, type);
            if (result != null)
            {
                UIManager.Instance.ShowToastAsync($"{type} Lv.{result.NewLevel}!", 2f, ToastType.Success);
            }
        }

        private async void OnFavoriteToggled()
        {
            if (_selectedCharacter == null) return;
            await _characterSystem.ToggleFavoriteAsync(_selectedCharacter.CharacterId);
        }

        private async void OnLockToggled()
        {
            if (_selectedCharacter == null) return;
            await _characterSystem.ToggleLockAsync(_selectedCharacter.CharacterId);
        }

        private void OnSkinsPressed()
        {
            // Open skins popup
            _uiManager?.OpenPopupAsync<UIPanel>("CharacterSkinsPopup", _selectedCharacter);
        }

        private void CloseDetail()
        {
            _detailPanel?.SetActive(false);
            _selectedCharacter = null;
            _selectedCharData = null;

            foreach (var item in _listItems)
            {
                item.Deselect();
            }
        }

        private void OnRarityFilterChanged(int value)
        {
            _currentRarityFilter = value == 0 ? Rarity.R : (Rarity)value;
            _ = RefreshCharacterListAsync();
        }

        private void OnElementFilterChanged(int value)
        {
            _currentElementFilter = value == 0 ? Element.None : (Element)value;
            _ = RefreshCharacterListAsync();
        }

        private void OnClassFilterChanged(int value)
        {
            _currentClassFilter = value == 0 ? CharacterClass.Warrior : (CharacterClass)value;
            _ = RefreshCharacterListAsync();
        }

        private void OnSearchChanged(string text)
        {
            _searchText = text;
            _ = RefreshCharacterListAsync();
        }

        private void OnCharacterAcquired(Guid characterId)
        {
            _ = RefreshCharacterListAsync();
        }

        private void OnCharacterUpdated(Guid characterId)
        {
            if (_selectedCharacter != null && _selectedCharacter.CharacterId == characterId)
            {
                var updated = _characterSystem.GetUserCharacter(characterId);
                var charData = _characterSystem.GetCharacterData(characterId);
                if (updated != null && charData != null)
                {
                    ShowDetail(updated, charData);
                }
            }
            _ = RefreshCharacterListAsync();
        }

        private void OnCharacterLeveledUp(Guid characterId, int levelsGained)
        {
            OnCharacterUpdated(characterId);
        }

        private void OnCharacterAscended(Guid characterId)
        {
            OnCharacterUpdated(characterId);
        }

        private void OnSkinChanged(Guid characterId, SkinData skin)
        {
            if (_selectedCharacter != null && _selectedCharacter.CharacterId == characterId)
            {
                _ = LoadSkinPreviewAsync(skin);
            }
        }

        private async UniTask LoadSkinPreviewAsync(SkinData skin)
        {
            // Load skin assets
        }

        private Color GetRarityColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.R => Color.gray,
                Rarity.SR => Color.blue,
                Rarity.SSR => new Color(0.5f, 0f, 1f), // Purple
                Rarity.UR => Color.yellow,
                _ => Color.white
            };
        }

        private void ClearContainer(RectTransform container)
        {
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
    }

    public class CharacterListItemUI : MonoBehaviour
    {
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

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => _onSelect?.Invoke(_character, _charData));
        }

        public void Select()
        {
            _selectedHighlight?.SetActive(true);
        }

        public void Deselect()
        {
            _selectedHighlight?.SetActive(false);
        }

        private bool IsNew(UserCharacterData character)
        {
            return (DateTime.UtcNow - character.AcquiredAt).TotalHours < 24;
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
    }
}