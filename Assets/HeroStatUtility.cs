using UnityEngine;

// Суммарные бонусы от всей экипировки героя — общий расчёт, чтобы не дублировать одну и ту же
// логику суммирования и в BattleManager (реальный бой), и в HeroInventoryUI (отображение до боя).
[System.Serializable]
public struct EquipmentBonuses
{
    public int health;
    public int mana;
    public float damageMultiplier;
    public int armor;
}

// Базовые статы героя ПОСЛЕ бонуса от уровня, но ДО экипировки — отдельная структура, чтобы не дублировать
// округление/умножение в HeroRuntimeState, HeroInventoryUI и CalculatePower по отдельности.
[System.Serializable]
public struct HeroBaseStats
{
    public int health;
    public int mana;
    public float damageMultiplier;
    public int armor;
}

public static class HeroStatUtility
{
    // Скромный бонус от уровня героя сверху базовых статов — экипировка остаётся основным рычагом силы,
    // уровень даёт лишь +1%/уровень (ур.40 → x1.39). Линейные 10%/уровень, как у предметов, тут не годятся —
    // уровень героя доходит до 160 (Orange + полное вознесение), это дало бы абсурдный множитель.
    public static float GetHeroLevelMultiplier(int level) => 1f + 0.01f * Mathf.Max(0, level - 1);

    // Вознесение даёт заметнее уровня (редкий ресурс — дубли с гачи, не капающий фарм) — +5%/ступень.
    // Purple макс 2 ступени (+10% на пике), Orange макс 3 (+15%). Начисляется НА КАЖДОЙ ступени, не только
    // на финальной — см. project_hero_ascension_system.
    public static float GetAscensionMultiplier(int ascensionLevel) => 1f + 0.05f * Mathf.Max(0, ascensionLevel);

    public static HeroBaseStats CalculateBaseStats(HeroData hero, int level, int ascensionLevel = 0)
    {
        float mult = GetHeroLevelMultiplier(level) * GetAscensionMultiplier(ascensionLevel);
        return new HeroBaseStats
        {
            health = Mathf.RoundToInt(hero.maxHealth * mult),
            mana = Mathf.RoundToInt(hero.maxResource * mult),
            damageMultiplier = hero.damageMultiplier * mult,
            armor = Mathf.RoundToInt(hero.armor * mult),
        };
    }

    public static EquipmentBonuses CalculateEquipmentBonuses(HeroOwnershipData ownership)
    {
        var bonuses = new EquipmentBonuses();
        if (ownership == null || ItemCollectionManager.Instance == null) return bonuses;

        foreach (var equipped in ownership.equippedItems)
        {
            var stack = ItemCollectionManager.Instance.GetStackByInstanceId(equipped.itemInstanceId);
            if (stack == null) continue;

            var item = ItemCollectionManager.Instance.GetItemById(stack.itemId);
            if (item == null) continue;

            // Один слот = один стат (см. EquipmentStatCurve) — значение целиком считается из
            // (тип слота, редкость, уровень стека), больше не читается с полей ItemData.bonus*.
            switch (item.slotType)
            {
                case EquipmentSlotType.Armor:
                    bonuses.health += Mathf.RoundToInt(EquipmentStatCurve.GetValue(item.slotType, item.rarity, stack.level));
                    break;
                case EquipmentSlotType.Trinket:
                    bonuses.mana += Mathf.RoundToInt(EquipmentStatCurve.GetValue(item.slotType, item.rarity, stack.level));
                    break;
                case EquipmentSlotType.Weapon:
                    bonuses.damageMultiplier += EquipmentStatCurve.GetValue(item.slotType, item.rarity, stack.level);
                    break;
                case EquipmentSlotType.Accessory:
                    bonuses.armor += Mathf.RoundToInt(EquipmentStatCurve.GetValue(item.slotType, item.rarity, stack.level));
                    break;
            }
        }

        return bonuses;
    }

    // Итоговый максимум маны (база×бонус уровня/вознесения + бонус экипировки, минус фикс. стоимость
    // пассивки расы, если включена) — общий расчёт для HeroInventoryUI (текущий герой) и HeroUpgradeUI
    // (превью на отложенном, ещё не подтверждённом уровне), level/ascensionLevel передаются явно, чтобы
    // оба места могли подставить как реальные, так и гипотетические значения без мутации ownership.
    public static int CalculateMaxMana(HeroData hero, HeroOwnershipData ownership, int level, int ascensionLevel)
    {
        if (hero == null) return 0;

        int total = CalculateBaseStats(hero, level, ascensionLevel).mana + CalculateEquipmentBonuses(ownership).mana;
        if (ownership != null && ownership.racePassiveEnabled)
            total = Mathf.Max(1, total - RacePassiveUtility.ManaCost);
        return total;
    }

    // "Мощь" — единая цифра для сравнения героев в коллекции (сортировка/фильтр) и для отображения на
    // карточке героя как "Might" (см. UX-правку 2026-08-18). Веса — плейсхолдер, легко поменять: мана и
    // броня ценятся выше HP за единицу, множитель урона переведён в очки как разница от базового x1.
    public static int CalculatePower(HeroData hero, HeroOwnershipData ownership)
        => CalculatePower(hero, ownership, ownership?.level ?? 1, ownership?.ascensionLevel ?? 0);

    // Явные level/ascensionLevel — тот же приём, что CalculateMaxMana выше: превью гипотетического
    // уровня/вознесения (HeroUpgradeUI, OpenAscendPopup) без мутации ownership. Экипировка от
    // level/ascensionLevel не зависит, поэтому bonuses всегда считается от РЕАЛЬНОГО ownership.
    public static int CalculatePower(HeroData hero, HeroOwnershipData ownership, int level, int ascensionLevel)
    {
        if (hero == null) return 0;

        var baseStats = CalculateBaseStats(hero, level, ascensionLevel);
        var bonuses = CalculateEquipmentBonuses(ownership);

        int health = baseStats.health + bonuses.health;
        int mana = baseStats.mana + bonuses.mana;
        int armor = baseStats.armor + bonuses.armor;
        float damageMultiplier = baseStats.damageMultiplier + bonuses.damageMultiplier;

        return health + mana * 2 + armor * 5 + Mathf.RoundToInt(damageMultiplier * 100f);
    }
}
