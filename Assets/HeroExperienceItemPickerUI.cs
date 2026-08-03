using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built window for picking a hero-experience item from inventory to spend on a fixed hero.
// Mirror of HeroExperienceUseUI but the direction is reversed: hero is fixed, item is chosen here.
public class HeroExperienceItemPickerUI : MonoBehaviour
{
    private HeroCollectionManager heroCollectionManager;
    private ItemCollectionManager itemCollectionManager;
    private Transform canvasRoot;

    private GameObject overlayRoot;
    private Transform listContainer;
    private GameObject emptyLabelHolder;

    private Image closeBg;
    private TMP_Text closeButtonText;

    private string heroId;
    private System.Action onApplied;

    public void Open(string heroId, System.Action onApplied = null)
    {
        heroCollectionManager = HeroCollectionManager.Instance;
        itemCollectionManager = ItemCollectionManager.Instance;
        if (heroCollectionManager == null || itemCollectionManager == null) return;

        if (canvasRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRoot = canvas != null ? canvas.transform : transform;
        }

        this.heroId = heroId;
        this.onApplied = onApplied;

        BuildOverlayIfNeeded();
        RefreshButtonTheme();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void RefreshButtonTheme()
    {
        if (closeBg != null) ConfirmationDialog.StyleAsButton(closeBg);
        if (closeButtonText != null) closeButtonText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("HeroExperienceItemPickerOverlay", typeof(RectTransform));
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
        windowRect.sizeDelta = new Vector2(560, 440);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 40);
        titleRect.anchoredPosition = new Vector2(0, -8);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Choose an experience item";
        title.fontSize = 26;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(16, 64);
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
        emptyLabel.text = "No experience items in your inventory";
        emptyLabel.alignment = TextAlignmentOptions.Center;
        emptyLabel.color = new Color(1, 1, 1, 0.6f);
        emptyLabel.fontSize = 18;
        emptyLabelObj.SetActive(false);
        emptyLabelHolder = emptyLabelObj;

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(160, 36);
        closeBtnRect.anchoredPosition = new Vector2(0, 12);
        closeBg = closeBtnObj.AddComponent<Image>();
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
        closeButtonText.text = "Close";
        closeButtonText.alignment = TextAlignmentOptions.Center;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        foreach (Transform child in listContainer)
            Destroy(child.gameObject);

        var candidates = itemCollectionManager.ownership
            .Select(o => (ownership: o, data: itemCollectionManager.GetItemById(o.itemId)))
            .Where(c => c.data != null && c.data.category == ItemCategory.HeroExperience && c.ownership.quantity > 0)
            .ToList();

        if (emptyLabelHolder != null)
            emptyLabelHolder.SetActive(candidates.Count == 0);

        foreach (var candidate in candidates)
            CreateItemEntry(candidate.data, candidate.ownership);
    }

    private void CreateItemEntry(ItemData itemData, ItemOwnershipData ownership)
    {
        var entryObj = new GameObject(itemData.itemId, typeof(RectTransform));
        var entryRect = (RectTransform)entryObj.transform;
        entryRect.SetParent(listContainer, false);

        var bg = entryObj.AddComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.06f);
        var btn = entryObj.AddComponent<Button>();

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(entryRect, false);
        iconRect.anchorMin = new Vector2(0, 0.35f);
        iconRect.anchorMax = new Vector2(1, 1);
        iconRect.offsetMin = new Vector2(6, 0);
        iconRect.offsetMax = new Vector2(-6, -6);
        var icon = iconObj.AddComponent<Image>();
        icon.sprite = itemData.icon;
        icon.preserveAspect = true;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(entryRect, false);
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 0.35f);
        labelRect.offsetMin = new Vector2(4, 2);
        labelRect.offsetMax = new Vector2(-4, 0);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = $"{itemData.itemName}\n+{itemData.heroExperienceValue} Exp.";
        label.fontSize = 12;
        label.alignment = TextAlignmentOptions.Center;
        label.color = itemData.GetRarityColor();

        TMP_Text quantityBadge = null;
        ItemBadgeUtility.ApplyQuantityBadge(iconRect, ownership.quantity, ref quantityBadge);

        string instanceId = ownership.instanceId;
        btn.onClick.AddListener(() => OnItemSelected(itemData, instanceId));
    }

    private void OnItemSelected(ItemData itemData, string instanceId)
    {
        var hero = heroCollectionManager.allHeroes.FirstOrDefault(h => h.heroId == heroId);
        string heroName = hero != null ? hero.heroName : heroId;

        ConfirmationDialog.Show(
            canvasRoot,
            $"Use {itemData.itemName} (+{itemData.heroExperienceValue} XP) on {heroName}?",
            () => Apply(instanceId, itemData.heroExperienceValue));
    }

    private void Apply(string instanceId, int amount)
    {
        if (!heroCollectionManager.GrantExperience(heroId, amount)) return;
        if (!itemCollectionManager.ConsumeItem(instanceId)) return;

        onApplied?.Invoke();
        Populate(); // окно остаётся открытым — можно сразу использовать ещё один предмет
    }
}
