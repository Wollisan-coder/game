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

        // Панель вже збережена вимкненою в сцені (m_IsActive: 0) — не гасимо її тут ще раз:
        // якщо викликати SetActive(false) з Awake(), а Awake() вперше запускається САМЕ під час
        // першого Open()->SetActive(true), цей виклик одразу скасовує щойно виконану активацію.
    }

    public void Open(EquipmentSlotType slotType, HeroInventoryUI ownerUI)
    {
        currentSlot = slotType;
        owner = ownerUI;

        transform.SetAsLastSibling(); // інакше панель, з якої відкрито (наприклад, HeroInventoryUI), може перекрити цю зверху
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
            var stacks = itemCollectionManager.GetStacks(item.itemId);

            if (stacks.Count == 0)
            {
                // Не отримано жодної копії — один "заблокований" рядок без можливості вибору
                GameObject lockedObj = Instantiate(itemEntryPrefab, itemsContainer);
                lockedObj.GetComponent<ItemPickerEntryUI>().Setup(item, false, 0, 0, null);
                continue;
            }

            // Кожен стек (окремий рівень) — окремий рядок, щоб предмети різного рівня не зливались в одну ячейку
            foreach (var stack in stacks)
            {
                GameObject entryObj = Instantiate(itemEntryPrefab, itemsContainer);
                var entry = entryObj.GetComponent<ItemPickerEntryUI>();
                string instanceId = stack.instanceId;
                entry.Setup(item, true, stack.level, stack.quantity, () => OnItemSelected(instanceId));
            }
        }

        RefreshUpgradeButtonVisibility();
    }

    private void OnItemSelected(string itemInstanceId)
    {
        // Якщо в стеку більше 1 копії — екіпірувати можна лише ОДНУ, тож відділяємо її в окремий стек,
        // а решта копій лишаються вільними (доступними) в інвентарі предметів.
        string toEquip = itemCollectionManager.SplitOneForEquip(itemInstanceId) ?? itemInstanceId;
        owner.EquipItem(currentSlot, toEquip);
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

        string equippedInstanceId = owner.GetEquippedItemInstanceId(currentSlot);
        if (string.IsNullOrEmpty(equippedInstanceId)) return;

        if (sacrificeUI == null)
            sacrificeUI = gameObject.AddComponent<ItemSacrificeUI>();

        sacrificeUI.Open(equippedInstanceId, Populate);
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

        string equippedInstanceId = owner.GetEquippedItemInstanceId(currentSlot);
        var equippedStack = !string.IsNullOrEmpty(equippedInstanceId)
            ? itemCollectionManager.GetStackByInstanceId(equippedInstanceId)
            : null;
        var equippedData = equippedStack != null ? itemCollectionManager.GetItemById(equippedStack.itemId) : null;

        bool canUpgrade = equippedData != null && equippedStack != null
            && equippedStack.level < equippedData.GetMaxLevel()
            && itemCollectionManager.ownership.Any(o => o.instanceId != equippedStack.instanceId);

        upgradeButton.gameObject.SetActive(canUpgrade);
    }
}
