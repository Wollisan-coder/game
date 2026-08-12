using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCollectionUI : MonoBehaviour
{
    public Transform gridContainer;
    public GameObject itemCardPrefab;
    public ItemDetailUI detailUI;

    private const float TitleReservedHeight = 100f; // высота существующего заголовка панели (чтобы вкладки не наезжали на него)
    private const float TabBarHeight = 40f;
    private const float TabBarTopGap = 4f;
    private const float FilterBarHeight = 50f;
    private const float FilterBarTopGap = 4f;

    private enum TopFilter { All, Equipment, Consumables }

    // 3 верхних вкладки вместо прежних 7 (All/Weapon/Armor/Accessory/Trinket/Vouchers) — Equipment теперь
    // объединяет все 4 типа экипировки без под-вкладок, Consumables объединяет ваучеры + Hero Experience
    // (валюта, не предмет) + Ascension Gems (по герою, не предмет) — одна общая сетка, без отдельных окон.
    private readonly (string label, TopFilter filter)[] tabs =
    {
        ("All", TopFilter.All),
        ("Equipment", TopFilter.Equipment),
        ("Consumables", TopFilter.Consumables),
    };

    private readonly List<Image> tabBackgrounds = new List<Image>();
    private int selectedTabIndex = 0;

    // Фильтр-ряд под вкладками. Rarity — цикл-кнопка (тот же приём, что в HeroCollectionUI). Level — НЕ
    // фильтр, а тоггл сортировки (High→Low / Low→High), ничего не прячет — так попросили явно, вместо
    // диапазонов-бакетов из прошлой версии.
    private static readonly Rarity[] RarityOptions = { Rarity.Green, Rarity.Blue, Rarity.Purple, Rarity.Orange };

    private int rarityFilterIndex = 0;       // 0 = "All"
    private bool levelSortDescending = true; // true = High→Low
    private TMP_Text rarityButtonText;
    private TMP_Text levelButtonText;

    // Справочный текст по кнопке "?" (см. project_gem_economy_docs_todo) — переехал сюда 2026-08-10 с
    // экрана входа в Death Dungeon (там гемам было не место, их основной дом — этот инвентарь).
    // Статичный экран справки, не контекстный tutorial-попап — выбор пользователя. Держим одним местом,
    // чтобы при следующем изменении экономики (уже было 5+ раз) текст можно было поправить без раскопок.
    private const string GemEconomyHelpText =
        "Purple and Orange heroes can still drop as duplicates from summoning even after you own them — " +
        "each duplicate grants that hero 1 Ascension Gem instead of nothing.\n\n" +
        "Spend a hero's gems on:\n" +
        "- Ascend: raise their ascension level (HeroInventoryUI) — higher level cap, a stat bonus every step, " +
        "and (Orange only) a stronger race passive at the final step.\n" +
        "- Hero Voucher: convert 1 gem into 1 Voucher of that rarity — only once the hero has reached max " +
        "ascension. 3 Vouchers grant 1 gem to ANY hero of that rarity, or unlock a locked one.\n" +
        "- Hero Experience: convert 1 gem into 1 Hero Experience item, always available.\n\n" +
        "Gems are also spent as Death Dungeon retreat currency — a safe retreat costs a mix of banked gems " +
        "and/or benched heroes (permanently sacrificed, your choice which). Gems survive a hero being lost " +
        "or reset — only their level/skills/gear don't.";

    private void Awake()
    {
        BuildTabBar();
        BuildFilterRow();
        RefreshFilterLabels();
        BuildGemHelpButton();

        // Перестраиваем сетку, когда закрывается попап деталей предмета — иначе апгрейд/использование,
        // сделанные внутри попапа, не будут видны в каталоге, пока вкладку не переключить вручную.
        if (detailUI != null)
            detailUI.OnClosed += PopulateGrid;
    }

    private void Start()
    {
        PopulateGrid();
    }

    // Кнопка "?" в правом верхнем углу панели — фиксированная, не зависит от вкладки/скролла (гемы видны
    // только под All/Consumables, но справка про них уместна с любой вкладки).
    private void BuildGemHelpButton()
    {
        var panelRect = (RectTransform)transform;

        var helpBtnObj = new GameObject("GemHelpButton", typeof(RectTransform));
        var helpBtnRect = (RectTransform)helpBtnObj.transform;
        helpBtnRect.SetParent(panelRect, false);
        helpBtnRect.anchorMin = new Vector2(1, 1);
        helpBtnRect.anchorMax = new Vector2(1, 1);
        helpBtnRect.pivot = new Vector2(1, 1);
        helpBtnRect.sizeDelta = new Vector2(50, 50);
        helpBtnRect.anchoredPosition = new Vector2(-30, -30);
        var helpBtnImg = helpBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(helpBtnImg);
        var helpBtn = helpBtnObj.AddComponent<Button>();
        helpBtn.targetGraphic = helpBtnImg;
        helpBtn.onClick.AddListener(() =>
        {
            var canvas = GetComponentInParent<Canvas>();
            Transform canvasRoot = canvas != null ? canvas.transform : transform;
            ConfirmationDialog.ShowInfo(canvasRoot, GemEconomyHelpText, title: "Ascension Gems & Vouchers");
        });

        var helpTextObj = new GameObject("Text", typeof(RectTransform));
        var helpTextRect = (RectTransform)helpTextObj.transform;
        helpTextRect.SetParent(helpBtnRect, false);
        helpTextRect.anchorMin = Vector2.zero;
        helpTextRect.anchorMax = Vector2.one;
        helpTextRect.offsetMin = Vector2.zero;
        helpTextRect.offsetMax = Vector2.zero;
        var helpText = helpTextObj.AddComponent<TextMeshProUGUI>();
        helpText.text = "?";
        helpText.fontStyle = FontStyles.Bold;
        helpText.fontSize = ConfirmationDialog.MinTextFontSize;
        helpText.alignment = TextAlignmentOptions.Center;
        helpText.color = ConfirmationDialog.ButtonTextColor;
    }

    // Строит ряд кнопок-вкладок над сеткой и освобождает под него место, подрезая Scroll View сверху.
    private void BuildTabBar()
    {
        var panelRect = (RectTransform)transform;

        var scrollRect = gridContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            var scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            float shrink = TitleReservedHeight + TabBarTopGap + TabBarHeight + FilterBarTopGap + FilterBarHeight;
            scrollRectTransform.offsetMax = new Vector2(scrollRectTransform.offsetMax.x, scrollRectTransform.offsetMax.y - shrink);
        }

        var barObj = new GameObject("CategoryTabs", typeof(RectTransform));
        var barRect = (RectTransform)barObj.transform;
        barRect.SetParent(panelRect, false);
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.sizeDelta = new Vector2(-40, TabBarHeight);
        barRect.anchoredPosition = new Vector2(0, -(TitleReservedHeight + TabBarTopGap));

        var layout = barObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; // захватываем копию для замыкания
            var tabObj = new GameObject(tabs[i].label, typeof(RectTransform));
            var tabRect = (RectTransform)tabObj.transform;
            tabRect.SetParent(barRect, false);

            var bg = tabObj.AddComponent<Image>();
            var btn = tabObj.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectTab(index));

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(tabRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = tabs[i].label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 14;
            text.color = ConfirmationDialog.ButtonTextColor;

            tabBackgrounds.Add(bg);
        }

        RefreshTabVisuals();
    }

    // Ряд под вкладками — Rarity (фильтр) и Level (тоггл сортировки). Тот же приём цикл-кнопок, что в
    // HeroCollectionUI.CreateCycleButton.
    private void BuildFilterRow()
    {
        var panelRect = (RectTransform)transform;

        var rowObj = new GameObject("FilterRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(panelRect, false);
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.sizeDelta = new Vector2(-40, FilterBarHeight);
        rowRect.anchoredPosition = new Vector2(0, -(TitleReservedHeight + TabBarTopGap + TabBarHeight + FilterBarTopGap));

        var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        rarityButtonText = CreateFilterCycleButton(rowRect, OnRarityClicked);
        levelButtonText = CreateFilterCycleButton(rowRect, OnLevelSortClicked);
    }

    private TMP_Text CreateFilterCycleButton(RectTransform parent, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject("FilterButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);

        var img = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(img);
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = 22;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;

        return text;
    }

    private void OnRarityClicked()
    {
        rarityFilterIndex = (rarityFilterIndex + 1) % (RarityOptions.Length + 1);
        RefreshFilterLabels();
        PopulateGrid();
    }

    private void OnLevelSortClicked()
    {
        levelSortDescending = !levelSortDescending;
        RefreshFilterLabels();
        PopulateGrid();
    }

    private void RefreshFilterLabels()
    {
        if (rarityButtonText != null)
            rarityButtonText.text = rarityFilterIndex == 0 ? "Rarity: All" : $"Rarity: {RarityOptions[rarityFilterIndex - 1]}";

        if (levelButtonText != null)
            levelButtonText.text = levelSortDescending ? "Level: High→Low" : "Level: Low→High";
    }

    private void SelectTab(int index)
    {
        selectedTabIndex = index;
        RefreshTabVisuals();
        PopulateGrid();
    }

    // Одна и та же рамка (StyleAsButton) на всех вкладках — раньше неактивные вообще оставались без
    // рамки (плоский прямоугольник), выбранная выглядела совсем другим элементом. Разница теперь только
    // в тоне: активная — полный белый (как задаёт сам StyleAsButton), неактивные — притемнены.
    private static readonly Color TabInactiveTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < tabBackgrounds.Count; i++)
        {
            ConfirmationDialog.StyleAsButton(tabBackgrounds[i]);
            if (i != selectedTabIndex)
                tabBackgrounds[i].color = TabInactiveTint;
        }
    }

    private bool MatchesCurrentTab(ItemData item)
    {
        switch (tabs[selectedTabIndex].filter)
        {
            case TopFilter.Equipment: return item.category == ItemCategory.Equipment;
            case TopFilter.Consumables: return item.category == ItemCategory.HeroVoucher; // единственный оставшийся ItemData-"расходник" — опыт и гемы вознесения не предметы, добавляются отдельно ниже
            default: return true; // "All"
        }
    }

    private bool MatchesRarityFilter(ItemData item)
    {
        if (rarityFilterIndex == 0) return true; // "All"
        return item.rarity == RarityOptions[rarityFilterIndex - 1];
    }

    // public — MainMenuUI.ShowItemCollection() зовёт это явно при каждом открытии экрана (тот же приём,
    // что и HeroCollectionUI.PopulateGrid/MainMenuUI.ShowCollection). Раньше грид строился только один
    // раз из Start(), из-за чего предметы/гемы, полученные призывом, не появлялись в каталоге, пока
    // вкладку/фильтр не тронуть вручную (найдено 2026-08-12).
    public void PopulateGrid()
    {
        var collectionManager = ItemCollectionManager.Instance;
        if (collectionManager == null) return;

        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        var matchingItems = collectionManager.allItems
            .Where(MatchesCurrentTab)
            .Where(MatchesRarityFilter);

        var heroManager = HeroCollectionManager.Instance;

        var cards = new List<(ItemData item, ItemOwnershipData stack)>();

        foreach (var item in matchingItems)
        {
            var allStacks = collectionManager.GetStacks(item.itemId);

            if (allStacks.Count == 0)
            {
                // Ни одной копии не получено — одна заблокированная карточка-плейсхолдер
                cards.Add((item, null));
                continue;
            }

            // Экипированные на герое стеки в инвентаре не показываем — предмет "занят"
            var visibleStacks = heroManager != null
                ? allStacks.Where(s => !heroManager.IsItemEquippedAnywhere(s.instanceId)).ToList()
                : allStacks;

            // Все имеющиеся копии сейчас экипированы — карточку вообще не показываем (она не "заблокирована", просто занята)
            foreach (var stack in visibleStacks)
                cards.Add((item, stack));
        }

        // Сортировка — уровень стека рулит порядком (тоггл High/Low), в пределах одного уровня — по
        // редкости, затем по названию. Плейсхолдеры без стека (level=0) — как самые низкоуровневые.
        IOrderedEnumerable<(ItemData item, ItemOwnershipData stack)> ordered = levelSortDescending
            ? cards.OrderByDescending(c => c.stack?.level ?? 0)
            : cards.OrderBy(c => c.stack?.level ?? 0);
        ordered = ordered.ThenBy(c => (int)c.item.rarity).ThenBy(c => c.item.itemName);

        foreach (var (item, stack) in ordered)
            CreateCard(item, stack);

        // Consumables/All — сверху ваучеры (ItemData, уже в cards выше), плюс всё, что НЕ ItemData:
        // Hero Experience и Armor Shards (баланс валют) + Ascension Gems и Wondrous Armor (по герою) —
        // добавляются отдельно в конец той же сетки.
        var filter = tabs[selectedTabIndex].filter;
        if (filter == TopFilter.All || filter == TopFilter.Consumables)
        {
            CreateHeroExperienceTile();
            CreateArmorShardsTile();
            AscensionGemGridUtility.Populate(gridContainer, heroManager, PopulateGrid);
            WondrousArmorGridUtility.Populate(gridContainer, heroManager, PopulateGrid);
        }
    }

    private void CreateCard(ItemData item, ItemOwnershipData stack)
    {
        GameObject cardObj = Instantiate(itemCardPrefab, gridContainer);
        var card = cardObj.GetComponent<ItemCollectionCardUI>();
        card.Setup(item, stack, detailUI);
    }

    // Карточка-плитка баланса CurrencyType.HeroExperience — отдельный префаб CurrencyBalanceTile (см.
    // CurrencyBalanceTileUI), не привязана к герою. Иконка задаётся в самом префабе (Inspector), код её
    // не трогает — раньше код грузил спрайт по Resources-пути и затирал то, что вручную ставили в префабе.
    private void CreateHeroExperienceTile()
    {
        var prefab = Resources.Load<GameObject>("UI/CurrencyBalanceTile");
        if (prefab == null) return;

        int balance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience) : 0;

        GameObject tileObj = Instantiate(prefab, gridContainer);
        tileObj.name = "HeroExperienceBalance";
        tileObj.GetComponent<CurrencyBalanceTileUI>()?.Setup("Hero Experience", balance);
    }

    // Карточка-плитка баланса CurrencyType.ArmorShards — отдельный префаб ArmorShardsTile (см.
    // ArmorShardsTileUI), не общий с Hero Experience: кликабельная, открывает подтверждение обмена
    // 3 осколков на случайный скин Дивной брони (см. HeroCollectionManager.TryRedeemArmorShardsForRandomSkin).
    // Иконка задаётся в самом префабе (Inspector), код её не трогает.
    private void CreateArmorShardsTile()
    {
        var prefab = Resources.Load<GameObject>("UI/ArmorShardsTile");
        if (prefab == null) return;

        int balance = PlayerCurrencies.Instance != null ? PlayerCurrencies.Instance.GetBalance(CurrencyType.ArmorShards) : 0;

        GameObject tileObj = Instantiate(prefab, gridContainer);
        tileObj.name = "ArmorShardsBalance";
        tileObj.GetComponent<ArmorShardsTileUI>()?.Setup(balance, OnRedeemArmorShardsClicked);
    }

    private void OnRedeemArmorShardsClicked()
    {
        var canvas = GetComponentInParent<Canvas>();
        Transform canvasRoot = canvas != null ? canvas.transform : transform;

        ConfirmationDialog.Show(canvasRoot, "Spend 3 Armor Shards on a random hero's Wondrous Armor skin?", () =>
        {
            var hero = HeroCollectionManager.Instance?.TryRedeemArmorShardsForRandomSkin();
            ConfirmationDialog.ShowInfo(canvasRoot, hero != null
                ? $"{hero.heroName} received a new Wondrous Armor skin!"
                : "Not enough Armor Shards (needs 3).");
            PopulateGrid();
        });
    }
}
