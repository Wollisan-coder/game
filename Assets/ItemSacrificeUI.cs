using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built window for selecting donor items to sacrifice (levels up the target item).
// Supports selecting several items at once; once the predicted level reaches the cap,
// the remaining candidates get locked out and a popup explains why. Built entirely in code.
public class ItemSacrificeUI : MonoBehaviour
{
    private static readonly Color NormalBgColor = new Color(1, 1, 1, 0.06f);
    private static readonly Color SelectedBgColor = new Color(0.3f, 0.65f, 0.35f, 0.5f);

    private ItemCollectionManager itemCollectionManager;
    private Transform canvasRoot;

    private GameObject overlayRoot;
    private Transform listContainer;
    private TMP_Text summaryText;
    private Button confirmButton;
    private GameObject emptyLabelHolder;

    private Image confirmBg;
    private TMP_Text confirmButtonText;
    private Image closeBg;
    private TMP_Text closeButtonText;

    private string targetItemId;
    private System.Action onApplied;
    private int baseLevel;
    private int baseExperience;
    private int maxLevel;

    private readonly HashSet<string> selectedDonorIds = new HashSet<string>();
    private readonly Dictionary<string, Button> donorButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Image> donorBackgrounds = new Dictionary<string, Image>();

    // targetId — предмет, який прокачуємо. onApplied — викликається після кожного успішного пожертвування
    // (щоб викликач міг оновити свій власний UI: деталі предмета, слот екіпіровки тощо).
    public void Open(string targetId, System.Action onApplied = null)
    {
        itemCollectionManager = ItemCollectionManager.Instance;
        if (itemCollectionManager == null) return;

        var targetData = itemCollectionManager.GetItemById(targetId);
        var targetOwnership = itemCollectionManager.GetOwnership(targetId);
        if (targetData == null || targetOwnership == null) return;

        if (canvasRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRoot = canvas != null ? canvas.transform : transform;
        }

        targetItemId = targetId;
        this.onApplied = onApplied;
        baseLevel = targetOwnership.level;
        baseExperience = targetOwnership.experience;
        maxLevel = targetData.GetMaxLevel();
        selectedDonorIds.Clear();

        BuildOverlayIfNeeded();
        RefreshButtonTheme();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    // Перечитує кольори кнопок при кожному відкритті — щоб правки палітри в коді
    // одразу застосовувались і до вже раз побудованого (закешованого) вікна, без рестарту Play Mode.
    private void RefreshButtonTheme()
    {
        if (confirmBg != null) confirmBg.color = ConfirmationDialog.ButtonColor;
        if (confirmButtonText != null) confirmButtonText.color = ConfirmationDialog.ButtonTextColor;
        if (closeBg != null) closeBg.color = ConfirmationDialog.ButtonColor;
        if (closeButtonText != null) closeButtonText.color = ConfirmationDialog.ButtonTextColor;
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("ItemSacrificeOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);

        var closeAreaObj = new GameObject("CloseArea", typeof(RectTransform));
        var closeAreaRect = (RectTransform)closeAreaObj.transform;
        closeAreaRect.SetParent(overlayRect, false);
        closeAreaRect.anchorMin = Vector2.zero;
        closeAreaRect.anchorMax = Vector2.one;
        closeAreaRect.offsetMin = Vector2.zero;
        closeAreaRect.offsetMax = Vector2.zero;
        var closeAreaImg = closeAreaObj.AddComponent<Image>();
        closeAreaImg.color = new Color(0, 0, 0, 0);
        var closeAreaBtn = closeAreaObj.AddComponent<Button>();
        closeAreaBtn.transition = Selectable.Transition.None;
        closeAreaBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(560, 480);
        var windowBg = windowObj.AddComponent<Image>();
        windowBg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 40);
        titleRect.anchoredPosition = new Vector2(0, -8);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Sacrifice items for experience";
        title.fontSize = 28;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(16, 112);
        scrollRect.offsetMax = new Vector2(-16, -56);

        var scroll = scrollObj.AddComponent<ScrollRect>();
        var scrollImage = scrollObj.AddComponent<Image>();
        scrollImage.color = new Color(1, 1, 1, 0.03f);
        var scrollMask = scrollObj.AddComponent<Mask>();
        scrollMask.showMaskGraphic = true;

        var contentObj = new GameObject("Content", typeof(RectTransform));
        var contentRect = (RectTransform)contentObj.transform;
        contentRect.SetParent(scrollRect, false);
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 120);
        grid.spacing = new Vector2(10, 10);
        grid.padding = new RectOffset(6, 6, 6, 6);

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        listContainer = contentRect;

        var emptyLabelObj = new GameObject("EmptyLabel", typeof(RectTransform));
        var emptyLabelRect = (RectTransform)emptyLabelObj.transform;
        emptyLabelRect.SetParent(windowRect, false);
        emptyLabelRect.anchorMin = new Vector2(0, 0.3f);
        emptyLabelRect.anchorMax = new Vector2(1, 0.7f);
        emptyLabelRect.offsetMin = new Vector2(16, 0);
        emptyLabelRect.offsetMax = new Vector2(-16, 0);
        var emptyLabel = emptyLabelObj.AddComponent<TextMeshProUGUI>();
        emptyLabel.text = "No other items available to sacrifice";
        emptyLabel.alignment = TextAlignmentOptions.Center;
        emptyLabel.color = new Color(1, 1, 1, 0.6f);
        emptyLabel.fontSize = 18;
        emptyLabelObj.SetActive(false);
        emptyLabelHolder = emptyLabelObj;

        var summaryObj = new GameObject("Summary", typeof(RectTransform));
        var summaryRect = (RectTransform)summaryObj.transform;
        summaryRect.SetParent(windowRect, false);
        summaryRect.anchorMin = new Vector2(0, 0);
        summaryRect.anchorMax = new Vector2(1, 0);
        summaryRect.pivot = new Vector2(0.5f, 0);
        summaryRect.sizeDelta = new Vector2(-32, 56);
        summaryRect.anchoredPosition = new Vector2(0, 52);
        summaryText = summaryObj.AddComponent<TextMeshProUGUI>();
        summaryText.fontSize = 16;
        summaryText.alignment = TextAlignmentOptions.Center;
        summaryText.color = Color.white;

        var confirmObj = new GameObject("ConfirmButton", typeof(RectTransform));
        var confirmRect = (RectTransform)confirmObj.transform;
        confirmRect.SetParent(windowRect, false);
        confirmRect.anchorMin = new Vector2(0.3f, 0);
        confirmRect.anchorMax = new Vector2(0.3f, 0);
        confirmRect.pivot = new Vector2(0.5f, 0);
        confirmRect.sizeDelta = new Vector2(180, 38);
        confirmRect.anchoredPosition = new Vector2(0, 10);
        confirmBg = confirmObj.AddComponent<Image>();
        confirmBg.color = ConfirmationDialog.ButtonColor;
        confirmButton = confirmObj.AddComponent<Button>();
        confirmButton.onClick.AddListener(OnConfirmClicked);

        var confirmTextObj = new GameObject("Text", typeof(RectTransform));
        var confirmTextRect = (RectTransform)confirmTextObj.transform;
        confirmTextRect.SetParent(confirmRect, false);
        confirmTextRect.anchorMin = Vector2.zero;
        confirmTextRect.anchorMax = Vector2.one;
        confirmTextRect.offsetMin = Vector2.zero;
        confirmTextRect.offsetMax = Vector2.zero;
        confirmButtonText = confirmTextObj.AddComponent<TextMeshProUGUI>();
        confirmButtonText.text = "Confirm";
        confirmButtonText.alignment = TextAlignmentOptions.Center;
        confirmButtonText.color = ConfirmationDialog.ButtonTextColor;

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.7f, 0);
        closeBtnRect.anchorMax = new Vector2(0.7f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(180, 38);
        closeBtnRect.anchoredPosition = new Vector2(0, 10);
        closeBg = closeBtnObj.AddComponent<Image>();
        closeBg.color = ConfirmationDialog.ButtonColor;
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeBtnRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        closeButtonText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeButtonText.text = "Cancel";
        closeButtonText.alignment = TextAlignmentOptions.Center;
        closeButtonText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        foreach (Transform child in listContainer)
            Destroy(child.gameObject);

        donorButtons.Clear();
        donorBackgrounds.Clear();

        var candidates = itemCollectionManager.ownership
            .Where(o => o.itemId != targetItemId)
            .Select(o => (ownership: o, data: itemCollectionManager.GetItemById(o.itemId)))
            .Where(c => c.data != null && c.data.category == ItemCategory.Equipment) // предмети досвіду героя сюди не годяться
            .ToList();

        if (emptyLabelHolder != null)
            emptyLabelHolder.SetActive(candidates.Count == 0);

        foreach (var candidate in candidates)
            CreateDonorEntry(candidate.data, candidate.ownership);

        RefreshSelectionVisuals();
        UpdateSummary();
    }

    private void CreateDonorEntry(ItemData donorData, ItemOwnershipData donorOwnership)
    {
        var entryObj = new GameObject(donorData.itemId, typeof(RectTransform));
        var entryRect = (RectTransform)entryObj.transform;
        entryRect.SetParent(listContainer, false);

        var bg = entryObj.AddComponent<Image>();
        bg.color = NormalBgColor;
        var btn = entryObj.AddComponent<Button>();

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(entryRect, false);
        iconRect.anchorMin = new Vector2(0, 0.35f);
        iconRect.anchorMax = new Vector2(1, 1);
        iconRect.offsetMin = new Vector2(6, 0);
        iconRect.offsetMax = new Vector2(-6, -6);
        var icon = iconObj.AddComponent<Image>();
        icon.sprite = donorData.icon;
        icon.preserveAspect = true;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(entryRect, false);
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 0.35f);
        labelRect.offsetMin = new Vector2(4, 2);
        labelRect.offsetMax = new Vector2(-4, 0);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        int xp = donorData.sacrificeExperience * donorOwnership.level;
        string qtySuffix = donorOwnership.quantity > 1 ? $" x{donorOwnership.quantity}" : "";
        label.text = $"{donorData.itemName}{qtySuffix}\nLvl.{donorOwnership.level}  +{xp} Exp.";
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.Center;
        label.color = donorData.GetRarityColor();

        string donorId = donorData.itemId;
        btn.onClick.AddListener(() => ToggleDonor(donorId));

        donorButtons[donorId] = btn;
        donorBackgrounds[donorId] = bg;
    }

    private int SumSelectedXp()
    {
        int sum = 0;
        foreach (var id in selectedDonorIds)
        {
            var data = itemCollectionManager.GetItemById(id);
            var ownership = itemCollectionManager.GetOwnership(id);
            if (data != null && ownership != null)
                sum += data.sacrificeExperience * ownership.level;
        }
        return sum;
    }

    private void ToggleDonor(string donorId)
    {
        if (selectedDonorIds.Contains(donorId))
        {
            selectedDonorIds.Remove(donorId);
        }
        else
        {
            var beforeResult = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (beforeResult.level >= maxLevel)
            {
                ConfirmationDialog.ShowInfo(canvasRoot, $"Max level reached ({maxLevel}).\nNo need to select more items.");
                return;
            }

            selectedDonorIds.Add(donorId);

            var afterResult = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (afterResult.wastedExperience > 0)
            {
                ConfirmationDialog.ShowInfo(canvasRoot,
                    $"Level {maxLevel} will be reached.\nExperience above the cap ({afterResult.wastedExperience}) will be lost.");
            }
        }

        RefreshSelectionVisuals();
        UpdateSummary();
    }

    private void RefreshSelectionVisuals()
    {
        var result = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
        bool atMax = result.level >= maxLevel;

        foreach (var kvp in donorButtons)
        {
            bool isSelected = selectedDonorIds.Contains(kvp.Key);
            kvp.Value.interactable = isSelected || !atMax;
            donorBackgrounds[kvp.Key].color = isSelected ? SelectedBgColor : NormalBgColor;
        }
    }

    private void UpdateSummary()
    {
        var result = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);

        if (selectedDonorIds.Count == 0)
        {
            summaryText.text = $"Selected: 0\nCurrent level: {baseLevel}/{maxLevel}";
        }
        else
        {
            string expLine = result.level >= maxLevel ? "MAX" : $"{result.experience}/{itemCollectionManager.ExperienceToNextLevel(result.level)}";
            summaryText.text = $"Selected: {selectedDonorIds.Count}\nPreview: level {result.level}/{maxLevel} ({expLine})";
        }

        if (confirmButton != null)
            confirmButton.interactable = selectedDonorIds.Count > 0;
    }

    private void OnConfirmClicked()
    {
        if (selectedDonorIds.Count == 0) return;

        int count = selectedDonorIds.Count;
        ConfirmationDialog.Show(
            canvasRoot,
            $"Sacrifice {count} item(s)?\nThis cannot be undone.",
            ApplySacrifice);
    }

    private void ApplySacrifice()
    {
        int totalWasted = 0;
        var idsToSacrifice = new List<string>(selectedDonorIds);

        foreach (var donorId in idsToSacrifice)
        {
            if (itemCollectionManager.SacrificeItem(donorId, targetItemId, out int wasted))
                totalWasted += wasted;
        }

        selectedDonorIds.Clear();

        var targetOwnership = itemCollectionManager.GetOwnership(targetItemId);
        if (targetOwnership != null)
        {
            baseLevel = targetOwnership.level;
            baseExperience = targetOwnership.experience;
        }

        Populate();
        onApplied?.Invoke();

        if (totalWasted > 0)
            ConfirmationDialog.ShowInfo(canvasRoot, $"Experience above max level lost: {totalWasted}");
    }
}
