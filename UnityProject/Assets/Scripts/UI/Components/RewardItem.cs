using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.UI.Components
{
    public class RewardItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private Image _rarityFrame;
        [SerializeField] private GameObject _firstClearLabel;
        [SerializeField] private TextMeshProUGUI _firstClearText;
        [SerializeField] private GameObject _dropRateLabel;
        [SerializeField] private TextMeshProUGUI _dropRateText;

        public void SetCurrencyData(CurrencyType type, long amount)
        {
            _nameText.text = GetCurrencyName(type);
            _quantityText.text = $"+{amount:N0}";
            _iconImage.sprite = GetCurrencyIcon(type);
            _rarityFrame.color = GetCurrencyColor(type);
            _firstClearLabel?.SetActive(false);
            _dropRateLabel?.SetActive(false);
        }

        public void SetItemData(string resourceId, int quantity)
        {
            _nameText.text = resourceId; // TODO: Load from data
            _quantityText.text = $"+{quantity}";
            _iconImage.sprite = GetItemIcon(resourceId);
            _rarityFrame.color = Color.white;
            _firstClearLabel?.SetActive(false);
            _dropRateLabel?.SetActive(false);
        }

        public void SetFragmentData(string characterId, int quantity)
        {
            _nameText.text = $"Fragmento: {characterId}";
            _quantityText.text = $"+{quantity}";
            _iconImage.sprite = GetFragmentIcon(characterId);
            _rarityFrame.color = Color.magenta;
            _firstClearLabel?.SetActive(false);
            _dropRateLabel?.SetActive(false);
        }

        public void SetAsFirstClearReward(string resourceId, int quantity, RewardType type)
        {
            if (type == RewardType.Currency && System.Enum.TryParse<CurrencyType>(resourceId, out var currencyType))
            {
                SetCurrencyData(currencyType, quantity);
            }
            else
            {
                SetItemData(resourceId, quantity);
            }
            _firstClearLabel?.SetActive(true);
            if (_firstClearText != null) _firstClearText.text = "Primeira Vez";
        }

        public void SetAsDropReward(string resourceId, int quantity, float dropRate, RewardType type)
        {
            if (type == RewardType.Currency && System.Enum.TryParse<CurrencyType>(resourceId, out var currencyType))
            {
                SetCurrencyData(currencyType, quantity);
            }
            else
            {
                SetItemData(resourceId, quantity);
            }
            _dropRateLabel?.SetActive(true);
            if (_dropRateText != null) _dropRateText.text = $"{dropRate:P0}";
        }

        private string GetCurrencyName(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => "Ouro",
                CurrencyType.Gem => "Gemas",
                CurrencyType.SummonTicket => "Ticket de Invocação",
                CurrencyType.PremiumSummonTicket => "Ticket Premium",
                CurrencyType.Experience => "EXP",
                CurrencyType.CharacterExp => "EXP Personagem",
                CurrencyType.WeaponExp => "EXP Arma",
                CurrencyType.Stamina => "Estamina",
                CurrencyType.Energy => "Energia",
                CurrencyType.ActionPoint => "Pontos de Ação",
                _ => type.ToString()
            };
        }

        private Sprite GetCurrencyIcon(CurrencyType type)
        {
            // TODO: Load from Addressables
            return null;
        }

        private Color GetCurrencyColor(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => Color.yellow,
                CurrencyType.Gem => new Color(0.2f, 0.8f, 1f),
                CurrencyType.PremiumSummonTicket => new Color(1f, 0.8f, 0f),
                CurrencyType.SummonTicket => new Color(0.8f, 0.6f, 1f),
                _ => Color.white
            };
        }

        private Sprite GetItemIcon(string resourceId) => null;
        private Sprite GetFragmentIcon(string characterId) => null;
    }
}