using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GoddessRequiem.Core.Application.Services
{
    public class AutoBattleService : IAutoBattleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AutoBattleService> _logger;
        private readonly IBattleAI _battleAI;

        public AutoBattleService(IUnitOfWork unitOfWork, ILogger<AutoBattleService> logger, IBattleAI battleAI)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleAI = battleAI;
        }

        public async Task<AutoBattleConfig> GetConfigAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            return user.AutoBattleConfig ?? new AutoBattleConfig
            {
                PriorityTargeting = TargetingPriority.WeakestEnemy,
                SkillUsage = SkillUsagePolicy.Smart,
                UltimateUsage = UltimateUsagePolicy.OnBossOrLowHP,
                HealThreshold = 0.4f,
                BuffPriority = BuffPriorityType.Offensive,
                FocusFire = true,
                AutoUltimate = true,
                AutoPotion = true,
                PotionThreshold = 0.3f
            };
        }

        public async Task UpdateConfigAsync(Guid userId, AutoBattleConfig config)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            // Validate config
            if (config.HealThreshold < 0f || config.HealThreshold > 1f)
                throw new ArgumentException("Heal threshold must be between 0 and 1");

            if (config.PotionThreshold < 0f || config.PotionThreshold > 1f)
                throw new ArgumentException("Potion threshold must be between 0 and 1");

            user.AutoBattleConfig = config;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<AutoBattleDecision> GetNextActionAsync(BattleStateDto state, List<UserCharacterDto> characters)
        {
            var playerUnits = state.PlayerUnits.Where(u => u.IsPlayerUnit && u.CurrentHP > 0).ToList();
            var enemyUnits = state.EnemyUnits.Where(u => !u.IsPlayerUnit && u.CurrentHP > 0).ToList();

            if (!playerUnits.Any() || !enemyUnits.Any())
                return new AutoBattleDecision { ActionType = BattleActionType.Wait, ActorId = Guid.Empty };

            // Get user config
            var config = await GetConfigAsync(state.PlayerUnits.First().UserCharacterId != Guid.Empty
                ? (await _unitOfWork.UserCharacters.GetByIdAsync(state.PlayerUnits.First().UserCharacterId.Value)).UserId
                : Guid.Empty);

            // Find best actor
            var actor = SelectBestActor(playerUnits, enemyUnits, config);
            if (actor == null)
                return new AutoBattleDecision { ActionType = BattleActionType.Wait, ActorId = Guid.Empty };

            // Determine action
            var action = DetermineAction(actor, playerUnits, enemyUnits, state, config, characters);

            return action;
        }

        public async Task<bool> ShouldUseUltimateAsync(BattleUnitDto unit, BattleStateDto state)
        {
            var config = await GetConfigAsync(Guid.Empty); // Would get actual user ID
            var enemyUnits = state.EnemyUnits.Where(u => !u.IsPlayerUnit && u.CurrentHP > 0).ToList();

            if (!enemyUnits.Any()) return false;

            var hpPercent = (float)unit.CurrentHP / unit.MaxHP;
            var hasBoss = enemyUnits.Any(e => e.Buffs.Any(b => b.Type == BuffType.Boss));
            var lowHPEnemies = enemyUnits.Count(e => (float)e.CurrentHP / e.MaxHP < 0.3f);

            switch (config.UltimateUsage)
            {
                case UltimateUsagePolicy.Always:
                    return unit.CurrentMP >= unit.MaxMP;

                case UltimateUsagePolicy.OnBossOrLowHP:
                    return unit.CurrentMP >= unit.MaxMP && (hasBoss || lowHPEnemies >= 2);

                case UltimateUsagePolicy.ManualOnly:
                    return false;

                case UltimateUsagePolicy.Smart:
                    // Use if boss present, multiple low HP enemies, or party in danger
                    var partyInDanger = state.PlayerUnits.Count(u => u.IsPlayerUnit && (float)u.CurrentHP / u.MaxHP < 0.3f) >= 2;
                    return unit.CurrentMP >= unit.MaxMP && (hasBoss || lowHPEnemies >= 2 || partyInDanger);

                default:
                    return false;
            }
        }

        public async Task<List<Guid>> GetPriorityTargetsAsync(BattleStateDto state, CharacterRole role)
        {
            var enemyUnits = state.EnemyUnits.Where(u => !u.IsPlayerUnit && u.CurrentHP > 0).ToList();
            var config = await GetConfigAsync(Guid.Empty);

            return config.PriorityTargeting switch
            {
                TargetingPriority.WeakestEnemy => enemyUnits
                    .OrderBy(e => (float)e.CurrentHP / e.MaxHP)
                    .ThenBy(e => e.CurrentHP)
                    .Select(e => e.Id)
                    .ToList(),

                TargetingPriority.StrongestEnemy => enemyUnits
                    .OrderByDescending(e => e.CurrentHP)
                    .Select(e => e.Id)
                    .ToList(),

                TargetingPriority.LowestHPPercent => enemyUnits
                    .OrderBy(e => (float)e.CurrentHP / e.MaxHP)
                    .Select(e => e.Id)
                    .ToList(),

                TargetingPriority.HighestThreat => enemyUnits
                    .OrderByDescending(e => CalculateThreat(e))
                    .Select(e => e.Id)
                    .ToList(),

                TargetingPriority.SpecificElement => enemyUnits
                    .Where(e => HasElementAdvantage(role, e))
                    .OrderBy(e => (float)e.CurrentHP / e.MaxHP)
                    .Select(e => e.Id)
                    .ToList(),

                _ => enemyUnits.Select(e => e.Id).ToList()
            };
        }

        private BattleUnitDto SelectBestActor(List<BattleUnitDto> playerUnits, List<BattleUnitDto> enemyUnits, AutoBattleConfig config)
        {
            // Prioritize units that can act (have MP for skills, or can basic attack)
            var availableActors = playerUnits.Where(u => u.CurrentMP > 0 || CanBasicAttack(u)).ToList();

            if (!availableActors.Any())
                return playerUnits.FirstOrDefault();

            // Sort by priority: units with ultimates ready, then highest damage potential
            return availableActors
                .OrderByDescending(u => u.CurrentMP >= u.MaxMP ? 1 : 0)
                .OrderByDescending(u => GetDamagePotential(u, enemyUnits))
                .FirstOrDefault();
        }

        private AutoBattleDecision DetermineAction(BattleUnitDto actor, List<BattleUnitDto> playerUnits,
            List<BattleUnitDto> enemyUnits, BattleStateDto state, AutoBattleConfig config, List<UserCharacterDto> characters)
        {
            var charData = characters.FirstOrDefault(c => c.Id == actor.UserCharacterId);
            var hpPercent = (float)actor.CurrentHP / actor.MaxHP;

            // Check for heal need
            if (hpPercent <= config.HealThreshold && HasHealSkill(charData))
            {
                var targets = playerUnits.Where(u => u.IsPlayerUnit && (float)u.CurrentHP / u.MaxHP <= config.HealThreshold).ToList();
                if (targets.Any())
                {
                    var target = targets.OrderBy(t => (float)t.CurrentHP / t.MaxHP).First();
                    return new AutoBattleDecision
                    {
                        ActorId = actor.Id,
                        ActionType = BattleActionType.Skill,
                        SkillId = GetHealSkillId(charData),
                        TargetIds = new List<Guid> { target.Id }
                    };
                }
            }

            // Check for buff/debuff
            if (ShouldBuff(actor, playerUnits, enemyUnits, config))
            {
                return new AutoBattleDecision
                {
                    ActorId = actor.Id,
                    ActionType = BattleActionType.Skill,
                    SkillId = GetBuffSkillId(charData),
                    TargetIds = GetBuffTargets(actor, playerUnits, config)
                };
            }

            // Check ultimate
            if (config.AutoUltimate && actor.CurrentMP >= actor.MaxMP)
            {
                var shouldUltimate = ShouldUseUltimateAsync(actor, state).Result;
                if (shouldUltimate)
                {
                    var targets = await GetPriorityTargetsAsync(state, charData?.Role ?? CharacterRole.DPS);
                    return new AutoBattleDecision
                    {
                        ActorId = actor.Id,
                        ActionType = BattleActionType.Ultimate,
                        TargetIds = targets.Take(1).ToList()
                    };
                }
            }

            // Check for offensive skill
            if (HasOffensiveSkill(charData) && actor.CurrentMP >= GetSkillCost(charData))
            {
                var targets = await GetPriorityTargetsAsync(state, charData?.Role ?? CharacterRole.DPS);
                if (targets.Any())
                {
                    return new AutoBattleDecision
                    {
                        ActorId = actor.Id,
                        ActionType = BattleActionType.Skill,
                        SkillId = GetMainOffensiveSkillId(charData),
                        TargetIds = targets.Take(GetSkillTargetCount(charData)).ToList()
                    };
                }
            }

            // Basic attack
            var basicTargets = await GetPriorityTargetsAsync(state, charData?.Role ?? CharacterRole.DPS);
            if (basicTargets.Any())
            {
                return new AutoBattleDecision
                {
                    ActorId = actor.Id,
                    ActionType = BattleActionType.BasicAttack,
                    TargetIds = basicTargets.Take(1).ToList()
                };
            }

            return new AutoBattleDecision { ActionType = BattleActionType.Wait, ActorId = actor.Id };
        }

        private bool ShouldBuff(BattleUnitDto actor, List<BattleUnitDto> playerUnits, List<BattleUnitDto> enemyUnits, AutoBattleConfig config)
        {
            // Check if party needs buffs
            var hasAttackBuff = playerUnits.Any(u => u.Buffs.Any(b => b.Type == BuffType.AttackUp));
            var hasDefenseBuff = playerUnits.Any(u => u.Buffs.Any(b => b.Type == BuffType.DefenseUp));
            var hasSpeedBuff = playerUnits.Any(u => u.Buffs.Any(b => b.Type == BuffType.SpeedUp));

            var enemyDebuffed = enemyUnits.Any(e => e.Debuffs.Any(d => d.Type == DebuffType.AttackDown || d.Type == DebuffType.DefenseDown));

            switch (config.BuffPriority)
            {
                case BuffPriorityType.Offensive:
                    return !hasAttackBuff || !enemyDebuffed;
                case BuffPriorityType.Defensive:
                    return !hasDefenseBuff || hpPercent < 0.5f;
                case BuffPriorityType.Support:
                    return !hasSpeedBuff || !hasAttackBuff;
                case BuffPriorityType.Balanced:
                    return !hasAttackBuff || !hasDefenseBuff || !enemyDebuffed;
                default:
                    return false;
            }
        }

        private float CalculateThreat(BattleUnitDto enemy)
        {
            var baseThreat = enemy.CurrentHP;
            var attackBuff = enemy.Buffs.FirstOrDefault(b => b.Type == BuffType.AttackUp)?.Value ?? 0;
            var isBoss = enemy.Buffs.Any(b => b.Type == BuffType.Boss) ? 2f : 1f;

            return baseThreat * (1 + attackBuff) * isBoss;
        }

        private bool HasElementAdvantage(CharacterRole role, BattleUnitDto enemy)
        {
            // Simplified - would check actual element advantage
            return true;
        }

        private bool CanBasicAttack(BattleUnitDto unit) => true;
        private float GetDamagePotential(BattleUnitDto unit, List<BattleUnitDto> enemies) => unit.CurrentMP;
        private bool HasHealSkill(UserCharacterDto charData) => charData?.Class == CharacterClass.Healer || charData?.Class == CharacterClass.Support;
        private bool HasOffensiveSkill(UserCharacterDto charData) => true;
        private Guid GetHealSkillId(UserCharacterDto charData) => Guid.NewGuid();
        private Guid GetBuffSkillId(UserCharacterDto charData) => Guid.NewGuid();
        private Guid GetMainOffensiveSkillId(UserCharacterDto charData) => Guid.NewGuid();
        private int GetSkillCost(UserCharacterDto charData) => 20;
        private int GetSkillTargetCount(UserCharacterDto charData) => 1;
        private List<Guid> GetBuffTargets(BattleUnitDto actor, List<BattleUnitDto> playerUnits, AutoBattleConfig config)
        {
            return config.FocusFire
                ? new List<Guid> { actor.Id }
                : playerUnits.Where(u => u.IsPlayerUnit).Select(u => u.Id).ToList();
        }
    }
}