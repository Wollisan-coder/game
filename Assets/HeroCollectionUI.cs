using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Coбирает грид карточек героев + фильтр-панель (поиск по имени, редкость/раса/сортировка — циклические
// кнопки вместо TMP_Dropdown, чтобы не собирать Dropdown Template руками) поверх ScrollRect'а из сцены.
public class HeroCollectionUI : MonoBehaviour
{
    public Transform gridContainer;
    public GameObject heroCardUIPrefab;
    public HeroInventoryUI inventoryUI; // общий попап на всю сцену, назначить в Inspector

    // Green у героев больше не используется (см. UX-правку 2026-08-19) — только у предметов, там не трогали.
    private static readonly Rarity[] RarityOptions = { Rarity.Blue, Rarity.Purple, Rarity.Orange };
    // Race.None (безрасовые герои, см. UX-правку 2026-08-19) не показываем отдельным пунктом фильтра —
    // как и Green выше для редкости, "нет расы" не нужна игроку как фильтр-кнопка с буквальной надписью
    // "None" (найдено на аудите 2026-08-20).
    private static readonly Race[] RaceOptions = ((Race[])System.Enum.GetValues(typeof(Race))).Where(r => r != Race.None).ToArray();

    // Rarity как отдельное поле сортировки убрали — уже есть выделенный фильтр по редкости,
    // сортировка по ней внутри уже отфильтрованного списка бессмысленна и только путала (казалось,
    // что в баре два разных фильтра редкости одновременно).
    private enum SortField { Name, Level, Power }
    private static readonly SortField[] SortOptions = { SortField.Name, SortField.Level, SortField.Power };

    private TMP_InputField searchField;
    private TMP_Text rarityButtonText;
    private TMP_Text raceButtonText;
    private TMP_Text sortButtonText;
    private TMP_Text sortDirectionButtonText;

    private int rarityFilterIndex = 0; // 0 = "All", иначе RarityOptions[index-1]
    private int raceFilterIndex = 0;   // 0 = "All", иначе RaceOptions[index-1]
    private int sortFieldIndex = 0;    // индекс в SortOptions
    private bool sortDescending = false;

    // Последний отображённый в сетке порядок (после поиска/фильтров/сортировки) — читает
    // HeroInventoryUI.NavigateHero, чтобы свайп шёл в том же порядке, что герой видел в сетке слева
    // направо, а не в порядке сырого HeroCollectionManager.allHeroes (баг, найденный 2026-08-19).
    public static List<HeroData> LastDisplayedOrder { get; private set; }

    // PopulateGrid() НЕ вызывается отсюда — только явно из MainMenuUI.ShowCollection() при каждом
    // открытии экрана (тот же приём и по той же причине, что и SquadUI.RefreshSlots/ShowSquad — вызов
    // и здесь, и из Start() означал бы двойную пересборку грида на самом первом заходе).
    private void Start()
    {
        BuildFilterBar();
        RefreshFilterLabels();

        // Перестраиваем сетку, когда закрывается попап инвентаря героя — иначе левелап/вознесение,
        // сделанные внутри попапа, не будут видны в каталоге, пока фильтр/сортировку не тронуть вручную.
        if (inventoryUI != null)
            inventoryUI.OnClosed += PopulateGrid;
    }

    // Карточка — общий 450x600 префаб (HeroMiniCard, тот же, что и в Squad), масштабированный тут
    // вниз до 225x300 (×0.5). Обёртка нужна потому, что GridLayoutGroup сам выставляет sizeDelta
    // прямому потомку под cellSize — если масштабировать сам инстанс карточки напрямую, грид тут же
    // вернёт его к исходному размеру до масштаба.
    private const float CardScale = 0.5f;

    // public — MainMenuUI.ShowCollection() зовёт это явно при каждом открытии экрана (тот же паттерн,
    // что и SquadUI.RefreshSlots вызывается из ShowSquad — см. её комментарий про то, почему OnEnable
    // сам по себе не покрывает повторный клик по уже активной вкладке). Раньше грид строился только
    // один раз из Start(), из-за чего уровни/лок-статус/кнопка Summon "замерзали" после первого захода
    // на экран (найдено 2026-08-10).
    public void PopulateGrid()
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null || gridContainer == null) return;

        // Обратный проход по индексу + немедленный SetParent(null) — а не foreach+Destroy() как раньше:
        // Destroy() отложен до конца кадра, так что старые дети ещё считались бы GridLayoutGroup'ом
        // ПОКА этот же метод синхронно создаёт новых — на один кадр сетка видела старые+новые сразу
        // и пересчитывала лишние ячейки, из-за чего карточки (и тени CardDepthUtility при них) визуально
        // "съезжали" при каждом переключении фильтра, прежде чем щёлкнуть на правильную раскладку
        // (баг 2026-08-18). SetParent(null) убирает ребёнка из gridContainer немедленно, не дожидаясь
        // фактического уничтожения.
        for (int i = gridContainer.childCount - 1; i >= 0; i--)
        {
            var child = gridContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        var displayOrder = GetFilteredSortedHeroes(collectionManager);
        LastDisplayedOrder = displayOrder;

        foreach (var hero in displayOrder)
        {
            var wrapper = new GameObject(hero.heroName + "_Slot", typeof(RectTransform));
            wrapper.transform.SetParent(gridContainer, false);

            GameObject cardObj = Instantiate(heroCardUIPrefab, wrapper.transform);
            var cardRect = (RectTransform)cardObj.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = new Vector3(CardScale, CardScale, 1f);

            var card = cardObj.GetComponent<HeroMiniCardUI>();
            var ownership = collectionManager.ownership.Find(o => o.heroId == hero.heroId);
            card.Setup(hero, ownership);

            bool unlocked = collectionManager.IsUnlocked(hero);
            card.SetLocked(!unlocked);

            if (card.selectButton != null)
            {
                card.selectButton.onClick.RemoveAllListeners();
                card.selectButton.onClick.AddListener(() => OnCardSelected(hero, collectionManager));
            }

            // Заперт, но уже накоплен гем через Hero Voucher (см. HeroCollectionManager.GrantGemToHero) —
            // поверх замка появляется кнопка призыва. selectButton у запертых карточек выключен (см.
            // SetLocked), поэтому это отдельная кнопка, не завязанная на обычный клик по карточке.
            if (!unlocked && ownership != null && ownership.ascensionGems >= 1)
                BuildSummonButton(wrapper.transform, hero);
        }
    }

    private void BuildSummonButton(Transform wrapper, HeroData hero)
    {
        var btnObj = new GameObject("SummonButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(wrapper, false);
        btnRect.anchorMin = new Vector2(0.5f, 0);
        btnRect.anchorMax = new Vector2(0.5f, 0);
        btnRect.pivot = new Vector2(0.5f, 0);
        btnRect.sizeDelta = new Vector2(150, 44); // увеличено под 28pt-пол в подписи (было 130x34 под 16pt)
        btnRect.anchoredPosition = new Vector2(0, 8);

        var bg = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(bg);
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OnSummonClicked(hero));

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Summon";
        text.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;
    }

    private void OnSummonClicked(HeroData hero)
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null) return;

        if (collectionManager.UnlockHeroWithGem(hero.heroId))
            PopulateGrid();
    }

    // Логика клика по карточке — раньше жила в HeroCollectionCardUI.OnSelected, перенесена сюда
    // вместе с уходом от отдельного скрипта на карточку коллекции (см. project_hero_card_unification_plan)
    private void OnCardSelected(HeroData heroData, HeroCollectionManager collectionManager)
    {
        // Collection используется как пикер героя для Boss Training — забираем клик раньше обычной логики
        // выбора отряда/открытия инвентаря, запускаем боевую сцену в training-режиме (см. BattleManager.Awake)
        if (collectionManager.pickingForBossTraining)
        {
            if (!collectionManager.IsUnlocked(heroData)) return; // нельзя тренировать ещё не открытого героя

            collectionManager.pickingForBossTraining = false;

            if (AccountManager.Instance != null)
            {
                AccountManager.Instance.pendingBossTraining = true;
                AccountManager.Instance.pendingBossTrainingHeroId = heroData.heroId;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            return;
        }

        // Если сейчас активен режим выбора героя для конкретного слота — назначаем туда
        if (collectionManager.slotBeingEdited >= 0)
        {
            bool assigned = collectionManager.AssignToSlot(heroData);
            if (assigned)
            {
                // Возвращаемся к экрану отряда после выбора
                var mainMenu = FindAnyObjectByType<MainMenuUI>();
                if (mainMenu != null) mainMenu.ShowSquad();
            }
            else
            {
                int projected = collectionManager.GetProjectedSquadWeight(heroData);
                var canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                    ConfirmationDialog.ShowInfo(canvas.transform,
                        $"Not enough squad weight capacity ({projected}/{collectionManager.MaxSquadWeight}).\nUpgrade Barracks to fit this hero.");
            }
            return;
        }

        // Обычный просмотр коллекции — открываем окно инвентаря героя
        if (inventoryUI != null)
            inventoryUI.Open(heroData);
    }

    private List<HeroData> GetFilteredSortedHeroes(HeroCollectionManager collectionManager)
    {
        string search = searchField != null ? searchField.text?.Trim() : null;

        IEnumerable<HeroData> heroes = collectionManager.allHeroes.Where(h => h != null);

        if (!string.IsNullOrEmpty(search))
            heroes = heroes.Where(h => h.heroName != null && h.heroName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);

        if (rarityFilterIndex > 0)
        {
            Rarity wanted = RarityOptions[rarityFilterIndex - 1];
            heroes = heroes.Where(h => h.rarity == wanted);
        }

        if (raceFilterIndex > 0)
        {
            Race wanted = RaceOptions[raceFilterIndex - 1];
            heroes = heroes.Where(h => h.race == wanted);
        }

        List<HeroData> list = heroes.ToList();
        SortField field = SortOptions[sortFieldIndex];

        switch (field)
        {
            case SortField.Level:
                list = sortDescending
                    ? list.OrderByDescending(h => GetOwnershipLevel(collectionManager, h)).ToList()
                    : list.OrderBy(h => GetOwnershipLevel(collectionManager, h)).ToList();
                break;
            case SortField.Power:
                list = sortDescending
                    ? list.OrderByDescending(h => HeroStatUtility.CalculatePower(h, GetOwnership(collectionManager, h))).ToList()
                    : list.OrderBy(h => HeroStatUtility.CalculatePower(h, GetOwnership(collectionManager, h))).ToList();
                break;
            default:
                list = sortDescending
                    ? list.OrderByDescending(h => h.heroName ?? "").ToList()
                    : list.OrderBy(h => h.heroName ?? "").ToList();
                break;
        }

        return list;
    }

    private static HeroOwnershipData GetOwnership(HeroCollectionManager manager, HeroData hero) =>
        manager.ownership.FirstOrDefault(o => o.heroId == hero.heroId);

    private static int GetOwnershipLevel(HeroCollectionManager manager, HeroData hero) =>
        GetOwnership(manager, hero)?.level ?? 1;

    // --- UI, построенный программно поверх ScrollRect'а карточек (тот уже размечен в сцене) ---

    private void BuildFilterBar()
    {
        if (gridContainer == null) return;

        var scrollRectTransform = gridContainer.GetComponentInParent<ScrollRect>()?.GetComponent<RectTransform>();
        if (scrollRectTransform == null) return;

        // Ужимаем ScrollRect сверху, чтобы освободить место под фильтр-бар — тот же приём, что и в CastleUI.
        const float barHeight = 120f;
        scrollRectTransform.offsetMax = new Vector2(scrollRectTransform.offsetMax.x, -barHeight);

        var barObj = new GameObject("FilterBar", typeof(RectTransform));
        var barRect = (RectTransform)barObj.transform;
        barRect.SetParent(scrollRectTransform.parent, false);
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.sizeDelta = new Vector2(0, barHeight);
        barRect.anchoredPosition = Vector2.zero;
        barObj.transform.SetAsFirstSibling();

        var barBg = barObj.AddComponent<Image>();
        barBg.color = new Color(0, 0, 0, 0.3f);

        searchField = CreateSearchField(barRect, new Vector2(0, -8), new Vector2(-32, 40));
        searchField.onValueChanged.AddListener(_ => PopulateGrid());

        rarityButtonText = CreateCycleButton(barRect, new Vector2(-373, -55), new Vector2(300, 60), OnRarityClicked);
        raceButtonText = CreateCycleButton(barRect, new Vector2(-97, -55), new Vector2(300, 60), OpenRacePopup);
        raceButtonRect = raceButtonText.transform.parent as RectTransform;
        sortButtonText = CreateCycleButton(barRect, new Vector2(155, -55), new Vector2(250, 60), OnSortFieldClicked);
        sortDirectionButtonText = CreateCycleButton(barRect, new Vector2(405, -54), new Vector2(250, 60), OnSortDirectionClicked);
    }

    // --- Раса — не циклическая кнопка (9 вариантов кликать по одному было бы неудобно), а попап-список:
    // клик по фильтру открывает панель, клик по варианту в списке применяет фильтр и сразу закрывает её.

    private GameObject racePopupRoot;
    private RectTransform racePopupListRect;
    private RectTransform raceButtonRect;

    private void OpenRacePopup()
    {
        if (racePopupRoot == null) BuildRacePopup();
        racePopupRoot.transform.SetAsLastSibling();
        racePopupRoot.SetActive(true);
        PositionRacePopupUnderButton();
    }

    // Пересчитывается при каждом открытии (не один раз при постройке) — RectTransformUtility даёт точную
    // позицию через границы Canvas'ов/родителей, надёжнее, чем вручную копировать anchoredPosition кнопки.
    private void PositionRacePopupUnderButton()
    {
        if (racePopupListRect == null || raceButtonRect == null) return;

        var canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        var corners = new Vector3[4];
        raceButtonRect.GetWorldCorners(corners); // 0=bottom-left, 3=bottom-right
        Vector3 bottomCenterWorld = (corners[0] + corners[3]) / 2f;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, bottomCenterWorld);
        var overlayRect = (RectTransform)racePopupRoot.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenPoint, cam, out Vector2 localPoint))
            racePopupListRect.anchoredPosition = localPoint + new Vector2(0, -6f);
    }

    private void CloseRacePopup()
    {
        if (racePopupRoot != null) racePopupRoot.SetActive(false);
    }

    private void BuildRacePopup()
    {
        var canvas = GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : transform;

        racePopupRoot = new GameObject("RaceFilterPopup", typeof(RectTransform));
        var overlayRect = (RectTransform)racePopupRoot.transform;
        overlayRect.SetParent(root, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = racePopupRoot.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.6f);
        var dimBtn = racePopupRoot.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(CloseRacePopup);

        int optionCount = RaceOptions.Length + 1; // + "All"
        float itemHeight = 52f;
        float listHeight = optionCount * itemHeight + 16f;

        var listObj = new GameObject("List", typeof(RectTransform));
        var listRect = (RectTransform)listObj.transform;
        listRect.SetParent(overlayRect, false);
        listRect.anchorMin = new Vector2(0.5f, 0.5f);
        listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.pivot = new Vector2(0.5f, 1f); // верхний край списка = точка привязки (низ кнопки)
        listRect.sizeDelta = new Vector2(300, listHeight);
        racePopupListRect = listRect;
        var listBg = listObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(listBg);

        var listLayout = listObj.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(25, 25, 45, 45);
        listLayout.spacing = 5;
        listLayout.childAlignment = TextAnchor.UpperLeft;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = true;
        
        var listFitter = listObj.AddComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Пустой Button-блокер поверх фона списка — чтобы клик по самому списку (не по варианту)
        // не проваливался на dim-кнопку позади и не закрывал попап раньше времени.
        var listBlocker = listObj.AddComponent<Button>();
        listBlocker.transition = Selectable.Transition.None;

        float y = -8f;
        CreateRaceOption(listRect, "All", y, () => SelectRace(0));
        y -= itemHeight;
        for (int i = 0; i < RaceOptions.Length; i++)
        {
            int optionIndex = i + 1; // 0 = All
            CreateRaceOption(listRect, RaceOptions[i].ToString(), y, () => SelectRace(optionIndex));
            y -= itemHeight;
        }

        racePopupRoot.SetActive(false);
    }

    private void CreateRaceOption(RectTransform parent, string label, float anchoredY, UnityEngine.Events.UnityAction onClick)
    {
        var rowObj = new GameObject("Option_" + label, typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.sizeDelta = new Vector2(-16, 44);
        rowRect.anchoredPosition = new Vector2(0, anchoredY);

        var img = rowObj.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.06f);
        var btn = rowObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rowRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 28;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    private void SelectRace(int index)
    {
        raceFilterIndex = index;
        RefreshFilterLabels();
        PopulateGrid();
        CloseRacePopup();
    }

    private void OnRarityClicked()
    {
        rarityFilterIndex = (rarityFilterIndex + 1) % (RarityOptions.Length + 1);
        RefreshFilterLabels();
        PopulateGrid();
    }

    private void OnSortFieldClicked()
    {
        sortFieldIndex = (sortFieldIndex + 1) % SortOptions.Length;
        RefreshFilterLabels();
        PopulateGrid();
    }

    private void OnSortDirectionClicked()
    {
        sortDescending = !sortDescending;
        RefreshFilterLabels();
        PopulateGrid();
    }

    private void RefreshFilterLabels()
    {
        if (rarityButtonText != null)
            rarityButtonText.text = rarityFilterIndex == 0 ? "Rarity: All" : $"Rarity: {RarityOptions[rarityFilterIndex - 1]}";

        if (raceButtonText != null)
            raceButtonText.text = raceFilterIndex == 0 ? "Race: All" : $"Race: {RaceOptions[raceFilterIndex - 1]}";

        if (sortButtonText != null)
            sortButtonText.text = $"Sort: {SortOptions[sortFieldIndex]}";

        if (sortDirectionButtonText != null)
            sortDirectionButtonText.text = sortDescending ? "Order: Desc" : "Order: Asc";
    }

    // Кнопка-переключатель (клик = следующее значение) — проще и надёжнее, чем собирать TMP_Dropdown
    // руками (Template/Viewport/Item), а вариантов всего несколько в каждой категории.
    private TMP_Text CreateCycleButton(RectTransform parent, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = new GameObject("CycleButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 1);
        btnRect.anchorMax = new Vector2(0.5f, 1);
        btnRect.pivot = new Vector2(0.5f, 1);
        btnRect.sizeDelta = size;
        btnRect.anchoredPosition = anchoredPosition;

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
        text.enableAutoSizing = true; // подписи разной длины ("Rarity: All" vs "Rarity: Orange") — фиксированный размер либо мелкий, либо не влезает
        text.fontSizeMin = 16;
        text.fontSizeMax = 30;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor; // тёмный текст был под старую светлую заливку, на новой тёмно-синей рамке нужен светлый

        return text;
    }

    // Минимальная рабочая сборка TMP_InputField руками (без неё — только TextArea/Text/Placeholder,
    // без которых поле не отображает и не редактирует текст).
    private TMP_InputField CreateSearchField(RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        var fieldObj = new GameObject("SearchField", typeof(RectTransform));
        var fieldRect = (RectTransform)fieldObj.transform;
        fieldRect.SetParent(parent, false);
        fieldRect.anchorMin = new Vector2(0, 1);
        fieldRect.anchorMax = new Vector2(1, 1);
        fieldRect.pivot = new Vector2(0.5f, 1);
        fieldRect.sizeDelta = new Vector2(size.x, size.y);
        fieldRect.anchoredPosition = anchoredPosition;

        var bg = fieldObj.AddComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.9f);
        var field = fieldObj.AddComponent<TMP_InputField>();
        field.targetGraphic = bg;

        var textAreaObj = new GameObject("Text Area", typeof(RectTransform));
        var textAreaRect = (RectTransform)textAreaObj.transform;
        textAreaRect.SetParent(fieldRect, false);
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 6);
        textAreaRect.offsetMax = new Vector2(-10, -6);
        textAreaObj.AddComponent<RectMask2D>();

        var placeholderObj = new GameObject("Placeholder", typeof(RectTransform));
        var placeholderRect = (RectTransform)placeholderObj.transform;
        placeholderRect.SetParent(textAreaRect, false);
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        var placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Search by name...";
        placeholder.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0, 0, 0, 0.4f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(textAreaRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        field.textViewport = textAreaRect;
        field.textComponent = text;
        field.placeholder = placeholder;

        return field;
    }
}
