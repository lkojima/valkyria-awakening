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
    public class BattleService : IBattleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BattleService> _logger;
        private readonly IAutoBattleService _autoBattleService;
        private readonly IBattleSimulator _battleSimulator;

        public BattleService(IUnitOfWork unitOfWork, ILogger<BattleService> logger,
            IAutoBattleService autoBattleService, IBattleSimulator battleSimulator)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _autoBattleService = autoBattleService;
            _battleSimulator = battleSimulator;
        }

        public async Task<BattleStartResult> StartBattleAsync(Guid userId, GameMode mode, int stageId, List<Guid> characterIds, BattleSpeed speed, AutoBattleMode autoMode)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            // Validate characters
            var characters = new List<UserCharacter>();
            foreach (var charId in characterIds)
            {
                var uc = await _unitOfWork.UserCharacters.GetByIdAsync(charId);
                if (uc == null || uc.UserId != userId)
                    throw new KeyNotFoundException($"Character {charId} not owned");
                characters.Add(uc);
            }

            if (characters.Count < 1 || characters.Count > 5)
                throw new ArgumentException("Party must have 1-5 characters");

            // Get stage/enemy data based on mode
            var stage = await GetStageAsync(mode, stageId);
            if (stage == null)
                throw new KeyNotFoundException("Stage not found");

            // Check energy/stamina cost
            var cost = GetEnergyCost(mode, stage);
            if (cost > 0)
            {
                var hasEnergy = await CheckEnergyAsync(userId, cost);
                if (!hasEnergy)
                    throw new InvalidOperationException("Insufficient energy");
                await ConsumeEnergyAsync(userId, cost);
            }

            // Create battle
            var battle = new Battle
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Mode = mode,
                StageId = stageId,
                Speed = speed,
                AutoMode = autoMode,
                PlayerParty = characters.Select(c => new BattleUnit
                {
                    UserCharacterId = c.Id,
                    CharacterId = c.CharacterId,
                    Level = c.Level,
                    AscensionLevel = c.AscensionLevel,
                    Skills = GetCharacterSkills(c),
                    CurrentHP = CalculateMaxHP(c),
                    MaxHP = CalculateMaxHP(c),
                    Position = characters.IndexOf(c)
                }).ToList(),
                EnemyParty = stage.Enemies.Select(e => new BattleUnit
                {
                    EnemyId = e.Id,
                    Level = e.Level,
                    Skills = e.Skills,
                    CurrentHP = e.HP,
                    MaxHP = e.HP,
                    Position = stage.Enemies.IndexOf(e)
                }).ToList(),
                State = BattleState.Preparing,
                StartedAt = DateTime.UtcNow,
                Seed = _battleSimulator.GenerateSeed()
            };

            await _unitOfWork.Battles.AddAsync(battle);
            await _unitOfWork.SaveChangesAsync();

            // Initialize battle state
            var battleState = _battleSimulator.Initialize(battle);

            return new BattleStartResult
            {
                BattleId = battle.Id,
                State = MapToStateDto(battleState),
                Seed = battle.Seed,
                StageInfo = MapToStageInfo(stage)
            };
        }

        public async Task<BattleActionResult> ProcessActionAsync(Guid battleId, BattleAction action)
        {
            var battle = await _unitOfWork.Battles.GetByIdAsync(battleId);
            if (battle == null)
                throw new KeyNotFoundException("Battle not found");

            if (battle.State != BattleState.InProgress)
                throw new InvalidOperationException("Battle not in progress");

            // Process action through simulator
            var result = _battleSimulator.ProcessAction(battle, action);

            // Update battle state
            battle.State = result.NewState;
            battle.CurrentTurn = result.TurnNumber;
            battle.ActionLog.Add(new BattleActionLog
            {
                Turn = result.TurnNumber,
                ActorId = action.ActorId,
                ActionType = action.Type,
                TargetIds = action.TargetIds,
                Damage = result.Damage,
                Effects = result.AppliedEffects,
                Timestamp = DateTime.UtcNow
            });

            if (result.IsFinished)
            {
                battle.State = BattleState.Finished;
                battle.FinishedAt = DateTime.UtcNow;
                battle.Result = result.BattleResult;
                battle.Rewards = result.Rewards;

                // Grant rewards
                await GrantBattleRewardsAsync(battle.UserId, result.Rewards);

                // Update progress based on mode
                await UpdateProgressAsync(battle);
            }

            await _unitOfWork.Battles.UpdateAsync(battle);
            await _unitOfWork.SaveChangesAsync();

            return new BattleActionResult
            {
                NewState = MapToStateDto(result.NewBattleState),
                DamageDealt = result.Damage,
                EffectsApplied = result.AppliedEffects,
                IsBattleEnded = result.IsFinished,
                BattleResult = result.IsFinished ? MapToBattleResult(result.BattleResult) : null
            };
        }

        public async Task<BattleActionResult> AutoBattleTurnAsync(Guid battleId)
        {
            var battle = await _unitOfWork.Battles.GetByIdAsync(battleId);
            if (battle == null)
                throw new KeyNotFoundException("Battle not found");

            if (battle.State != BattleState.InProgress || battle.AutoMode == AutoBattleMode.Manual)
                throw new InvalidOperationException("Auto battle not active");

            // Get AI decision
            var playerUnits = battle.PlayerParty.Where(u => u.CurrentHP > 0).ToList();
            var enemyUnits = battle.EnemyParty.Where(u => u.CurrentHP > 0).ToList();

            var decision = await _autoBattleService.GetNextActionAsync(
                MapToStateDto(battle),
                await GetUserCharactersAsync(battle.UserId, playerUnits.Select(u => u.UserCharacterId).ToList())
            );

            var action = new BattleAction
            {
                ActorId = decision.ActorId,
                Type = decision.ActionType,
                TargetIds = decision.TargetIds,
                SkillId = decision.SkillId
            };

            return await ProcessActionAsync(battleId, action);
        }

        public async Task<BattleEndResult> EndBattleAsync(Guid battleId, bool surrender = false)
        {
            var battle = await _unitOfWork.Battles.GetByIdAsync(battleId);
            if (battle == null)
                throw new KeyNotFoundException("Battle not found");

            if (battle.State == BattleState.Finished)
                throw new InvalidOperationException("Battle already finished");

            var result = surrender ? BattleResult.Lose : BattleResult.Win;

            battle.State = BattleState.Finished;
            battle.FinishedAt = DateTime.UtcNow;
            battle.Result = result;
            battle.IsSurrender = surrender;

            if (!surrender)
            {
                // Calculate rewards for win
                var rewards = CalculateBattleRewards(battle);
                battle.Rewards = rewards;
                await GrantBattleRewardsAsync(battle.UserId, rewards);
            }

            await _unitOfWork.Battles.UpdateAsync(battle);
            await _unitOfWork.SaveChangesAsync();

            return new BattleEndResult
            {
                Result = result,
                Rewards = battle.Rewards?.Select(MapToRewardDto).ToList() ?? new List<RewardDto>(),
                ExperienceGained = battle.Rewards?.Sum(r => r.Type == RewardType.Experience ? r.Quantity : 0) ?? 0,
                BattleDuration = (long)(battle.FinishedAt.Value - battle.StartedAt).TotalSeconds
            };
        }

        public async Task<BattleStateDto> GetBattleStateAsync(Guid battleId)
        {
            var battle = await _unitOfWork.Battles.GetByIdAsync(battleId);
            if (battle == null) return null;

            return MapToStateDto(battle);
        }

        public async Task<IReadOnlyList<BattleReplayDto>> GetBattleReplaysAsync(Guid userId, int limit = 10)
        {
            var battles = await _unitOfWork.Battles.GetRecentByUserAsync(userId, limit);
            return battles.Where(b => b.State == BattleState.Finished)
                .Select(MapToReplayDto)
                .ToList();
        }

        // Helper methods
        private async Task<Stage> GetStageAsync(GameMode mode, int stageId)
        {
            return mode switch
            {
                GameMode.Campaign => await _unitOfWork.CampaignStages.GetByIdAsync(stageId),
                GameMode.Tower => await _unitOfWork.TowerFloors.GetByIdAsync(stageId),
                GameMode.Dungeon => await _unitOfWork.DungeonStages.GetByIdAsync(stageId),
                GameMode.Event => await _unitOfWork.EventStages.GetByIdAsync(stageId),
                _ => null
            };
        }

        private int GetEnergyCost(GameMode mode, Stage stage)
        {
            return mode switch
            {
                GameMode.Campaign => stage.EnergyCost,
                GameMode.Tower => 0, // Tower uses tickets
                GameMode.Dungeon => stage.EnergyCost,
                GameMode.Event => stage.EnergyCost,
                GameMode.Arena => 1, // Arena tickets
                GameMode.WorldBoss => 1, // World boss tickets
                _ => 0
            };
        }

        private async Task<bool> CheckEnergyAsync(Guid userId, int cost) => true;
        private async Task ConsumeEnergyAsync(Guid userId, int cost) { }

        private List<SkillData> GetCharacterSkills(UserCharacter c) => new();
        private long CalculateMaxHP(UserCharacter c) => 10000;

        private async Task GrantBattleRewardsAsync(Guid userId, List<Reward> rewards)
        {
            foreach (var reward in rewards ?? new List<Reward>())
            {
                switch (reward.Type)
                {
                    case RewardType.Currency:
                        await AddCurrencyAsync(userId, reward.ResourceId, reward.Quantity);
                        break;
                    case RewardType.Item:
                        await AddItemAsync(userId, reward.ResourceId, reward.Quantity);
                        break;
                    case RewardType.Experience:
                        await AddExperienceAsync(userId, reward.Quantity);
                        break;
                    case RewardType.Character:
                        await _unitOfWork.UserCharacters.AddOrUpdateAsync(new UserCharacter(userId, reward.ResourceId, DateTime.UtcNow));
                        break;
                }
            }
        }

        private async Task UpdateProgressAsync(Battle battle)
        {
            switch (battle.Mode)
            {
                case GameMode.Campaign:
                    await _unitOfWork.CampaignProgress.UpdateStageClearAsync(battle.UserId, battle.StageId, battle.Result == BattleResult.Win);
                    break;
                case GameMode.Tower:
                    await _unitOfWork.TowerProgress.UpdateFloorClearAsync(battle.UserId, battle.StageId, battle.Result == BattleResult.Win);
                    break;
                case GameMode.Dungeon:
                    await _unitOfWork.DungeonProgress.UpdateClearAsync(battle.UserId, battle.StageId, battle.Result == BattleResult.Win);
                    break;
            }
        }

        private List<Reward> CalculateBattleRewards(Battle battle) => new();

        private async Task<IReadOnlyList<UserCharacterDto>> GetUserCharactersAsync(Guid userId, List<Guid> characterIds)
        {
            var result = new List<UserCharacterDto>();
            foreach (var id in characterIds)
            {
                var uc = await _unitOfWork.UserCharacters.GetByIdAsync(id);
                if (uc != null)
                {
                    var c = await _unitOfWork.Characters.GetByIdAsync(uc.CharacterId);
                    result.Add(MapToUserCharacterDto(uc, c));
                }
            }
            return result;
        }

        // Mapping methods
        private BattleStateDto MapToStateDto(Battle battle) => new BattleStateDto
        {
            BattleId = battle.Id,
            State = battle.State,
            CurrentTurn = battle.CurrentTurn,
            Speed = battle.Speed,
            AutoMode = battle.AutoMode,
            PlayerUnits = battle.PlayerParty.Select(MapToUnitDto).ToList(),
            EnemyUnits = battle.EnemyParty.Select(MapToUnitDto).ToList(),
            TurnOrder = battle.TurnOrder,
            FieldEffects = battle.FieldEffects.Select(MapToEffectDto).ToList()
        };

        private BattleStateDto MapToStateDto(BattleSimulatorState state) => new BattleStateDto
        {
            BattleId = state.BattleId,
            State = state.State,
            CurrentTurn = state.CurrentTurn,
            Speed = state.Speed,
            AutoMode = state.AutoMode,
            PlayerUnits = state.PlayerUnits.Select(MapToUnitDto).ToList(),
            EnemyUnits = state.EnemyUnits.Select(MapToUnitDto).ToList(),
            TurnOrder = state.TurnOrder,
            FieldEffects = state.FieldEffects.Select(MapToEffectDto).ToList()
        };

        private BattleUnitDto MapToUnitDto(BattleUnit u) => new BattleUnitDto
        {
            Id = u.Id,
            UserCharacterId = u.UserCharacterId,
            EnemyId = u.EnemyId,
            CharacterId = u.CharacterId,
            CurrentHP = u.CurrentHP,
            MaxHP = u.MaxHP,
            CurrentMP = u.CurrentMP,
            MaxMP = u.MaxMP,
            Buffs = u.Buffs.Select(MapToBuffDto).ToList(),
            Debuffs = u.Debuffs.Select(MapToDebuffDto).ToList(),
            Position = u.Position,
            IsPlayerUnit = u.UserCharacterId.HasValue
        };

        private BuffDto MapToBuffDto(Buff b) => new BuffDto { Id = b.Id, Type = b.Type, RemainingTurns = b.RemainingTurns, Value = b.Value };
        private DebuffDto MapToDebuffDto(Debuff d) => new DebuffDto { Id = d.Id, Type = d.Type, RemainingTurns = d.RemainingTurns, Value = d.Value };
        private FieldEffectDto MapToEffectDto(FieldEffect e) => new FieldEffectDto { Type = e.Type, RemainingTurns = e.RemainingTurns, Value = e.Value };

        private StageInfoDto MapToStageInfo(Stage s) => new StageInfoDto { Id = s.Id, Name = s.Name, Enemies = s.Enemies.Select(e => new EnemyInfoDto { Id = e.Id, Name = e.Name, Level = e.Level, Element = e.Element }).ToList() };

        private BattleResultDto MapToBattleResult(BattleResult r) => new BattleResultDto { Result = r, StarsEarned = r == BattleResult.Win ? 3 : 0, TurnsTaken = 0 };
        private RewardDto MapToRewardDto(Reward r) => new RewardDto { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity };
        private BattleReplayDto MapToReplayDto(Battle b) => new BattleReplayDto { BattleId = b.Id, Mode = b.Mode, StageId = b.StageId, Result = b.Result.Value, Duration = (long)(b.FinishedAt.Value - b.StartedAt).TotalSeconds, Timestamp = b.StartedAt };
        private UserCharacterDto MapToUserCharacterDto(UserCharacter uc, Character c) => new UserCharacterDto { Id = uc.Id, CharacterId = uc.CharacterId, CharacterName = c?.DisplayName ?? "", Level = uc.Level };

        // Placeholders
        private async Task AddCurrencyAsync(Guid userId, Guid currencyId, int amount) { }
        private async Task AddItemAsync(Guid userId, Guid itemId, int quantity) { }
        private async Task AddExperienceAsync(Guid userId, int amount) { }
    }
}