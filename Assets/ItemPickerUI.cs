using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private Button upgradeButton;      // будується програмно — прокачати предмет, що зараз в слоті
    private Image upgradeButtonBg;
    private TMP_Text upgradeButtonText;
    private ItemSacrificeUI sacrificeUI;

    private HeroInventoryUI owner;
    private EquipmentSlotType currentSlot;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (unequipButton != null) unequipButton.onClick.AddListener(OnUnequipClicked);

        CreateUpgradeButtonIfNeeded();

        gameObject.SetActive(false);
    }

    public void Open(EquipmentSlotType slotType, HeroInventoryUI ownerUI)
    {
        currentSlot = slotType;
        owner = ownerUI;

        gameObject.SetActive(true);
        RefreshUpgradeButtonTheme();
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
            int level = owned ? itemCollectionManager.GetLevel(item.itemId) : 0;
            int quantity = owned ? itemCollectionManager.GetQuantity(item.itemId) : 0;
            entry.Setup(item, owned, level, quantity, () => OnItemSelected(item));
        }

        RefreshUpgradeButtonVisibility();
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

    private void OnUpgradeClicked()
    {
        if (owner == null || itemCollectionManager == null) return;

        string equippedId = owner.GetEquippedItemId(currentSlot);
        if (string.IsNullOrEmpty(equippedId)) return;

        if (sacrificeUI == null)
            sacrificeUI = gameObject.AddComponent<ItemSacrificeUI>();

        sacrificeUI.Open(equippedId, Populate);
    }

    // Кнопку "Upgrade" будуємо програмно поруч із Unequip/Close (копіюючи їхній трансформ),
    // щоб не редагувати вручну розмітку ItemPickerPanel у сцені.
    private void CreateUpgradeButtonIfNeeded()
    {
        if (upgradeButton != null) return;

        RectTransform referenceRect = unequipButton != null ? unequipButton.GetComponent<RectTransform>()
            : closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        var upgradeObj = new GameObject("UpgradeButton", typeof(RectTransform));
        var upgradeRect = (RectTransform)upgradeObj.transform;
        upgradeRect.SetParent(referenceRect.parent, false);
        upgradeRect.anchorMin = referenceRect.anchorMin;
        upgradeRect.anchorMax = referenceRect.anchorMax;
        upgradeRect.pivot = referenceRect.pivot;
        upgradeRect.sizeDelta = referenceRect.sizeDelta;
        upgradeRect.anchoredPosition = referenceRect.anchoredPosition + new Vector2(0, referenceRect.sizeDelta.y + 12f);

        upgradeButtonBg = upgradeObj.AddComponent<Image>();
        upgradeButton = upgradeObj.AddComponent<Button>();
        upgradeButton.onClick.AddListener(OnUpgradeClicked);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(upgradeRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        upgradeButtonText = textObj.AddComponent<TextMeshProUGUI>();
        upgradeButtonText.text = "Upgrade";
        upgradeButtonText.alignment = TextAlignmentOptions.Center;

        upgradeButton.gameObject.SetActive(false);
    }

    private void RefreshUpgradeButtonTheme()
    {
        if (upgradeButtonBg != null) upgradeButtonBg.color = ConfirmationDialog.ButtonColor;
        if (upgradeButtonText != null) upgradeButtonText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void RefreshUpgradeButtonVisibility()
    {
        if (upgradeButton == null || owner == null || itemCollectionManager == null) return;

        string equippedId = owner.GetEquippedItemId(currentSlot);
        bool hasEquipped = !string.IsNullOrEmpty(equippedId);

        var equippedData = hasEquipped ? itemCollectionManager.GetItemById(equippedId) : null;
        var equippedOwnership = hasEquipped ? itemCollectionManager.GetOwnership(equippedId) : null;

        bool canUpgrade = equippedData != null && equippedOwnership != null
            && equippedOwnership.level < equippedData.GetMaxLevel()
            && itemCollectionManager.ownership.Any(o => o.itemId != equippedId);

        upgradeButton.gameObject.SetActive(canUpgrade);
    }
}
