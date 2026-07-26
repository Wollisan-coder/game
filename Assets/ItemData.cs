using UnityEngine;

public enum EquipmentSlotType
{
    Weapon,
    Armor,
    Accessory,
    Trinket
}

// Категорія предмета: звичайна екіпіровка або витратний предмет для прокачки героя
public enum ItemCategory
{
    Equipment,
    HeroExperience
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Battle/Item")]
public class ItemData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string itemId;

    [Header("Основні параметри")]
    public string itemName;
    public ItemCategory category = ItemCategory.Equipment;
    public EquipmentSlotType slotType; // враховується лише для category == Equipment
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Рідкість і прокачка")]
    public Rarity rarity = Rarity.White;
    public int sacrificeExperience = 10; // досвід, який предмет дає іншому предмету при пожертвуванні (масштабується рівнем предмета-донора)

    [Header("Бонуси характеристик при екіпіровці (category == Equipment)")]
    public int bonusHealth;
    public int bonusMana;
    public float bonusDamageMultiplier;

    [Header("Досвід для героя (category == HeroExperience)")]
    public int heroExperienceValue = 0; // скільки досвіду герой отримує при використанні цього предмета

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }

    public Color GetRarityColor() => RarityUtility.GetColor(rarity);

    // Максимальний рівень предмета залежить від його рідкості
    public int GetMaxLevel()
    {
        switch (rarity)
        {
            case Rarity.White: return 20;
            case Rarity.Green: return 40;
            case Rarity.Blue: return 60;
            case Rarity.Purple: return 80;
            case Rarity.Orange: return 100;
            default: return 20;
        }
    }
}
