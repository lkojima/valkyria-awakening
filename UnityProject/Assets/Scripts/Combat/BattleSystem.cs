using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Combat
{
    public class BattleSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BattleConfig _config;

        [Header("References")]
        [SerializeField] private Transform _playerUnitRoot;
        [SerializeField] private Transform _enemyUnitRoot;
        [SerializeField] private BattleUI _battleUI;
        [SerializeField] private ParticleSystem _vfxContainer;

        private BattleState _currentState;
        private List<BattleUnit> _playerUnits = new();
        private List<BattleUnit> _enemyUnits = new();
        private Queue<BattleAction> _actionQueue = new();
        private BattleUnit _activeUnit;
        private int _currentTurn;
        private bool _isAutoBattle;
        private AutoBattleMode _autoMode;
        private BattleSpeed _battleSpeed;
        private System.Random _rng;
        private string _battleSeed;

        public BattleState CurrentState => _currentState;
        public IReadOnlyList<BattleUnit> PlayerUnits => _playerUnits;
        public IReadOnlyList<BattleUnit> EnemyUnits => _enemyUnits;
        public BattleUnit ActiveUnit => _activeUnit;
        public int CurrentTurn => _currentTurn;
        public bool IsAutoBattle => _isAutoBattle;
        public bool IsPlayerTurn => _activeUnit?.IsPlayerUnit ?? false;

        public event System.Action<BattleState> OnStateChanged;
        public event System.Action<BattleSetup> OnBattleStarted;
        public event System.Action<int, BattleUnit> OnTurnStarted;
        public event System.Action<BattleUnit> OnTurnStart;
        public event System.Action<BattleAction> OnActionExecuted;
        public event System.Action<BattleResult> OnBattleEnded;

        public async UniTask<BattleResult> StartBattleAsync(BattleSetup setup)
        {
            _currentState = BattleState.Preparing;
            _battleSeed = setup.Seed ?? System.Guid.NewGuid().ToString("N")[..16];
            _rng = new System.Random(_battleSeed.GetHashCode());
            _battleSpeed = setup.Speed;
            _isAutoBattle = setup.AutoMode != AutoBattleMode.Off;
            _autoMode = setup.AutoMode;
            _currentTurn = 0;

            // Initialize units
            await InitializeUnitsAsync(setup.PlayerUnits, setup.EnemyUnits);

            // Determine turn order
            DetermineTurnOrder();

            _currentState = BattleState.Playing;
            OnStateChanged?.Invoke(_currentState);

            // Fire battle started event
            OnBattleStarted?.Invoke(setup);

            // Start battle loop
            return await RunBattleLoopAsync();
        }

        private async UniTask InitializeUnitsAsync(List<UnitSetup> playerSetups, List<UnitSetup> enemySetups)
        {
            _playerUnits.Clear();
            _enemyUnits.Clear();

            // Create player units
            for (int i = 0; i < playerSetups.Count; i++)
            {
                var unit = await CreateUnitAsync(playerSetups[i], true, i);
                _playerUnits.Add(unit);
            }

            // Create enemy units
            for (int i = 0; i < enemySetups.Count; i++)
            {
                var unit = await CreateUnitAsync(enemySetups[i], false, i);
                _enemyUnits.Add(unit);
            }

            // Initialize UI
            if (_battleUI != null)
            {
                await _battleUI.InitializeAsync(_playerUnits, _enemyUnits);
            }
        }

        private async UniTask<BattleUnit> CreateUnitAsync(UnitSetup setup, bool isPlayer, int positionIndex)
        {
            var unitGO = new GameObject($"Unit_{setup.CharacterName}");
            unitGO.transform.SetParent(isPlayer ? _playerUnitRoot : _enemyUnitRoot);

            var unit = unitGO.AddComponent<BattleUnit>();
            await unit.InitializeAsync(setup, isPlayer, positionIndex, _config);

            return unit;
        }

        private void DetermineTurnOrder()
        {
            var allUnits = new List<BattleUnit>();
            allUnits.AddRange(_playerUnits);
            allUnits.AddRange(_enemyUnits);

            allUnits.Sort((a, b) => b.Stats.Speed.CompareTo(a.Stats.Speed));

            for (int i = 0; i < allUnits.Count; i++)
            {
                allUnits[i].TurnOrder = i + 1;
            }
        }

        private async UniTask<BattleResult> RunBattleLoopAsync()
        {
            while (_currentState == BattleState.Playing)
            {
                _currentTurn++;
                _battleUI.UpdateTurnCounter(_currentTurn);

                // Process turn for each unit in order
                var allUnits = new List<BattleUnit>();
                allUnits.AddRange(_playerUnits);
                allUnits.AddRange(_enemyUnits);
                allUnits.Sort((a, b) => a.TurnOrder.CompareTo(b.TurnOrder));

                foreach (var unit in allUnits)
                {
                    if (_currentState != BattleState.Playing) break;
                    if (!unit.IsAlive) continue;

                    _activeUnit = unit;
                    unit.OnTurnStart();
                    OnTurnStart?.Invoke(unit);
                    OnTurnStarted?.Invoke(_currentTurn, unit);
                    _battleUI.HighlightActiveUnit(unit);

                    // Process status effects
                    unit.ProcessTurnStartEffects();

                    // Check for victory/defeat
                    var result = CheckVictoryConditions();
                    if (result != BattleOutcome.None)
                    {
                        return await EndBattleAsync(result);
                    }

                    // Execute turn
                    if (unit.IsPlayerUnit)
                    {
                        if (_isAutoBattle)
                        {
                            await ExecuteAutoTurnAsync(unit);
                        }
                        else
                        {
                            await WaitForPlayerInputAsync(unit);
                        }
                    }
                    else
                    {
                        await ExecuteEnemyTurnAsync(unit);
                    }

                    // Check again after action
                    result = CheckVictoryConditions();
                    if (result != BattleOutcome.None)
                    {
                        return await EndBattleAsync(result);
                    }

                    unit.OnTurnEnd();
                }

                // Turn end effects
                ProcessTurnEndEffects();

                // Check max turns
                if (_currentTurn >= _config.MaxTurns)
                {
                    return await EndBattleAsync(BattleOutcome.Draw);
                }
            }

            return BattleOutcome.None;
        }

        private async UniTask ExecuteAutoTurnAsync(BattleUnit unit)
        {
            var action = SelectAutoAction(unit);
            await ExecuteActionAsync(action);
        }

        private async UniTask WaitForPlayerInputAsync(BattleUnit unit)
        {
            var action = await _battleUI.WaitForPlayerActionAsync(unit);
            await ExecuteActionAsync(action);
        }

        private async UniTask ExecuteEnemyTurnAsync(BattleUnit unit)
        {
            var action = SelectEnemyAction(unit);
            await ExecuteActionAsync(action);
        }

        private BattleAction SelectAutoAction(BattleUnit unit)
        {
            // Simple AI: prioritize ultimate if available, then best skill
            if (unit.CanUseUltimate && _rng.NextDouble() < 0.7)
            {
                return new BattleAction
                {
                    Actor = unit,
                    Skill = unit.UltimateSkill,
                    Targets = SelectTargets(unit.UltimateSkill, unit)
                };
            }

            var availableSkills = unit.GetAvailableSkills();
            if (availableSkills.Count > 0)
            {
                var skill = availableSkills[_rng.Next(availableSkills.Count)];
                return new BattleAction
                {
                    Actor = unit,
                    Skill = skill,
                    Targets = SelectTargets(skill, unit)
                };
            }

            // Basic attack
            return new BattleAction
            {
                Actor = unit,
                Skill = unit.BasicSkill,
                Targets = SelectTargets(unit.BasicSkill, unit)
            };
        }

        private BattleAction SelectEnemyAction(BattleUnit unit)
        {
            // Enemy AI based on personality
            return SelectAutoAction(unit);
        }

        private List<BattleUnit> SelectTargets(SkillData skill, BattleUnit caster)
        {
            var targets = new List<BattleUnit>();
            var potentialTargets = skill.TargetsEnemies ? _enemyUnits : _playerUnits;
            potentialTargets = potentialTargets.FindAll(u => u.IsAlive);

            switch (skill.TargetType)
            {
                case SkillTargetType.SingleEnemy:
                case SkillTargetType.SingleAlly:
                    if (potentialTargets.Count > 0)
                    {
                        targets.Add(potentialTargets[_rng.Next(potentialTargets.Count)]);
                    }
                    break;

                case SkillTargetType.AllEnemies:
                case SkillTargetType.AllAllies:
                    targets.AddRange(potentialTargets);
                    break;

                case SkillTargetType.RandomEnemy:
                case SkillTargetType.RandomAlly:
                    if (potentialTargets.Count > 0)
                    {
                        var count = Mathf.Min(skill.TargetCount, potentialTargets.Count);
                        var shuffled = new List<BattleUnit>(potentialTargets);
                        for (int i = 0; i < count; i++)
                        {
                            int idx = _rng.Next(i, shuffled.Count);
                            (shuffled[i], shuffled[idx]) = (shuffled[idx], shuffled[i]);
                            targets.Add(shuffled[i]);
                        }
                    }
                    break;

                case SkillTargetType.LowestHPAlly:
                    targets.Add(potentialTargets.OrderBy(u => u.Stats.CurrentHP).First());
                    break;

                case SkillTargetType.HighestHPEnemy:
                    targets.Add(potentialTargets.OrderByDescending(u => u.Stats.CurrentHP).First());
                    break;
            }

            return targets;
        }

        private async UniTask ExecuteActionAsync(BattleAction action)
        {
            _battleUI.ShowActionPreview(action);
            await UniTask.Delay((int)(500 / _battleSpeed));

            var results = new List<ActionResult>();

            foreach (var target in action.Targets)
            {
                var result = CalculateActionResult(action, target);
                results.Add(result);

                await ApplyActionResultAsync(action, target, result);
            }

            action.Results = results;
            _actionQueue.Enqueue(action);
            OnActionExecuted?.Invoke(action);

            _battleUI.ShowActionResults(action);
            await UniTask.Delay((int)(1000 / _battleSpeed));
        }

        private ActionResult CalculateActionResult(BattleAction action, BattleUnit target)
        {
            var result = new ActionResult
            {
                Target = target,
                Skill = action.Skill
            };

            // Calculate hit chance
            var hitChance = action.Actor.Stats.Accuracy - target.Stats.Evasion;
            hitChance = Mathf.Clamp(hitChance, 0.05f, 0.95f);
            result.IsHit = _rng.NextDouble() < hitChance;

            if (!result.IsHit)
            {
                result.IsDodged = true;
                return result;
            }

            // Calculate crit
            var critChance = action.Actor.Stats.CritRate;
            result.IsCrit = _rng.NextDouble() < critChance;

            // Calculate damage/heal
            if (action.Skill.EffectType == SkillEffectType.Damage)
            {
                result.Value = CalculateDamage(action, target, result.IsCrit);
                result.IsDamage = true;
            }
            else if (action.Skill.EffectType == SkillEffectType.Heal)
            {
                result.Value = CalculateHeal(action, target, result.IsCrit);
                result.IsHeal = true;
            }

            // Apply elemental multiplier
            result.Value = ApplyElementalMultiplier(result.Value, action.Skill.Element, target.Stats.Element);

            return result;
        }

        private int CalculateDamage(BattleAction action, BattleUnit target, bool isCrit)
        {
            var attacker = action.Actor;
            var skill = action.Skill;

            var baseDamage = skill.BaseDamage + attacker.Stats.Attack * skill.AttackMultiplier;
            var defense = target.Stats.Defense;
            var damageReduction = defense / (defense + 1000f);

            var damage = baseDamage * (1 - damageReduction);

            if (isCrit)
            {
                damage *= attacker.Stats.CritDamage;
            }

            // Random variance ±10%
            damage *= 0.9f + (float)_rng.NextDouble() * 0.2f;

            return Mathf.Max(1, Mathf.RoundToInt(damage));
        }

        private int CalculateHeal(BattleAction action, BattleUnit target, bool isCrit)
        {
            var healer = action.Actor;
            var skill = action.Skill;

            var baseHeal = skill.BaseHeal + healer.Stats.Attack * skill.HealMultiplier;

            if (isCrit)
            {
                baseHeal *= 1.5f;
            }

            return Mathf.RoundToInt(baseHeal);
        }

        private int ApplyElementalMultiplier(int value, Element attackElement, Element targetElement)
        {
            if (attackElement == Element.None || targetElement == Element.None)
                return value;

            var multiplier = GetElementalMultiplier(attackElement, targetElement);
            return Mathf.RoundToInt(value * multiplier);
        }

        private float GetElementalMultiplier(Element attack, Element defense)
        {
            // Fire > Nature > Water > Fire
            // Light <-> Dark
            if (attack == Element.Fire && defense == Element.Nature) return 1.5f;
            if (attack == Element.Nature && defense == Element.Water) return 1.5f;
            if (attack == Element.Water && defense == Element.Fire) return 1.5f;
            if (attack == Element.Light && defense == Element.Dark) return 1.5f;
            if (attack == Element.Dark && defense == Element.Light) return 1.5f;

            if (attack == Element.Fire && defense == Element.Water) return 0.5f;
            if (attack == Element.Water && defense == Element.Nature) return 0.5f;
            if (attack == Element.Nature && defense == Element.Fire) return 0.5f;
            if (attack == Element.Light && defense == Element.Light) return 0.5f;
            if (attack == Element.Dark && defense == Element.Dark) return 0.5f;

            return 1f;
        }

        private async UniTask ApplyActionResultAsync(BattleAction action, BattleUnit target, ActionResult result)
        {
            if (result.IsDodged)
            {
                target.PlayDodgeAnimation();
                return;
            }

            if (result.IsDamage)
            {
                target.TakeDamage(result.Value, result.IsCrit);
            }
            else if (result.IsHeal)
            {
                target.Heal(result.Value);
            }

            // Apply status effects
            foreach (var effect in action.Skill.Effects)
            {
                if (_rng.NextDouble() < effect.Chance)
                {
                    target.ApplyStatusEffect(effect);
                }
            }
        }

        private void ProcessTurnEndEffects()
        {
            foreach (var unit in _playerUnits)
            {
                if (unit.IsAlive) unit.ProcessTurnEndEffects();
            }
            foreach (var unit in _enemyUnits)
            {
                if (unit.IsAlive) unit.ProcessTurnEndEffects();
            }
        }

        private BattleOutcome CheckVictoryConditions()
        {
            var alivePlayers = _playerUnits.FindAll(u => u.IsAlive).Count;
            var aliveEnemies = _enemyUnits.FindAll(u => u.IsAlive).Count;

            if (aliveEnemies == 0) return BattleOutcome.Victory;
            if (alivePlayers == 0) return BattleOutcome.Defeat;

            return BattleOutcome.None;
        }

        private async UniTask<BattleOutcome> EndBattleAsync(BattleOutcome result)
        {
            _currentState = BattleState.Ended;
            OnStateChanged?.Invoke(_currentState);

            var battleResult = new BattleResult
            {
                Outcome = result,
                Turns = _currentTurn,
                PlayerUnits = _playerUnits,
                EnemyUnits = _enemyUnits,
                ActionLog = _actionQueue.ToArray(),
                Seed = _battleSeed
            };

            OnBattleEnded?.Invoke(battleResult);
            await _battleUI.ShowBattleResultAsync(battleResult);

            return result;
        }

        public void SetAutoBattle(bool enabled, AutoBattleMode mode = AutoBattleMode.Smart)
        {
            _isAutoBattle = enabled;
            _autoMode = mode;
        }

        public void SetBattleSpeed(BattleSpeed speed)
        {
            _battleSpeed = speed;
        }

        public void ChangeState(BattleState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            OnStateChanged?.Invoke(_currentState);
        }

        public static BattleSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    public enum BattleState
    {
        Preparing,
        Playing,
        Paused,
        Ended
    }

    public enum BattleOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
        Draw = 3
    }

    public enum AutoBattleMode
    {
        Off = 0,
        Basic = 1,
        Smart = 2,
        Full = 3
    }

    public enum BattleSpeed
    {
        Normal = 1,
        Fast = 2,
        UltraFast = 3
    }

    public enum SkillTargetType
    {
        SingleEnemy,
        AllEnemies,
        RandomEnemy,
        SingleAlly,
        AllAllies,
        RandomAlly,
        LowestHPAlly,
        HighestHPEnemy
    }

    public enum SkillEffectType
    {
        Damage,
        Heal,
        Buff,
        Debuff
    }

    [System.Serializable]
    public class BattleSetup
    {
        public List<UnitSetup> PlayerUnits;
        public List<UnitSetup> EnemyUnits;
        public BattleSpeed Speed;
        public AutoBattleMode AutoMode;
        public string Seed;
    }

    [System.Serializable]
    public class UnitSetup
    {
        public string CharacterId;
        public string CharacterName;
        public int Level;
        public int Ascension;
        public List<SkillData> Skills;
        public EquipmentSetup Equipment;
        public RelicSetup Relic;
        public List<RuneSetup> Runes;
    }

    [System.Serializable]
    public class BattleResult
    {
        public BattleOutcome Outcome;
        public int Turns;
        public List<BattleUnit> PlayerUnits;
        public List<BattleUnit> EnemyUnits;
        public BattleAction[] ActionLog;
        public string Seed;
    }

    [System.Serializable]
    public class BattleAction
    {
        public BattleUnit Actor;
        public SkillData Skill;
        public List<BattleUnit> Targets;
        public List<ActionResult> Results;
    }

    [System.Serializable]
    public class ActionResult
    {
        public BattleUnit Target;
        public SkillData Skill;
        public int Value;
        public bool IsHit;
        public bool IsCrit;
        public bool IsDodged;
        public bool IsDamage;
        public bool IsHeal;
    }

    [System.Serializable]
    public class BattleConfig
    {
        public int MaxTurns = 100;
        public float TurnTimeLimit = 30f;
        public float ActionAnimationTime = 1f;
    }
}