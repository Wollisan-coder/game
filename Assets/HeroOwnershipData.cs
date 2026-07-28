using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EquippedItem
{
    public EquipmentSlotType slotType;
    public string itemInstanceId; // уникальный ID конкретного стека предмета (не itemId — тот не уникален, потому что
                                   // один и тот же предмет может существовать несколькими стеками разного уровня)
}

[System.Serializable]
public class HeroOwnershipData
{
    public string heroId;
    public bool isUnlocked;
    public int level = 1;
    public int experience = 0; // накопленный опыт в пределах текущего уровня (от предметов-источников опыта)

    [Header("Выбранные навыки")]
    public int activeSkillIndex = 0;   // навык, используемый кнопкой в бою
    public int passiveSkillIndex = -1; // -1 = не выбран

    [Header("Экипированные предметы (по одному на тип слота)")]
    public List<EquippedItem> equippedItems = new List<EquippedItem>();

    public string GetEquippedItemInstanceId(EquipmentSlotType slotType)
    {
        var entry = equippedItems.Find(e => e.slotType == slotType);
        return entry != null ? entry.itemInstanceId : null;
    }

    public void SetEquippedItem(EquipmentSlotType slotType, string itemInstanceId)
    {
        var entry = equippedItems.Find(e => e.slotType == slotType);

        if (entry != null)
            entry.itemInstanceId = itemInstanceId;
        else
            equippedItems.Add(new EquippedItem { slotType = slotType, itemInstanceId = itemInstanceId });
    }
}
