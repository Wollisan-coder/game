using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EquippedItem
{
    public EquipmentSlotType slotType;
    public string itemInstanceId; // унікальний ID конкретного стека предмета (не itemId — той не унікальний, бо
                                   // один і той самий предмет може існувати кількома стеками різного рівня)
}

[System.Serializable]
public class HeroOwnershipData
{
    public string heroId;
    public bool isUnlocked;
    public int level = 1;
    public int experience = 0; // накопичений досвід у межах поточного рівня (від предметів-джерел досвіду)

    [Header("Обрані навички")]
    public int activeSkillIndex = 0;   // навичка, що використовується кнопкою в бою
    public int passiveSkillIndex = -1; // -1 = не обрано

    [Header("Екіпіровані предмети (по одному на тип слота)")]
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
