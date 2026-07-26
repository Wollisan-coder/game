using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EquippedItem
{
    public EquipmentSlotType slotType;
    public string itemId;
}

[System.Serializable]
public class HeroOwnershipData
{
    public string heroId;
    public bool isUnlocked;
    public int level = 1;

    [Header("Обрані навички")]
    public int activeSkillIndex = 0;   // навичка, що використовується кнопкою в бою
    public int passiveSkillIndex = -1; // -1 = не обрано

    [Header("Екіпіровані предмети (по одному на тип слота)")]
    public List<EquippedItem> equippedItems = new List<EquippedItem>();

    public string GetEquippedItemId(EquipmentSlotType slotType)
    {
        var entry = equippedItems.Find(e => e.slotType == slotType);
        return entry != null ? entry.itemId : null;
    }

    public void SetEquippedItem(EquipmentSlotType slotType, string itemId)
    {
        var entry = equippedItems.Find(e => e.slotType == slotType);

        if (entry != null)
            entry.itemId = itemId;
        else
            equippedItems.Add(new EquippedItem { slotType = slotType, itemId = itemId });
    }
}
