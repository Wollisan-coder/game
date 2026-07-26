using UnityEngine;

public enum EquipmentSlotType
{
    Weapon,
    Armor,
    Accessory,
    Trinket
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Battle/Item")]
public class ItemData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string itemId;

    [Header("Основні параметри")]
    public string itemName;
    public EquipmentSlotType slotType;
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Бонуси характеристик при екіпіровці")]
    public int bonusHealth;
    public int bonusMana;
    public float bonusDamageMultiplier;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }
}
