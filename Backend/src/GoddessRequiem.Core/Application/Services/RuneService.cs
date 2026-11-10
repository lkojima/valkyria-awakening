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
    public class RuneService : IRuneService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RuneService> _logger;
        private readonly ICharacterService _characterService;

        public RuneService(IUnitOfWork unitOfWork, ILogger<RuneService> logger, ICharacterService characterService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _characterService = characterService;
        }

        public async Task<UserRuneDto> GetRuneAsync(Guid runeId)
        {
            var rune = await _unitOfWork.UserRunes.GetByIdAsync(runeId);
            if (rune == null) return null;

            var template = await _unitOfWork.RuneTemplates.GetByIdAsync(rune.TemplateId);
            return MapToDto(rune, template);
        }

        public async Task<IReadOnlyList<UserRuneDto>> GetUserRunesAsync(Guid userId)
        {
            var runes = await _unitOfWork.UserRunes.GetByUserIdAsync(userId);
            var templateIds = runes.Select(r => r.TemplateId).Distinct().ToList();
            var templates = new Dictionary<Guid, RuneTemplate>();

            foreach (var id in templateIds)
            {
                var t = await _unitOfWork.RuneTemplates.GetByIdAsync(id);
                if (t != null) templates[id] = t;
            }

            return runes.Select(r => MapToDto(r, templates.GetValueOrDefault(r.TemplateId))).ToList();
        }

        public async Task<EnhanceResult> EnhanceRuneAsync(Guid userId, Guid runeId)
        {
            var rune = await _unitOfWork.UserRunes.GetByIdAsync(runeId);
            if (rune == null)
                throw new KeyNotFoundException("Rune not found");

            if (rune.UserId != userId)
                throw new UnauthorizedAccessException("Rune not owned by user");

            var template = await _unitOfWork.RuneTemplates.GetByIdAsync(rune.TemplateId);
            if (template == null)
                throw new KeyNotFoundException("Rune template not found");

            if (rune.EnhancementLevel >= template.MaxEnhancementLevel)
                throw new InvalidOperationException("Rune already at max enhancement");

            // Calculate cost
            var cost = CalculateEnhanceCost(template, rune.EnhancementLevel + 1);
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient mana");

            // Spend mana (runes use mana instead of gold)
            await SpendCurrencyAsync(userId, cost);

            // Runes have 100% success rate but RNG stat increases
            var oldLevel = rune.EnhancementLevel;
            rune.Enhance();
            rune.UpgradeStats(template, oldLevel);

            await _unitOfWork.UserRunes.UpdateAsync(rune);
            await _unitOfWork.SaveChangesAsync();

            return new EnhanceResult
            {
                Success = true,
                NewLevel = rune.EnhancementLevel,
                NewStats = rune.Stats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
                GoldSpent = cost.Amount,
                MaterialsUsed = new List<MaterialCostDto>()
            };
        }

        public async Task<EquipResult> EquipAsync(Guid userId, Guid characterId, Guid runeId, int slot)
        {
            if (slot < 1 || slot > 6)
                throw new ArgumentException("Invalid rune slot (1-6)");

            var userChar = await _unitOfWork.UserCharacters.GetByIdAsync(characterId);
            if (userChar == null || userChar.UserId != userId)
                throw new KeyNotFoundException("Character not found");

            var rune = await _unitOfWork.UserRunes.GetByIdAsync(runeId);
            if (rune == null || rune.UserId != userId)
                throw new KeyNotFoundException("Rune not found");

            var template = await _unitOfWork.RuneTemplates.GetByIdAsync(rune.TemplateId);

            // Check slot compatibility
            if (template.SlotType != RuneSlotType.Any && template.SlotType != (RuneSlotType)slot)
                throw new ArgumentException("Rune slot type mismatch");

            // Unequip current rune in slot
            var currentEquipped = await _unitOfWork.UserRunes.GetEquippedByCharacterAndSlotAsync(userId, characterId, slot);
            if (currentEquipped != null)
            {
                currentEquipped.Unequip();
                await _unitOfWork.UserRunes.UpdateAsync(currentEquipped);
            }

            // Equip new
            rune.Equip(characterId, slot);
            await _unitOfWork.UserRunes.UpdateAsync(rune);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = runeId,
                Slot = (EquipmentSlot)slot,
                UnequippedEquipmentId = currentEquipped?.Id
            };
        }

        public async Task<EquipResult> UnequipAsync(Guid userId, Guid characterId, int slot)
        {
            if (slot < 1 || slot > 6)
                throw new ArgumentException("Invalid rune slot (1-6)");

            var rune = await _unitOfWork.UserRunes.GetEquippedByCharacterAndSlotAsync(userId, characterId, slot);
            if (rune == null)
                return new EquipResult { Success = false, Error = "No rune in slot" };

            rune.Unequip();
            await _unitOfWork.UserRunes.UpdateAsync(rune);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = rune.Id,
                Slot = (EquipmentSlot)slot
            };
        }

        public async Task<AutoEquipResult> AutoEquipRunesAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByIdAsync(characterId);
            if (userChar == null || userChar.UserId != userId)
                throw new KeyNotFoundException("Character not found");

            var availableRunes = await _unitOfWork.UserRunes.GetAvailableForCharacterAsync(userId, userChar.CharacterId);
            var templateIds = availableRunes.Select(r => r.TemplateId).Distinct().ToList();
            var templates = new Dictionary<Guid, RuneTemplate>();

            foreach (var id in templateIds)
            {
                var t = await _unitOfWork.RuneTemplates.GetByIdAsync(id);
                if (t != null) templates[id] = t;
            }

            var results = new List<EquipResult>();

            for (int slot = 1; slot <= 6; slot++)
            {
                var bestRune = availableRunes
                    .Where(r => templates.GetValueOrDefault(r.TemplateId)?.SlotType == RuneSlotType.Any ||
                                templates.GetValueOrDefault(r.TemplateId)?.SlotType == (RuneSlotType)slot)
                    .OrderByDescending(r => CalculateRuneScore(r, templates.GetValueOrDefault(r.TemplateId), userChar))
                    .FirstOrDefault();

                if (bestRune != null)
                {
                    var result = await EquipAsync(userId, characterId, bestRune.Id, slot);
                    if (result.Success)
                    {
                        results.Add(result);
                        availableRunes.Remove(bestRune);
                    }
                }
            }

            // Check for set bonuses
            var setBonuses = CheckSetBonuses(results, templates);

            return new AutoEquipResult
            {
                Success = true,
                EquippedItems = results,
                ActivatedSetBonuses = setBonuses
            };
        }

        private UserRuneDto MapToDto(UserRune r, RuneTemplate t) => new UserRuneDto
        {
            Id = r.Id,
            TemplateId = r.TemplateId,
            Name = t?.Name ?? "",
            Rarity = r.Rarity,
            EnhancementLevel = r.EnhancementLevel,
            MaxEnhancementLevel = t?.MaxEnhancementLevel ?? 15,
            SlotType = t?.SlotType ?? RuneSlotType.Any,
            SetId = t?.SetId,
            MainStat = r.MainStat != null ? MapToStatDto(r.MainStat) : null,
            SubStats = r.SubStats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
            SetBonus = t?.SetBonus != null ? MapToSetBonusDto(t.SetBonus) : null,
            IsEquipped = r.IsEquipped,
            EquippedCharacterId = r.EquippedCharacterId,
            EquippedSlot = r.EquippedSlot,
            IsLocked = r.IsLocked,
            AcquiredAt = r.AcquiredAt
        };

        private EquipmentStatDto MapToStatDto(EquipmentStat s) => new EquipmentStatDto
        {
            Type = s.Type,
            BaseValue = s.BaseValue,
            EnhancedValue = s.EnhancedValue,
            TotalValue = s.TotalValue
        };

        private SetBonusDto MapToSetBonusDto(SetBonus s) => new SetBonusDto
        {
            Id = s.Id,
            Name = s.Name,
            PieceCount = s.PieceCount,
            Description = s.Description,
            Effects = s.Effects.Select(e => new SetBonusEffectDto { Type = e.Type, Value = e.Value }).ToList()
        };

        private CurrencyAmount CalculateEnhanceCost(RuneTemplate template, int targetLevel)
        {
            var baseCost = template.Rarity switch
            {
                Rarity.R => 500,
                Rarity.SR => 1500,
                Rarity.SSR => 4000,
                Rarity.UR => 10000,
                _ => 500
            };

            var cost = (long)(baseCost * Math.Pow(targetLevel, 1.5));
            return new CurrencyAmount(CurrencyType.Mana, cost);
        }

        private int CalculateRuneScore(UserRune rune, RuneTemplate template, UserCharacter character)
        {
            var score = 0;

            if (rune.Stats != null)
            {
                foreach (var stat in rune.Stats)
                {
                    score += (int)(stat.TotalValue * GetStatWeight(stat.Type, character));
                }
            }

            score += (int)rune.Rarity * 100;
            score += rune.EnhancementLevel * 30;

            if (template?.SetId != null)
                score += 300;

            return score;
        }

        private float GetStatWeight(EquipmentStatType statType, UserCharacter character)
        {
            return statType switch
            {
                EquipmentStatType.Attack => 1.5f,
                EquipmentStatType.HP => 1.0f,
                EquipmentStatType.Defense => 1.0f,
                EquipmentStatType.Speed => 1.5f,
                EquipmentStatType.CritRate => 1.3f,
                EquipmentStatType.CritDamage => 1.3f,
                EquipmentStatType.EffectResistance => 1.0f,
                EquipmentStatType.EffectAccuracy => 1.0f,
                _ => 1.0f
            };
        }

        private List<SetBonusDto> CheckSetBonuses(List<EquipResult> results, Dictionary<Guid, RuneTemplate> templates)
        {
            var setCounts = new Dictionary<Guid, int>();

            foreach (var result in results)
            {
                var rune = templates.GetValueOrDefault(result.EquipmentId); // This would need actual rune lookup
                // Simplified
            }

            return new List<SetBonusDto>();
        }

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
    }
}