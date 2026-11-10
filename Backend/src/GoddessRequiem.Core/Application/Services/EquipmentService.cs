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
    public class EquipmentService : IEquipmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EquipmentService> _logger;
        private readonly ICharacterService _characterService;

        public EquipmentService(IUnitOfWork unitOfWork, ILogger<EquipmentService> logger, ICharacterService characterService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _characterService = characterService;
        }

        public async Task<UserEquipmentDto> GetEquipmentAsync(Guid equipmentId)
        {
            var equipment = await _unitOfWork.UserEquipments.GetByIdAsync(equipmentId);
            if (equipment == null) return null;

            var template = await _unitOfWork.EquipmentTemplates.GetByIdAsync(equipment.TemplateId);
            return MapToDto(equipment, template);
        }

        public async Task<IReadOnlyList<UserEquipmentDto>> GetUserEquipmentAsync(Guid userId)
        {
            var equipments = await _unitOfWork.UserEquipments.GetByUserIdAsync(userId);
            var templateIds = equipments.Select(e => e.TemplateId).Distinct().ToList();
            var templates = new Dictionary<Guid, EquipmentTemplate>();

            foreach (var id in templateIds)
            {
                var t = await _unitOfWork.EquipmentTemplates.GetByIdAsync(id);
                if (t != null) templates[id] = t;
            }

            return equipments.Select(e => MapToDto(e, templates.GetValueOrDefault(e.TemplateId))).ToList();
        }

        public async Task<EnhanceResult> EnhanceEquipmentAsync(Guid userId, Guid equipmentId)
        {
            var equipment = await _unitOfWork.UserEquipments.GetByIdAsync(equipmentId);
            if (equipment == null)
                throw new KeyNotFoundException("Equipment not found");

            if (equipment.UserId != userId)
                throw new UnauthorizedAccessException("Equipment not owned by user");

            var template = await _unitOfWork.EquipmentTemplates.GetByIdAsync(equipment.TemplateId);
            if (template == null)
                throw new KeyNotFoundException("Equipment template not found");

            if (equipment.EnhancementLevel >= template.MaxEnhancementLevel)
                throw new InvalidOperationException("Equipment already at max enhancement");

            // Calculate cost
            var cost = CalculateEnhanceCost(template, equipment.EnhancementLevel + 1);
            var canAfford = await CheckCurrencyAsync(userId, cost);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient gold");

            // Check materials
            var materials = GetRequiredMaterials(template, equipment.EnhancementLevel + 1);
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

            // Calculate success rate
            var successRate = CalculateSuccessRate(template, equipment.EnhancementLevel + 1);
            var roll = new Random().NextDouble();
            var success = roll <= successRate;

            if (success)
            {
                equipment.Enhance();
                equipment.UpdateStats(template);
            }
            else
            {
                // Failed enhancement - lose some materials but keep level (or downgrade based on config)
                // For now, just fail without downgrade
            }

            await _unitOfWork.UserEquipments.UpdateAsync(equipment);
            await _unitOfWork.SaveChangesAsync();

            return new EnhanceResult
            {
                Success = success,
                NewLevel = equipment.EnhancementLevel,
                NewStats = equipment.Stats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
                GoldSpent = cost.Amount,
                MaterialsUsed = materials.Select(m => new MaterialCostDto { MaterialId = m.MaterialId, Quantity = m.Quantity }).ToList()
            };
        }

        public async Task<EquipResult> EquipAsync(Guid userId, Guid characterId, Guid equipmentId, EquipmentSlot slot)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByIdAsync(characterId);
            if (userChar == null || userChar.UserId != userId)
                throw new KeyNotFoundException("Character not found");

            var equipment = await _unitOfWork.UserEquipments.GetByIdAsync(equipmentId);
            if (equipment == null || equipment.UserId != userId)
                throw new KeyNotFoundException("Equipment not found");

            var template = await _unitOfWork.EquipmentTemplates.GetByIdAsync(equipment.TemplateId);
            if (template.Slot != slot)
                throw new ArgumentException("Equipment slot mismatch");

            // Check if character can equip this type
            if (!CanCharacterEquip(userChar, template))
                throw new InvalidOperationException("Character cannot equip this equipment type");

            // Unequip current equipment in slot
            var currentEquipped = await _unitOfWork.UserEquipments.GetEquippedByCharacterAndSlotAsync(userId, characterId, slot);
            if (currentEquipped != null)
            {
                currentEquipped.Unequip();
                await _unitOfWork.UserEquipments.UpdateAsync(currentEquipped);
            }

            // Equip new
            equipment.Equip(characterId);
            await _unitOfWork.UserEquipments.UpdateAsync(equipment);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = equipmentId,
                Slot = slot,
                UnequippedEquipmentId = currentEquipped?.Id
            };
        }

        public async Task<EquipResult> UnequipAsync(Guid userId, Guid characterId, EquipmentSlot slot)
        {
            var equipment = await _unitOfWork.UserEquipments.GetEquippedByCharacterAndSlotAsync(userId, characterId, slot);
            if (equipment == null)
                return new EquipResult { Success = false, Error = "No equipment in slot" };

            equipment.Unequip();
            await _unitOfWork.UserEquipments.UpdateAsync(equipment);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = equipment.Id,
                Slot = slot
            };
        }

        public async Task<SellResult> SellEquipmentAsync(Guid userId, List<Guid> equipmentIds)
        {
            var equipments = new List<UserEquipment>();
            long totalGold = 0;
            var soldItems = new List<SoldItemDto>();

            foreach (var id in equipmentIds)
            {
                var equipment = await _unitOfWork.UserEquipments.GetByIdAsync(id);
                if (equipment == null || equipment.UserId != userId)
                    continue;

                if (equipment.IsEquipped)
                    continue; // Skip equipped items

                if (equipment.IsLocked)
                    continue; // Skip locked items

                var template = await _unitOfWork.EquipmentTemplates.GetByIdAsync(equipment.TemplateId);
                var sellPrice = CalculateSellPrice(template, equipment.EnhancementLevel, equipment.Rarity);

                equipments.Add(equipment);
                totalGold += sellPrice;
                soldItems.Add(new SoldItemDto { EquipmentId = id, TemplateName = template.Name, SellPrice = sellPrice });
            }

            if (!equipments.Any())
                return new SellResult { Success = false, Error = "No valid equipment to sell" };

            // Remove equipments
            foreach (var eq in equipments)
            {
                await _unitOfWork.UserEquipments.DeleteAsync(eq.Id);
            }

            // Grant gold
            await AddCurrencyAsync(userId, CurrencyType.Gold, totalGold);

            await _unitOfWork.SaveChangesAsync();

            return new SellResult
            {
                Success = true,
                TotalGold = totalGold,
                ItemsSold = soldItems
            };
        }

        public async Task<AutoEquipResult> AutoEquipAsync(Guid userId, Guid characterId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByIdAsync(characterId);
            if (userChar == null || userChar.UserId != userId)
                throw new KeyNotFoundException("Character not found");

            var availableEquipments = await _unitOfWork.UserEquipments.GetAvailableForCharacterAsync(userId, userChar.CharacterId);
            var templateIds = availableEquipments.Select(e => e.TemplateId).Distinct().ToList();
            var templates = new Dictionary<Guid, EquipmentTemplate>();

            foreach (var id in templateIds)
            {
                var t = await _unitOfWork.EquipmentTemplates.GetByIdAsync(id);
                if (t != null) templates[id] = t;
            }

            var results = new List<EquipResult>();
            var slots = Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>().ToList();

            foreach (var slot in slots)
            {
                // Find best equipment for this slot
                var bestEquip = availableEquipments
                    .Where(e => templates.GetValueOrDefault(e.TemplateId)?.Slot == slot)
                    .Where(e => CanCharacterEquip(userChar, templates.GetValueOrDefault(e.TemplateId)))
                    .OrderByDescending(e => CalculateEquipmentScore(e, templates.GetValueOrDefault(e.TemplateId), userChar))
                    .FirstOrDefault();

                if (bestEquip != null)
                {
                    var result = await EquipAsync(userId, characterId, bestEquip.Id, slot);
                    if (result.Success)
                    {
                        results.Add(result);
                        availableEquipments.Remove(bestEquip);
                    }
                }
            }

            return new AutoEquipResult
            {
                Success = true,
                EquippedItems = results
            };
        }

        private UserEquipmentDto MapToDto(UserEquipment e, EquipmentTemplate t) => new UserEquipmentDto
        {
            Id = e.Id,
            TemplateId = e.TemplateId,
            Name = t?.Name ?? "",
            Slot = t?.Slot ?? EquipmentSlot.Weapon,
            Rarity = e.Rarity,
            EnhancementLevel = e.EnhancementLevel,
            MaxEnhancementLevel = t?.MaxEnhancementLevel ?? 15,
            BaseStats = t?.BaseStats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
            CurrentStats = e.Stats?.Select(MapToStatDto).ToList() ?? new List<EquipmentStatDto>(),
            SetBonusId = t?.SetBonusId,
            IsEquipped = e.IsEquipped,
            EquippedCharacterId = e.EquippedCharacterId,
            IsLocked = e.IsLocked,
            AcquiredAt = e.AcquiredAt
        };

        private EquipmentStatDto MapToStatDto(EquipmentStat s) => new EquipmentStatDto
        {
            Type = s.Type,
            BaseValue = s.BaseValue,
            EnhancedValue = s.EnhancedValue,
            TotalValue = s.TotalValue
        };

        private CurrencyAmount CalculateEnhanceCost(EquipmentTemplate template, int targetLevel)
        {
            var baseCost = template.Rarity switch
            {
                Rarity.R => 1000,
                Rarity.SR => 2500,
                Rarity.SSR => 5000,
                Rarity.UR => 10000,
                _ => 1000
            };

            var cost = (long)(baseCost * Math.Pow(targetLevel, 1.8));
            return new CurrencyAmount(CurrencyType.Gold, cost);
        }

        private List<MaterialRequirement> GetRequiredMaterials(EquipmentTemplate template, int targetLevel)
        {
            // Return materials based on template and level
            return new List<MaterialRequirement>
            {
                new MaterialRequirement { MaterialId = Guid.NewGuid(), MaterialName = "Enhancement Stone", Quantity = targetLevel * 2 }
            };
        }

        private double CalculateSuccessRate(EquipmentTemplate template, int targetLevel)
        {
            var baseRate = 1.0;
            var levelPenalty = (targetLevel - 1) * 0.05;
            var rarityBonus = template.Rarity switch
            {
                Rarity.R => 0.1,
                Rarity.SR => 0.05,
                Rarity.SSR => 0,
                Rarity.UR => -0.05,
                _ => 0
            };

            return Math.Max(0.1, Math.Min(1.0, baseRate - levelPenalty + rarityBonus));
        }

        private bool CanCharacterEquip(UserCharacter character, EquipmentTemplate template)
        {
            // Check class/weapon type compatibility
            return true; // Simplified
        }

        private long CalculateSellPrice(EquipmentTemplate template, int enhancementLevel, Rarity rarity)
        {
            var basePrice = template.Rarity switch
            {
                Rarity.R => 500,
                Rarity.SR => 1500,
                Rarity.SSR => 5000,
                Rarity.UR => 15000,
                _ => 500
            };

            var enhanceValue = enhancementLevel * (basePrice / 10);
            return basePrice + enhanceValue;
        }

        private int CalculateEquipmentScore(UserEquipment equipment, EquipmentTemplate template, UserCharacter character)
        {
            var score = 0;

            // Base stats
            if (equipment.Stats != null)
            {
                foreach (var stat in equipment.Stats)
                {
                    score += (int)(stat.TotalValue * GetStatWeight(stat.Type, character));
                }
            }

            // Rarity bonus
            score += (int)equipment.Rarity * 100;

            // Enhancement bonus
            score += equipment.EnhancementLevel * 50;

            // Set bonus
            if (template?.SetBonusId != null)
                score += 500;

            return score;
        }

        private float GetStatWeight(EquipmentStatType statType, UserCharacter character)
        {
            var charTemplate = character.CharacterId; // Would get actual template
            return statType switch
            {
                EquipmentStatType.Attack => 1.5f,
                EquipmentStatType.HP => 1.0f,
                EquipmentStatType.Defense => 1.0f,
                EquipmentStatType.Speed => 1.2f,
                EquipmentStatType.CritRate => 1.3f,
                EquipmentStatType.CritDamage => 1.3f,
                _ => 1.0f
            };
        }

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
        private async Task<int> GetMaterialCountAsync(Guid userId, Guid materialId) => 999;
        private async Task ConsumeMaterialAsync(Guid userId, Guid materialId, int quantity) { }
        private async Task AddCurrencyAsync(Guid userId, CurrencyType currencyType, long amount) { }
    }
}