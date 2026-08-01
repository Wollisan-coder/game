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

    // Бонус характеристик экипировки (category == Equipment) больше НЕ авторится здесь — полностью
    // считается из (slotType, rarity, уровень стека) через EquipmentStatCurve.GetValue. Один слот = один
    // стат: Weapon->Damage, Armor->HP, Accessory->Defense/броня, Trinket->Mana.

    [Header("Опыт для героя (category == HeroExperience)")]
    public int heroExperienceValue = 0; // сколько опыта герой получает при использовании этого предмета

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }

    public Color GetRarityColor() => RarityUtility.GetColor(rarity);

    // Единый кап уровня для всех редкостей (см. EquipmentStatCurve) — редкость задаёт стартовое/финишное
    // ЗНАЧЕНИЕ бонуса, не диапазон уровней.
    public int GetMaxLevel() => EquipmentStatCurve.MaxLevel;
}
