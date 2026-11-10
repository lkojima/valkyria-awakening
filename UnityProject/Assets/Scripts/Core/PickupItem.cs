using UnityEngine;

/// <summary>
/// Item coletável no chão. Configurado via JSON do PrefabGenerator.
/// </summary>
public class PickupItem : MonoBehaviour
{
    public enum ItemType
    {
        HealthPotion = 0,
        ManaPotion = 1,
        Gold = 2,
        Gem = 3,
        XPOrb = 4,
        Key = 5
    }

    public enum Rarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

    [Header("Info")]
    public string itemName = "Item";
    public ItemType itemType = ItemType.Gold;
    public Rarity rarity = Rarity.Common;
    [TextArea] public string description = "";
    public int maxStack = 1;

    [Header("Value")]
    public int value = 1; // cura, mana, gold, xp, etc.

    [Header("Visual")]
    public float floatAmplitude = 0.2f;
    public float floatFrequency = 1.5f;
    public float rotationSpeed = 90f;

    Vector3 _startPos;
    float _timeOffset;

    void Awake()
    {
        _startPos = transform.position;
        _timeOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        // Animação de flutuação
        float y = Mathf.Sin((Time.time + _timeOffset) * floatFrequency) * floatAmplitude;
        transform.position = _startPos + Vector3.up * y;

        // Rotação lenta
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<PlayerInventory>();
        if (player != null && player.TryAddItem(this))
        {
            // Feedback visual/sonoro
            Debug.Log($"[Pickup] Player coletou: {itemName} x{value}");
            Destroy(gameObject);
        }
    }
}