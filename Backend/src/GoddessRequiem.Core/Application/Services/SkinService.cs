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
    public class SkinService : ISkinService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SkinService> _logger;

        public SkinService(IUnitOfWork unitOfWork, ILogger<SkinService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SkinDto>> GetAvailableSkinsAsync(Guid characterId)
        {
            var skins = await _unitOfWork.SkinTemplates.GetByCharacterIdAsync(characterId);
            return skins.Select(MapToDto).ToList();
        }

        public async Task<UserSkinDto> GetUserSkinAsync(Guid userId, Guid skinId)
        {
            var userSkin = await _unitOfWork.UserSkins.GetByUserAndSkinAsync(userId, skinId);
            if (userSkin == null) return null;

            var template = await _unitOfWork.SkinTemplates.GetByIdAsync(skinId);
            return MapToUserSkinDto(userSkin, template);
        }

        public async Task<IReadOnlyList<UserSkinDto>> GetUserSkinsAsync(Guid userId, Guid characterId)
        {
            var userSkins = await _unitOfWork.UserSkins.GetByUserAndCharacterAsync(userId, characterId);
            var skinIds = userSkins.Select(s => s.SkinId).ToList();
            var templates = new Dictionary<Guid, SkinTemplate>();

            foreach (var id in skinIds)
            {
                var t = await _unitOfWork.SkinTemplates.GetByIdAsync(id);
                if (t != null) templates[id] = t;
            }

            return userSkins.Select(s => MapToUserSkinDto(s, templates.GetValueOrDefault(s.SkinId))).ToList();
        }

        public async Task<PurchaseResult> PurchaseSkinAsync(Guid userId, Guid skinId, CurrencyAmount price)
        {
            var template = await _unitOfWork.SkinTemplates.GetByIdAsync(skinId);
            if (template == null)
                throw new KeyNotFoundException("Skin not found");

            if (!template.IsPurchasable)
                throw new InvalidOperationException("Skin not available for purchase");

            // Check if already owned
            var existing = await _unitOfWork.UserSkins.GetByUserAndSkinAsync(userId, skinId);
            if (existing != null)
                throw new InvalidOperationException("Skin already owned");

            // Check currency
            var canAfford = await CheckCurrencyAsync(userId, price);
            if (!canAfford)
                throw new InvalidOperationException("Insufficient currency");

            // Spend currency
            await SpendCurrencyAsync(userId, price);

            // Grant skin
            var userSkin = new UserSkin
            {
                UserId = userId,
                SkinId = skinId,
                AcquiredAt = DateTime.UtcNow,
                Source = SkinSource.Purchase,
                DyeSlots = template.DyeSlots.Select(ds => new UserSkinDyeSlot { SlotIndex = ds.Index, ColorId = ds.DefaultColorId }).ToList()
            };

            await _unitOfWork.UserSkins.AddAsync(userSkin);
            await _unitOfWork.SaveChangesAsync();

            return new PurchaseResult
            {
                Success = true,
                SkinId = skinId,
                CurrencySpent = price,
                AcquiredAt = userSkin.AcquiredAt
            };
        }

        public async Task<EquipResult> EquipSkinAsync(Guid userId, Guid characterId, Guid? skinId)
        {
            var userChar = await _unitOfWork.UserCharacters.GetByIdAsync(characterId);
            if (userChar == null || userChar.UserId != userId)
                throw new KeyNotFoundException("Character not found");

            // If skinId is null, unequip current skin
            if (!skinId.HasValue)
            {
                userChar.EquippedSkinId = null;
                await _unitOfWork.UserCharacters.UpdateAsync(userChar);
                await _unitOfWork.SaveChangesAsync();

                return new EquipResult { Success = true, EquipmentId = Guid.Empty };
            }

            // Check ownership
            var userSkin = await _unitOfWork.UserSkins.GetByUserAndSkinAsync(userId, skinId.Value);
            if (userSkin == null)
                throw new KeyNotFoundException("Skin not owned");

            var template = await _unitOfWork.SkinTemplates.GetByIdAsync(skinId.Value);
            if (template.CharacterId != userChar.CharacterId)
                throw new ArgumentException("Skin not compatible with character");

            userChar.EquippedSkinId = skinId;
            await _unitOfWork.UserCharacters.UpdateAsync(userChar);
            await _unitOfWork.SaveChangesAsync();

            return new EquipResult
            {
                Success = true,
                EquipmentId = skinId.Value
            };
        }

        public async Task<DyeResult> DyeSkinAsync(Guid userId, Guid skinId, SkinDyeOption option)
        {
            var userSkin = await _unitOfWork.UserSkins.GetByUserAndSkinAsync(userId, skinId);
            if (userSkin == null)
                throw new KeyNotFoundException("Skin not owned");

            var template = await _unitOfWork.SkinTemplates.GetByIdAsync(skinId);
            var dyeSlot = template.DyeSlots.FirstOrDefault(ds => ds.Index == option.SlotIndex);
            if (dyeSlot == null)
                throw new ArgumentException("Invalid dye slot");

            var dye = await _unitOfWork.DyeTemplates.GetByIdAsync(option.DyeId);
            if (dye == null)
                throw new KeyNotFoundException("Dye not found");

            // Check if user owns the dye
            var userDye = await _unitOfWork.UserDyes.GetByUserAndDyeAsync(userId, option.DyeId);
            if (userDye == null || userDye.Quantity < 1)
            {
                // Check if dye is free/premium currency
                if (dye.CurrencyCost != null)
                {
                    var canAfford = await CheckCurrencyAsync(userId, dye.CurrencyCost);
                    if (!canAfford)
                        throw new InvalidOperationException("Insufficient currency for dye");

                    await SpendCurrencyAsync(userId, dye.CurrencyCost);
                }
                else
                {
                    throw new InvalidOperationException("Dye not owned");
                }
            }
            else
            {
                userDye.Quantity--;
                await _unitOfWork.UserDyes.UpdateAsync(userDye);
            }

            // Apply dye
            var slot = userSkin.DyeSlots.FirstOrDefault(ds => ds.SlotIndex == option.SlotIndex);
            if (slot == null)
            {
                slot = new UserSkinDyeSlot { SlotIndex = option.SlotIndex, ColorId = option.DyeId };
                userSkin.DyeSlots.Add(slot);
            }
            else
            {
                slot.ColorId = option.DyeId;
            }

            userSkin.LastDyedAt = DateTime.UtcNow;
            await _unitOfWork.UserSkins.UpdateAsync(userSkin);
            await _unitOfWork.SaveChangesAsync();

            return new DyeResult
            {
                Success = true,
                SkinId = skinId,
                SlotIndex = option.SlotIndex,
                NewColorId = option.DyeId,
                PreviewAssetPath = GeneratePreviewPath(template, userSkin.DyeSlots)
            };
        }

        public async Task<IReadOnlyList<DyeTemplateDto>> GetAvailableDyesAsync(Guid userId)
        {
            var dyes = await _unitOfWork.DyeTemplates.GetAllAsync();
            var userDyes = await _unitOfWork.UserDyes.GetByUserIdAsync(userId);
            var userDyeDict = userDyes.ToDictionary(d => d.DyeId, d => d.Quantity);

            return dyes.Select(d => new DyeTemplateDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                ColorHex = d.ColorHex,
                Category = d.Category,
                Rarity = d.Rarity,
                CurrencyCost = d.CurrencyCost,
                OwnedQuantity = userDyeDict.GetValueOrDefault(d.Id, 0),
                IsOwned = userDyeDict.ContainsKey(d.Id)
            }).ToList();
        }

        public async Task<DyeResult> ApplyDyeFromItemAsync(Guid userId, Guid skinId, Guid dyeItemId)
        {
            var userItem = await _unitOfWork.UserItems.GetByIdAsync(dyeItemId);
            if (userItem == null || userItem.UserId != userId)
                throw new KeyNotFoundException("Dye item not found");

            var template = await _unitOfWork.ItemTemplates.GetByIdAsync(userItem.TemplateId);
            if (template.Type != ItemType.Dye)
                throw new ArgumentException("Item is not a dye");

            var dyeData = template.DyeData;
            if (dyeData == null)
                throw new InvalidOperationException("Invalid dye item");

            return await DyeSkinAsync(userId, skinId, new SkinDyeOption
            {
                SlotIndex = dyeData.SlotIndex,
                DyeId = dyeData.DyeId
            });
        }

        private SkinDto MapToDto(SkinTemplate t) => new SkinDto
        {
            Id = t.Id,
            CharacterId = t.CharacterId,
            Name = t.Name,
            Description = t.Description,
            Rarity = t.Rarity,
            Type = t.Type,
            PortraitAssetPath = t.PortraitAssetPath,
            FullBodyAssetPath = t.FullBodyAssetPath,
            EffectsAssetPath = t.EffectsAssetPath,
            VoicePackId = t.VoicePackId,
            IsPurchasable = t.IsPurchasable,
            Price = t.Price,
            DyeSlots = t.DyeSlots.Select(ds => new DyeSlotDto { Index = ds.Index, Name = ds.Name, DefaultColorId = ds.DefaultColorId }).ToList(),
            UnlockCondition = t.UnlockCondition
        };

        private UserSkinDto MapToUserSkinDto(UserSkin s, SkinTemplate t) => new UserSkinDto
        {
            Id = s.Id,
            SkinId = s.SkinId,
            Name = t?.Name ?? "",
            Rarity = t?.Rarity ?? Rarity.R,
            Type = t?.Type ?? SkinType.Default,
            PortraitAssetPath = t?.PortraitAssetPath ?? "",
            FullBodyAssetPath = t?.FullBodyAssetPath ?? "",
            EffectsAssetPath = t?.EffectsAssetPath,
            VoicePackId = t?.VoicePackId,
            IsEquipped = s.IsEquipped,
            DyeSlots = s.DyeSlots?.Select(ds => new UserDyeSlotDto { SlotIndex = ds.SlotIndex, ColorId = ds.ColorId, ColorHex = GetColorHex(ds.ColorId) }).ToList() ?? new List<UserDyeSlotDto>(),
            AcquiredAt = s.AcquiredAt,
            Source = s.Source
        };

        private string GetColorHex(Guid colorId)
        {
            // Would lookup actual color
            return "#FFFFFF";
        }

        private string GeneratePreviewPath(SkinTemplate template, List<UserSkinDyeSlot> dyeSlots)
        {
            // Would generate preview with applied dyes
            return template.FullBodyAssetPath;
        }

        private async Task<bool> CheckCurrencyAsync(Guid userId, CurrencyAmount cost) => true;
        private async Task SpendCurrencyAsync(Guid userId, CurrencyAmount cost) { }
    }
}