using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built window for selecting donor items to sacrifice (levels up the target item).
// Selection is per-copy: each click on a stack adds exactly ONE unit of it to the selection
// (clicking again adds another, up to its owned quantity; clicking once more after reaching
// the cap resets that stack's selection back to 0). Different stacks can be mixed freely.
// Once the predicted level reaches the cap, the remaining candidates get locked out and a popup explains why. Built entirely in code.
public class ItemSacrificeUI : MonoBehaviour
{
    private static readonly Color NormalBgColor = new Color(1, 1, 1, 0.06f);
    private static readonly Color SelectedBgColor = new Color(0.3f, 0.65f, 0.35f, 0.5f);

    private ItemCollectionManager itemCollectionManager;
    private Transform canvasRoot;

    private GameObject overlayRoot;
    private RectTransform windowRect;
    private Image commitFlashOverlay;
    private Coroutine commitFlashRoutine;
    private Transform listContainer;
    private TMP_Text summaryText;
    private Button confirmButton;
    private GameObject emptyLabelHolder;

    private Image confirmBg;
    private TMP_Text confirmButtonText;
    private Image closeBg;
    private TMP_Text closeButtonText;

    // Степпер вокруг центрального индикатора уровня: -10 -1 [Level X/Y] +1 +10 Max. Значения — СДВИГ
    // относительно текущего превью (не committed baseLevel), см. AdjustPreviewTarget — поэтому +1/+10
    // можно жать многократно подряд, пока хватает донор-предметов (раньше был жёсткий baseLevel+шаг,
    // из-за чего повторный клик по "+1" ничего не менял — жалоба пользователя 2026-08-12, здесь исправлено).
    // Текст переехал с самих кнопок (было мелко/нечитаемо) в один общий центральный индикатор.
    private static readonly int[] QuickSelectDeltas = { -10, -1, 1, 10 };
    private static readonly string[] QuickSelectLabels = { "-10", "-1", "+1", "+10" };
    private readonly Button[] quickSelectButtons = new Button[QuickSelectDeltas.Length];
    private readonly Image[] quickSelectBgs = new Image[QuickSelectDeltas.Length];
    private readonly TMP_Text[] quickSelectTexts = new TMP_Text[QuickSelectDeltas.Length];
    private Button maxLevelButton;
    private Image maxLevelBg;
    private TMP_Text maxLevelText;
    private Image centerLevelBg;
    private TMP_Text centerLevelText;

    private string targetInstanceId;
    private ItemData targetItemData; // нужен для CalculateSacrificeGain (мердж редкости зависит от rarity цели)
    private System.Action onApplied;
    private int baseLevel;
    private int baseExperience;
    private int maxLevel;

    // instanceId стека, который сейчас фактически несёт уровень цели — может меняться во время пакетного
    // пожертвования (если цель делится на новый стек или сливается с существующим-близнецом)
    public string CurrentTargetInstanceId => targetInstanceId;

    // instanceId стека -> сколько единиц именно из него выбрано (0..quantity этого стека)
    private readonly Dictionary<string, int> selectedDonorCounts = new Dictionary<string, int>();
    private readonly Dictionary<string, Button> donorButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Image> donorBackgrounds = new Dictionary<string, Image>();
    private readonly Dictionary<string, TMP_Text> donorLabels = new Dictionary<string, TMP_Text>();

    // targetInstanceId — конкретный стек (уровень) предмета, который прокачиваем. onApplied — вызывается после
    // каждого успешного пожертвования (чтобы вызывающий мог обновить свой собственный UI: детали предмета, слот экипировки и т.д.).
    public void Open(string targetId, System.Action onApplied = null)
    {
        itemCollectionManager = ItemCollectionManager.Instance;
        if (itemCollectionManager == null) return;

        var targetStack = itemCollectionManager.GetStackByInstanceId(targetId);
        var targetData = targetStack != null ? itemCollectionManager.GetItemById(targetStack.itemId) : null;
        if (targetData == null || targetStack == null) return;

        if (canvasRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRoot = canvas != null ? canvas.transform : transform;
        }

        targetInstanceId = targetId;
        targetItemData = targetData;
        this.onApplied = onApplied;
        baseLevel = targetStack.level;
        baseExperience = targetStack.experience;
        maxLevel = targetData.GetMaxLevel();
        selectedDonorCounts.Clear();

        BuildOverlayIfNeeded();
        RefreshButtonTheme();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    // Перечитывает цвета кнопок при каждом открытии — чтобы правки палитры в коде
    // сразу применялись и к уже однажды построенному (закэшированному) окну, без рестарта Play Mode.
    private void RefreshButtonTheme()
    {
        if (confirmBg != null) ConfirmationDialog.StyleAsButton(confirmBg);
        if (confirmButtonText != null) confirmButtonText.color = ConfirmationDialog.ButtonTextColor;
        if (closeBg != null) ConfirmationDialog.StyleAsButton(closeBg);
        if (closeButtonText != null) closeButtonText.color = ConfirmationDialog.ButtonTextColor;

        foreach (var bg in quickSelectBgs)
            if (bg != null) ConfirmationDialog.StyleAsButton(bg);
        foreach (var text in quickSelectTexts)
            if (text != null) text.color = ConfirmationDialog.ButtonTextColor;

        if (maxLevelBg != null) ConfirmationDialog.StyleAsButton(maxLevelBg);
        if (maxLevelText != null) maxLevelText.color = ConfirmationDialog.ButtonTextColor;

        if (centerLevelBg != null) ConfirmationDialog.StyleAsDescriptionPanel(centerLevelBg);
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
        windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1000, 1650);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 50);
        titleRect.anchoredPosition = new Vector2(0, -40);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Sacrifice items for experience";
        title.fontSize = 34;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(20, 240);
        scrollRect.offsetMax = new Vector2(-20, -90);

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
        grid.cellSize = new Vector2(218, 300); // 230 -> 300: 28pt-минимум текста (донор-лейбл, до 3 строк) не влезал в старую высоту
        grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(24, 6, 6, 6);

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
        emptyLabel.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        emptyLabelObj.SetActive(false);
        emptyLabelHolder = emptyLabelObj;

        // Степпер: -10 -1 [Level X/Y индикатор] +1 +10 Max — между списком (сверху) и Summary/Confirm (снизу).
        // Координаты по x — фиксированные смещения от центра окна, не формула по количеству кнопок (левая и
        // правая половины не симметричны: 2 кнопки слева, 3 справа — см. class-level комментарий).
        const float quickRowY = 205f;
        const float stepRowHeight = 55f;
        const float stepBtnWidth = 100f;
        const float centerWidth = 240f;

        var centerObj = new GameObject("LevelPreview", typeof(RectTransform));
        var centerRect = (RectTransform)centerObj.transform;
        centerRect.SetParent(windowRect, false);
        centerRect.anchorMin = new Vector2(0.5f, 0);
        centerRect.anchorMax = new Vector2(0.5f, 0);
        centerRect.pivot = new Vector2(0.5f, 0);
        centerRect.sizeDelta = new Vector2(centerWidth, stepRowHeight);
        centerRect.anchoredPosition = new Vector2(0, quickRowY);
        centerLevelBg = centerObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsDescriptionPanel(centerLevelBg);

        var centerTextObj = new GameObject("Text", typeof(RectTransform));
        var centerTextRect = (RectTransform)centerTextObj.transform;
        centerTextRect.SetParent(centerRect, false);
        centerTextRect.anchorMin = Vector2.zero;
        centerTextRect.anchorMax = Vector2.one;
        centerTextRect.offsetMin = Vector2.zero;
        centerTextRect.offsetMax = Vector2.zero;
        centerLevelText = centerTextObj.AddComponent<TextMeshProUGUI>();
        centerLevelText.alignment = TextAlignmentOptions.Center;
        centerLevelText.color = Color.white;
        centerLevelText.enableAutoSizing = true;
        centerLevelText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        centerLevelText.fontSizeMax = 30;

        float[] stepCenterX = { -286f, -178f, 178f, 286f }; // -10, -1, +1, +10 — симметрично вокруг центра
        for (int i = 0; i < QuickSelectDeltas.Length; i++)
        {
            int delta = QuickSelectDeltas[i]; // локальная копия — безопасна для замыкания кнопки ниже

            var qObj = new GameObject($"QuickStep_{QuickSelectLabels[i]}", typeof(RectTransform));
            var qRect = (RectTransform)qObj.transform;
            qRect.SetParent(windowRect, false);
            qRect.anchorMin = new Vector2(0.5f, 0);
            qRect.anchorMax = new Vector2(0.5f, 0);
            qRect.pivot = new Vector2(0.5f, 0);
            qRect.sizeDelta = new Vector2(stepBtnWidth, stepRowHeight);
            qRect.anchoredPosition = new Vector2(stepCenterX[i], quickRowY);

            quickSelectBgs[i] = qObj.AddComponent<Image>();
            ConfirmationDialog.StyleAsStepperButton(quickSelectBgs[i]);
            quickSelectButtons[i] = qObj.AddComponent<Button>();
            quickSelectButtons[i].targetGraphic = quickSelectBgs[i]; // без этого interactable=false ничего не красит
            quickSelectButtons[i].onClick.AddListener(() => AdjustPreviewTarget(delta));

            var qTextObj = new GameObject("Text", typeof(RectTransform));
            var qTextRect = (RectTransform)qTextObj.transform;
            qTextRect.SetParent(qRect, false);
            qTextRect.anchorMin = Vector2.zero;
            qTextRect.anchorMax = Vector2.one;
            qTextRect.offsetMin = Vector2.zero;
            qTextRect.offsetMax = Vector2.zero;
            quickSelectTexts[i] = qTextObj.AddComponent<TextMeshProUGUI>();
            quickSelectTexts[i].text = QuickSelectLabels[i];
            quickSelectTexts[i].alignment = TextAlignmentOptions.Center;
            quickSelectTexts[i].fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
            quickSelectTexts[i].color = Color.white;
        }

        var maxObj = new GameObject("QuickStep_Max", typeof(RectTransform));
        var maxRect = (RectTransform)maxObj.transform;
        maxRect.SetParent(windowRect, false);
        maxRect.anchorMin = new Vector2(0.5f, 0);
        maxRect.anchorMax = new Vector2(0.5f, 0);
        maxRect.pivot = new Vector2(0.5f, 0);
        maxRect.sizeDelta = new Vector2(stepBtnWidth, stepRowHeight);
        maxRect.anchoredPosition = new Vector2(394f, quickRowY);
        maxLevelBg = maxObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsStepperButton(maxLevelBg);
        maxLevelButton = maxObj.AddComponent<Button>();
        maxLevelButton.targetGraphic = maxLevelBg; // без этого interactable=false ничего не красит
        maxLevelButton.onClick.AddListener(() => QuickSelectToLevel(maxLevel));

        var maxTextObj = new GameObject("Text", typeof(RectTransform));
        var maxTextRect = (RectTransform)maxTextObj.transform;
        maxTextRect.SetParent(maxRect, false);
        maxTextRect.anchorMin = Vector2.zero;
        maxTextRect.anchorMax = Vector2.one;
        maxTextRect.offsetMin = Vector2.zero;
        maxTextRect.offsetMax = Vector2.zero;
        maxLevelText = maxTextObj.AddComponent<TextMeshProUGUI>();
        maxLevelText.text = "Max";
        maxLevelText.alignment = TextAlignmentOptions.Center;
        maxLevelText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        maxLevelText.color = Color.white;

        var summaryObj = new GameObject("Summary", typeof(RectTransform));
        var summaryRect = (RectTransform)summaryObj.transform;
        summaryRect.SetParent(windowRect, false);
        summaryRect.anchorMin = new Vector2(0, 0);
        summaryRect.anchorMax = new Vector2(1, 0);
        summaryRect.pivot = new Vector2(0.5f, 0);
        summaryRect.sizeDelta = new Vector2(-40, 80);
        summaryRect.anchoredPosition = new Vector2(0, 90);
        summaryText = summaryObj.AddComponent<TextMeshProUGUI>();
        summaryText.alignment = TextAlignmentOptions.Center;
        summaryText.color = Color.white;
        summaryText.enableAutoSizing = true; // "Selected: N" + "Current level: X/Y" — длина плавает
        summaryText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        summaryText.fontSizeMax = 30;

        var confirmObj = new GameObject("ConfirmButton", typeof(RectTransform));
        var confirmRect = (RectTransform)confirmObj.transform;
        confirmRect.SetParent(windowRect, false);
        confirmRect.anchorMin = new Vector2(0.3f, 0);
        confirmRect.anchorMax = new Vector2(0.3f, 0);
        confirmRect.pivot = new Vector2(0.5f, 0);
        confirmRect.sizeDelta = new Vector2(250, 65);
        confirmRect.anchoredPosition = new Vector2(0, 40);
        confirmBg = confirmObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(confirmBg);
        confirmButton = confirmObj.AddComponent<Button>();
        confirmButton.targetGraphic = confirmBg; // без этого interactable=false ничего не красит
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
        closeBtnRect.sizeDelta = new Vector2(250, 65);
        closeBtnRect.anchoredPosition = new Vector2(0, 40);
        closeBg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBg);
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

    // Общий фильтр кандидатов в доноры — используется и Populate() (список плиток), и QuickSelectToLevel()
    // (жадный автоподбор под кнопки +1/+10/Max), чтобы оба места не могли разойтись в критериях.
    private List<(ItemOwnershipData ownership, ItemData data)> GetCandidates()
    {
        var heroManager = HeroCollectionManager.Instance;

        return itemCollectionManager.ownership
            .Where(o => o.instanceId != targetInstanceId) // исключаем именно целевой СТЕК, а не весь itemId —
                                                            // другой уровень того же предмета вполне годится как топливо
            .Where(o => heroManager == null || !heroManager.IsItemEquippedAnywhere(o.instanceId)) // экипированные на герое предметы — не топливо
            .Select(o => (ownership: o, data: itemCollectionManager.GetItemById(o.itemId)))
            // Экипировка — обычные доноры; ItemExperience (см. ItemCollectionManager.GrantExperienceAsItems) —
            // тоже валидный донор с фиксированной ценой (sacrificeExperience). Опыт героя (HeroExperience) сюда
            // по-прежнему не годится — тратится отдельно, только внутри HeroUpgradeUI.
            .Where(c => c.data != null && (c.data.category == ItemCategory.Equipment || c.data.category == ItemCategory.ItemExperience))
            .ToList();
    }

    private void Populate()
    {
        foreach (Transform child in listContainer)
            Destroy(child.gameObject);

        donorButtons.Clear();
        donorBackgrounds.Clear();
        donorLabels.Clear();

        var candidates = GetCandidates();

        if (emptyLabelHolder != null)
            emptyLabelHolder.SetActive(candidates.Count == 0);

        foreach (var candidate in candidates)
            CreateDonorEntry(candidate.data, candidate.ownership);

        RefreshSelectionVisuals();
        UpdateSummary();
        RefreshQuickSelectButtons();
    }

    private void CreateDonorEntry(ItemData donorData, ItemOwnershipData donorOwnership)
    {
        PickerTileUtility.BuildTile(listContainer, donorOwnership.instanceId, NormalBgColor,
            out Image bg, out Image icon, out TMP_Text label, out Button btn);

        icon.sprite = donorData.icon;

        label.enableAutoSizing = true; // строка = имя+уровень+опыт, а при выборе ещё и "Selected: X/Y" — длина плавает
        label.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize (было 14/22 — пред-существующее нарушение, замечено при сегодняшней зачистке)
        label.fontSizeMax = 30;
        label.color = donorData.GetRarityColor();

        TMP_Text quantityBadge = null;
        ItemBadgeUtility.ApplyQuantityBadge(icon.rectTransform, donorOwnership.quantity, ref quantityBadge);

        string donorId = donorOwnership.instanceId;
        btn.onClick.AddListener(() => IncrementDonor(donorId));

        donorButtons[donorId] = btn;
        donorBackgrounds[donorId] = bg;
        donorLabels[donorId] = label;

        RefreshDonorLabel(donorId, donorData, donorOwnership);
    }

    // Перестраивает текст строки донора с учётом того, сколько единиц именно из него сейчас выбрано
    private void RefreshDonorLabel(string donorId, ItemData donorData, ItemOwnershipData donorOwnership)
    {
        if (!donorLabels.TryGetValue(donorId, out var label)) return;

        int xp = itemCollectionManager.CalculateSacrificeGain(donorData, donorOwnership.level, donorOwnership.experience, targetItemData);
        int selected = selectedDonorCounts.TryGetValue(donorId, out int c) ? c : 0;
        string selectedLine = selected > 0 ? $"\nSelected: {selected}/{donorOwnership.quantity}" : "";

        label.text = $"{donorData.itemName}\nLvl.{donorOwnership.level}  +{xp} Exp.{selectedLine}";
    }

    // Сумма опыта только за ВЫБРАННОЕ количество единиц с каждого стека (не за весь стек)
    private int SumSelectedXp()
    {
        int sum = 0;
        foreach (var kvp in selectedDonorCounts)
        {
            var ownership = itemCollectionManager.GetStackByInstanceId(kvp.Key);
            var data = ownership != null ? itemCollectionManager.GetItemById(ownership.itemId) : null;
            if (data != null && ownership != null)
                sum += itemCollectionManager.CalculateSacrificeGain(data, ownership.level, ownership.experience, targetItemData) * kvp.Value;
        }
        return sum;
    }

    // Сколько предметов реально будет списано (сумма выбранных единиц по всем стекам) — для текста подтверждения
    private int SumSelectedItemCount() => selectedDonorCounts.Values.Sum();

    // Клик по донору добавляет РОВНО ОДНУ единицу именно из него. Если весь стек уже выбран —
    // клик сбрасывает выбор этого стека назад до 0 (чтобы не нужно было кликать по одному, чтобы снять выбор).
    private void IncrementDonor(string donorId)
    {
        var donorStack = itemCollectionManager.GetStackByInstanceId(donorId);
        if (donorStack == null) return;

        int current = selectedDonorCounts.TryGetValue(donorId, out int c) ? c : 0;

        if (current >= donorStack.quantity)
        {
            selectedDonorCounts.Remove(donorId);
        }
        else
        {
            var beforeResult = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (beforeResult.level >= maxLevel)
            {
                ConfirmationDialog.ShowInfo(canvasRoot, $"Max level reached ({maxLevel}).\nNo need to select more items.");
                return;
            }

            selectedDonorCounts[donorId] = current + 1;

            var afterResult = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (afterResult.wastedExperience > 0)
            {
                ConfirmationDialog.ShowInfo(canvasRoot,
                    $"Lv. {maxLevel} will be reached.\nExperience above the cap ({afterResult.wastedExperience}) will be lost.");
            }
        }

        var donorData = itemCollectionManager.GetItemById(donorStack.itemId);
        if (donorData != null) RefreshDonorLabel(donorId, donorData, donorStack);

        RefreshSelectionVisuals();
        UpdateSummary();
        RefreshQuickSelectButtons(); // центральный индикатор/степпер должен отражать и ручной выбор, не только QuickSelectToLevel
    }

    // Сдвигает превью (не committed baseLevel) на levelDelta ОТНОСИТЕЛЬНО уже выбранного сейчас — то есть
    // повторные клики +1/+10/-1/-10 складываются друг с другом и с ручным выбором через IncrementDonor,
    // а не сбрасываются на "baseLevel+шаг" при каждом клике. -1/-10 не уводят превью ниже baseLevel.
    private void AdjustPreviewTarget(int levelDelta)
    {
        if (levelDelta < 0)
        {
            DecreasePreviewTarget(-levelDelta);
            return;
        }

        var preview = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
        QuickSelectToLevel(preview.level + levelDelta);
    }

    // -1/-10: в отличие от QuickSelectToLevel (жадный набор С НУЛЯ от committed baseLevel), здесь СНИМАЕМ
    // уже выбранные единицы по одной, начиная с самой "дорогой" (наибольший gain за единицу этого донора).
    // Причина: жадный набор всегда округляет ВВЕРХ до целых предметов — если один донор с большим gain
    // с запасом перекрывает сразу несколько уровней, пересбор С НУЛЯ под чуть меньшую цель (та же сортировка,
    // тот же пул) мог отобрать РОВНО ТОТ ЖЕ набор доноров — округление то же самое — и -1/-10 визуально
    // ничего не менял (кнопка нажималась, но уровень не сдвигался — баг найден пользователем 2026-08-20).
    // Снятие реальных единиц гарантирует фактическое изменение выбора при каждом клике, пока есть что снимать.
    private void DecreasePreviewTarget(int levels)
    {
        var current = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
        int targetLevel = Mathf.Max(baseLevel, current.level - levels);

        while (selectedDonorCounts.Count > 0)
        {
            var preview = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (preview.level <= targetLevel) break;

            string mostValuableId = null;
            int mostValuableGain = -1;
            foreach (var donorId in selectedDonorCounts.Keys)
            {
                var ownership = itemCollectionManager.GetStackByInstanceId(donorId);
                var data = ownership != null ? itemCollectionManager.GetItemById(ownership.itemId) : null;
                if (data == null) continue;

                int gain = itemCollectionManager.CalculateSacrificeGain(data, ownership.level, ownership.experience, targetItemData);
                if (gain > mostValuableGain) { mostValuableGain = gain; mostValuableId = donorId; }
            }

            if (mostValuableId == null) break; // ни один оставшийся донор не резолвится — не зацикливаемся

            selectedDonorCounts[mostValuableId]--;
            if (selectedDonorCounts[mostValuableId] <= 0)
                selectedDonorCounts.Remove(mostValuableId);
        }

        Populate();
    }

    // Абсолютный целевой уровень — сбрасывает текущее выделение и жадно набирает донор-предметы заново.
    // Самые дешёвые доноры (наименьший gain за единицу — обычно непрокачанный лежалый хлам) идут первыми,
    // чтобы не сжигать ценные высокоуровневые предметы, когда хватает мусора. Итоговое выделение остаётся
    // полностью видимым/редактируемым как обычно — Confirm всё ещё нужен явно.
    private void QuickSelectToLevel(int targetLevel)
    {
        targetLevel = Mathf.Clamp(targetLevel, baseLevel, maxLevel);
        selectedDonorCounts.Clear();

        if (targetLevel > baseLevel)
        {
            int needed = itemCollectionManager.ExperienceNeededForLevel(baseLevel, baseExperience, targetLevel);

            var pool = GetCandidates()
                .Select(c => (c.ownership, c.data,
                    gain: itemCollectionManager.CalculateSacrificeGain(c.data, c.ownership.level, c.ownership.experience, targetItemData)))
                .Where(c => c.gain > 0)
                .OrderBy(c => c.gain)
                .ToList();

            int accumulated = 0;
            foreach (var candidate in pool)
            {
                if (accumulated >= needed) break;

                int remaining = needed - accumulated;
                int take = Mathf.Clamp(Mathf.CeilToInt(remaining / (float)candidate.gain), 1, candidate.ownership.quantity);

                selectedDonorCounts[candidate.ownership.instanceId] = take;
                accumulated += candidate.gain * take;
            }

            var result = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (result.level < targetLevel)
                ConfirmationDialog.ShowInfo(canvasRoot,
                    $"Not enough items to reach level {targetLevel} — selected everything available (reaches level {result.level}).");
        }

        Populate();
    }

    private void RefreshSelectionVisuals()
    {
        var result = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
        bool atMax = result.level >= maxLevel;

        foreach (var kvp in donorButtons)
        {
            bool isSelected = selectedDonorCounts.TryGetValue(kvp.Key, out int count) && count > 0;
            kvp.Value.interactable = isSelected || !atMax;
            donorBackgrounds[kvp.Key].color = isSelected ? SelectedBgColor : NormalBgColor;
        }
    }

    // Уровень/опыт-превью теперь живёт в центральном индикаторе степпера (см. RefreshQuickSelectButtons) —
    // здесь остался только счётчик выбранных предметов.
    private void UpdateSummary()
    {
        int selectedCount = SumSelectedItemCount();
        string summary = $"Selected: {selectedCount} item(s)";

        // "Было -> стало" для стата, который реально даёт этот слот — живой, обновляется на каждый тап
        // донора (превью уже пересчитан выше по стеку вызовов, см. RefreshQuickSelectButtons).
        if (targetItemData != null)
        {
            var preview = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);
            if (preview.level != baseLevel)
            {
                float oldValue = EquipmentStatCurve.GetValue(targetItemData.slotType, targetItemData.rarity, baseLevel);
                float newValue = EquipmentStatCurve.GetValue(targetItemData.slotType, targetItemData.rarity, preview.level);
                summary += "\n" + UpgradeFeedbackUtility.FormatBeforeAfter(GetStatLabelForSlot(targetItemData.slotType),
                    FormatStatValue(targetItemData.slotType, oldValue), FormatStatValue(targetItemData.slotType, newValue));
            }
        }

        summaryText.text = summary;

        if (confirmButton != null)
            confirmButton.interactable = selectedCount > 0;
    }

    // Один слот = один стат (см. HeroStatUtility.CalculateEquipmentBonuses) — та же привязка, просто с
    // человекочитаемым именем для UI.
    private static string GetStatLabelForSlot(EquipmentSlotType slot) => slot switch
    {
        EquipmentSlotType.Armor => "HP",
        EquipmentSlotType.Trinket => "Mana",
        EquipmentSlotType.Weapon => "Attack",
        EquipmentSlotType.Accessory => "Armor",
        _ => "Stat",
    };

    // Weapon даёт damageMultiplier (доля, напр. 0.05) — общий ×100 приём отображения с героем
    // (см. UpgradeFeedbackUtility.AttackDisplayValue), чтобы "Attack" везде читался одинаково, не 0.05 против 100.
    private static string FormatStatValue(EquipmentSlotType slot, float value) =>
        slot == EquipmentSlotType.Weapon ? UpgradeFeedbackUtility.AttackDisplayValue(value).ToString() : Mathf.RoundToInt(value).ToString();

    // Обновляет центральный индикатор "Level X/Y" + текущий опыт внутри уровня, и включает/выключает
    // стрелки степпера по границам: -1/-10 доступны только если превью выше committed baseLevel (есть что
    // убрать), +1/+10/Max — только если превью ниже maxLevel (есть куда расти).
    private void RefreshQuickSelectButtons()
    {
        if (quickSelectButtons[0] == null || centerLevelText == null) return;

        var preview = itemCollectionManager.SimulateExperienceGain(baseLevel, baseExperience, SumSelectedXp(), maxLevel);

        string expLine = preview.level >= maxLevel ? "MAX" : $"{preview.experience}/{itemCollectionManager.ExperienceToNextLevel(preview.level)} Exp.";
        centerLevelText.text = $"Lv. {preview.level}/{maxLevel}\n{expLine}";

        bool canGoDown = preview.level > baseLevel;
        bool canGoUp = preview.level < maxLevel;

        for (int i = 0; i < QuickSelectDeltas.Length; i++)
            quickSelectButtons[i].interactable = QuickSelectDeltas[i] < 0 ? canGoDown : canGoUp;

        if (maxLevelButton != null)
            maxLevelButton.interactable = canGoUp;
    }

    private void OnConfirmClicked()
    {
        int itemCount = SumSelectedItemCount();
        if (itemCount == 0) return;

        ConfirmationDialog.Show(
            canvasRoot,
            $"Sacrifice {itemCount} item(s)?\nThis cannot be undone.",
            ApplySacrifice);
    }

    private void ApplySacrifice()
    {
        int levelBefore = baseLevel;
        float statBefore = targetItemData != null
            ? EquipmentStatCurve.GetValue(targetItemData.slotType, targetItemData.rarity, levelBefore) : 0f;

        int totalWasted = 0;
        var countsToSacrifice = new Dictionary<string, int>(selectedDonorCounts);

        foreach (var kvp in countsToSacrifice)
        {
            string donorId = kvp.Key;
            int quantityToSacrifice = kvp.Value; // именно столько единиц было выбрано из этого стека, не весь стек

            for (int i = 0; i < quantityToSacrifice; i++)
            {
                // targetInstanceId может измениться (стек делится на новый или сливается с существующим) —
                // SacrificeItem возвращает актуальный instanceId, и следующий вызов должен целиться именно в него.
                if (itemCollectionManager.SacrificeItem(donorId, targetInstanceId, out int wasted, out string resultingId))
                {
                    totalWasted += wasted;
                    targetInstanceId = resultingId;
                }
            }
        }

        selectedDonorCounts.Clear();

        var targetOwnership = itemCollectionManager.GetStackByInstanceId(targetInstanceId);
        if (targetOwnership != null)
        {
            baseLevel = targetOwnership.level;
            baseExperience = targetOwnership.experience;
        }

        Populate();
        onApplied?.Invoke();

        // Момент-победа только на КОММИТЕ (не на каждом промежуточном тапе степпера — иначе было бы навязчиво
        // мигать при любом +1/-1). targetItemData уже мог замениться на новый инстанс (SacrificeItem split/merge
        // выше), но slotType/rarity предмета не меняются, так что statBefore остаётся корректной точкой отсчёта.
        if (targetItemData != null && baseLevel != levelBefore)
        {
            float statAfter = EquipmentStatCurve.GetValue(targetItemData.slotType, targetItemData.rarity, baseLevel);

            EnsureCommitFlashOverlay();
            if (commitFlashRoutine != null) StopCoroutine(commitFlashRoutine);
            commitFlashRoutine = UpgradeFeedbackUtility.FlashPanel(this, commitFlashOverlay, FloatingDamageText.PositiveGainColor);

            if (windowRect != null)
                FloatingDamageText.Spawn(windowRect, $"+{FormatStatValue(targetItemData.slotType, statAfter - statBefore)}", FloatingDamageText.PositiveGainColor);

            HapticsUtility.PlayLightTap();
            AudioManager.Instance?.PlayUpgradeSound();
        }

        if (totalWasted > 0)
        {
            // Излишек больше не сгорает — конвертируется в предметы ItemExperience (см.
            // ItemCollectionManager.GrantExperienceAsItems) и возвращается в инвентарь. Если номиналы ещё
            // не заведены в allItems, метод вернёт false — сообщаем как раньше, не обещая лишнего.
            bool converted = itemCollectionManager.GrantExperienceAsItems(totalWasted);
            ConfirmationDialog.ShowInfo(canvasRoot, converted
                ? $"Excess experience ({totalWasted}) returned to your inventory as Item Experience."
                : $"Experience above max level lost: {totalWasted}");
        }
    }

    // Отдельный прозрачный слой поверх окна — НЕ трогаем сам windowBg напрямую (он стилизован через
    // ConfirmationDialog.StyleAsPanel и должен оставаться видимым всегда; вспышка гасит альфу до 0, что
    // сделало бы весь фон попапа невидимым, если бы красили его настоящий фон).
    private void EnsureCommitFlashOverlay()
    {
        if (commitFlashOverlay != null || windowRect == null) return;

        var overlayObj = new GameObject("CommitFlashOverlay", typeof(RectTransform));
        var overlayImgRect = (RectTransform)overlayObj.transform;
        overlayImgRect.SetParent(windowRect, false);
        overlayImgRect.anchorMin = Vector2.zero;
        overlayImgRect.anchorMax = Vector2.one;
        overlayImgRect.offsetMin = Vector2.zero;
        overlayImgRect.offsetMax = Vector2.zero;

        commitFlashOverlay = overlayObj.AddComponent<Image>();
        commitFlashOverlay.color = new Color(0, 0, 0, 0);
        commitFlashOverlay.raycastTarget = false;
    }
}
