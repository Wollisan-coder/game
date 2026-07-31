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

public static class HeroStatUtility
{
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

            float levelMultiplier = ItemCollectionManager.Instance.GetLevelMultiplierForLevel(stack.level);

            bonuses.health += Mathf.RoundToInt(item.bonusHealth * levelMultiplier);
            bonuses.mana += Mathf.RoundToInt(item.bonusMana * levelMultiplier);
            bonuses.damageMultiplier += item.bonusDamageMultiplier * levelMultiplier;
            bonuses.armor += Mathf.RoundToInt(item.bonusArmor * levelMultiplier);
        }

        return bonuses;
    }
}
