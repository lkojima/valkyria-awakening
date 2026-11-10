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
    public class RelicService : IRelicService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RelicService> _logger;
        private readonly ICharacterService _characterService;

        public RelicService(IUnitOfWork unitOfWork, ILogger<RelicService> logger, ICharacterService characterService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _characterService = characterService;
        }

        public async Task<UserRelicDto> GetRelicAsync(Guid relicId)
        {
            var relic = await _unitOfWork.UserRelics.GetByIdAsync(relicId);
            if (relic == null) return null;

            var template = await _unitOfWork.RelicTemplates.GetByIdAsync(relic.TemplateId);
            return MapToDto(relic, template);
        }

        public async Task<IReadOnlyList<UserRelicDto>> GetUserRelicsAsync(Guid userId)
        {
            var relics = await _unitOfWork.UserRelics.GetByUserIdAsync(userId);
            var templateIds = relics.Select(r => r.TemplateId).Distinct().ToList();
            var templates = new Dictionary<Guid, RelicTemplate>();

            foreach (var id in templateIds)
            {
                var t = await _unitOfWork.RelicTemplates.GetByIdAsync(id);
                if (t != null) templates[id] = t;
            }

            return relics.Select(r => MapToDto(r, templates.GetValueOrDefault(r.TemplateId))).ToList();
        }

        public async Task<EnhanceResult> EnhanceRelicAsync(Guid userId, Guid relicId)
        {
            var relic = await _unitOfWork.UserRelics.GetByIdAsync(relicId);
            if (relic == null)
                throw new KeyNotFoundException("Relic not found");

            if (relic.UserId != userId)
                throw new UnauthorizedAccessException("Relic not owned by user");

            var template = await _unitOfWork.RelicTemplates.GetByIdAsync(relic.TemplateId);
            if (template == null)
                throw new KeyNotFoundException("Relic template not found");

            if (relic.EnhancementLevel >= template.MaxEnhancementLevel)
                throw new InvalidOperationException("Relic already at max enhancement");

            // Calculate cost
            var cost = CalculateEnhanceCost(template, relic.EnhancementLevel + 1);
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient gold");

            // Check materials
            var materials = GetRequiredMaterials(template, relic.EnhancementLevel + 1);
            foreach (var material in materials)
            {
                var owned = await GetMaterialCountAsync(userId, material.MaterialId);
                if (owned < material.Quantity)
                    throw new InvalidOperationException($"Insufficient {material.MaterialName}");
            }

            // Spend resources
            await SpendCurrencyAsync(userId, cost);
            foreach (var material in materials)
            {
                await ConsumeMaterialAsync(userId, material.MaterialId, material.Quantity);
            }

            // Relics always succeed but have RNG sub-stat rolls
            var oldLevel = relic.EnhancementLevel;
            relic.Enhance();
            relic.RollSubStats(template, oldLevel);

            await _unitOfWork.UserRelics.UpdateAsync(relic);
            await _unitOfWork.SaveChangesAsync();

            return new EnhanceResult
            {
                Success = true,
                NewLevel = relic.EnhancementLevel,
                NewStats = relic.MainStat != null ? new List<EquipmentStatDto> { MapToStatDto(relic.MainStat) } : new List<EquipmentStatDto>(),
                SubStats = relic.SubStats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
                GoldSpent = cost.Amount,
                MaterialsUsed = materials.Select(m => new MaterialCostDto { MaterialId = m.MaterialId, Quantity = m.Quantity }).ToList()
            };
        }

        public async Task<EquipResult> EquipAsync(Guid userId, Guid characterId, Guid relicId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByIdAsync(characterId);
            if (userChar == null || userChar.UserId != userId)
                throw new KeyNotFoundException("Character not found");

            var relic = await _unitOfWork.UserRelics.GetByIdAsync(relicId);
            if (relic == null || relic.UserId != userId)
                throw new KeyNotFoundException("Relic not found");

            // Unequip current relic
            var currentEquipped = await _unitOfWork.UserRelics.GetEquippedByCharacterAsync(userId, characterId);
            if (currentEquipped != null)
            {
                currentEquipped.Unequip();
                await _unitOfWork.UserRelics.UpdateAsync(currentEquipped);
            }

            // Equip new
            relic.Equip(characterId);
            await _unitOfWork.UserRelics.UpdateAsync(relic);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = relicId,
                UnequippedEquipmentId = currentEquipped?.Id
            };
        }

        public async Task<EquipResult> UnequipAsync(Guid userId, Guid characterId)
        {
            var relic = await _unitOfWork.UserRelics.GetEquippedByCharacterAsync(userId, characterId);
            if (relic == null)
                return new EquipResult { Success = false, Error = "No relic equipped" };

            relic.Unequip();
            await _unitOfWork.UserRelics.UpdateAsync(relic);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = relic.Id
            };
        }

        private UserRelicDto MapToDto(UserRelic r, RelicTemplate t) => new UserRelicDto
        {
            Id = r.Id,
            TemplateId = r.TemplateId,
            Name = t?.Name ?? "",
            Rarity = r.Rarity,
            EnhancementLevel = r.EnhancementLevel,
            MaxEnhancementLevel = t?.MaxEnhancementLevel ?? 15,
            Slot = t?.Slot ?? RelicSlot.Head,
            SetId = t?.SetId,
            MainStat = r.MainStat != null ? MapToStatDto(r.MainStat) : null,
            SubStats = r.SubStats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
            SetBonus = t?.SetBonus != null ? MapToSetBonusDto(t.SetBonus) : null,
            IsEquipped = r.IsEquipped,
            EquippedCharacterId = r.EquippedCharacterId,
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

        private CurrencyAmount CalculateEnhanceCost(RelicTemplate template, int targetLevel)
        {
            var baseCost = template.Rarity switch
            {
                Rarity.R => 2000,
                Rarity.SR => 5000,
                Rarity.SSR => 10000,
                Rarity.UR => 20000,
                _ => 2000
            };

            var cost = (long)(baseCost * Math.Pow(targetLevel, 1.6));
            return new CurrencyAmount(CurrencyType.Gold, cost);
        }

        private List<MaterialRequirement> GetRequiredMaterials(RelicTemplate template, int targetLevel)
        {
            return new List<MaterialRequirement>
            {
                new MaterialRequirement { MaterialId = Guid.NewGuid(), MaterialName = "Relic Essence", Quantity = targetLevel * 3 }
            };
        }

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task<int> GetMaterialCountAsync(Guid userId, Guid materialId) => 999;
        private async Task ConsumeMaterialAsync(Guid userId, Guid materialId, int quantity) { }
    }
}