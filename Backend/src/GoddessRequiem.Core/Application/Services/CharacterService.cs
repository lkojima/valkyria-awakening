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
    public class CharacterService : ICharacterService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CharacterService> _logger;

        public CharacterService(IUnitOfWork unitOfWork, ILogger<CharacterService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<CharacterDetailDto> GetCharacterAsync(Guid characterId)
        {
            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null) return null;

            return MapToDetailDto(character);
        }

        public async Task<IReadOnlyList<CharacterSummaryDto>> GetAllCharactersAsync(Rarity? rarity = null, Element? element = null)
        {
            IEnumerable<Character> characters;

            if (rarity.HasValue)
                characters = await _unitOfWork.Characters.GetByRarityAsync(rarity.Value);
            else if (element.HasValue)
                characters = await _unitOfWork.Characters.GetByElementAsync(element.Value);
            else
                characters = await _unitOfWork.Characters.GetObtainableAsync();

            if (rarity.HasValue && element.HasValue)
            {
                characters = characters.Where(c => c.Rarity == rarity.Value && c.Element == element.Value);
            }

            return characters.Select(MapToSummaryDto).ToList();
        }

        public async Task<UserCharacterDto> GetUserCharacterAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null) return null;

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            return MapToUserCharacterDto(userChar, character);
        }

        public async Task<IReadOnlyList<UserCharacterDto>> GetUserCharactersAsync(Guid userId)
        {
            var userChars = await _unitOfWork.UserCharacters.GetByUserIdAsync(userId);
            var characterIds = userChars.Select(c => c.CharacterId).ToList();
            var characters = new Dictionary<Guid, Character>();

            foreach (var id in characterIds)
            {
                var c = await _unitOfWork.Characters.GetByIdAsync(id);
                if (c != null) characters[id] = c;
            }

            return userChars.Select(uc => MapToUserCharacterDto(uc, characters.GetValueOrDefault(uc.CharacterId))).ToList();
        }

        public async Task<UserCharacterDto> AcquireCharacterAsync(Guid userId, Guid characterId, AcquisitionSource source)
        {
            var existing = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);

            if (character == null)
                throw new KeyNotFoundException("Character not found");

            if (existing != null)
            {
                // Convert to fragments
                var fragmentCount = GetDuplicateFragmentCount(character.Rarity);
                await AddCharacterFragmentsAsync(userId, characterId, fragmentCount, source);

                existing.AddAffinityExperience(50, character.MaxAffinityLevel, character.AffinityRewards,
                    async (reward) => await GrantAffinityRewardAsync(userId, reward));

                await _unitOfWork.UserCharacters.UpdateAsync(existing);
                await _unitOfWork.SaveChangesAsync();

                return MapToUserCharacterDto(existing, character);
            }

            var userCharacter = new UserCharacter(userId, characterId, DateTime.UtcNow);
            await _unitOfWork.UserCharacters.AddAsync(userCharacter);
            await _unitOfWork.SaveChangesAsync();

            // Grant acquisition rewards
            await GrantAcquisitionRewardsAsync(userId, character, source);

            return MapToUserCharacterDto(userCharacter, character);
        }

        public async Task<LevelUpResult> LevelUpCharacterAsync(Guid userId, Guid characterId, int targetLevel, CurrencyAmount cost)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null)
                throw new KeyNotFoundException("Character not owned");

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null)
                throw new KeyNotFoundException("Character not found");

            if (targetLevel <= userChar.Level || targetLevel > character.MaxLevel)
                throw new ArgumentException("Invalid target level");

            // Check currency
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient currency");

            // Spend currency
            await SpendCurrencyAsync(userId, cost);

            // Calculate experience needed
            var totalExpNeeded = CalculateExpToLevel(character, userChar.Level, targetLevel);
            userChar.AddExperience(totalExpNeeded, character.MaxLevel, out int levelsGained);

            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            return new LevelUpResult
            {
                NewLevel = userChar.Level,
                LevelsGained = levelsGained,
                RemainingExp = userChar.Experience,
                GoldSpent = cost.Amount,
                NewStats = await CalculateStatsAsync(userId, characterId)
            };
        }

        public async Task<AscensionResult> AscendCharacterAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null)
                throw new KeyNotFoundException("Character not owned");

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null)
                throw new KeyNotFoundException("Character not found");

            if (userChar.AscensionLevel >= character.MaxAscension)
                throw new InvalidOperationException("Already at max ascension");

            if (userChar.Level < GetRequiredLevelForAscension(userChar.AscensionLevel + 1))
                throw new InvalidOperationException("Insufficient level for ascension");

            // Check materials
            var nextAscensionMaterials = character.EvolutionMaterials
                .Where(m => m.RequiredAscensionLevel == userChar.AscensionLevel + 1)
                .ToList();

            foreach (var material in nextAscensionMaterials)
            {
                var owned = await GetUserMaterialCountAsync(userId, material.MaterialId);
                if (owned < material.Quantity)
                    throw new InvalidOperationException($"Insufficient {material.MaterialName}");
            }

            // Consume materials
            foreach (var material in nextAscensionMaterials)
            {
                await ConsumeMaterialAsync(userId, material.MaterialId, material.Quantity);
            }

            userChar.TryAscend(character.EvolutionMaterials, GetUserMaterialCountAsync, ConsumeMaterialAsync);

            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            // Unlock new skill/stat bonuses
            var bonuses = GetAscensionBonuses(character, userChar.AscensionLevel);

            return new AscensionResult
            {
                NewAscensionLevel = userChar.AscensionLevel,
                Bonuses = bonuses,
                NewStats = await CalculateStatsAsync(userId, characterId)
            };
        }

        public async Task<SkillUpgradeResult> UpgradeSkillAsync(Guid userId, Guid characterId, SkillType skillType)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null)
                throw new KeyNotFoundException("Character not owned");

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null)
                throw new KeyNotFoundException("Character not found");

            var currentLevel = userChar.GetSkillLevel(skillType);
            var maxLevel = character.MaxLevel / 10; // Simplified

            if (currentLevel >= maxLevel)
                throw new InvalidOperationException("Skill already at max level");

            var cost = CalculateSkillUpgradeCost(currentLevel + 1, character.Rarity);
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient currency");

            await SpendCurrencyAsync(userId, cost);

            userChar.UpgradeSkill(skillType, maxLevel, cost, SpendCurrencyAsync);

            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            return new SkillUpgradeResult
            {
                SkillType = skillType,
                NewLevel = currentLevel + 1,
                NewMultiplier = GetSkillMultiplier(skillType, currentLevel + 1),
                GoldSpent = cost.Amount
            };
        }

        public async Task<LimitBreakResult> LimitBreakAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null)
                throw new KeyNotFoundException("Character not owned");

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null)
                throw new KeyNotFoundException("Character not found");

            if (userChar.LimitBreakCount >= 5) // Max 5 limit breaks
                throw new InvalidOperationException("Max limit breaks reached");

            // Need duplicate character or universal fragments
            var fragmentCost = GetLimitBreakFragmentCost(userChar.LimitBreakCount + 1, character.Rarity);
            var owned = await GetUserCharacterFragmentsAsync(userId, characterId);

            if (owned < fragmentCost)
                throw new InvalidOperationException("Insufficient character fragments");

            await ConsumeCharacterFragmentsAsync(userId, characterId, fragmentCost);

            userChar.LimitBreak();

            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            return new LimitBreakResult
            {
                NewLimitBreakCount = userChar.LimitBreakCount,
                StatBonus = GetLimitBreakBonus(character.Rarity, userChar.LimitBreakCount),
                FragmentsUsed = fragmentCost
            };
        }

        public async Task<bool> ToggleFavoriteAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null) return false;

            userChar.ToggleFavorite();
            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            return userChar.IsFavorite;
        }

        public async Task<bool> ToggleLockAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null) return false;

            userChar.ToggleLock();
            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            return userChar.IsLocked;
        }

        public async Task<CharacterStatsDto> CalculateStatsAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null) return null;

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null) return null;

            // Get equipped items
            var equipment = await GetEquippedEquipmentAsync(userId, userChar.Id);
            var relic = await GetEquippedRelicAsync(userId, userChar.Id);
            var runes = await GetEquippedRunesAsync(userId, userChar.Id);

            var stats = character.CalculateStats(
                userChar.Level,
                userChar.AscensionLevel,
                userChar.AffinityLevel,
                equipment,
                relic,
                runes
            );

            return MapToStatsDto(stats);
        }

        public async Task<AffinityResult> AddAffinityExpAsync(Guid userId, Guid characterId, long amount)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByUserAndCharacterAsync(userId, characterId);
            if (userChar == null) return null;

            var character = await _unitOfWork.Characters.GetByIdAsync(characterId);
            if (character == null) return null;

            var oldLevel = userChar.AffinityLevel;
            userChar.AddAffinityExperience(amount, character.MaxAffinityLevel, character.AffinityRewards,
                async (reward) => await GrantAffinityRewardAsync(userId, reward));

            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            var rewards = new List<AffinityRewardDto>();
            for (int level = oldLevel + 1; level <= userChar.AffinityLevel; level++)
            {
                var reward = character.AffinityRewards.FirstOrDefault(r => r.AffinityLevel == level);
                if (reward != null)
                {
                    rewards.Add(new AffinityRewardDto
                    {
                        AffinityLevel = level,
                        RewardType = reward.RewardType,
                        ResourceId = reward.RewardId,
                        Quantity = reward.Quantity
                    });
                }
            }

            return new AffinityResult
            {
                NewAffinityLevel = userChar.AffinityLevel,
                RemainingExp = userChar.AffinityExperience,
                Rewards = rewards
            };
        }

        // Helper methods
        private CharacterDetailDto MapToDetailDto(Character c) => new CharacterDetailDto
        {
            Id = c.Id,
            Name = c.Name,
            DisplayName = c.DisplayName,
            Description = c.Description,
            Lore = c.Lore,
            Rarity = c.Rarity,
            Element = c.Element,
            Class = c.Class,
            Role = c.Role,
            BaseHP = c.BaseHP,
            BaseAttack = c.BaseAttack,
            BaseDefense = c.BaseDefense,
            BaseSpeed = c.BaseSpeed,
            BaseCritRate = c.BaseCritRate,
            BaseCritDamage = c.BaseCritDamage,
            BaseResistance = c.BaseResistance,
            BaseAccuracy = c.BaseAccuracy,
            MaxLevel = c.MaxLevel,
            MaxAscension = c.MaxAscension,
            PortraitAssetPath = c.PortraitAssetPath,
            FullBodyAssetPath = c.FullBodyAssetPath,
            Skills = new List<SkillSummaryDto>() // Would load skills
        };

        private CharacterSummaryDto MapToSummaryDto(Character c) => new CharacterSummaryDto
        {
            Id = c.Id,
            Name = c.Name,
            DisplayName = c.DisplayName,
            Rarity = c.Rarity,
            Element = c.Element,
            Class = c.Class,
            Role = c.Role,
            PortraitAssetPath = c.PortraitAssetPath
        };

        private UserCharacterDto MapToUserCharacterDto(UserCharacter uc, Character c) => new UserCharacterDto
        {
            Id = uc.Id,
            CharacterId = uc.CharacterId,
            CharacterName = c?.DisplayName ?? "",
            CharacterRarity = c?.Rarity ?? Rarity.R,
            CharacterElement = c?.Element ?? Element.None,
            CharacterClass = c?.Class ?? CharacterClass.Warrior,
            Level = uc.Level,
            Experience = uc.Experience,
            AscensionLevel = uc.AscensionLevel,
            AffinityLevel = uc.AffinityLevel,
            AffinityExperience = uc.AffinityExperience,
            Skill1Level = uc.Skill1Level,
            Skill2Level = uc.Skill2Level,
            Skill3Level = uc.Skill3Level,
            UltimateLevel = uc.UltimateLevel,
            IsFavorite = uc.IsFavorite,
            IsLocked = uc.IsLocked,
            AcquiredAt = uc.AcquiredAt,
            LimitBreakCount = uc.LimitBreakCount
        };

        private CharacterStatsDto MapToStatsDto(CharacterStats stats) => new CharacterStatsDto
        {
            HP = stats.HP,
            Attack = stats.Attack,
            Defense = stats.Defense,
            Speed = stats.Speed,
            CritRate = stats.CritRate,
            CritDamage = stats.CritDamage,
            Resistance = stats.Resistance,
            Accuracy = stats.Accuracy
        };

        private int GetDuplicateFragmentCount(Rarity rarity) => rarity switch
        {
            Rarity.R => 10,
            Rarity.SR => 20,
            Rarity.SSR => 50,
            Rarity.UR => 100,
            _ => 10
        };

        private int GetRequiredLevelForAscension(int ascension) => ascension switch
        {
            1 => 20,
            2 => 40,
            3 => 50,
            4 => 60,
            5 => 70,
            6 => 80,
            _ => 20
        };

        private long CalculateExpToLevel(Character character, int fromLevel, int toLevel)
        {
            long total = 0;
            for (int level = fromLevel; level < toLevel; level++)
            {
                total += (long)(100 * Math.Pow(level, 1.5));
            }
            return total;
        }

        private CurrencyAmount CalculateSkillUpgradeCost(int targetLevel, Rarity rarity)
        {
            var baseCost = rarity switch
            {
                Rarity.R => 1000,
                Rarity.SR => 2500,
                Rarity.SSR => 5000,
                Rarity.UR => 10000,
                _ => 1000
            };

            var cost = (long)(baseCost * Math.Pow(targetLevel, 1.5));
            return new CurrencyAmount(CurrencyType.Gold, cost);
        }

        private float GetSkillMultiplier(SkillType type, int level) => 1f + (level - 1) * 0.1f;

        private int GetLimitBreakFragmentCost(int lbLevel, Rarity rarity) => rarity switch
        {
            Rarity.R => 20 * lbLevel,
            Rarity.SR => 40 * lbLevel,
            Rarity.SSR => 100 * lbLevel,
            Rarity.UR => 200 * lbLevel,
            _ => 20 * lbLevel
        };

        private float GetLimitBreakBonus(Rarity rarity, int lbLevel) => 0.02f * lbLevel; // 2% per LB

        private List<AscensionBonusDto> GetAscensionBonuses(Character character, int ascensionLevel) => new();

        // Placeholder implementations for dependencies
        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task AddCharacterFragmentsAsync(Guid userId, Guid characterId, int count, AcquisitionSource source) { }
        private async Task GrantAcquisitionRewardsAsync(Guid userId, Character character, AcquisitionSource source) { }
        private async Task GrantAffinityRewardAsync(Guid userId, AffinityReward reward) { }
        private async Task<int> GetUserMaterialCountAsync(Guid userId, Guid materialId) => 999;
        private async Task ConsumeMaterialAsync(Guid userId, Guid materialId, int quantity) { }
        private async Task<int> GetUserCharacterFragmentsAsync(Guid userId, Guid characterId) => 999;
        private async Task ConsumeCharacterFragmentsAsync(Guid userId, Guid characterId, int quantity) { }
        private async Task<EquipmentSet> GetEquippedEquipmentAsync(Guid userId, Guid userCharacterId) => new EquipmentSet();
        private async Task<RelicSet> GetEquippedRelicAsync(Guid userId, Guid userCharacterId) => new RelicSet();
        private async Task<RuneSet> GetEquippedRunesAsync(Guid userId, Guid userCharacterId) => new RuneSet();
    }
}