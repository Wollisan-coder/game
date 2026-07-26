using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    public Image iconImage;
    public Sprite emptySlotSprite;
    public Button slotButton;

    private EquipmentSlotType slotType;
    private HeroInventoryUI owner;

    public void Setup(EquipmentSlotType type, HeroInventoryUI ownerUI)
    {
        slotType = type;
        owner = ownerUI;

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => owner.OpenItemPicker(slotType));
        }
    }

    public void Refresh(ItemData equippedItem)
    {
        if (iconImage == null) return;

        iconImage.sprite = equippedItem != null ? equippedItem.icon : emptySlotSprite;
    }
}
