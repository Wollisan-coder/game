using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemPickerUI : MonoBehaviour
{
    [Header("Посилання")]
    public ItemCollectionManager itemCollectionManager;

    [Header("Список предметів")]
    public Transform itemsContainer;
    public GameObject itemEntryPrefab;

    [Header("Кнопки")]
    public Button closeButton;
    public Button unequipButton; // зняти поточний предмет із цього слота

    private HeroInventoryUI owner;
    private EquipmentSlotType currentSlot;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (unequipButton != null) unequipButton.onClick.AddListener(OnUnequipClicked);

        gameObject.SetActive(false);
    }

    public void Open(EquipmentSlotType slotType, HeroInventoryUI ownerUI)
    {
        currentSlot = slotType;
        owner = ownerUI;

        gameObject.SetActive(true);
        Populate();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Populate()
    {
        if (itemsContainer == null || itemEntryPrefab == null || itemCollectionManager == null) return;

        foreach (Transform child in itemsContainer)
            Destroy(child.gameObject);

        List<ItemData> itemsOfType = itemCollectionManager.GetItemsOfType(currentSlot);

        foreach (var item in itemsOfType)
        {
            GameObject entryObj = Instantiate(itemEntryPrefab, itemsContainer);
            var entry = entryObj.GetComponent<ItemPickerEntryUI>();
            bool owned = itemCollectionManager.IsOwned(item);
            entry.Setup(item, owned, () => OnItemSelected(item));
        }
    }

    private void OnItemSelected(ItemData item)
    {
        owner.EquipItem(currentSlot, item.itemId);
        Close();
    }

    private void OnUnequipClicked()
    {
        owner.EquipItem(currentSlot, null);
        Close();
    }
}
