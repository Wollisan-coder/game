using UnityEngine;

public enum EquipmentSlotType
{
    Weapon,
    Armor,
    Accessory,
    Trinket
}

// Категория предмета: обычная экипировка или расходный предмет для прокачки героя
public enum ItemCategory
{
    Equipment,
    HeroExperience
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Battle/Item")]
public class ItemData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string itemId;

    [Header("Основные параметры")]
    public string itemName;
    public ItemCategory category = ItemCategory.Equipment;
    public EquipmentSlotType slotType; // учитывается только для category == Equipment
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    // White исключён из выдачи (Алтарь/Кузня больше не спавнят её) — новые ассеты по умолчанию создаются с Green
    [Header("Редкость и прокачка")]
    public Rarity rarity = Rarity.Green;
    public int sacrificeExperience = 10; // опыт, который предмет даёт другому предмету при пожертвовании (масштабируется уровнем предмета-донора)

    [Header("Бонусы характеристик при экипировке (category == Equipment)")]
    public int bonusHealth;
    public int bonusMana;
    public float bonusDamageMultiplier;
    public int bonusArmor; // защита — по конвенции даётся бижутерией (Accessory), но суммируется с любого слота

    [Header("Опыт для героя (category == HeroExperience)")]
    public int heroExperienceValue = 0; // сколько опыта герой получает при использовании этого предмета

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }

    public Color GetRarityColor() => RarityUtility.GetColor(rarity);

    // Максимальный уровень предмета зависит от его редкости
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
