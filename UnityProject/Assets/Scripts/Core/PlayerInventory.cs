using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventário simples do player para coletar itens.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [System.Serializable]
    public class ItemStack
    {
        public PickupItem.ItemType type;
        public string name;
        public int count;
        public int maxStack;
        public PickupItem.Rarity rarity;
    }

    public List<ItemStack> items = new List<ItemStack>();
    public int maxSlots = 20;

    public int CurrentGold { get; private set; }
    public int CurrentXP { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TryAddItem(PickupItem pickup)
    {
        switch (pickup.itemType)
        {
            case PickupItem.ItemType.Gold:
                CurrentGold += pickup.value;
                return true;

            case PickupItem.ItemType.XPOrb:
                CurrentXP += pickup.value;
                return true;

            case PickupItem.ItemType.HealthPotion:
                // Aplica cura imediata se player tiver Health component
                if (TryGetComponent<PlayerHealth>(out var health))
                    health.Heal(pickup.value);
                return true;

            case PickupItem.ItemType.ManaPotion:
                if (TryGetComponent<PlayerMana>(out var mana))
                    mana.Restore(pickup.value);
                return true;

            default: // Gem, Key, etc. -> inventário
                return AddToInventory(pickup);
        }
    }

    bool AddToInventory(PickupItem pickup)
    {
        // Tenta empilhar
        var existing = items.Find(i => i.type == pickup.itemType && i.name == pickup.itemName && i.count < i.maxStack);
        if (existing != null)
        {
            existing.count += pickup.value;
            return true;
        }

        // Novo slot
        if (items.Count >= maxSlots)
        {
            Debug.Log("[Inventory] Cheio!");
            return false;
        }

        items.Add(new ItemStack
        {
            type = pickup.itemType,
            name = pickup.itemName,
            count = pickup.value,
            maxStack = pickup.maxStack,
            rarity = pickup.rarity
        });
        return true;
    }

    public void UseItem(int index)
    {
        if (index < 0 || index >= items.Count) return;

        var item = items[index];
        // TODO: Implementar uso por tipo
        item.count--;
        if (item.count <= 0) items.RemoveAt(index);
    }
}

/// <summary>
/// Saúde do player (referenciado pelo inventário para poções)
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    void Awake() => currentHealth = maxHealth;

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Debug.Log($"[Player] Curado: {amount} HP ({currentHealth}/{maxHealth})");
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0) Die();
    }

    void Die() => Debug.Log("[Player] Morreu!");
}

/// <summary>
/// Mana do player
/// </summary>
public class PlayerMana : MonoBehaviour
{
    public int maxMana = 50;
    public int currentMana;

    void Awake() => currentMana = maxMana;

    public void Restore(int amount)
    {
        currentMana = Mathf.Min(maxMana, currentMana + amount);
        Debug.Log($"[Player] Mana restaurada: {amount} ({currentMana}/{maxMana})");
    }

    public bool Spend(int amount)
    {
        if (currentMana >= amount)
        {
            currentMana -= amount;
            return true;
        }
        return false;
    }
}