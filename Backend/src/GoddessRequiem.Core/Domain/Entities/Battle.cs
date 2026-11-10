using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class Battle : BaseEntity
    {
        public Guid UserId { get; private set; }
        public GameMode Mode { get; private set; }
        public int StageId { get; private set; }
        public BattlePhase Phase { get; private set; }
        public int CurrentTurn { get; private set; }
        public int MaxTurns { get; private set; }
        public BattleSpeed Speed { get; private set; }
        public AutoBattleMode AutoMode { get; private set; }
        public AutoProgressMode AutoProgressMode { get; private set; }
        public List<BattleUnit> PlayerUnits { get; private set; } = new();
        public List<BattleUnit> EnemyUnits { get; private set; } = new();
        public List<BattleAction> ActionLog { get; private set; } = new();
        public BattleResult? Result { get; private set; }
        public List<Reward> Rewards { get; private set; } = new();
        public DateTime StartedAt { get; private set; }
        public DateTime? EndedAt { get; private set; }
        public string Seed { get; private set; }
        public Dictionary<string, object> BattleData { get; private set; } = new();

        private Battle() { }

        public Battle(
            Guid userId,
            GameMode mode,
            int stageId,
            List<BattleUnit> playerUnits,
            List<BattleUnit> enemyUnits,
            BattleSpeed speed = BattleSpeed.Normal,
            AutoBattleMode autoMode = AutoBattleMode.Off,
            AutoProgressMode autoProgressMode = AutoProgressMode.Off,
            int maxTurns = 100)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Mode = mode;
            StageId = stageId;
            Phase = BattlePhase.Preparation;
            CurrentTurn = 0;
            MaxTurns = maxTurns;
            Speed = speed;
            AutoMode = autoMode;
            AutoProgressMode = autoProgressMode;
            PlayerUnits = playerUnits;
            EnemyUnits = enemyUnits;
            StartedAt = DateTime.UtcNow;
            Seed = Guid.NewGuid().ToString("N")[..16];
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Start()
        {
            Phase = BattlePhase.TurnStart;
            CurrentTurn = 1;
            InitializeTurnOrder();
            UpdatedAt = DateTime.UtcNow;
        }

        public void NextTurn()
        {
            CurrentTurn++;
            Phase = BattlePhase.TurnStart;
            ProcessTurnStartEffects();

            if (CurrentTurn > MaxTurns)
            {
                End(BattleResult.Draw);
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public void ProcessAction(BattleAction action)
        {
            ActionLog.Add(action);
            ApplyActionEffects(action);
            CheckVictoryConditions();
            UpdatedAt = DateTime.UtcNow;
        }

        public void End(BattleResult result)
        {
            Phase = result == BattleResult.Victory ? BattlePhase.Victory :
                    result == BattleResult.Defeat ? BattlePhase.Defeat : BattlePhase.Draw;
            Result = result;
            EndedAt = DateTime.UtcNow;
            CalculateRewards();
            UpdatedAt = DateTime.UtcNow;
        }

        private void InitializeTurnOrder()
        {
            var allUnits = new List<BattleUnit>();
            allUnits.AddRange(PlayerUnits);
            allUnits.AddRange(EnemyUnits);

            // Sort by speed descending
            allUnits.Sort((a, b) => b.Speed.CompareTo(a.Speed));

            for (int i = 0; i < allUnits.Count; i++)
            {
                allUnits[i].TurnOrder = i + 1;
            }

            BattleData["TurnOrder"] = allUnits.Select(u => u.Id).ToList();
        }

        private void ProcessTurnStartEffects()
        {
            // Process regen, dot, buff/debuff tick down
            foreach (var unit in PlayerUnits.Concat(EnemyUnits))
            {
                unit.ProcessTurnStart();
            }
        }

        private void ApplyActionEffects(BattleAction action)
        {
            // Apply damage, healing, buffs, debuffs
            foreach (var effect in action.Effects)
            {
                foreach (var targetId in action.TargetIds)
                {
                    var target = FindUnit(targetId);
                    if (target != null)
                    {
                        target.ApplyEffect(effect);
                    }
                }
            }
        }

        private void CheckVictoryConditions()
        {
            var alivePlayerUnits = PlayerUnits.Count(u => u.CurrentHP > 0);
            var aliveEnemyUnits = EnemyUnits.Count(u => u.CurrentHP > 0);

            if (aliveEnemyUnits == 0)
            {
                End(BattleResult.Victory);
            }
            else if (alivePlayerUnits == 0)
            {
                End(BattleResult.Defeat);
            }
        }

        private void CalculateRewards()
        {
            if (Result == BattleResult.Victory)
            {
                // Calculate based on stage, mode, performance
                Rewards = GenerateRewards();
            }
        }

        private List<Reward> GenerateRewards()
        {
            // Implementation would use stage config, drop tables, etc.
            return new List<Reward>();
        }

        private BattleUnit FindUnit(Guid id)
        {
            return PlayerUnits.FirstOrDefault(u => u.Id == id)
                   ?? EnemyUnits.FirstOrDefault(u => u.Id == id);
        }
    }

    public class BattleUnit
    {
        public Guid Id { get; set; }
        public Guid CharacterId { get; set; }
        public Guid UserCharacterId { get; set; }
        public string Name { get; set; }
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public float CritRate { get; set; }
        public float CritDamage { get; set; }
        public float Resistance { get; set; }
        public float Accuracy { get; set; }
        public Element Element { get; set; }
        public CharacterRole Role { get; set; }
        public int Energy { get; set; }
        public int MaxEnergy { get; set; } = 100;
        public int TurnOrder { get; set; }
        public bool IsPlayerUnit { get; set; }
        public List<ActiveBuff> Buffs { get; set; } = new();
        public List<ActiveDebuff> Debuffs { get; set; } = new();
        public List<SkillData> AvailableSkills { get; set; } = new();
        public int Skill1Cooldown { get; set; }
        public int Skill2Cooldown { get; set; }
        public int Skill3Cooldown { get; set; }
        public int UltimateCooldown { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new();

        public void TakeDamage(int damage, bool isTrueDamage = false, bool isCrit = false)
        {
            if (isTrueDamage)
            {
                CurrentHP = Math.Max(0, CurrentHP - damage);
                return;
            }

            var actualDamage = damage;
            if (!isTrueDamage)
            {
                actualDamage = CalculateDamageReduction(damage);
            }

            CurrentHP = Math.Max(0, CurrentHP - actualDamage);
        }

        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        }

        public void GainEnergy(int amount)
        {
            Energy = Math.Min(MaxEnergy, Energy + amount);
        }

        public void SpendEnergy(int amount)
        {
            Energy = Math.Max(0, Energy - amount);
        }

        public void ApplyEffect(SkillEffect effect)
        {
            // Apply buff/debuff based on effect type
        }

        public void ProcessTurnStart()
        {
            // Tick down buffs/debuffs
            Buffs.RemoveAll(b => b.RemainingTurns <= 0);
            Debuffs.RemoveAll(d => d.RemainingTurns <= 0);

            foreach (var buff in Buffs) buff.RemainingTurns--;
            foreach (var debuff in Debuffs) debuff.RemainingTurns--;

            // Process regen
            var hpRegen = Buffs.Where(b => b.Type == BuffType.HPRegeneration).Sum(b => b.Value);
            if (hpRegen > 0) Heal((int)hpRegen);

            // Process DoT
            var poisonDmg = Debuffs.Where(d => d.Type == DebuffType.Poison).Sum(d => d.Value);
            var burnDmg = Debuffs.Where(d => d.Type == DebuffType.Burn).Sum(d => d.Value);
            var bleedDmg = Debuffs.Where(d => d.Type == DebuffType.Bleed).Sum(d => d.Value);

            TakeDamage((int)(poisonDmg + burnDmg + bleedDmg), isTrueDamage: true);

            // Reduce cooldowns
            Skill1Cooldown = Math.Max(0, Skill1Cooldown - 1);
            Skill2Cooldown = Math.Max(0, Skill2Cooldown - 1);
            Skill3Cooldown = Math.Max(0, Skill3Cooldown - 1);
            UltimateCooldown = Math.Max(0, UltimateCooldown - 1);
        }

        private int CalculateDamageReduction(int damage)
        {
            var reduction = (float)Defense / (Defense + 1000f);
            return (int)(damage * (1f - reduction));
        }

        public bool HasBuff(BuffType type) => Buffs.Any(b => b.Type == type);
        public bool HasDebuff(DebuffType type) => Debuffs.Any(d => d.Type == type);

        public ActiveBuff GetBuff(BuffType type) => Buffs.FirstOrDefault(b => b.Type == type);
        public ActiveDebuff GetDebuff(DebuffType type) => Debuffs.FirstOrDefault(d => d.Type == type);
    }

    public class ActiveBuff
    {
        public BuffType Type { get; set; }
        public float Value { get; set; }
        public int RemainingTurns { get; set; }
        public int MaxStacks { get; set; }
        public int CurrentStacks { get; set; }
        public Guid SourceId { get; set; }
        public bool IsPermanent { get; set; }
    }

    public class ActiveDebuff
    {
        public DebuffType Type { get; set; }
        public float Value { get; set; }
        public int RemainingTurns { get; set; }
        public int MaxStacks { get; set; }
        public int CurrentStacks { get; set; }
        public Guid SourceId { get; set; }
        public bool IsPermanent { get; set; }
        public bool IgnoresImmunity { get; set; }
    }

    public class BattleAction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ActorId { get; set; }
        public Guid SkillId { get; set; }
        public SkillType SkillType { get; set; }
        public List<Guid> TargetIds { get; set; } = new();
        public List<BattleActionEffect> Effects { get; set; } = new();
        public int TurnNumber { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsAuto { get; set; }
    }

    public class BattleActionEffect
    {
        public SkillEffectType Type { get; set; }
        public Guid TargetId { get; set; }
        public float Value { get; set; }
        public bool IsCrit { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsDodged { get; set; }
        public Element? Element { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public enum BattleResult
    {
        Victory = 1,
        Defeat = 2,
        Draw = 3
    }

    public class Reward
    {
        public RewardType Type { get; set; }
        public Guid ResourceId { get; set; }
        public int Quantity { get; set; }
        public float DropRate { get; set; }
        public bool IsGuaranteed { get; set; }
    }
}