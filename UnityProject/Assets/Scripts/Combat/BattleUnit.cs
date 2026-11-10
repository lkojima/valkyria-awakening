using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DG.Tweening;
using GoddessRequiem.Core;
using GoddessRequiem.Client.Combat;

namespace GoddessRequiem.Client.Combat
{
    public class BattleUnit : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BattleUnitConfig _config;

        [Header("Components")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _vfxAttachPoint;
        [SerializeField] private Transform _hpBarAttachPoint;

        // State
        private UnitSetup _setup;
        private bool _isPlayerUnit;
        private int _positionIndex;
        private BattleUnitConfig _unitConfig;

        // Stats
        private UnitStats _baseStats;
        private UnitStats _currentStats;
        private List<Buff> _buffs = new();
        private List<Debuff> _debuffs = new();
        private List<StatusEffect> _statusEffects = new();

        // Runtime
        private int _turnOrder;
        private bool _isAlive = true;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;

        // Properties
        public UnitSetup Setup => _setup;
        public bool IsPlayerUnit => _isPlayerUnit;
        public int PositionIndex => _positionIndex;
        public int TurnOrder { get => _turnOrder; set => _turnOrder = value; }
        public bool IsAlive => _isAlive;
        public UnitStats Stats => _currentStats;
        public UnitStats BaseStats => _baseStats;
        public IReadOnlyList<Buff> Buffs => _buffs;
        public IReadOnlyList<Debuff> Debuffs => _debuffs;
        public IReadOnlyList<StatusEffect> StatusEffects => _statusEffects;
        public int CurrentHP => _currentStats.CurrentHP;
        public int MaxHP => _currentStats.MaxHP;
        public int CurrentMP => _currentStats.CurrentMP;
        public int MaxMP => _currentStats.MaxMP;
        public bool CanUseUltimate => _currentStats.CurrentMP >= _currentStats.MaxMP;
        public SkillData BasicSkill => _setup.Skills.Find(s => s.Type == SkillType.Basic);
        public SkillData UltimateSkill => _setup.Skills.Find(s => s.Type == SkillType.Ultimate);

        public event System.Action<BattleUnit> OnDeath;
        public event System.Action<BattleUnit, int, bool> OnDamageTaken; // unit, damage, isCrit
        public event System.Action<BattleUnit, int> OnHealed;

        public async UniTask InitializeAsync(UnitSetup setup, bool isPlayer, int positionIndex, BattleUnitConfig config)
        {
            _setup = setup;
            _isPlayerUnit = isPlayer;
            _positionIndex = positionIndex;
            _unitConfig = config;

            // Store original transform
            _originalPosition = transform.localPosition;
            _originalRotation = transform.localRotation;
            _originalScale = transform.localScale;

            // Calculate base stats from character data
            _baseStats = CalculateBaseStats(setup);
            _currentStats = _baseStats.Clone();
            _currentStats.CurrentHP = _currentStats.MaxHP;
            _currentStats.CurrentMP = _currentStats.MaxMP;

            // Load visual assets
            await LoadVisualAssetsAsync();

            // Setup animator
            SetupAnimator();

            // Initialize position
            SetPosition(positionIndex);

            // Subscribe to events
            _animator?.gameObject.SetActive(true);
        }

        private UnitStats CalculateBaseStats(UnitSetup setup)
        {
            // This would normally come from Character data + Equipment + Relics + Runes
            // For now, using setup values with level scaling
            float levelMultiplier = 1f + (setup.Level - 1) * 0.02f;
            float ascensionMultiplier = 1f + setup.Ascension * 0.05f;
            float totalMultiplier = levelMultiplier * ascensionMultiplier;

            return new UnitStats
            {
                MaxHP = Mathf.RoundToInt(setup.BaseHP * totalMultiplier),
                MaxMP = 100, // Base MP
                Attack = Mathf.RoundToInt(setup.BaseAttack * totalMultiplier),
                Defense = Mathf.RoundToInt(setup.BaseDefense * totalMultiplier),
                Speed = Mathf.RoundToInt(setup.BaseSpeed * totalMultiplier),
                CritRate = Mathf.Min(1f, setup.BaseCritRate + setup.Ascension * 0.02f),
                CritDamage = 1.5f + setup.Ascension * 0.1f,
                Resistance = Mathf.Min(1f, setup.BaseResistance),
                Accuracy = Mathf.Min(1f, setup.BaseAccuracy),
                Evasion = 0.05f,
                Element = setup.Element,
                CurrentHP = 0,
                CurrentMP = 100
            };
        }

        private async UniTask LoadVisualAssetsAsync()
        {
            // Load character sprite/model based on setup
            string assetPath = _setup.SpriteAssetPath;

            if (string.IsNullOrEmpty(assetPath))
            {
                // Fallback to character ID based path
                assetPath = $"characters/{_setup.CharacterId}/battle_sprite";
            }

            var handle = Addressables.LoadAssetAsync<Sprite>(assetPath);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = handle.Result;
                _spriteRenderer.sortingOrder = _isPlayerUnit ? 10 + _positionIndex : 20 + _positionIndex;
            }
            else
            {
                // Create placeholder
                CreatePlaceholderSprite();
            }

            // Load Live2D for high rarity if available
            if (_setup.Rarity >= Rarity.SSR && !string.IsNullOrEmpty(_setup.Live2DModelPath))
            {
                var live2dHandle = Addressables.LoadAssetAsync<GameObject>(_setup.Live2DModelPath);
                await live2dHandle.Task;

                if (live2dHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    var live2d = Instantiate(live2dHandle.Result, transform);
                    live2d.transform.localPosition = Vector3.zero;
                    // Disable SpriteRenderer if using Live2D
                    if (_spriteRenderer != null) _spriteRenderer.enabled = false;
                }
            }
        }

        private void CreatePlaceholderSprite()
        {
            // Create a simple colored square as placeholder
            var texture = new Texture2D(64, 64);
            Color color = _isPlayerUnit ? Color.blue : Color.red;
            var pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            if (_spriteRenderer != null) _spriteRenderer.sprite = sprite;
        }

        private void SetupAnimator()
        {
            if (_animator == null) return;

            // Load animator controller based on character class
            string controllerPath = $"characters/{_setup.CharacterId}/animator";
            var handle = Addressables.LoadAssetAsync<RuntimeAnimatorController>(controllerPath);
            handle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    _animator.runtimeAnimatorController = op.Result;
                }
            };
        }

        private void SetPosition(int positionIndex)
        {
            // Position units in formation
            float xOffset = _isPlayerUnit ? -2f : 2f;
            float yOffset = (positionIndex - 2) * 1.5f; // Centered around 0

            transform.localPosition = _originalPosition + new Vector3(xOffset, yOffset, 0);

            // Face correct direction
            transform.localRotation = _isPlayerUnit
                ? Quaternion.Euler(0, 0, 0)
                : Quaternion.Euler(0, 180, 0);
        }

        public void OnTurnStart()
        {
            // Process status effects at turn start
            ProcessTurnStartEffects();

            // Regenerate MP
            RegenerateMP(10);

            // Play idle animation
            _animator?.Play("Idle");
        }

        public void OnTurnEnd()
        {
            // Process status effects at turn end
            ProcessTurnEndEffects();

            // Decrease cooldowns
            DecreaseSkillCooldowns();
        }

        private void ProcessTurnStartEffects()
        {
            var effectsToRemove = new List<StatusEffect>();

            foreach (var effect in _statusEffects)
            {
                effect.OnTurnStart(this);

                if (effect.RemainingTurns <= 0)
                {
                    effectsToRemove.Add(effect);
                }
            }

            foreach (var effect in effectsToRemove)
            {
                RemoveStatusEffect(effect);
            }
        }

        private void ProcessTurnEndEffects()
        {
            var effectsToRemove = new List<StatusEffect>();

            foreach (var effect in _statusEffects)
            {
                effect.OnTurnEnd(this);
                effect.RemainingTurns--;

                if (effect.RemainingTurns <= 0)
                {
                    effectsToRemove.Add(effect);
                }
            }

            foreach (var effect in effectsToRemove)
            {
                RemoveStatusEffect(effect);
            }
        }

        private void RegenerateMP(int amount)
        {
            _currentStats.CurrentMP = Mathf.Min(_currentStats.MaxMP, _currentStats.CurrentMP + amount);
        }

        private void DecreaseSkillCooldowns()
        {
            foreach (var skill in _setup.Skills)
            {
                if (skill.CurrentCooldown > 0)
                    skill.CurrentCooldown--;
            }
        }

        public List<SkillData> GetAvailableSkills()
        {
            var available = new List<SkillData>();

            foreach (var skill in _setup.Skills)
            {
                if (skill.CurrentCooldown <= 0 && _currentStats.CurrentMP >= skill.MPCost)
                {
                    available.Add(skill);
                }
            }

            return available;
        }

        public void TakeDamage(int damage, bool isCrit)
        {
            if (!_isAlive) return;

            _currentStats.CurrentHP = Mathf.Max(0, _currentStats.CurrentHP - damage);

            // Play damage animation
            PlayDamageAnimation(damage, isCrit);

            OnDamageTaken?.Invoke(this, damage, isCrit);

            if (_currentStats.CurrentHP <= 0)
            {
                Die();
            }
        }

        public void Heal(int amount)
        {
            if (!_isAlive) return;

            int oldHP = _currentStats.CurrentHP;
            _currentStats.CurrentHP = Mathf.Min(_currentStats.MaxHP, _currentStats.CurrentHP + amount);
            int actualHeal = _currentStats.CurrentHP - oldHP;

            if (actualHeal > 0)
            {
                PlayHealAnimation(actualHeal);
                OnHealed?.Invoke(this, actualHeal);
            }
        }

        public void PlayDamageAnimation(int damage, bool isCrit)
        {
            // Shake
            transform.DOShakePosition(0.3f, 0.2f, 10, 45f, false, true);

            // Flash red
            if (_spriteRenderer != null)
            {
                _spriteRenderer.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo);
            }

            // Crit effect
            if (isCrit)
            {
                PlayCritEffect();
            }

            // Damage number handled by BattleUI
        }

        public void PlayHealAnimation(int amount)
        {
            // Green flash
            if (_spriteRenderer != null)
            {
                _spriteRenderer.DOColor(Color.green, 0.2f).SetLoops(2, LoopType.Yoyo);
            }

            // Float up animation
            transform.DOLocalMoveY(transform.localPosition.y + 0.5f, 0.5f).SetRelative().SetEase(Ease.OutQuad);
        }

        public void PlayDodgeAnimation()
        {
            // Quick sidestep
            Vector3 dodgePos = transform.localPosition + (_isPlayerUnit ? Vector3.left : Vector3.right) * 0.5f;
            transform.DOLocalMove(dodgePos, 0.15f).SetEase(Ease.OutQuad)
                .OnComplete(() => transform.DOLocalMove(transform.localPosition, 0.15f).SetEase(Ease.InQuad));

            // "Miss" text handled by BattleUI
        }

        public void ApplyStatusEffect(StatusEffectData effectData)
        {
            // Check if already has this effect (refresh or stack)
            var existing = _statusEffects.Find(e => e.Type == effectData.Type);

            if (existing != null)
            {
                // Refresh duration, stack if applicable
                existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, effectData.RemainingTurns);
                existing.Value = Mathf.Max(existing.Value, effectData.Value);
            }
            else
            {
                var effect = new StatusEffect
                {
                    Type = effectData.Type,
                    RemainingTurns = effectData.RemainingTurns,
                    Value = effectData.Value,
                    Icon = effectData.Icon
                };

                _statusEffects.Add(effect);
                ApplyEffectImmediate(effect);
            }
        }

        private void ApplyEffectImmediate(StatusEffect effect)
        {
            switch (effect.Type)
            {
                case StatusEffectType.AttackUp:
                    _currentStats.Attack = Mathf.RoundToInt(_currentStats.Attack * (1f + effect.Value));
                    break;
                case StatusEffectType.DefenseUp:
                    _currentStats.Defense = Mathf.RoundToInt(_currentStats.Defense * (1f + effect.Value));
                    break;
                case StatusEffectType.SpeedUp:
                    _currentStats.Speed = Mathf.RoundToInt(_currentStats.Speed * (1f + effect.Value));
                    break;
                case StatusEffectType.CritRateUp:
                    _currentStats.CritRate = Mathf.Min(1f, _currentStats.CritRate + effect.Value);
                    break;
                case StatusEffectType.CritDamageUp:
                    _currentStats.CritDamage += effect.Value;
                    break;
                case StatusEffectType.Shield:
                    // Shield handled separately in damage calculation
                    break;
                case StatusEffectType.Invulnerability:
                    // Invulnerability handled in damage calculation
                    break;
                case StatusEffectType.Regeneration:
                    // Heal per turn
                    break;
                case StatusEffectType.Poison:
                    // Damage per turn
                    break;
                case StatusEffectType.Burn:
                    // Damage per turn + defense down
                    break;
                case StatusEffectType.Stun:
                    // Skip turn
                    break;
                case StatusEffectType.Silence:
                    // Cannot use skills
                    break;
            }
        }

        private void RemoveStatusEffect(StatusEffect effect)
        {
            // Reverse stat modifications
            switch (effect.Type)
            {
                case StatusEffectType.AttackUp:
                    _currentStats.Attack = Mathf.RoundToInt(_currentStats.Attack / (1f + effect.Value));
                    break;
                case StatusEffectType.DefenseUp:
                    _currentStats.Defense = Mathf.RoundToInt(_currentStats.Defense / (1f + effect.Value));
                    break;
                case StatusEffectType.SpeedUp:
                    _currentStats.Speed = Mathf.RoundToInt(_currentStats.Speed / (1f + effect.Value));
                    break;
                case StatusEffectType.CritRateUp:
                    _currentStats.CritRate = Mathf.Max(0f, _currentStats.CritRate - effect.Value);
                    break;
                case StatusEffectType.CritDamageUp:
                    _currentStats.CritDamage = Mathf.Max(1.5f, _currentStats.CritDamage - effect.Value);
                    break;
            }

            _statusEffects.Remove(effect);
        }

        private void Die()
        {
            _isAlive = false;

            // Play death animation
            _animator?.Play("Death");

            // Fade out
            if (_spriteRenderer != null)
            {
                _spriteRenderer.DOFade(0f, 1f).SetDelay(1f);
            }

            // Disable collider
            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;

            OnDeath?.Invoke(this);
        }

        public void PlayCritEffect()
        {
            // Spawn crit VFX
            var vfx = Instantiate(_config.CritEffectPrefab, _vfxAttachPoint);
            vfx.transform.localPosition = Vector3.zero;
            Destroy(vfx, 2f);
        }

        public void PlaySkillAnimation(SkillData skill)
        {
            if (_animator == null) return;

            string animName = skill.AnimationTrigger;
            if (!string.IsNullOrEmpty(animName))
            {
                _animator.SetTrigger(animName);
            }
            else
            {
                _animator.Play("Attack");
            }
        }

        public void ResetForNewBattle()
        {
            _isAlive = true;
            _currentStats = _baseStats.Clone();
            _currentStats.CurrentHP = _currentStats.MaxHP;
            _currentStats.CurrentMP = _currentStats.MaxMP;
            _buffs.Clear();
            _debuffs.Clear();
            _statusEffects.Clear();

            transform.localPosition = _originalPosition;
            transform.localRotation = _originalRotation;
            transform.localScale = _originalScale;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = Color.white;
                _spriteRenderer.enabled = true;
            }

            _animator?.Play("Idle");

            var collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = true;
        }

        // Skill management
        public bool CanUseSkill(SkillData skill)
        {
            return skill.CurrentCooldown <= 0 && _currentStats.CurrentMP >= skill.MPCost && _isAlive;
        }

        public void UseSkill(SkillData skill)
        {
            skill.CurrentCooldown = skill.Cooldown;
            _currentStats.CurrentMP -= skill.MPCost;
        }

        // Utility
        private void OnDestroy()
        {
            // Cleanup DOTween tweens
            DOTween.Kill(transform);
        }
    }

    // Data Structures
    [System.Serializable]
    public class UnitStats
    {
        public int MaxHP;
        public int MaxMP;
        public int Attack;
        public int Defense;
        public int Speed;
        public float CritRate;
        public float CritDamage;
        public float Resistance;
        public float Accuracy;
        public float Evasion;
        public Element Element;
        public int CurrentHP;
        public int CurrentMP;

        public UnitStats Clone()
        {
            return (UnitStats)MemberwiseClone();
        }
    }

    [System.Serializable]
    public class Buff
    {
        public BuffType Type;
        public float Value;
        public int RemainingTurns;
        public Sprite Icon;
        public string Name;
    }

    [System.Serializable]
    public class Debuff
    {
        public DebuffType Type;
        public float Value;
        public int RemainingTurns;
        public Sprite Icon;
        public string Name;
    }

    [System.Serializable]
    public class StatusEffect
    {
        public StatusEffectType Type;
        public float Value;
        public int RemainingTurns;
        public Sprite Icon;

        public void OnTurnStart(BattleUnit unit) { }
        public void OnTurnEnd(BattleUnit unit) { }
    }

    [System.Serializable]
    public class BattleUnitConfig
    {
        public GameObject CritEffectPrefab;
        public float DefaultMPRegeneration = 10f;
        public float DeathFadeDuration = 1f;
    }

    // Extensions for DOTween on SpriteRenderer
    public static class SpriteRendererExtensions
    {
        public static Tween DOColor(this SpriteRenderer renderer, Color endValue, float duration)
        {
            return DOTween.To(() => renderer.color, x => renderer.color = x, endValue, duration);
        }

        public static Tween DOFade(this SpriteRenderer renderer, float endValue, float duration)
        {
            return DOTween.To(() => renderer.color, x => renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, x), endValue, duration);
        }
    }
}