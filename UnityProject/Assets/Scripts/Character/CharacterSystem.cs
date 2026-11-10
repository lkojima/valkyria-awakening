using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Character
{
    public class CharacterSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private CharacterConfig _config;

        private Dictionary<Guid, CharacterData> _characterDatabase = new();
        private Dictionary<Guid, UserCharacterData> _userCharacters = new();
        private Dictionary<Guid, CharacterAssetBundle> _assetBundles = new();

        public IReadOnlyDictionary<Guid, UserCharacterData> UserCharacters => _userCharacters;

        public event System.Action<Guid> OnCharacterAcquired;
        public event System.Action<Guid> OnCharacterUpdated;
        public event System.Action<Guid, int> OnCharacterLeveledUp;
        public event System.Action<Guid> OnCharacterAscended;
        public event System.Action<Guid, SkinData> OnSkinChanged;

        public async UniTask InitializeAsync()
        {
            await LoadCharacterDatabaseAsync();
            await LoadUserCharactersAsync();
        }

        public async UniTask RefreshUserCharactersAsync()
        {
            await LoadUserCharactersAsync();
        }

        private async UniTask LoadCharacterDatabaseAsync()
        {
            var response = await GameManager.Instance.Network.GetAsync<List<CharacterData>>("characters");
            if (response.Success && response.Data != null)
            {
                _characterDatabase.Clear();
                foreach (var character in response.Data)
                {
                    _characterDatabase[character.Id] = character;
                }
            }
        }

        private async UniTask LoadUserCharactersAsync()
        {
            var response = await GameManager.Instance.Network.GetAsync<List<UserCharacterData>>("characters/user");
            if (response.Success && response.Data != null)
            {
                _userCharacters.Clear();
                foreach (var character in response.Data)
                {
                    _userCharacters[character.CharacterId] = character;
                }
            }
        }

        public CharacterData GetCharacterData(Guid characterId)
        {
            _characterDatabase.TryGetValue(characterId, out var data);
            return data;
        }

        public UserCharacterData GetUserCharacter(Guid characterId)
        {
            _userCharacters.TryGetValue(characterId, out var data);
            return data;
        }

        public List<UserCharacterData> GetAllUserCharacters()
        {
            return new List<UserCharacterData>(_userCharacters.Values);
        }

        public List<UserCharacterData> GetUserCharactersByRarity(Rarity rarity)
        {
            var result = new List<UserCharacterData>();
            foreach (var kvp in _userCharacters)
            {
                var charData = GetCharacterData(kvp.Key);
                if (charData != null && charData.Rarity == rarity)
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        public List<UserCharacterData> GetUserCharactersByElement(Element element)
        {
            var result = new List<UserCharacterData>();
            foreach (var kvp in _userCharacters)
            {
                var charData = GetCharacterData(kvp.Key);
                if (charData != null && charData.Element == element)
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        public async UniTask<LevelUpResult> LevelUpAsync(Guid characterId, int targetLevel)
        {
            var userChar = GetUserCharacter(characterId);
            if (userChar == null) return null;

            var charData = GetCharacterData(characterId);
            if (charData == null) return null;

            var cost = CalculateLevelUpCost(userChar.Level, targetLevel, charData.Rarity);

            var response = await GameManager.Instance.Network.PostAsync<LevelUpResult>($"characters/{characterId}/level-up", new
            {
                targetLevel,
                currencyType = (int)CurrencyType.Gold,
                amount = cost
            });

            if (response.Success && response.Data != null)
            {
                userChar.Level = response.Data.NewLevel;
                userChar.Experience = response.Data.RemainingExp;
                OnCharacterLeveledUp?.Invoke(characterId, response.Data.LevelsGained);
                OnCharacterUpdated?.Invoke(characterId);
            }

            return response.Data;
        }

        public async UniTask<AscensionResult> AscendAsync(Guid characterId)
        {
            var userChar = GetUserCharacter(characterId);
            if (userChar == null) return null;

            var response = await GameManager.Instance.Network.PostAsync<AscensionResult>($"characters/{characterId}/ascend");

            if (response.Success && response.Data != null)
            {
                userChar.AscensionLevel = response.Data.NewAscensionLevel;
                OnCharacterAscended?.Invoke(characterId);
                OnCharacterUpdated?.Invoke(characterId);
            }

            return response.Data;
        }

        public async UniTask<SkillUpgradeResult> UpgradeSkillAsync(Guid characterId, SkillType skillType)
        {
            var userChar = GetUserCharacter(characterId);
            if (userChar == null) return null;

            var response = await GameManager.Instance.Network.PostAsync<SkillUpgradeResult>($"characters/{characterId}/skill/upgrade", new
            {
                skillType = (int)skillType
            });

            if (response.Success && response.Data != null)
            {
                switch (skillType)
                {
                    case SkillType.Basic: userChar.Skill1Level = response.Data.NewLevel; break;
                    case SkillType.Special: userChar.Skill2Level = response.Data.NewLevel; break;
                    case SkillType.Ultimate: userChar.UltimateLevel = response.Data.NewLevel; break;
                }
                OnCharacterUpdated?.Invoke(characterId);
            }

            return response.Data;
        }

        public async UniTask<AffinityResult> AddAffinityExpAsync(Guid characterId, long amount)
        {
            var userChar = GetUserCharacter(characterId);
            if (userChar == null) return null;

            var response = await GameManager.Instance.Network.PostAsync<AffinityResult>($"characters/{characterId}/affinity", new
            {
                amount
            });

            if (response.Success && response.Data != null)
            {
                userChar.AffinityLevel = response.Data.NewAffinityLevel;
                userChar.AffinityExperience = response.Data.RemainingExp;
                OnCharacterUpdated?.Invoke(characterId);
            }

            return response.Data;
        }

        public async UniTask<bool> ToggleFavoriteAsync(Guid characterId)
        {
            var response = await GameManager.Instance.Network.PostAsync<bool>($"characters/{characterId}/favorite");
            if (response.Success)
            {
                var userChar = GetUserCharacter(characterId);
                if (userChar != null)
                {
                    userChar.IsFavorite = response.Data;
                    OnCharacterUpdated?.Invoke(characterId);
                }
                return response.Data;
            }
            return false;
        }

        public async UniTask<bool> ToggleLockAsync(Guid characterId)
        {
            var response = await GameManager.Instance.Network.PostAsync<bool>($"characters/{characterId}/lock");
            if (response.Success)
            {
                var userChar = GetUserCharacter(characterId);
                if (userChar != null)
                {
                    userChar.IsLocked = response.Data;
                    OnCharacterUpdated?.Invoke(characterId);
                }
                return response.Data;
            }
            return false;
        }

        public async UniTask<CharacterStats> CalculateStatsAsync(Guid characterId)
        {
            var response = await GameManager.Instance.Network.GetAsync<CharacterStats>($"characters/{characterId}/stats");
            return response.Success ? response.Data : null;
        }

        public async UniTask<CharacterAssetBundle> LoadCharacterAssetsAsync(Guid characterId, SkinData skin = null)
        {
            if (_assetBundles.TryGetValue(characterId, out var cached) && cached.IsLoaded)
            {
                return cached;
            }

            var charData = GetCharacterData(characterId);
            if (charData == null) return null;

            var bundle = new CharacterAssetBundle { CharacterId = characterId };

            // Load portrait
            var portraitHandle = Addressables.LoadAssetAsync<Sprite>(skin?.PortraitPath ?? charData.PortraitAssetPath);
            await portraitHandle.Task;
            if (portraitHandle.Status == AsyncOperationStatus.Succeeded)
            {
                bundle.Portrait = portraitHandle.Result;
            }

            // Load full body
            var fullBodyHandle = Addressables.LoadAssetAsync<Sprite>(skin?.FullBodyPath ?? charData.FullBodyAssetPath);
            await fullBodyHandle.Task;
            if (fullBodyHandle.Status == AsyncOperationStatus.Succeeded)
            {
                bundle.FullBody = fullBodyHandle.Result;
            }

            // Load battle sprite/model
            var spriteHandle = Addressables.LoadAssetAsync<Sprite>(skin?.SpritePath ?? charData.SpriteAssetPath);
            await spriteHandle.Task;
            if (spriteHandle.Status == AsyncOperationStatus.Succeeded)
            {
                bundle.BattleSprite = spriteHandle.Result;
            }

            // Load Live2D if available
            if (!string.IsNullOrEmpty(charData.Live2DModelPath))
            {
                var live2dHandle = Addressables.LoadAssetAsync<GameObject>(skin?.Live2DPath ?? charData.Live2DModelPath);
                await live2dHandle.Task;
                if (live2dHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    bundle.Live2DModel = live2dHandle.Result;
                }
            }

            bundle.IsLoaded = true;
            _assetBundles[characterId] = bundle;

            return bundle;
        }

        public void UnloadCharacterAssets(Guid characterId)
        {
            if (_assetBundles.TryGetValue(characterId, out var bundle))
            {
                if (bundle.Portrait != null) Addressables.Release(bundle.Portrait);
                if (bundle.FullBody != null) Addressables.Release(bundle.FullBody);
                if (bundle.BattleSprite != null) Addressables.Release(bundle.BattleSprite);
                if (bundle.Live2DModel != null) Addressables.Release(bundle.Live2DModel);

                bundle.IsLoaded = false;
            }
        }

        private long CalculateLevelUpCost(int currentLevel, int targetLevel, Rarity rarity)
        {
            long totalCost = 0;
            var baseCost = GetBaseCostForRarity(rarity);

            for (int level = currentLevel; level < targetLevel; level++)
            {
                totalCost += (long)(baseCost * Mathf.Pow(level, 1.5f));
            }

            return totalCost;
        }

        private long GetBaseCostForRarity(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.R => 100,
                Rarity.SR => 200,
                Rarity.SSR => 500,
                Rarity.UR => 1000,
                _ => 100
            };
        }

        public static CharacterSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    [System.Serializable]
    public class CharacterConfig
    {
        public int MaxLevel = 80;
        public int MaxAscension = 6;
        public int MaxSkillLevel = 10;
        public int MaxAffinityLevel = 10;
    }

    [System.Serializable]
    public class CharacterData
    {
        public Guid Id;
        public string Name;
        public string DisplayName;
        public string Description;
        public string Lore;
        public Rarity Rarity;
        public Element Element;
        public CharacterClass Class;
        public CharacterRole Role;
        public int BaseHP;
        public int BaseAttack;
        public int BaseDefense;
        public int BaseSpeed;
        public float BaseCritRate;
        public float BaseCritDamage;
        public float BaseResistance;
        public float BaseAccuracy;
        public float HPGrowth;
        public float AttackGrowth;
        public float DefenseGrowth;
        public float SpeedGrowth;
        public string PortraitAssetPath;
        public string FullBodyAssetPath;
        public string SpriteAssetPath;
        public string Live2DModelPath;
        public Guid BasicSkillId;
        public Guid SpecialSkill1Id;
        public Guid SpecialSkill2Id;
        public Guid UltimateSkillId;
        public int MaxLevel;
        public int MaxAscension;
    }

    [System.Serializable]
    public class UserCharacterData
    {
        public Guid Id;
        public Guid CharacterId;
        public int Level;
        public int Experience;
        public int AscensionLevel;
        public int AffinityLevel;
        public long AffinityExperience;
        public int Skill1Level;
        public int Skill2Level;
        public int Skill3Level;
        public int UltimateLevel;
        public List<Guid> EquippedEquipmentIds;
        public Guid? EquippedRelicId;
        public List<Guid> EquippedRuneIds;
        public Guid? CurrentSkinId;
        public bool IsFavorite;
        public bool IsLocked;
        public DateTimeOffset AcquiredAt;
        public int LimitBreakCount;
    }

    [System.Serializable]
    public class CharacterAssetBundle
    {
        public Guid CharacterId;
        public Sprite Portrait;
        public Sprite FullBody;
        public Sprite BattleSprite;
        public GameObject Live2DModel;
        public bool IsLoaded;
    }

    [System.Serializable]
    public class LevelUpResult
    {
        public int NewLevel;
        public int LevelsGained;
        public int RemainingExp;
        public long GoldSpent;
    }

    [System.Serializable]
    public class AscensionResult
    {
        public int NewAscensionLevel;
        public List<AscensionBonus> Bonuses;
    }

    [System.Serializable]
    public class AscensionBonus
    {
        public string StatName;
        public float Value;
    }

    [System.Serializable]
    public class SkillUpgradeResult
    {
        public SkillType SkillType;
        public int NewLevel;
        public float NewMultiplier;
    }

    [System.Serializable]
    public class AffinityResult
    {
        public int NewAffinityLevel;
        public long RemainingExp;
        public List<AffinityReward> Rewards;
    }

    [System.Serializable]
    public class AffinityReward
    {
        public int AffinityLevel;
        public RewardType RewardType;
        public Guid ResourceId;
        public int Quantity;
    }

    [System.Serializable]
    public class CharacterStats
    {
        public int HP;
        public int Attack;
        public int Defense;
        public int Speed;
        public float CritRate;
        public float CritDamage;
        public float Resistance;
        public float Accuracy;
        public Dictionary<string, float> ElementalBonuses;
    }

    public enum SkillType
    {
        Basic = 1,
        Special = 2,
        Ultimate = 3
    }

    public enum Rarity
    {
        R = 1,
        SR = 2,
        SSR = 3,
        UR = 4
    }

    public enum Element
    {
        None = 0,
        Fire = 1,
        Water = 2,
        Nature = 3,
        Light = 4,
        Dark = 5
    }

    public enum CharacterClass
    {
        Warrior = 1,
        Mage = 2,
        Archer = 3,
        Assassin = 4,
        Tank = 5,
        Support = 6,
        Healer = 7,
        Summoner = 8,
        Bard = 9,
        Paladin = 10
    }

    public enum CharacterRole
    {
        DPS = 1,
        Tank = 2,
        Support = 3,
        Healer = 4,
        Controller = 5,
        Buffer = 6,
        Debuffer = 7,
        Hybrid = 8
    }

    public enum RewardType
    {
        Currency = 1,
        Item = 2,
        Character = 3,
        CharacterFragment = 4,
        Equipment = 5,
        Relic = 6,
        Rune = 7,
        Skin = 8,
        Experience = 9
    }
}