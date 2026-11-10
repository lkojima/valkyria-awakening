using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.UI
{
    public class ToastUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _rectTransform;

        [SerializeField] private Sprite _infoIcon;
        [SerializeField] private Sprite _successIcon;
        [SerializeField] private Sprite _warningIcon;
        [SerializeField] private Sprite _errorIcon;

        [SerializeField] private Color _infoColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color _successColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.8f, 0f);
        [SerializeField] private Color _errorColor = new Color(1f, 0.2f, 0.2f);

        private float _duration;
        private UniTaskCompletionSource<bool> _tcs;

        public async UniTask ShowAsync(string message, float duration = 2f, ToastType type = ToastType.Info)
        {
            _duration = duration;
            _tcs = new UniTaskCompletionSource<bool>();

            if (_messageText != null) _messageText.text = message;

            SetupAppearance(type);

            // Animate in
            _canvasGroup.alpha = 0f;
            _rectTransform.anchoredPosition = new Vector2(0, -100f);

            await _canvasGroup.DOFade(1f, 0.3f).AsyncWaitForCompletion();
            await _rectTransform.DOAnchorPosY(0f, 0.3f).SetEase(Ease.OutBack).AsyncWaitForCompletion();

            // Wait duration
            await UniTask.Delay((int)(duration * 1000));

            // Animate out
            await _rectTransform.DOAnchorPosY(100f, 0.3f).SetEase(Ease.InBack).AsyncWaitForCompletion();
            await _canvasGroup.DOFade(0f, 0.3f).AsyncWaitForCompletion();

            _tcs.TrySetResult(true);
        }

        private void SetupAppearance(ToastType type)
        {
            Sprite icon = null;
            Color color = Color.white;

            switch (type)
            {
                case ToastType.Info:
                    icon = _infoIcon;
                    color = _infoColor;
                    break;
                case ToastType.Success:
                    icon = _successIcon;
                    color = _successColor;
                    break;
                case ToastType.Warning:
                    icon = _warningIcon;
                    color = _warningColor;
                    break;
                case ToastType.Error:
                    icon = _errorIcon;
                    color = _errorColor;
                    break;
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.color = color;
            }

            if (_backgroundImage != null)
            {
                _backgroundImage.color = color;
            }
        }
    }

    public class ConfirmDialogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmText;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _cancelText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panelRect;

        private UniTaskCompletionSource<bool> _tcs;

        public async UniTask<bool> ShowAsync(string title, string message, string confirmText = "Confirmar", string cancelText = "Cancelar")
        {
            _tcs = new UniTaskCompletionSource<bool>();

            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            if (_confirmText != null) _confirmText.text = confirmText;
            if (_cancelText != null) _cancelText.text = cancelText;

            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() => OnConfirm(true));

            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(() => OnConfirm(false));

            // Animate in
            _canvasGroup.alpha = 0f;
            _panelRect.localScale = Vector3.zero;

            await _canvasGroup.DOFade(1f, 0.2f).AsyncWaitForCompletion();
            await _panelRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).AsyncWaitForCompletion();

            return await _tcs.Task;
        }

        private void OnConfirm(bool result)
        {
            _confirmButton.interactable = false;
            _cancelButton.interactable = false;

            _panelRect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack);
            _canvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
            {
                _tcs.TrySetResult(result);
            });
        }
    }

    public class IdleRewardPopupUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _timeAwayText;
        [SerializeField] private RectTransform _rewardsContainer;
        [SerializeField] private GameObject _rewardItemPrefab;
        [SerializeField] private Button _claimButton;
        [SerializeField] private TextMeshProUGUI _claimButtonText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panelRect;

        private IdleRewardSummary _summary;
        private System.Action _onClaimed;

        public async UniTask ShowAsync(IdleRewardSummary summary, System.Action onClaimed = null)
        {
            _summary = summary;
            _onClaimed = onClaimed;

            if (_titleText != null) _titleText.text = "Recompensas de Ausência";
            if (_timeAwayText != null)
                _timeAwayText.text = $"Você esteve ausente por {summary.HoursAccumulated} hora(s)";

            PopulateRewards();

            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(OnClaimPressed);

            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(OnClosePressed);

            // Animate in
            _canvasGroup.alpha = 0f;
            _panelRect.localScale = Vector3.zero;

            await _canvasGroup.DOFade(1f, 0.2f).AsyncWaitForCompletion();
            await _panelRect.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).AsyncWaitForCompletion();
        }

        private void PopulateRewards()
        {
            foreach (Transform child in _rewardsContainer)
            {
                Destroy(child.gameObject);
            }

            // Currencies
            if (_summary.Currencies != null)
            {
                foreach (var currency in _summary.Currencies)
                {
                    var item = Instantiate(_rewardItemPrefab, _rewardsContainer);
                    var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();
                    itemUI.SetCurrencyData(currency.Type, currency.Amount);
                }
            }

            // Materials
            if (_summary.Materials != null)
            {
                foreach (var material in _summary.Materials)
                {
                    var item = Instantiate(_rewardItemPrefab, _rewardsContainer);
                    var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();
                    itemUI.SetItemData(material.ResourceId, material.Quantity);
                }
            }

            // Equipment
            if (_summary.Equipment != null)
            {
                foreach (var equip in _summary.Equipment)
                {
                    var item = Instantiate(_rewardItemPrefab, _rewardsContainer);
                    var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();
                    itemUI.SetItemData(equip.ResourceId, equip.Quantity);
                }
            }

            // Character Fragments
            if (_summary.CharacterFragments != null)
            {
                foreach (var fragment in _summary.CharacterFragments)
                {
                    var item = Instantiate(_rewardItemPrefab, _rewardsContainer);
                    var itemUI = item.GetComponent<RewardItemUI>() ?? item.AddComponent<RewardItemUI>();
                    itemUI.SetFragmentData(fragment.ResourceId, fragment.Quantity);
                }
            }
        }

        private async void OnClaimPressed()
        {
            _claimButton.interactable = false;
            _closeButton.interactable = false;

            // Animate claim
            await AnimateClaimAsync();

            _onClaimed?.Invoke();

            await HideAsync();
        }

        private void OnClosePressed()
        {
            _ = HideAsync();
        }

        private async UniTask AnimateClaimAsync()
        {
            // Stagger animation for each reward item
            for (int i = 0; i < _rewardsContainer.childCount; i++)
            {
                var child = _rewardsContainer.GetChild(i);
                var canvasGroup = child.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = child.gameObject.AddComponent<CanvasGroup>();

                await child.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
                await child.DOScale(1f, 0.15f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
                await UniTask.Delay(50);
            }

            if (_claimButtonText != null) _claimButtonText.text = "RECEBIDO!";
        }

        private async UniTask HideAsync()
        {
            await _panelRect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).AsyncWaitForCompletion();
            await _canvasGroup.DOFade(0f, 0.2f).AsyncWaitForCompletion();

            Destroy(gameObject);
        }
    }

    public class RewardItemUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private Image _rarityFrame;

        public void SetCurrencyData(CurrencyType type, long amount)
        {
            _nameText.text = GetCurrencyName(type);
            _quantityText.text = $"+{amount:N0}";
            _iconImage.sprite = GetCurrencyIcon(type);
            _rarityFrame.color = GetCurrencyColor(type);
        }

        public void SetItemData(string resourceId, int quantity)
        {
            _nameText.text = resourceId; // TODO: Load from data
            _quantityText.text = $"+{quantity}";
            _iconImage.sprite = GetItemIcon(resourceId);
        }

        public void SetFragmentData(string characterId, int quantity)
        {
            _nameText.text = $"Fragmento: {characterId}";
            _quantityText.text = $"+{quantity}";
            _iconImage.sprite = GetFragmentIcon(characterId);
            _rarityFrame.color = Color.magenta;
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
                CurrencyType.Stamina => "Estamina",
                CurrencyType.Energy => "Energia",
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
                _ => Color.white
            };
        }

        private Sprite GetItemIcon(string resourceId) => null;
        private Sprite GetFragmentIcon(string characterId) => null;
    }
}