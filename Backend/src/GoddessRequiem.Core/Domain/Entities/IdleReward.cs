using System;
using System.Collections.Generic;
using GoddessRequiem.Core.Domain.ValueObjects;
using GoddessRequiem.Core.Domain.Enums;

namespace GoddessRequiem.Core.Domain.Entities
{
    public class IdleReward : BaseEntity
    {
        public Guid UserId { get; private set; }
        public DateTime LastClaimAt { get; private set; }
        public DateTime NextClaimAt { get; private set; }
        public int AccumulatedHours { get; private set; }
        public int MaxAccumulationHours { get; private set; }
        public List<CurrencyAmount> AccumulatedCurrencies { get; private set; } = new();
        public List<ResourceQuantity> AccumulatedMaterials { get; private set; } = new();
        public List<ResourceQuantity> AccumulatedEquipment { get; private set; } = new();
        public List<ResourceQuantity> AccumulatedCharacterFragments { get; private set; } = new();
        public bool IsAutoClaimEnabled { get; private set; }
        public DateTime LastCalculatedAt { get; private set; }
        public ServerTimestamp ServerTimeAtLastCalc { get; private set; }

        private IdleReward() { }

        public IdleReward(Guid userId, int maxHours = 12)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            LastClaimAt = DateTime.UtcNow;
            NextClaimAt = DateTime.UtcNow.AddHours(maxHours);
            AccumulatedHours = 0;
            MaxAccumulationHours = maxHours;
            IsAutoClaimEnabled = false;
            LastCalculatedAt = DateTime.UtcNow;
            ServerTimeAtLastCalc = ServerTimestamp.Now;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void CalculateRewards(ServerTimestamp serverTime, IdleRewardConfig config, IReadOnlyList<UserCharacter> characters)
        {
            var elapsedHours = (serverTime.Value - LastCalculatedAt).TotalHours;
            var hoursToAdd = Math.Min(elapsedHours, MaxAccumulationHours - AccumulatedHours);

            if (hoursToAdd <= 0) return;

            AccumulatedHours += (int)Math.Floor(hoursToAdd);

            // Calculate per-hour rewards based on team power, stage progress, etc.
            var teamPower = CalculateTeamPower(characters);
            var stageMultiplier = GetStageMultiplier(config);
            var vipMultiplier = 1f; // Would get from user VIP

            var hourlyGold = (long)(config.BaseGoldPerHour * teamPower * stageMultiplier * vipMultiplier);
            var hourlyExp = (long)(config.BaseExpPerHour * teamPower * stageMultiplier * vipMultiplier);

            AddCurrency(CurrencyType.Gold, (long)(hourlyGold * hoursToAdd));
            AddCurrency(CurrencyType.Experience, (long)(hourlyExp * hoursToAdd));

            // Materials based on unlocked dungeons
            foreach (var material in config.HourlyMaterials)
            {
                if (IsDungeonUnlocked(material.DungeonId, characters))
                {
                    var qty = (int)(material.BaseQuantity * hoursToAdd * stageMultiplier);
                    AddMaterial(material.MaterialId, qty);
                }
            }

            // Character fragments from current campaign
            var fragmentChance = config.FragmentChancePerHour * hoursToAdd;
            if (Random.Shared.NextDouble() < fragmentChance)
            {
                var fragment = GetRandomCharacterFragment(config);
                if (fragment != null)
                {
                    AddCharacterFragment(fragment.CharacterId, fragment.Quantity);
                }
            }

            // Equipment drops
            var equipChance = config.EquipmentChancePerHour * hoursToAdd;
            if (Random.Shared.NextDouble() < equipChance)
            {
                var equip = GetRandomEquipment(config);
                if (equip != null)
                {
                    AddEquipment(equip.EquipmentId, equip.Quantity);
                }
            }

            LastCalculatedAt = serverTime.Value;
            ServerTimeAtLastCalc = serverTime;
            UpdatedAt = DateTime.UtcNow;
        }

        public IdleRewardSummary Claim(ServerTimestamp serverTime)
        {
            CalculateRewards(serverTime, GetDefaultConfig(), Array.Empty<UserCharacter>());

            var summary = new IdleRewardSummary
            {
                HoursAccumulated = AccumulatedHours,
                Currencies = AccumulatedCurrencies.ToList(),
                Materials = AccumulatedMaterials.ToList(),
                Equipment = AccumulatedEquipment.ToList(),
                CharacterFragments = AccumulatedCharacterFragments.ToList(),
                ClaimedAt = serverTime.Value
            };

            // Reset accumulators
            AccumulatedHours = 0;
            AccumulatedCurrencies.Clear();
            AccumulatedMaterials.Clear();
            AccumulatedEquipment.Clear();
            AccumulatedCharacterFragments.Clear();
            LastClaimAt = serverTime.Value;
            NextClaimAt = serverTime.Value.AddHours(MaxAccumulationHours);
            UpdatedAt = DateTime.UtcNow;

            return summary;
        }

        public void SetAutoClaim(bool enabled)
        {
            IsAutoClaimEnabled = enabled;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetMaxAccumulationHours(int hours)
        {
            MaxAccumulationHours = Math.Clamp(hours, 1, 24);
            UpdatedAt = DateTime.UtcNow.
        }

        private void AddCurrency(CurrencyType type, long amount)
        {
            var existing = AccumulatedCurrencies.FirstOrDefault(c => c.Type == type);
            if (existing.Type != default)
            {
                var index = AccumulatedCurrencies.IndexOf(existing);
                AccumulatedCurrencies[index] = existing + new CurrencyAmount(type, amount);
            }
            else
            {
                AccumulatedCurrencies.Add(new CurrencyAmount(type, amount));
            }
        }

        private void AddMaterial(Guid materialId, int quantity)
        {
            var existing = AccumulatedMaterials.FirstOrDefault(m => m.ResourceId == materialId);
            if (existing.ResourceId != default)
            {
                var index = AccumulatedMaterials.IndexOf(existing);
                AccumulatedMaterials[index] = existing + new ResourceQuantity(materialId, quantity);
            }
            else
            {
                AccumulatedMaterials.Add(new ResourceQuantity(materialId, quantity));
            }
        }

        private void AddEquipment(Guid equipmentId, int quantity)
        {
            var existing = AccumulatedEquipment.FirstOrDefault(e => e.ResourceId == equipmentId);
            if (existing.ResourceId != default)
            {
                var index = AccumulatedEquipment.IndexOf(existing);
                AccumulatedEquipment[index] = existing + new ResourceQuantity(equipmentId, quantity);
            }
            else
            {
                AccumulatedEquipment.Add(new ResourceQuantity(equipmentId, quantity));
            }
        }

        private void AddCharacterFragment(Guid characterId, int quantity)
        {
            var existing = AccumulatedCharacterFragments.FirstOrDefault(f => f.ResourceId == characterId);
            if (existing.ResourceId != default)
            {
                var index = AccumulatedCharacterFragments.IndexOf(existing);
                AccumulatedCharacterFragments[index] = existing + new ResourceQuantity(characterId, quantity);
            }
            else
            {
                AccumulatedCharacterFragments.Add(new ResourceQuantity(characterId, quantity));
            }
        }

        private float CalculateTeamPower(IReadOnlyList<UserCharacter> characters)
        {
            if (!characters.Any()) return 1f;
            return characters.Average(c => c.Level * (1 + c.AscensionLevel * 0.1f));
        }

        private float GetStageMultiplier(IdleRewardConfig config)
        {
            // Would be based on highest cleared stage
            return 1f;
        }

        private bool IsDungeonUnlocked(Guid dungeonId, IReadOnlyList<UserCharacter> characters)
        {
            // Check if player has unlocked this dungeon
            return true;
        }

        private (Guid CharacterId, int Quantity)? GetRandomCharacterFragment(IdleRewardConfig config)
        {
            // Weighted random from available characters
            return null;
        }

        private (Guid EquipmentId, int Quantity)? GetRandomEquipment(IdleRewardConfig config)
        {
            // Weighted random from equipment pool
            return null;
        }

        private IdleRewardConfig GetDefaultConfig() => new();
    }

    public class IdleRewardConfig
    {
        public float BaseGoldPerHour { get; set; } = 100;
        public float BaseExpPerHour { get; set; } = 50;
        public List<HourlyMaterialDrop> HourlyMaterials { get; set; } = new();
        public float FragmentChancePerHour { get; set; } = 0.01f;
        public float EquipmentChancePerHour { get; set; } = 0.005f;
        public List<Guid> AvailableCharacterFragments { get; set; } = new();
        public List<Guid> AvailableEquipment { get; set; } = new();
    }

    public class HourlyMaterialDrop
    {
        public Guid MaterialId { get; set; }
        public Guid DungeonId { get; set; }
        public float BaseQuantity { get; set; }
        public float DropRate { get; set; } = 1f;
    }

    public class IdleRewardSummary
    {
        public int HoursAccumulated { get; set; }
        public List<CurrencyAmount> Currencies { get; set; } = new();
        public List<ResourceQuantity> Materials { get; set; } = new();
        public List<ResourceQuantity> Equipment { get; set; } = new();
        public List<ResourceQuantity> CharacterFragments { get; set; } = new();
        public DateTime ClaimedAt { get; set; }
    }
}