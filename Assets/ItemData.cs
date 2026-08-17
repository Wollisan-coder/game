using UnityEngine;

public enum EquipmentSlotType
{
    Weapon,
    Armor,
    Accessory,
    Trinket
}

// Категория предмета: обычная экипировка, расходный предмет для прокачки героя, или ваучер разблокировки
// героя (см. HeroCollectionManager.RedeemHeroVoucher/heroVoucherItems, project_death_dungeon_concept) —
// добавлен ПОСЛЕ существующих значений намеренно: enum сериализуется как int, вставка в середину сдвинула
// бы значения у уже сохранённых ассетов.
public enum ItemCategory
{
    Equipment,
    HeroExperience, // СНОВА живой с 2026-08-13 (по прямой просьбе пользователя) — расходуемый предмет,
                     // применимый к ЛЮБОМУ герою, начисляет ItemData.heroExperienceAmount опыта как
                     // альтернативный источник оплаты внутри HeroUpgradeUI (см. ComputeSpendPlan) — наравне
                     // с балансом CurrencyType.HeroExperience. Раньше был мёртвым пережитком старой
                     // системы (см. project_gem_economy_v2_redesign_pending) — история сохранена в памяти,
                     // но актуальное поведение теперь снова реальное. В любом случае НЕ переставлять/не
                     // удалять — HeroVoucher сериализован как int 2 в реальных ассетах.
    HeroVoucher
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

    // Используется только при category == HeroExperience — сколько CurrencyType.HeroExperience (левелап-
    // опыта) начисляет герою применение ОДНОЙ единицы этого предмета, см. HeroUpgradeUI.ComputeSpendPlan.
    // Возвращённый предмет 2026-08-13 по прямой просьбе пользователя — не путать со старой мёртвой веткой,
    // см. комментарий у ItemCategory.HeroExperience.
    [Header("Опыт героя (только category == HeroExperience)")]
    public int heroExperienceAmount = 50;

    // Бонус характеристик экипировки (category == Equipment) больше НЕ авторится здесь — полностью
    // считается из (slotType, rarity, уровень стека) через EquipmentStatCurve.GetValue. Один слот = один
    // стат: Weapon->Damage, Armor->HP, Accessory->Defense/броня, Trinket->Mana.

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
