using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Дебаг-инструмент (пункт #6 бэклога от 2026-08-10) — собрать тестовый отряд под конкретный контент
// (например boss-wall кампании) без реального гринда: разблокировать любого героя, выставить
// уровень/вознесение каждому герою в отряде напрямую, заспавнить и экипировать предмет любой
// редкости/уровня в любой слот. Открывается из Castle (см. CastleUI). Никакой валюты/гемов не тратит —
// это тестовый инструмент, не игровой контент.
public class DebugConstructorUI : MonoBehaviour
{
    private const int GearSlotCount = 4;
    private static readonly EquipmentSlotType[] GearSlots =
        { EquipmentSlotType.Weapon, EquipmentSlotType.Armor, EquipmentSlotType.Accessory, EquipmentSlotType.Trinket };
    private static readonly Rarity[] PickableRarities = { Rarity.Green, Rarity.Blue, Rarity.Purple, Rarity.Orange };

    private Transform canvasRoot;
    private GameObject overlayRoot;
    private Transform contentContainer;

    // Пер-слот выбор редкости/уровня для ещё НЕ заспавненного предмета (что подставится в GrantItemAtLevel,
    // если нажать "Equip") — держится отдельно от того, что реально надето, чтобы можно было подобрать
    // параметры до применения.
    private readonly int[,] pendingGearRarityIndex = new int[4, GearSlotCount];
    private readonly int[,] pendingGearLevel = new int[4, GearSlotCount];

    public void Open(Transform canvasRoot)
    {
        this.canvasRoot = canvasRoot;

        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Populate();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("DebugConstructorOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);

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
        windowRect.sizeDelta = new Vector2(1000, 1700);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 50);
        titleRect.anchoredPosition = new Vector2(0, -35);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "Debug Constructor";
        title.fontSize = 32;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(windowRect, false);
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(20, 90);
        scrollRect.offsetMax = new Vector2(-20, -75);

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
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var vlayout = contentObj.AddComponent<VerticalLayoutGroup>();
        vlayout.padding = new RectOffset(10, 10, 10, 10);
        vlayout.spacing = 18;
        vlayout.childControlWidth = true;
        vlayout.childControlHeight = true; // без этого LayoutElement.preferredHeight у детей (лейблы, блоки) молча игнорируется — они падают на дефолтный RectTransform-размер (100x100), см. комментарий у BuildSquadSlotBlock
        vlayout.childForceExpandWidth = true;
        vlayout.childForceExpandHeight = false;

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        contentContainer = contentRect;

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(250, 50);
        closeBtnRect.anchoredPosition = new Vector2(0, 25);
        var closeBg = closeBtnObj.AddComponent<Image>();
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
        var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "Close";
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private void Populate()
    {
        if (contentContainer == null) return;

        foreach (Transform child in contentContainer)
            Destroy(child.gameObject);

        BuildUnlockHeroSection();

        var heroManager = HeroCollectionManager.Instance;
        int squadCount = heroManager != null ? heroManager.squad.Count : 0;
        for (int slot = 0; slot < 4; slot++)
        {
            HeroData hero = slot < squadCount ? heroManager.squad[slot] : null;
            BuildSquadSlotBlock(slot, hero);
        }
    }

    // ---------------- Unlock Hero ----------------

    private void BuildUnlockHeroSection()
    {
        var heroManager = HeroCollectionManager.Instance;
        if (heroManager == null || heroManager.allHeroes == null) return;

        CreateSectionLabel(contentContainer, "Unlock Hero (click to unlock instantly)");

        var gridObj = new GameObject("UnlockHeroGrid", typeof(RectTransform));
        var gridRect = (RectTransform)gridObj.transform;
        gridRect.SetParent(contentContainer, false);
        var gridLE = gridObj.AddComponent<LayoutElement>();

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(160, 44);
        grid.spacing = new Vector2(8, 8);
        grid.childAlignment = TextAnchor.UpperLeft;

        int columns = 6;
        int rows = Mathf.CeilToInt(heroManager.allHeroes.Length / (float)columns);
        gridLE.preferredHeight = rows * (44 + 8);

        foreach (var hero in heroManager.allHeroes)
        {
            if (hero == null) continue;

            bool unlocked = heroManager.IsUnlocked(hero);

            var btnObj = new GameObject(hero.heroId, typeof(RectTransform));
            var btnRect = (RectTransform)btnObj.transform;
            btnRect.SetParent(gridRect, false);

            var btnImg = btnObj.AddComponent<Image>();
            ConfirmationDialog.StyleAsButton(btnImg, unlocked ? new Color(0.2f, 0.35f, 0.2f) : (Color?)null);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg; // без этого interactable=false ничего не красит — кнопка выглядит активной
            btn.interactable = !unlocked;

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(btnRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = unlocked ? $"{hero.heroName} ✓" : hero.heroName;
            text.color = hero.GetRarityColor();
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = 18;

            var h = hero;
            btn.onClick.AddListener(() =>
            {
                heroManager.UnlockHero(h);
                Populate();
            });
        }
    }

    // ---------------- Squad slot block (hero level/ascension + 4 gear slots) ----------------

    private void BuildSquadSlotBlock(int slotIndex, HeroData hero)
    {
        var heroManager = HeroCollectionManager.Instance;
        var blockObj = new GameObject($"SquadSlot_{slotIndex}", typeof(RectTransform));
        var blockRect = (RectTransform)blockObj.transform;
        blockRect.SetParent(contentContainer, false);

        var blockBg = blockObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsDescriptionPanel(blockBg);

        // childControlHeight=true — VerticalLayoutGroup сама тоже реализует ILayoutElement и репортит наружу
        // СВОЙ вычисленный preferredHeight (сумма высот детей + padding/spacing), поэтому родительскому
        // Content'у (см. BuildOverlayIfNeeded) не нужно ничего знать заранее — высота блока считается
        // снизу вверх автоматически. Раньше тут в конце руками прибавлялась "оценка" высоты — с
        // childControlHeight=false у ОБОИХ уровней (тут и в Content) она всё равно ничего не решала,
        // реальная высота была дефолтные 100x100 на каждый элемент — то самое "всё съехало".
        var vlayout = blockObj.AddComponent<VerticalLayoutGroup>();
        vlayout.padding = new RectOffset(14, 14, 12, 12);
        vlayout.spacing = 8;
        vlayout.childControlWidth = true;
        vlayout.childControlHeight = true;
        vlayout.childForceExpandWidth = true;
        vlayout.childForceExpandHeight = false;

        if (hero == null || heroManager == null)
        {
            CreateRowLabel(blockRect, $"Squad slot {slotIndex + 1}: empty (assign in Squad screen)");
            return;
        }

        var ownership = heroManager.ownership.FirstOrDefault(o => o.heroId == hero.heroId);
        if (ownership == null)
        {
            CreateRowLabel(blockRect, $"Squad slot {slotIndex + 1}: {hero.heroName} (no ownership data)");
            return;
        }

        int levelCap = HeroAscensionUtility.GetLevelCap(hero.rarity, ownership.ascensionLevel);
        int maxAscension = HeroAscensionUtility.GetMaxAscension(hero.rarity);

        var headerText = CreateRowLabel(blockRect, "");
        headerText.color = hero.GetRarityColor();
        headerText.fontSize = 24;

        void RefreshHeader()
        {
            headerText.text = $"{hero.heroName} ({hero.rarity}) — Lvl {ownership.level}/{levelCap}, Ascension {ownership.ascensionLevel}/{maxAscension}";
        }
        RefreshHeader();

        // Уровень: -10/-1/+1/+10, кламп на levelCap текущей ступени вознесения внутри DebugSetLevelAscension
        CreateStepperRow(blockRect, "Level", new (string, int)[] { ("-10", -10), ("-1", -1), ("+1", 1), ("+10", 10) }, delta =>
        {
            heroManager.DebugSetLevelAscension(hero.heroId, ownership.level + delta, ownership.ascensionLevel);
            levelCap = HeroAscensionUtility.GetLevelCap(hero.rarity, ownership.ascensionLevel);
            RefreshHeader();
        });

        // Вознесение: -1/+1, кламп на GetMaxAscension внутри DebugSetLevelAscension. Уровень СОХРАНЯЕТСЯ
        // как есть (просто передаём текущий ownership.level) — DebugSetLevelAscension сам подожмёт его под
        // новый (обычно более высокий) потолок, если он оказался ниже прежнего значения.
        CreateStepperRow(blockRect, "Ascension", new (string, int)[] { ("-1", -1), ("+1", 1) }, delta =>
        {
            heroManager.DebugSetLevelAscension(hero.heroId, ownership.level, ownership.ascensionLevel + delta);
            levelCap = HeroAscensionUtility.GetLevelCap(hero.rarity, ownership.ascensionLevel);
            RefreshHeader();
        });

        for (int g = 0; g < GearSlotCount; g++)
            BuildGearRow(blockRect, slotIndex, g, hero, ownership);
    }

    private void BuildGearRow(Transform parent, int slotIndex, int gearIndex, HeroData hero, HeroOwnershipData ownership)
    {
        var itemManager = ItemCollectionManager.Instance;
        var slotType = GearSlots[gearIndex];

        if (pendingGearLevel[slotIndex, gearIndex] <= 0)
            pendingGearLevel[slotIndex, gearIndex] = 1; // первая отрисовка этой ячейки — дефолт

        var rowObj = new GameObject($"Gear_{slotType}", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(parent, false);
        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 36;

        var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true; // false игнорирует LayoutElement.preferredWidth у детей — см. комментарий в BuildSquadSlotBlock
        layout.childControlHeight = true;

        string equippedId = ownership.GetEquippedItemInstanceId(slotType);
        var equippedStack = !string.IsNullOrEmpty(equippedId) ? itemManager?.GetStackByInstanceId(equippedId) : null;
        var equippedData = equippedStack != null ? itemManager.GetItemById(equippedStack.itemId) : null;
        string equippedLabel = equippedData != null ? $"{equippedData.itemName} L{equippedStack.level}" : "empty";

        var infoText = CreateInlineLabel(rowRect, 260, $"{slotType}: {equippedLabel}");

        var rarityText = CreateInlineLabel(rowRect, 90, PickableRarities[pendingGearRarityIndex[slotIndex, gearIndex]].ToString());
        rarityText.alignment = TextAlignmentOptions.Center;

        CreateSmallButton(rowRect, "Rarity", () =>
        {
            pendingGearRarityIndex[slotIndex, gearIndex] = (pendingGearRarityIndex[slotIndex, gearIndex] + 1) % PickableRarities.Length;
            Populate();
        });

        var levelText = CreateInlineLabel(rowRect, 70, $"Lv{pendingGearLevel[slotIndex, gearIndex]}");
        levelText.alignment = TextAlignmentOptions.Center;

        CreateSmallButton(rowRect, "-1", () =>
        {
            pendingGearLevel[slotIndex, gearIndex] = Mathf.Max(1, pendingGearLevel[slotIndex, gearIndex] - 1);
            Populate();
        });
        CreateSmallButton(rowRect, "+1", () =>
        {
            pendingGearLevel[slotIndex, gearIndex] = Mathf.Min(EquipmentStatCurve.MaxLevel, pendingGearLevel[slotIndex, gearIndex] + 1);
            Populate();
        });

        CreateSmallButton(rowRect, "Equip", () =>
        {
            if (itemManager == null) return;

            var rarity = PickableRarities[pendingGearRarityIndex[slotIndex, gearIndex]];
            var candidate = itemManager.GetItemsOfType(slotType).FirstOrDefault(i => i.rarity == rarity);
            if (candidate == null) return;

            string newInstanceId = itemManager.GrantItemAtLevel(candidate, pendingGearLevel[slotIndex, gearIndex]);
            if (string.IsNullOrEmpty(newInstanceId)) return;

            // Если предмет уже был экипирован на другом герое — снимаем оттуда (тот же принцип, что и
            // HeroInventoryUI.EquipItem), новый инстанс по построению никем ещё не занят.
            HeroCollectionManager.Instance?.UnequipItemFromAllHeroes(newInstanceId, hero.heroId);
            ownership.SetEquippedItem(slotType, newInstanceId);
            HeroCollectionManager.Instance?.SaveOwnership();
            Populate();
        });
    }

    // ---------------- маленькие билдеры-хелперы ----------------

    private static void CreateSectionLabel(Transform parent, string text)
    {
        var obj = new GameObject("SectionLabel", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 30;
        var t = obj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 20;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Left;
    }

    private static TMP_Text CreateRowLabel(Transform parent, string text)
    {
        var obj = new GameObject("Label", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 30;
        var t = obj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 18;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Left;
        return t;
    }

    private static TMP_Text CreateInlineLabel(Transform parent, float width, string text)
    {
        var obj = new GameObject("Label", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        var t = obj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 16;
        t.color = new Color(1, 1, 1, 0.9f);
        t.alignment = TextAlignmentOptions.Left;
        t.enableAutoSizing = true;
        t.fontSizeMin = 10;
        t.fontSizeMax = 16;
        return t;
    }

    // Ряд из label + N кнопок-дельт (например "-10 -1 +1 +10") — используется для Level/Ascension.
    // onDelta вызывается с числом, УЖЕ соответствующим подписи кнопки (не индексом).
    private static void CreateStepperRow(Transform parent, string label, (string text, int delta)[] steps, System.Action<int> onDelta)
    {
        var rowObj = new GameObject($"Stepper_{label}", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(parent, false);
        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 34;

        var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true; // false игнорирует LayoutElement.preferredWidth у детей — см. комментарий в BuildSquadSlotBlock
        layout.childControlHeight = true;

        CreateInlineLabel(rowRect, 110, label + ":");

        foreach (var step in steps)
        {
            int delta = step.delta;
            CreateSmallButton(rowRect, step.text, () => onDelta(delta));
        }
    }

    private static void CreateSmallButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var obj = new GameObject($"Btn_{label}", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = 70;

        var img = obj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(img);
        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10;
        text.fontSizeMax = 18;
    }
}
