using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class UserCharacter : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid CharacterId { get; private set; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int AscensionLevel { get; private set; }
        public int AffinityLevel { get; private set; }
        public long AffinityExperience { get; private set; }
        public int Skill1Level { get; private set; }
        public int Skill2Level { get; private set; }
        public int Skill3Level { get; private set; }
        public int UltimateLevel { get; private set; }
        public List<Guid> EquippedEquipmentIds { get; private set; } = new();
        public Guid? EquippedRelicId { get; private set; }
        public List<Guid> EquippedRuneIds { get; private set; } = new();
        public Guid? CurrentSkinId { get; private set; }
        public bool IsFavorite { get; private set; }
        public bool IsLocked { get; private set; }
        public DateTime AcquiredAt { get; private set; }
        public int LimitBreakCount { get; private set; }
        public Dictionary<string, object> CustomData { get; private set; } = new();

        private UserCharacter() { }

        public UserCharacter(Guid userId, Guid characterId, DateTime acquiredAt)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            CharacterId = characterId;
            Level = 1;
            Experience = 0;
            AscensionLevel = 0;
            AffinityLevel = 0;
            AffinityExperience = 0;
            Skill1Level = 1;
            Skill2Level = 1;
            Skill3Level = 1;
            UltimateLevel = 1;
            IsFavorite = false;
            IsLocked = false;
            AcquiredAt = acquiredAt;
            LimitBreakCount = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddExperience(int amount, int maxLevel, out int levelsGained)
        {
            levelsGained = 0;
            if (Level >= maxLevel) return;

            Experience += amount;
            var requiredExp = GetRequiredExperience(Level);

            while (Experience >= requiredExp && Level < maxLevel)
            {
                Experience -= requiredExp;
                Level++;
                levelsGained++;
                requiredExp = GetRequiredExperience(Level);
            }

            if (Level >= maxLevel)
            {
                Level = maxLevel;
                Experience = 0;
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public bool TryAscend(List<CharacterEvolutionMaterial> requiredMaterials, Func<Guid, int> getUserMaterialCount, Action<Guid, int> consumeMaterial)
        {
            foreach (var material in requiredMaterials)
            {
                if (material.RequiredAscensionLevel != AscensionLevel + 1) continue;

                var owned = getUserMaterialCount(material.MaterialId);
                if (owned < material.Quantity)
                    return false;
            }

            foreach (var material in requiredMaterials)
            {
                if (material.RequiredAscensionLevel != AscensionLevel + 1) continue;
                consumeMaterial(material.MaterialId, material.Quantity);
            }

            AscensionLevel++;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        public void AddAffinityExperience(long amount, int maxAffinityLevel, List<AffinityReward> rewards, Action<RaffinityReward> grantReward)
        {
            if (AffinityLevel >= maxAffinityLevel) return;

            AffinityExperience += amount;
            var requiredExp = GetRequiredAffinityExperience(AffinityLevel);

            while (AffinityExperience >= requiredExp && AffinityLevel < maxAffinityLevel)
            {
                AffinityExperience -= requiredExp;
                AffinityLevel++;

                var reward = rewards.FirstOrDefault(r => r.AffinityLevel == AffinityLevel);
                if (reward != null)
                {
                    grantReward(reward);
                }

                requiredExp = GetRequiredAffinityExperience(AffinityLevel);
            }

            if (AffinityLevel >= maxAffinityLevel)
            {
                AffinityLevel = maxAffinityLevel;
                AffinityExperience = 0;
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public bool UpgradeSkill(SkillType skillType, int maxLevel, CurrencyAmount cost, Func<CurrencyAmount, bool> trySpendCurrency)
        {
            var currentLevel = GetSkillLevel(skillType);
            if (currentLevel >= maxLevel) return false;

            if (!trySpendCurrency(cost)) return false;

            SetSkillLevel(skillType, currentLevel + 1);
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        public void EquipEquipment(Guid equipmentId, EquipmentSlot slot)
        {
            var index = (int)slot - 1;
            while (EquippedEquipmentIds.Count <= index)
                EquippedEquipmentIds.Add(Guid.Empty);

            EquippedEquipmentIds[index] = equipmentId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UnequipEquipment(EquipmentSlot slot)
        {
            var index = (int)slot - 1;
            if (index >= 0 && index < EquippedEquipmentIds.Count)
            {
                EquippedEquipmentIds[index] = Guid.Empty;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void EquipRelic(Guid relicId)
        {
            EquippedRelicId = relicId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void EquipRune(Guid runeId, int slotIndex)
        {
            while (EquippedRuneIds.Count <= slotIndex)
                EquippedRuneIds.Add(Guid.Empty);

            EquippedRuneIds[slotIndex] = runeId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetSkin(Guid? skinId)
        {
            CurrentSkinId = skinId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ToggleLock()
        {
            IsLocked = !IsLocked;
            UpdatedAt = DateTime.UtcNow;
        }

        public void LimitBreak()
        {
            LimitBreakCount++;
            UpdatedAt = DateTime.UtcNow;
        }

        public int GetSkillLevel(SkillType type)
        {
            return type switch
            {
                SkillType.Basic => Skill1Level,
                SkillType.Special => Skill2Level,
                SkillType.Special => Skill3Level, // This would need refinement
                SkillType.Ultimate => UltimateLevel,
                _ => 1
            };
        }

        public void SetSkillLevel(SkillType type, int level)
        {
            switch (type)
            {
                case SkillType.Basic: Skill1Level = level; break;
                case SkillType.Special: Skill2Level = level; break;
                case SkillType.Ultimate: UltimateLevel = level; break;
            }
        }

        private static int GetRequiredExperience(int level)
        {
            // Exponential curve: base 100 * level^1.5
            return (int)(100 * Math.Pow(level, 1.5));
        }

        private static long GetRequiredAffinityExperience(int level)
        {
            // Linear growth: 1000 * (level + 1)
            return 1000L * (level + 1);
        }
    }
}