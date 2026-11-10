using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Gacha;

namespace GoddessRequiem.Client.Gacha
{
    public class SummonAnimation : MonoBehaviour
    {
        [Header("Animation Configuration")]
        [SerializeField] private SummonAnimationConfig _config;

        [Header("Scene References")]
        [SerializeField] private Transform _characterSpawnPoint;
        [SerializeField] private Transform _vfxRoot;
        [SerializeField] private Canvas _uiCanvas;
        [SerializeField] private GameObject _skipButton;
        [SerializeField] private GameObject _tapToContinueText;

        [Header("Background")]
        [SerializeField] private GameObject _backgroundRoot;
        [SerializeField] private Renderer _backgroundRenderer;
        [SerializeField] private VideoPlayerController _backgroundVideo;

        private SummonResult _currentResult;
        private int _currentResultIndex;
        private bool _isPlaying;
        private bool _isSkipped;
        private float _minimumWatchTime;
        private float _elapsedTime;
        private System.Action _onComplete;

        private readonly List<GameObject> _spawnedObjects = new();
        private readonly List<AsyncOperationHandle> _loadedHandles = new();

        public event System.Action OnAnimationComplete;

        private void Awake()
        {
            _skipButton?.SetActive(false);
            _tapToContinueText?.SetActive(false);

            var skipBtn = _skipButton?.GetComponent<UnityEngine.UI.Button>();
            if (skipBtn != null)
            {
                skipBtn.onClick.AddListener(OnSkipPressed);
            }
        }

        public async UniTask PlayAsync(SummonResult result, System.Action onComplete = null)
        {
            if (_isPlaying) return;

            _currentResult = result;
            _onComplete = onComplete;
            _isPlaying = true;
            _isSkipped = false;
            _currentResultIndex = 0;
            _elapsedTime = 0f;

            _skipButton?.SetActive(false);
            _tapToContinueText?.SetActive(false);

            try
            {
                await PlaySummonSequenceAsync();
            }
            finally
            {
                Cleanup();
                _isPlaying = false;
                _onComplete?.Invoke();
                OnAnimationComplete?.Invoke();
            }
        }

        private async UniTask PlaySummonSequenceAsync()
        {
            // 1. Load and show background
            await LoadBackgroundAsync(_currentResult.History.BannerId);

            // 2. Play opening sequence (portal, particles, sound)
            await PlayOpeningSequenceAsync();

            // 3. Process each result
            for (int i = 0; i < _currentResult.History.Results.Count; i++)
            {
                _currentResultIndex = i;
                _minimumWatchTime = GetMinimumWatchTime(_currentResult.History.Results[i].Rarity);
                _elapsedTime = 0f;

                _skipButton?.SetActive(true);

                await PlaySingleResultAsync(_currentResult.History.Results[i], _currentResult.AnimationData[i]);

                _skipButton?.SetActive(false);

                // Wait for tap to continue (unless skipped or last)
                if (!_isSkipped && i < _currentResult.History.Results.Count - 1)
                {
                    await WaitForTapAsync();
                }

                if (_isSkipped) break;
            }

            // 4. Show summary if multi-summon
            if (_currentResult.History.IsMulti && !_isSkipped)
            {
                await ShowMultiSummonSummaryAsync();
            }

            // 5. Final fade out
            await FadeOutAsync(0.5f);
        }

        private async UniTask PlaySingleResultAsync(GachaResult result, SummonAnimationData animData)
        {
            var rarity = result.Rarity;
            var isGuaranteed = result.IsGuaranteed;
            var isNew = result.IsNew;

            // Determine animation variant
            string variant = isGuaranteed ? "guaranteed" : rarity switch
            {
                Rarity.UR => "ur",
                Rarity.SSR => "ssr",
                Rarity.SR => "sr",
                _ => "r"
            };

            // Load rarity-specific VFX
            await LoadRarityVFXAsync(variant);

            // Play rarity reveal sequence
            await PlayRarityRevealAsync(variant, isGuaranteed);

            // Load and spawn character
            await SpawnCharacterAsync(result.CharacterId, variant);

            // Play character entrance animation
            await PlayCharacterEntranceAsync(variant);

            // Show character info (name, rarity, element, class)
            await ShowCharacterInfoAsync(result);

            // Play "NEW!" animation if first time
            if (isNew)
            {
                await PlayNewCharacterAnimationAsync();
            }

            // Hold for minimum watch time
            await HoldForMinimumTimeAsync();

            // Cleanup character for next result
            await CleanupCharacterAsync();
        }

        private async UniTask LoadBackgroundAsync(Guid bannerId)
        {
            // Try to load banner-specific background video
            if (_backgroundVideo != null && _currentResult != null)
            {
                var banner = _currentResult.History.BannerId;
                var videoHandle = Addressables.LoadAssetAsync<VideoClip>($"gacha/backgrounds/{banner}_bg");
                await videoHandle.Task;

                if (videoHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    _backgroundVideo.Play(videoHandle.Result);
                    _loadedHandles.Add(videoHandle);
                }
            }

            // Fade in background
            if (_backgroundRenderer != null)
            {
                var mat = _backgroundRenderer.material;
                var color = mat.color;
                color.a = 0f;
                mat.color = color;

                await FadeMaterialAlphaAsync(mat, 0f, 1f, 1f);
            }

            _backgroundRoot?.SetActive(true);
        }

        private async UniTask PlayOpeningSequenceAsync()
        {
            // Portal opening VFX
            var portalHandle = Addressables.LoadAssetAsync<GameObject>($"gacha/vfx/portal_opening");
            await portalHandle.Task;

            if (portalHandle.Status == AsyncOperationStatus.Succeeded)
            {
                var portal = Instantiate(portalHandle.Result, _vfxRoot);
                _spawnedObjects.Add(portal);
                _loadedHandles.Add(portalHandle);

                var ps = portal.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                    await UniTask.WaitUntil(() => !ps.isPlaying);
                }
            }

            // Screen flash
            await ScreenFlashAsync(Color.white, 0.3f);

            // Sound: summon start
            AudioManager.Instance?.PlaySFX("gacha_summon_start");
        }

        private async UniTask PlayRarityRevealAsync(string variant, bool isGuaranteed)
        {
            // Load rarity reveal VFX
            var vfxHandle = Addressables.LoadAssetAsync<GameObject>($"gacha/vfx/rarity_reveal_{variant}");
            await vfxHandle.Task;

            if (vfxHandle.Status == AsyncOperationStatus.Succeeded)
            {
                var vfx = Instantiate(vfxHandle.Result, _vfxRoot);
                _spawnedObjects.Add(vfx);
                _loadedHandles.Add(vfxHandle);

                var ps = vfx.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                }

                // Play timeline if exists
                var director = vfx.GetComponent<PlayableDirector>();
                if (director != null)
                {
                    director.Play();
                    await UniTask.WaitUntil(() => director.state != PlayState.Playing);
                }
                else
                {
                    await UniTask.Delay(GetRarityRevealDuration(variant));
                }
            }

            // Guaranteed overlay
            if (isGuaranteed)
            {
                await ShowGuaranteedOverlayAsync();
            }

            // Rarity text animation
            await AnimateRarityTextAsync(variant);

            // Sound: rarity reveal
            AudioManager.Instance?.PlaySFX($"gacha_rarity_{variant}");
        }

        private async UniTask SpawnCharacterAsync(Guid characterId, string variant)
        {
            // Load character model/sprite based on variant quality
            string assetPath = variant switch
            {
                "ur" or "ssr" => $"characters/{characterId}/live2d",
                "sr" => $"characters/{characterId}/fullbody",
                _ => $"characters/{characterId}/portrait"
            };

            var handle = Addressables.LoadAssetAsync<GameObject>(assetPath);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var character = Instantiate(handle.Result, _characterSpawnPoint);
                _spawnedObjects.Add(character);
                _loadedHandles.Add(handle);

                // Setup based on type
                if (variant == "ur" || variant == "ssr")
                {
                    SetupLive2D(character);
                }
                else
                {
                    SetupSprite(character);
                }
            }
        }

        private void SetupLive2D(GameObject character)
        {
            var cubismModel = character.GetComponent<CubismModel>(); // Live2D Cubism
            if (cubismModel != null)
            {
                // Enable physics, expressions
                var physics = character.GetComponent<CubismPhysicsController>();
                physics?.enabled = true;
            }
        }

        private void SetupSprite(GameObject character)
        {
            var spriteRenderer = character.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 10;
            }
        }

        private async UniTask PlayCharacterEntranceAsync(string variant)
        {
            // Scale up from 0 with bounce
            var characterObj = _spawnedObjects.Find(o => o.transform.parent == _characterSpawnPoint);
            if (characterObj == null) return;

            characterObj.transform.localScale = Vector3.zero;

            await characterObj.transform
                .DOScale(Vector3.one, 0.6f)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion();

            // Idle animation
            var animator = characterObj.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("Idle");
            }

            // Camera shake for high rarity
            if (variant == "ssr" || variant == "ur")
            {
                CameraShake.Instance?.Shake(0.3f, 0.5f);
            }
        }

        private async UniTask ShowCharacterInfoAsync(GachaResult result)
        {
            var charData = await LoadCharacterDisplayDataAsync(result.CharacterId);
            if (charData == null) return;

            // Create info panel
            var infoHandle = Addressables.LoadAssetAsync<GameObject>("ui/gacha/CharacterInfoPanel");
            await infoHandle.Task;

            if (infoHandle.Status == AsyncOperationStatus.Succeeded)
            {
                var panel = Instantiate(infoHandle.Result, _uiCanvas.transform);
                _spawnedObjects.Add(panel);
                _loadedHandles.Add(infoHandle);

                var infoUI = panel.GetComponent<CharacterInfoUI>();
                if (infoUI != null)
                {
                    infoUI.SetData(charData, result.Rarity, result.IsNew);
                    await infoUI.AnimateInAsync();
                }
            }
        }

        private async UniTask<CharacterDisplayData> LoadCharacterDisplayDataAsync(Guid characterId)
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>($"data/characters/{characterId}");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var json = handle.Result.text;
                var data = JsonUtility.FromJson<CharacterDisplayData>(json);
                _loadedHandles.Add(handle);
                return data;
            }

            return null;
        }

        private async UniTask PlayNewCharacterAnimationAsync()
        {
            var handle = Addressables.LoadAssetAsync<GameObject>("gacha/vfx/new_character_badge");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var vfx = Instantiate(handle.Result, _uiCanvas.transform);
                _spawnedObjects.Add(vfx);
                _loadedHandles.Add(handle);

                var anim = vfx.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.Play("NewBadgeIn");
                    await UniTask.WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
                }

                AudioManager.Instance?.PlaySFX("gacha_new_character");
            }
        }

        private async UniTask ShowGuaranteedOverlayAsync()
        {
            var handle = Addressables.LoadAssetAsync<GameObject>("gacha/vfx/guaranteed_overlay");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var overlay = Instantiate(handle.Result, _uiCanvas.transform);
                _spawnedObjects.Add(overlay);
                _loadedHandles.Add(handle);

                var anim = overlay.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.Play("GuaranteedIn");
                    await UniTask.WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
                }

                await UniTask.Delay(1000);
                anim.Play("GuaranteedOut");
                await UniTask.WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
            }
        }

        private async UniTask AnimateRarityTextAsync(string variant)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>($"ui/gacha/RarityText_{variant.ToUpper()}");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var textObj = Instantiate(handle.Result, _uiCanvas.transform);
                _spawnedObjects.Add(textObj);
                _loadedHandles.Add(handle);

                var anim = textObj.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.Play("RarityTextIn");
                    await UniTask.WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
                }
            }
        }

        private async UniTask HoldForMinimumTimeAsync()
        {
            _tapToContinueText?.SetActive(true);

            float startTime = Time.unscaledTime;
            while (Time.unscaledTime - startTime < _minimumWatchTime)
            {
                _elapsedTime = Time.unscaledTime - startTime;
                await UniTask.Yield();

                if (_isSkipped) break;
            }

            _tapToContinueText?.SetActive(false);
        }

        private async UniTask WaitForTapAsync()
        {
            _tapToContinueText?.SetActive(true);
            _tapToContinueText.GetComponent<Animator>()?.Play("Pulse");

            var tcs = new UniTaskCompletionSource<bool>();

            System.Action onTap = null;
            onTap = () => {
                _tapToContinueText.GetComponent<Animator>()?.Play("FadeOut");
                tcs.TrySetResult(true);
            };

            // Register tap handler (using InputSystem or legacy)
            var tapHandler = gameObject.AddComponent<TapDetector>();
            tapHandler.OnTap += onTap;

            await tcs.Task;

            tapHandler.OnTap -= onTap;
            Destroy(tapHandler);
            _tapToContinueText?.SetActive(false);
        }

        private async UniTask ShowMultiSummonSummaryAsync()
        {
            var handle = Addressables.LoadAssetAsync<GameObject>("ui/gacha/MultiSummonSummary");
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var summary = Instantiate(handle.Result, _uiCanvas.transform);
                _spawnedObjects.Add(summary);
                _loadedHandles.Add(handle);

                var summaryUI = summary.GetComponent<MultiSummonSummaryUI>();
                if (summaryUI != null)
                {
                    summaryUI.SetData(_currentResult.History.Results);
                    await summaryUI.AnimateInAsync();

                    // Wait for confirm
                    await summaryUI.WaitForConfirmAsync();
                    await summaryUI.AnimateOutAsync();
                }
            }
        }

        private async UniTask FadeOutAsync(float duration)
        {
            // Fade background
            if (_backgroundRenderer != null)
            {
                var mat = _backgroundRenderer.material;
                await FadeMaterialAlphaAsync(mat, mat.color.a, 0f, duration);
            }

            // Fade UI canvas
            var canvasGroup = _uiCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = _uiCanvas.gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                await UniTask.Yield();
            }
            canvasGroup.alpha = 0f;
        }

        private async UniTask FadeMaterialAlphaAsync(Material mat, float from, float to, float duration)
        {
            float elapsed = 0f;
            var color = mat.color;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(from, to, elapsed / duration);
                mat.color = color;
                await UniTask.Yield();
            }
            color.a = to;
            mat.color = color;
        }

        private async UniTask ScreenFlashAsync(Color color, float duration)
        {
            var flashObj = new GameObject("ScreenFlash");
            flashObj.transform.SetParent(_uiCanvas.transform);
            var img = flashObj.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(color.r, color.g, color.b, 0f);
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;

            float elapsed = 0f;
            // Fade in
            while (elapsed < duration * 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                img.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0f, color.a, elapsed / (duration * 0.3f)));
                await UniTask.Yield();
            }
            // Fade out
            elapsed = 0f;
            while (elapsed < duration * 0.7f)
            {
                elapsed += Time.unscaledDeltaTime;
                img.color = new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, 0f, elapsed / (duration * 0.7f)));
                await UniTask.Yield();
            }

            Destroy(flashObj);
        }

        private void OnSkipPressed()
        {
            if (_elapsedTime >= _minimumWatchTime)
            {
                _isSkipped = true;
                _skipButton?.SetActive(false);
            }
        }

        private float GetMinimumWatchTime(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.UR => 3f,
                Rarity.SSR => 2f,
                Rarity.SR => 1.5f,
                _ => 1f
            };
        }

        private float GetRarityRevealDuration(string variant)
        {
            return variant switch
            {
                "ur" => 4f,
                "ssr" => 3f,
                "sr" => 2f,
                "guaranteed" => 3f,
                _ => 1.5f
            };
        }

        private async UniTask LoadRarityVFXAsync(string variant)
        {
            // Preload all VFX for this rarity tier
            var vfxKeys = new[]
            {
                $"gacha/vfx/rarity_reveal_{variant}",
                $"gacha/vfx/character_entrance_{variant}",
                $"ui/gacha/RarityText_{variant.ToUpper()}"
            };

            var tasks = new List<UniTask>();
            foreach (var key in vfxKeys)
            {
                var handle = Addressables.LoadAssetAsync<GameObject>(key);
                tasks.Add(handle.Task);
                _loadedHandles.Add(handle);
            }
            await UniTask.WhenAll(tasks);
        }

        private void Cleanup()
        {
            _backgroundVideo?.Stop();
            _backgroundRoot?.SetActive(false);

            foreach (var obj in _spawnedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedObjects.Clear();

            foreach (var handle in _loadedHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _loadedHandles.Clear();

            _skipButton?.SetActive(false);
            _tapToContinueText?.SetActive(false);
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }

    // Supporting Components
    public class TapDetector : MonoBehaviour
    {
        public event System.Action OnTap;

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                OnTap?.Invoke();
            }
#elif UNITY_ANDROID || UNITY_IOS
            if (UnityEngine.Input.touchCount > 0 && UnityEngine.Input.GetTouch(0).phase == TouchPhase.Began)
            {
                OnTap?.Invoke();
            }
#endif
        }
    }

    [System.Serializable]
    public class SummonAnimationConfig
    {
        public float OpeningDuration = 2f;
        public float RarityRevealDuration = 2f;
        public float CharacterEntranceDuration = 1f;
        public float InfoDisplayDuration = 3f;
        public float FadeOutDuration = 0.5f;
        public bool EnableHaptics = true;
        public bool EnableCameraShake = true;
    }

    [System.Serializable]
    public class CharacterDisplayData
    {
        public Guid Id;
        public string Name;
        public string DisplayName;
        public Rarity Rarity;
        public Element Element;
        public CharacterClass Class;
        public CharacterRole Role;
        public string PortraitPath;
        public string FullBodyPath;
        public string Live2DPath;
        public string SummonQuote;
        public string VoiceLinePath;
    }
}

// Extension for DOTween async support
namespace DG.Tweening
{
    public static class TweenExtensions
    {
        public static UniTask AsyncWaitForCompletion(this Tween tween)
        {
            var tcs = new UniTaskCompletionSource<bool>();
            tween.OnComplete(() => tcs.TrySetResult(true));
            return tcs.Task;
        }
    }
}