using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemPickerUI : MonoBehaviour
{
    [Header("Список предметов")]
    public Transform itemsContainer;
    public GameObject itemEntryPrefab;

    [Header("Кнопки")]
    public Button closeButton;
    public Button unequipButton; // снять текущий предмет с этого слота

    private Button upgradeButton;      // строится программно — прокачать предмет, который сейчас в слоте
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

        // Панель уже сохранена выключенной в сцене (m_IsActive: 0) — не гасим её здесь ещё раз:
        // если вызвать SetActive(false) из Awake(), а Awake() впервые запускается ИМЕННО во время
        // первого Open()->SetActive(true), этот вызов сразу отменяет только что выполненную активацию.
    }

    public void Open(EquipmentSlotType slotType, HeroInventoryUI ownerUI)
    {
        currentSlot = slotType;
        owner = ownerUI;

        transform.SetAsLastSibling(); // иначе панель, из которой открыто (например, HeroInventoryUI), может перекрыть эту сверху
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
        if (itemsContainer == null || itemEntryPrefab == null || ItemCollectionManager.Instance == null) return;

        foreach (Transform child in itemsContainer)
            Destroy(child.gameObject);

        List<ItemData> itemsOfType = ItemCollectionManager.Instance.GetItemsOfType(currentSlot);

        foreach (var item in itemsOfType)
        {
            var stacks = ItemCollectionManager.Instance.GetStacks(item.itemId);

            if (stacks.Count == 0)
            {
                // Не получено ни одной копии — одна "заблокированная" строка без возможности выбора
                GameObject lockedObj = Instantiate(itemEntryPrefab, itemsContainer);
                lockedObj.GetComponent<ItemPickerEntryUI>().Setup(item, false, 0, 0, null);
                continue;
            }

            // Каждый стек (отдельный уровень) — отдельная строка, чтобы предметы разного уровня не сливались в одну ячейку
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
        // Если в стеке больше 1 копии — экипировать можно только ОДНУ, поэтому отделяем её в отдельный стек,
        // а остальные копии остаются свободными (доступными) в инвентаре предметов.
        string toEquip = ItemCollectionManager.Instance.SplitOneForEquip(itemInstanceId) ?? itemInstanceId;
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
        if (owner == null || ItemCollectionManager.Instance == null) return;

        string equippedInstanceId = owner.GetEquippedItemInstanceId(currentSlot);
        if (string.IsNullOrEmpty(equippedInstanceId)) return;

        if (sacrificeUI == null)
            sacrificeUI = gameObject.AddComponent<ItemSacrificeUI>();

        sacrificeUI.Open(equippedInstanceId, Populate);
    }

    // Кнопку "Upgrade" строим программно рядом с Unequip/Close (копируя их трансформ),
    // чтобы не редактировать вручную разметку ItemPickerPanel в сцене.
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
        if (upgradeButtonBg != null) ConfirmationDialog.StyleAsButton(upgradeButtonBg);
        if (upgradeButtonText != null) upgradeButtonText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void RefreshUpgradeButtonVisibility()
    {
        if (upgradeButton == null || owner == null || ItemCollectionManager.Instance == null) return;

        string equippedInstanceId = owner.GetEquippedItemInstanceId(currentSlot);
        var equippedStack = !string.IsNullOrEmpty(equippedInstanceId)
            ? ItemCollectionManager.Instance.GetStackByInstanceId(equippedInstanceId)
            : null;
        var equippedData = equippedStack != null ? ItemCollectionManager.Instance.GetItemById(equippedStack.itemId) : null;

        bool canUpgrade = equippedData != null && equippedStack != null
            && equippedStack.level < equippedData.GetMaxLevel()
            && ItemCollectionManager.Instance.ownership.Any(o => o.instanceId != equippedStack.instanceId);

        upgradeButton.gameObject.SetActive(canUpgrade);
    }
}
