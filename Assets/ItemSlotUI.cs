using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    public Image iconImage;
    public Sprite emptySlotSprite;
    public Button slotButton;

    private EquipmentSlotType slotType;
    private HeroInventoryUI owner;

    public void Setup(EquipmentSlotType type, HeroInventoryUI ownerUI, Sprite emptySprite = null)
    {
        slotType = type;
        owner = ownerUI;

        // Позволяет вызывающему коду задать свою картинку пустого слота на конкретный тип экипировки
        // (см. HeroInventoryUI.PopulateItems) — иначе все 4 слота показывают один и тот же спрайт,
        // запечённый в общем itemSlotPrefab. Если не передали — оставляем то, что уже на префабе.
        if (emptySprite != null)
            emptySlotSprite = emptySprite;

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
