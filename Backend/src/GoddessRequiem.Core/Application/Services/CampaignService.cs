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
    public class CampaignService : ICampaignService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CampaignService> _logger;
        private readonly IBattleService _battleService;

        public CampaignService(IUnitOfWork unitOfWork, ILogger<CampaignService> logger, IBattleService battleService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _battleService = battleService;
        }

        public async Task<CampaignProgressDto> GetProgressAsync(Guid userId)
        {
            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            if (progress == null)
            {
                progress = new CampaignProgress { UserId = userId, CurrentChapter = 1, CurrentStage = 1 };
                await _unitOfWork.CampaignProgress.AddAsync(progress);
                await _unitOfWork.SaveChangesAsync();
            }

            var clearedStages = await _unitOfWork.CampaignProgress.GetClearedStagesAsync(userId);
            var starredStages = await _unitOfWork.CampaignProgress.GetStarredStagesAsync(userId);
            var claimedChapters = await _unitOfWork.CampaignProgress.GetClaimedChapterRewardsAsync(userId);

            return new CampaignProgressDto
            {
                CurrentChapter = progress.CurrentChapter,
                CurrentStage = progress.CurrentStage,
                ClearedStages = clearedStages,
                StarredStages = starredStages,
                ClaimedChapterRewards = claimedChapters,
                TotalStars = starredStages.Sum(s => s.Stars),
                AutoProgressEnabled = progress.AutoProgressEnabled,
                AutoProgressConfig = progress.AutoProgressConfig
            };
        }

        public async Task<StageDetailDto> GetStageDetailAsync(Guid userId, int stageId)
        {
            var stage = await _unitOfWork.CampaignStages.GetByIdAsync(stageId);
            if (stage == null) return null;

            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            var isUnlocked = IsStageUnlocked(progress, stage);
            var clearData = await _unitOfWork.CampaignProgress.GetStageClearDataAsync(userId, stageId);

            return new StageDetailDto
            {
                Id = stage.Id,
                Chapter = stage.Chapter,
                StageNumber = stage.StageNumber,
                Name = stage.Name,
                Description = stage.Description,
                Difficulty = stage.Difficulty,
                EnergyCost = stage.EnergyCost,
                RecommendedPower = stage.RecommendedPower,
                Enemies = stage.Enemies.Select(MapToEnemyInfo).ToList(),
                Rewards = stage.Rewards.Select(MapToRewardPreview).ToList(),
                FirstClearRewards = stage.FirstClearRewards.Select(MapToRewardPreview).ToList(),
                StarConditions = stage.StarConditions.Select(MapToStarCondition).ToList(),
                IsUnlocked = isUnlocked,
                IsCleared = clearData != null,
                BestStars = clearData?.Stars ?? 0,
                BestTurns = clearData?.TurnsTaken,
                ClearCount = clearData?.ClearCount ?? 0
            };
        }

        public async Task<StageClearResult> ClearStageAsync(Guid userId, int stageId, BattleResultData result)
        {
            var stage = await _unitOfWork.CampaignStages.GetByIdAsync(stageId);
            if (stage == null)
                throw new KeyNotFoundException("Stage not found");

            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Campaign progress not found");

            if (!IsStageUnlocked(progress, stage))
                throw new InvalidOperationException("Stage not unlocked");

            var existingClear = await _unitOfWork.CampaignProgress.GetStageClearDataAsync(userId, stageId);
            var isFirstClear = existingClear == null;
            var starsEarned = CalculateStars(result, stage.StarConditions);

            // Update stage clear data
            var clearData = existingClear ?? new CampaignStageClear { UserId = userId, StageId = stageId };
            clearData.UpdateClear(result, starsEarned, isFirstClear);

            if (existingClear == null)
                await _unitOfWork.CampaignProgress.AddStageClearAsync(clearData);
            else
                await _unitOfWork.CampaignProgress.UpdateStageClearAsync(clearData);

            // Update progress
            if (isFirstClear)
            {
                var nextStage = await _unitOfWork.CampaignStages.GetNextStageAsync(stage.Chapter, stage.StageNumber);
                if (nextStage != null)
                {
                    progress.CurrentChapter = nextStage.Chapter;
                    progress.CurrentStage = nextStage.StageNumber;
                }
                else
                {
                    // Check for next chapter
                    var nextChapter = await _unitOfWork.CampaignStages.GetFirstStageOfChapterAsync(stage.Chapter + 1);
                    if (nextChapter != null)
                    {
                        progress.CurrentChapter = nextChapter.Chapter;
                        progress.CurrentStage = nextChapter.StageNumber;
                    }
                }

                await _unitOfWork.CampaignProgress.UpdateAsync(progress);
            }

            // Grant rewards
            var rewards = new List<Reward>();

            if (isFirstClear)
            {
                rewards.AddRange(stage.FirstClearRewards);
            }

            rewards.AddRange(stage.Rewards);

            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            // Check for auto-progress
            var autoProgressResult = new AutoProgressResult();
            if (progress.AutoProgressEnabled && progress.AutoProgressConfig != null)
            {
                autoProgressResult = await ProcessAutoProgressAsync(userId, progress);
            }

            return new StageClearResult
            {
                Success = true,
                StarsEarned = starsEarned,
                IsFirstClear = isFirstClear,
                Rewards = rewards.Select(MapToRewardDto).ToList(),
                NextStageUnlocked = isFirstClear ? progress.CurrentStage : (int?)null,
                AutoProgressResult = autoProgressResult
            };
        }

        public async Task<ChapterRewardResult> ClaimChapterRewardAsync(Guid userId, int chapter)
        {
            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Campaign progress not found");

            if (progress.ClaimedChapterRewards.Contains(chapter))
                throw new InvalidOperationException("Chapter reward already claimed");

            var chapterData = await _unitOfWork.CampaignChapters.GetByIdAsync(chapter);
            if (chapterData == null)
                throw new KeyNotFoundException("Chapter not found");

            // Check if all stages in chapter are cleared with 3 stars
            var chapterStages = await _unitOfWork.CampaignStages.GetByChapterAsync(chapter);
            var clearData = await _unitOfWork.CampaignProgress.GetChapterClearDataAsync(userId, chapter);

            if (clearData.ClearedStages < chapterStages.Count || clearData.TotalStars < chapterStages.Count * 3)
                throw new InvalidOperationException("Chapter not fully cleared with 3 stars");

            progress.ClaimedChapterRewards.Add(chapter);
            await _unitOfWork.CampaignProgress.UpdateAsync(progress);

            var rewards = chapterData.Rewards;
            await GrantRewardsAsync(userId, rewards);

            await _unitOfWork.SaveChangesAsync();

            return new ChapterRewardResult
            {
                Success = true,
                Chapter = chapter,
                Rewards = rewards.Select(MapToRewardDto).ToList()
            };
        }

        public async Task<AutoProgressResult> StartAutoProgressAsync(Guid userId, AutoProgressConfig config)
        {
            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            if (progress == null)
                throw new KeyNotFoundException("Campaign progress not found");

            // Validate config
            if (config.MaxStagesPerSession < 1 || config.MaxStagesPerSession > 50)
                throw new ArgumentException("Max stages per session must be 1-50");

            if (config.MinPartyPower < 0)
                throw new ArgumentException("Min party power cannot be negative");

            progress.AutoProgressEnabled = true;
            progress.AutoProgressConfig = config;
            await _unitOfWork.CampaignProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();

            // Start first auto battle
            var result = await ProcessAutoProgressAsync(userId, progress);

            return result;
        }

        public async Task StopAutoProgressAsync(Guid userId)
        {
            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            if (progress == null) return;

            progress.AutoProgressEnabled = false;
            await _unitOfWork.CampaignProgress.UpdateAsync(progress);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<AutoProgressStatus> GetAutoProgressStatusAsync(Guid userId)
        {
            var progress = await _unitOfWork.CampaignProgress.GetByUserIdAsync(userId);
            if (progress == null || !progress.AutoProgressEnabled)
                return new AutoProgressStatus { IsRunning = false };

            return new AutoProgressStatus
            {
                IsRunning = true,
                CurrentStage = progress.CurrentStage,
                StagesClearedThisSession = progress.AutoProgressStagesCleared,
                MaxStagesPerSession = progress.AutoProgressConfig?.MaxStagesPerSession ?? 10,
                LastBattleResult = progress.LastAutoBattleResult,
                NextBattleIn = progress.AutoProgressConfig?.BattleInterval ?? TimeSpan.FromSeconds(5)
            };
        }

        private bool IsStageUnlocked(CampaignProgress progress, CampaignStage stage)
        {
            if (stage.Chapter < progress.CurrentChapter) return true;
            if (stage.Chapter > progress.CurrentChapter) return false;
            return stage.StageNumber <= progress.CurrentStage;
        }

        private int CalculateStars(BattleResultData result, List<StarCondition> conditions)
        {
            int stars = 0;
            foreach (var condition in conditions)
            {
                if (CheckCondition(result, condition))
                    stars++;
            }
            return Math.Min(stars, 3);
        }

        private bool CheckCondition(BattleResultData result, StarCondition condition)
        {
            return condition.Type switch
            {
                StarConditionType.Win => result.Result == BattleResult.Win,
                StarConditionType.NoDeaths => result.PlayerDeaths == 0,
                StarConditionType.TurnLimit => result.TurnsTaken <= condition.Value,
                StarConditionType.TimeLimit => result.DurationSeconds <= condition.Value,
                StarConditionType.SpecificCharacter => result.UsedCharacterIds.Contains(condition.CharacterId.Value),
                StarConditionType.ElementAdvantage => result.ElementAdvantageCount >= condition.Value,
                _ => false
            };
        }

        private async Task<AutoProgressResult> ProcessAutoProgressAsync(Guid userId, CampaignProgress progress)
        {
            var config = progress.AutoProgressConfig;
            if (config == null) return new AutoProgressResult { Success = false, Error = "No config" };

            var stagesCleared = 0;
            var totalRewards = new List<Reward>();

            for (int i = 0; i < config.MaxStagesPerSession; i++)
            {
                var stage = await _unitOfWork.CampaignStages.GetByIdAsync(progress.CurrentChapter, progress.CurrentStage);
                if (stage == null) break;

                // Check if already cleared with 3 stars
                var clearData = await _unitOfWork.CampaignProgress.GetStageClearDataAsync(userId, stage.Id);
                if (clearData?.Stars == 3) break;

                // Check party power
                var partyPower = await GetPartyPowerAsync(userId, config.PartyCharacterIds);
                if (partyPower < config.MinPartyPower) break;

                // Check energy
                var hasEnergy = await CheckEnergyAsync(userId, stage.EnergyCost);
                if (!hasEnergy) break;

                // Start battle
                var battleResult = await _battleService.StartBattleAsync(userId, GameMode.Campaign, stage.Id,
                    config.PartyCharacterIds, config.BattleSpeed, AutoBattleMode.FullAuto);

                // Simulate auto battle to completion
                var finalResult = await SimulateAutoBattleAsync(battleResult.BattleId);

                // Process clear
                var clearResult = await ClearStageAsync(userId, stage.Id, finalResult);

                stagesCleared++;
                totalRewards.AddRange(clearResult.Rewards.Select(r => new Reward { Type = r.Type, ResourceId = r.ResourceId, Quantity = r.Quantity }));

                progress.LastAutoBattleResult = MapToAutoBattleResult(finalResult);
                progress.AutoProgressStagesCleared = stagesCleared;
                await _unitOfWork.CampaignProgress.UpdateAsync(progress);

                if (finalResult.Result == BattleResult.Lose)
                {
                    // Stop on loss if configured
                    if (config.StopOnLoss)
                    {
                        progress.AutoProgressEnabled = false;
                        await _unitOfWork.CampaignProgress.UpdateAsync(progress);
                        break;
                    }
                }

                await _unitOfWork.SaveChangesAsync();
            }

            return new AutoProgressResult
            {
                Success = true,
                StagesCleared = stagesCleared,
                TotalRewards = totalRewards.Select(MapToRewardDto).ToList(),
                StoppedReason = stagesCleared < config.MaxStagesPerSession ? "Completed or stopped" : "Max stages reached"
            };
        }

        private async Task<BattleResultData> SimulateAutoBattleAsync(Guid battleId)
        {
            // In real implementation, this would run the battle simulation
            // For now, return a mock win result
            return new BattleResultData
            {
                Result = BattleResult.Win,
                TurnsTaken = 5,
                DurationSeconds = 30,
                PlayerDeaths = 0,
                UsedCharacterIds = new List<Guid>(),
                ElementAdvantageCount = 2
            };
        }

        private async Task<long> GetPartyPowerAsync(Guid userId, List<Guid> characterIds)
        {
            long total = 0;
            foreach (var id in characterIds)
            {
                var uc = await _unitOfWork.UserCharacters.GetByIdAsync(id);
                if (uc != null)
                {
                    total += uc.CalculatePower();
                }
            }
            return total;
        }

        private async Task<bool> CheckEnergyAsync(Guid userId, int cost) => true;
        private async Task GrantRewardsAsync(Guid userId, List<Reward> rewards) { }

        private EnemyInfoDto MapToEnemyInfo(CampaignStageEnemy e) => new EnemyInfoDto
        {
            Id = e.EnemyId,
            Name = e.Name,
            Level = e.Level,
            Element = e.Element,
            HP = e.HP,
            Attack = e.Attack,
            Skills = e.Skills.Select(s => s.ToString()).ToList()
        };

        private RewardPreviewDto MapToRewardPreview(Reward r) => new RewardPreviewDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity,
            Probability = r.Probability,
            IsGuaranteed = r.IsGuaranteed
        };

        private StarConditionDto MapToStarCondition(StarCondition c) => new StarConditionDto
        {
            Type = c.Type,
            Description = c.Description,
            Value = c.Value,
            CharacterId = c.CharacterId
        };

        private RewardDto MapToRewardDto(RewardPreviewDto r) => new RewardDto
        {
            Type = r.Type,
            ResourceId = r.ResourceId,
            Quantity = r.Quantity
        };

        private AutoBattleResultDto MapToAutoBattleResult(BattleResultData r) => new AutoBattleResultDto
        {
            Result = r.Result,
            TurnsTaken = r.TurnsTaken,
            DurationSeconds = r.DurationSeconds
        };
    }
}