using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HeroInventoryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Карточка героя")]
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text healthText;
    public TMP_Text levelText;
    public TMP_Text descriptionText;

    [Header("Рамка редкости — общий ассет RarityFrameSet на всю игру")]
    public Image rarityFrame;
    public RarityFrameSet rarityFrameSet;

    [Header("Эмблема расы — общий ассет RaceEmblemSet на всю игру")]
    public Image raceEmblem;
    public RaceEmblemSet raceEmblemSet;

    [Header("Вознесение — общий ассет AscensionOverlaySet на всю игру")]
    public Image ascensionOverlay;
    public AscensionOverlaySet ascensionOverlaySet;
    public Button ascendButton;  // клик по самому значку — тратит гемы, поднимает потолок уровня
    public Image ascendOutline;  // тонкое кольцо вокруг значка, видно пока вознесение вообще доступно (relevant)
    public Image ascendGlow;     // мягкое пятно позади значка — маленькое и статичное, пока просто "доступно";
                                  // крупнее и мерцает, когда гемов хватает прямо сейчас (см. RefreshAscendButton)

    [Header("Пассивка расы — вкл/выкл за RacePassiveUtility.ManaCost маны, см. BattleManager")]
    public TMP_Text racePassiveInfoText;
    public Button racePassiveToggleButton;
    public Image racePassiveToggleHighlight; // необязательно — подсветка "включено", как у passiveSkillHighlights

    [Header("Статы (база героя + бонусы от текущей экипировки) — справа под слотами предметов")]
    public TMP_Text statsText;

    [Header("Навыки — вкладки активного (4 кнопки, по индексу heroData.skills[i])")]
    public Button[] activeSkillTabs;
    public Image[] activeSkillHighlights; // подсветка выбранной вкладки, тот же порядок (необязательно)

    [Header("Навыки — кнопки выбора пассивного (4, тот же индекс; может совпадать с активным)")]
    public Button[] passiveSkillButtons;
    public Image[] passiveSkillHighlights; // необязательно

    [Header("Инфо — оба видны одновременно, каждый под свой текущий выбор (не только по клику)")]
    public TMP_Text activeSkillInfoText;  // мана + описание текущего активного навыка
    public TMP_Text passiveSkillInfoText; // мана + описание текущего пассивного навыка (пусто, если не выбран)

    [Header("Фон под инфо-блоки — панель под текст описания, см. ConfirmationDialog.StyleAsDescriptionPanel")]
    public Image activeSkillInfoBg;
    public Image passiveSkillInfoBg;
    public Image racePassiveInfoBg;

    [Header("Скролл описаний (необязательно) — сбрасывается наверх при смене навыка")]
    public ScrollRect activeSkillInfoScroll;
    public ScrollRect passiveSkillInfoScroll;

    [Header("Инвентарь (предметы)")]
    public Transform itemsContainer;
    public GameObject itemSlotPrefab; // слот с компонентом ItemSlotUI
    public ItemPickerUI itemPicker;

    private static readonly EquipmentSlotType[] AllSlotTypes =
    {
        EquipmentSlotType.Weapon,
        EquipmentSlotType.Armor,
        EquipmentSlotType.Accessory,
        EquipmentSlotType.Trinket
    };

    [Header("Кнопка закрытия")]
    public Button closeButton;

    private Button heroUpgradeButton; // строится программно — прокачать героя за валюту CurrencyType.HeroExperience
    private Image heroUpgradeBg;
    private TMP_Text heroUpgradeText;

    // Конвертация банка ascensionGems — 1 кнопка над Upgrade (см. CreateGemConversionUIIfNeeded).
    // ascensionGems тратятся ТОЛЬКО на AscendHero и на Hero Voucher (Orange-путь) — конвертация в опыт
    // убрана полностью (см. project_gem_economy_v2_redesign_pending): единая валюта опыта копится сама
    // по себе через HandleDuplicatePull/ProgressExchangeUI, а не выжимается из гемов вознесения.
    private TMP_Text gemCountText;
    private Button convertToVoucherButton;
    private TMP_Text convertToVoucherText;

    private const float SwipeThreshold = 80f; // минимальная длина свайпа по X (пиксели), чтобы засчитать переключение героя
    private const float SkillButtonTextPadding = 24f; // запас по ширине сверх самого текста названия навыка

    private HeroData currentHero;
    private HeroOwnershipData currentOwnership;

    // Исходная (заданная в редакторе) ширина каждой кнопки — растём только сверх неё, никогда не сжимаем меньше
    private float[] activeSkillTabBaseWidths;
    private float[] passiveSkillButtonBaseWidths;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (racePassiveToggleButton != null)
            racePassiveToggleButton.onClick.AddListener(OnRacePassiveToggleClicked);

        if (ascendButton != null)
            ascendButton.onClick.AddListener(OnAscendClicked);

        if (ascendGlow != null)
        {
            ascendGlow.sprite = GetRadialGlowSprite();
            ascendGlow.raycastTarget = false;
        }

        if (ascendOutline != null)
        {
            ascendOutline.sprite = GetRingOutlineSprite();
            ascendOutline.color = new Color(1f, 0.9f, 0.55f, 1f);
            ascendOutline.raycastTarget = false;
        }

        if (activeSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(activeSkillInfoBg);
        if (passiveSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(passiveSkillInfoBg);
        if (racePassiveInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(racePassiveInfoBg);

        CacheSkillButtonBaseWidths();
        CreateUpgradeButtonIfNeeded();
        CreateGemConversionUIIfNeeded();

        // Панель уже сохранена выключенной в сцене (m_IsActive: 0) — не гасим её здесь ещё раз:
        // если вызвать SetActive(false) из Awake(), а Awake() впервые запускается ИМЕННО во время
        // первого Open()->SetActive(true), этот вызов сразу отменяет только что выполненную активацию.
    }

    // IBeginDragHandler/IDragHandler намеренно пустые — нам нужен только суммарный сдвиг в OnEndDrag,
    // но Unity определяет цель перетаскивания именно через IBeginDragHandler, поэтому без него OnEndDrag не сработает.
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }

    // Свайп по X внутри панели — переключает на соседнего героя (влево = предыдущий, вправо = следующий)
    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - eventData.pressPosition.x;
        if (Mathf.Abs(deltaX) < SwipeThreshold) return;

        NavigateHero(deltaX > 0 ? -1 : 1);
    }

    // Переходит к предыдущему/следующему (по направлению) разблокированному герою в том же порядке,
    // что и в коллекции (HeroCollectionManager.Instance.allHeroes), с переходом по кругу.
    private void NavigateHero(int direction)
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        var unlocked = HeroCollectionManager.Instance.allHeroes.Where(h => HeroCollectionManager.Instance.IsUnlocked(h)).ToList();
        if (unlocked.Count < 2) return;

        int index = unlocked.FindIndex(h => h.heroId == currentHero.heroId);
        if (index < 0) return;

        int nextIndex = (index + direction + unlocked.Count) % unlocked.Count;
        Open(unlocked[nextIndex]);
    }

    public void Open(HeroData hero)
    {
        if (hero == null || HeroCollectionManager.Instance == null) return;

        currentHero = hero;
        currentOwnership = HeroCollectionManager.Instance.ownership.Find(o => o.heroId == hero.heroId);

        transform.SetAsLastSibling(); // поднимаем над другими панелями (Squad, Collection и т.д.), иначе они перехватывают клик
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (currentHero == null) return;

        if (portraitImage != null) portraitImage.sprite = HeroAscensionUtility.GetInventoryPortrait(currentHero, currentOwnership);
        if (heroNameText != null) heroNameText.text = currentHero.heroName;
        if (healthText != null) healthText.text = $"HP: {currentHero.maxHealth}";
        if (descriptionText != null) descriptionText.text = currentHero.description;

        RarityUtility.ApplyFrame(rarityFrame, rarityFrameSet, currentHero.rarity);
        HeroAscensionUtility.ApplyOverlay(ascensionOverlay, ascensionOverlaySet, currentHero.rarity, currentOwnership != null ? currentOwnership.ascensionLevel : 0);

        if (raceEmblem != null)
        {
            Sprite emblem = raceEmblemSet != null ? raceEmblemSet.GetEmblem(currentHero.race) : null;
            raceEmblem.sprite = emblem;
            raceEmblem.enabled = emblem != null;
        }

        RefreshRacePassiveUI();

        if (levelText != null)
        {
            int level = currentOwnership != null ? currentOwnership.level : 1;

            if (currentOwnership != null && HeroCollectionManager.Instance != null)
            {
                int nextThreshold = HeroCollectionManager.Instance.ExperienceToNextLevel(level);
                levelText.text = $"Level: {level} ({currentOwnership.experience}/{nextThreshold} Exp)";
            }
            else
            {
                levelText.text = $"Level: {level}";
            }
        }

        PopulateSkillSelectors();
        PopulateItems();
        RefreshUpgradeButtonTheme();
        RefreshUpgradeButtonVisibility();
        RefreshAscendButton();
        RefreshGemConversionUI();
    }

    private void PopulateSkillSelectors()
    {
        if (currentHero == null || currentHero.skills == null) return;

        int activeIndex = currentOwnership != null ? currentOwnership.activeSkillIndex : 0;
        int passiveIndex = currentOwnership != null ? currentOwnership.passiveSkillIndex : -1;

        // Перебираем по количеству КНОПОК в сцене, а не по currentHero.skills.Length — герои с меньшим
        // числом скиллов (например, только 1) иначе оставляли кнопки с большими индексами нетронутыми,
        // с исходным дефолтным "New Text" и старой шириной из сцены, вместо того чтобы их скрыть.
        int slotCount = Mathf.Max(activeSkillTabs?.Length ?? 0, passiveSkillButtons?.Length ?? 0);
        for (int i = 0; i < slotCount; i++)
        {
            int index = i; // копия для замыкания в лямбдах ниже
            bool hasSkill = index < currentHero.skills.Length && currentHero.skills[index] != null;
            string skillName = hasSkill ? currentHero.skills[index].skillName : "";

            if (activeSkillTabs != null && index < activeSkillTabs.Length && activeSkillTabs[index] != null)
            {
                // Пустой слот скилла (SkillData не назначен) — прятать кнопку целиком, а не показывать
                // пустую узкую (preferredWidth для пустой строки почти 0, кнопка сжималась до ~24px).
                activeSkillTabs[index].gameObject.SetActive(hasSkill);
                if (hasSkill)
                {
                    activeSkillTabs[index].onClick.RemoveAllListeners();
                    activeSkillTabs[index].onClick.AddListener(() => OnActiveSkillTabClicked(index));

                    float baseWidth = activeSkillTabBaseWidths != null && index < activeSkillTabBaseWidths.Length
                        ? activeSkillTabBaseWidths[index] : 0f;
                    SetButtonLabelAndFitWidth(activeSkillTabs[index], skillName, baseWidth);
                }
            }

            if (activeSkillHighlights != null && index < activeSkillHighlights.Length && activeSkillHighlights[index] != null)
                activeSkillHighlights[index].enabled = index == activeIndex;

            if (passiveSkillButtons != null && index < passiveSkillButtons.Length && passiveSkillButtons[index] != null)
            {
                passiveSkillButtons[index].gameObject.SetActive(hasSkill);
                if (hasSkill)
                {
                    // Один и тот же навык может одновременно быть и активным, и пассивным — кнопки не исключают друг друга.
                    passiveSkillButtons[index].onClick.RemoveAllListeners();
                    passiveSkillButtons[index].onClick.AddListener(() => OnPassiveSkillButtonClicked(index));

                    float baseWidth = passiveSkillButtonBaseWidths != null && index < passiveSkillButtonBaseWidths.Length
                        ? passiveSkillButtonBaseWidths[index] : 0f;
                    SetButtonLabelAndFitWidth(passiveSkillButtons[index], skillName, baseWidth);
                }
            }

            if (passiveSkillHighlights != null && index < passiveSkillHighlights.Length && passiveSkillHighlights[index] != null)
                passiveSkillHighlights[index].enabled = index == passiveIndex;
        }

        // Оба инфо-блока всегда отражают текущий выбор, а не только последний клик — активный и пассивный видны разом
        SkillData activeSkill = activeIndex >= 0 && activeIndex < currentHero.skills.Length ? currentHero.skills[activeIndex] : null;
        SkillData passiveSkill = passiveIndex >= 0 && passiveIndex < currentHero.skills.Length ? currentHero.skills[passiveIndex] : null;

        SetSkillInfoText(activeSkillInfoText, activeSkill);
        SetSkillInfoText(passiveSkillInfoText, passiveSkill);

        ResetScrollToTop(activeSkillInfoScroll);
        ResetScrollToTop(passiveSkillInfoScroll);
    }

    // Длинное описание может не влезть в видимую область скролла — при смене навыка возвращаем его наверх,
    // иначе после переключения останется прокручено туда, где было у предыдущего (другого по длине) текста.
    private static void ResetScrollToTop(ScrollRect scroll)
    {
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    // Запоминаем изначальную (заданную вручную в редакторе) ширину каждой кнопки один раз — чтобы потом
    // расширять их под длинное название навыка, но никогда не ужимать обратно ниже авторского размера.
    private void CacheSkillButtonBaseWidths()
    {
        if (activeSkillTabs != null)
        {
            activeSkillTabBaseWidths = new float[activeSkillTabs.Length];
            for (int i = 0; i < activeSkillTabs.Length; i++)
            {
                var rect = activeSkillTabs[i] != null ? activeSkillTabs[i].GetComponent<RectTransform>() : null;
                activeSkillTabBaseWidths[i] = rect != null ? rect.sizeDelta.x : 0f;
            }
        }

        if (passiveSkillButtons != null)
        {
            passiveSkillButtonBaseWidths = new float[passiveSkillButtons.Length];
            for (int i = 0; i < passiveSkillButtons.Length; i++)
            {
                var rect = passiveSkillButtons[i] != null ? passiveSkillButtons[i].GetComponent<RectTransform>() : null;
                passiveSkillButtonBaseWidths[i] = rect != null ? rect.sizeDelta.x : 0f;
            }
        }
    }

    // Ставит название навыка на текстовую надпись внутри кнопки и, если оно не влезает в исходную ширину
    // кнопки, увеличивает саму кнопку под текст (но никогда не сужает меньше исходного размера).
    private static void SetButtonLabelAndFitWidth(Button button, string skillName, float baseWidth)
    {
        if (button == null) return;

        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = skillName ?? "";
        if (label == null) return;

        float preferredWidth = label.GetPreferredValues(skillName ?? "").x + SkillButtonTextPadding;
        float width = Mathf.Max(baseWidth, preferredWidth);

        // Если кнопка лежит под Horizontal/Vertical Layout Group, та каждый layout-проход перетирает
        // RectTransform.sizeDelta своим расчётом — реальный способ повлиять на итоговую ширину в этом
        // случае это LayoutElement.preferredWidth, который Layout Group учитывает сама.
        var layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = button.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;

        // На случай, если родитель НЕ под Layout Group — правим и sizeDelta напрямую тоже (не мешает,
        // если Layout Group реально управляет размером — она всё равно пересчитает поверх этого).
        var rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 size = rect.sizeDelta;
            size.x = width;
            rect.sizeDelta = size;
        }
    }

    private static void SetSkillInfoText(TMP_Text label, SkillData skill)
    {
        if (label == null) return;
        label.text = skill != null ? $"Mana: {skill.cost}\n{skill.description}" : "";
    }

    private void OnActiveSkillTabClicked(int index)
    {
        if (currentHero == null || currentOwnership == null || currentHero.skills == null) return;
        if (index < 0 || index >= currentHero.skills.Length) return;

        SkillData skill = currentHero.skills[index];

        int totalMaxMana = GetTotalMaxMana();
        if (totalMaxMana < skill.cost)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough mana for this skill (needs {skill.cost}, hero's max is {totalMaxMana}).");
            return;
        }

        currentOwnership.activeSkillIndex = index;
        HeroCollectionManager.Instance?.SaveOwnership();
        PopulateSkillSelectors();
    }

    private void OnPassiveSkillButtonClicked(int index)
    {
        if (currentHero == null || currentOwnership == null || currentHero.skills == null) return;
        if (index < 0 || index >= currentHero.skills.Length) return;

        // Повторный клик по уже выбранной пассивке снимает её — мана при этом только освобождается,
        // проверка нехватки маны тут не нужна.
        if (currentOwnership.passiveSkillIndex == index)
        {
            currentOwnership.passiveSkillIndex = -1;
            HeroCollectionManager.Instance?.SaveOwnership();
            PopulateSkillSelectors();
            return;
        }

        SkillData passiveSkill = currentHero.skills[index];

        int activeIndex = currentOwnership.activeSkillIndex;
        SkillData activeSkill = activeIndex >= 0 && activeIndex < currentHero.skills.Length
            ? currentHero.skills[activeIndex] : null;
        int requiredForActive = activeSkill != null ? activeSkill.cost : 0;

        // Пассивка "съедает" часть maxResource в бою (см. BattleManager) — если после этого не хватит
        // маны даже на уже выбранный активный навык, выбор пассивки нужно заблокировать попапом.
        int remainingMana = GetTotalMaxMana() - passiveSkill.cost;
        if (remainingMana < requiredForActive)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough mana — this passive costs {passiveSkill.cost}, leaving only {remainingMana}, " +
                    $"but the active skill needs {requiredForActive}.");
            return;
        }

        currentOwnership.passiveSkillIndex = index;
        HeroCollectionManager.Instance?.SaveOwnership();
        PopulateSkillSelectors();
    }

    private void OnRacePassiveToggleClicked()
    {
        if (currentHero == null || currentOwnership == null) return;

        // Выключение — мана освобождается сразу, проверка нехватки не нужна (симметрично OnPassiveSkillButtonClicked).
        if (currentOwnership.racePassiveEnabled)
        {
            currentOwnership.racePassiveEnabled = false;
            HeroCollectionManager.Instance?.SaveOwnership();
            RefreshRacePassiveUI();
            RefreshStats();
            return;
        }

        int activeIndex = currentOwnership.activeSkillIndex;
        SkillData activeSkill = currentHero.skills != null && activeIndex >= 0 && activeIndex < currentHero.skills.Length
            ? currentHero.skills[activeIndex] : null;
        int requiredForActive = activeSkill != null ? activeSkill.cost : 0;

        // Пассивка расы тоже "съедает" часть maxResource в бою (см. BattleManager) — та же защита, что и
        // у старого passiveSkillIndex: не дать включить, если после этого не хватит маны на активный скилл.
        int remainingMana = GetTotalMaxMana() - RacePassiveUtility.ManaCost;
        if (remainingMana < requiredForActive)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
                ConfirmationDialog.ShowInfo(canvas.transform,
                    $"Not enough mana — the race passive costs {RacePassiveUtility.ManaCost}, leaving only {remainingMana}, " +
                    $"but the active skill needs {requiredForActive}.");
            return;
        }

        currentOwnership.racePassiveEnabled = true;
        HeroCollectionManager.Instance?.SaveOwnership();
        RefreshRacePassiveUI();
        RefreshStats();
    }

    // Текст описания + пометка вкл/выкл с ценой, плюс подсветка кнопки (если назначена).
    private void RefreshRacePassiveUI()
    {
        if (currentHero == null) return;

        bool enabled = currentOwnership != null && currentOwnership.racePassiveEnabled;

        if (racePassiveInfoText != null)
        {
            string state = enabled ? "ON" : "OFF";
            racePassiveInfoText.text = $"{RacePassiveUtility.GetDescription(currentHero.race)}\n" +
                $"[{state}] costs {RacePassiveUtility.ManaCost} mana";
        }

        if (racePassiveToggleHighlight != null)
        {
            Color c = racePassiveToggleHighlight.color;
            c.a = enabled ? 0.6f : 0.05f;
            racePassiveToggleHighlight.color = c;
        }
    }

    private void PopulateItems()
    {
        if (itemsContainer == null || itemSlotPrefab == null) return;

        foreach (Transform child in itemsContainer)
            Destroy(child.gameObject);

        foreach (var slotType in AllSlotTypes)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemsContainer);
            var slot = slotObj.GetComponent<ItemSlotUI>();
            slot.Setup(slotType, this);

            string equippedInstanceId = currentOwnership != null ? currentOwnership.GetEquippedItemInstanceId(slotType) : null;
            ItemOwnershipData equippedStack = (!string.IsNullOrEmpty(equippedInstanceId) && ItemCollectionManager.Instance != null)
                ? ItemCollectionManager.Instance.GetStackByInstanceId(equippedInstanceId)
                : null;
            ItemData equippedItem = equippedStack != null ? ItemCollectionManager.Instance.GetItemById(equippedStack.itemId) : null;

            slot.Refresh(equippedItem);
        }

        RefreshStats();
    }

    // База героя + сумма бонусов от текущей экипировки (через общий HeroStatUtility — тот же расчёт,
    // что использует BattleManager при старте боя, чтобы цифры в меню совпадали с реальными в бою).
    private void RefreshStats()
    {
        if (statsText == null || currentHero == null) return;

        var baseStats = HeroStatUtility.CalculateBaseStats(currentHero,
            currentOwnership != null ? currentOwnership.level : 1,
            currentOwnership != null ? currentOwnership.ascensionLevel : 0);
        var bonuses = HeroStatUtility.CalculateEquipmentBonuses(currentOwnership);

        int totalHealth = baseStats.health + bonuses.health;
        float totalDamage = baseStats.damageMultiplier + bonuses.damageMultiplier;

        statsText.text =
            $"HP: {totalHealth}\n" +
            $"Mana: {GetTotalMaxMana()}\n" +
            $"Damage x{totalDamage:0.00}\n" +
            $"Armor: {baseStats.armor + bonuses.armor}";
    }

    // Итоговый максимум маны героя (база*бонус уровня + бонус экипировки) — используется и для отображения
    // статов, и для проверки "хватит ли маны" при выборе активного/пассивного навыка (раньше эти проверки
    // сверялись с "голым" currentHero.maxResource без бонусов, из-за чего расходились с панелью статов).
    private int GetTotalMaxMana()
    {
        if (currentHero == null) return 0;
        int level = currentOwnership != null ? currentOwnership.level : 1;
        int ascensionLevel = currentOwnership != null ? currentOwnership.ascensionLevel : 0;
        int total = HeroStatUtility.CalculateBaseStats(currentHero, level, ascensionLevel).mana
            + HeroStatUtility.CalculateEquipmentBonuses(currentOwnership).mana;

        // Пассивка расы, если включена, съедает фиксированную ману — та же цифра, что BattleManager
        // вычитает при старте боя (Awake/InitializeBossTraining), чтобы панель статов не расходилась с боем.
        if (currentOwnership != null && currentOwnership.racePassiveEnabled)
            total = Mathf.Max(1, total - RacePassiveUtility.ManaCost);

        return total;
    }

    public void OpenItemPicker(EquipmentSlotType slotType)
    {
        if (itemPicker != null)
            itemPicker.Open(slotType, this);
    }

    // Instance ID стека, экипированного в указанный слот этого героя (null, если слот пуст)
    public string GetEquippedItemInstanceId(EquipmentSlotType slotType)
    {
        return currentOwnership != null ? currentOwnership.GetEquippedItemInstanceId(slotType) : null;
    }

    public void EquipItem(EquipmentSlotType slotType, string itemInstanceId)
    {
        if (currentOwnership == null) return;

        // Конкретный стек уникален — если он уже экипирован на другом герое, снимаем его оттуда ("переносим" сюда)
        if (!string.IsNullOrEmpty(itemInstanceId) && HeroCollectionManager.Instance != null)
            HeroCollectionManager.Instance.UnequipItemFromAllHeroes(itemInstanceId, currentHero.heroId);

        currentOwnership.SetEquippedItem(slotType, itemInstanceId);
        HeroCollectionManager.Instance?.SaveOwnership();
        PopulateItems();
    }

    private void OnUpgradeClicked()
    {
        if (currentHero == null || HeroCollectionManager.Instance == null || PlayerCurrencies.Instance == null) return;

        int needed = HeroCollectionManager.Instance.GetExperienceNeededToCap(currentHero.heroId);
        if (needed <= 0) return;

        int balance = PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience);
        int spend = Mathf.Min(needed, balance);
        if (spend <= 0) return;

        if (PlayerCurrencies.Instance.Spend(CurrencyType.HeroExperience, spend))
        {
            HeroCollectionManager.Instance.GrantExperience(currentHero.heroId, spend);
            Refresh();
        }
    }

    // Кнопку "Upgrade" строим программно рядом с Close (копируя его трансформ),
    // чтобы не редактировать вручную разметку панели героя в сцене.
    private void CreateUpgradeButtonIfNeeded()
    {
        if (heroUpgradeButton != null) return;

        RectTransform referenceRect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        var upgradeObj = new GameObject("HeroUpgradeButton", typeof(RectTransform));
        var upgradeRect = (RectTransform)upgradeObj.transform;
        upgradeRect.SetParent(referenceRect.parent, false);
        upgradeRect.anchorMin = referenceRect.anchorMin;
        upgradeRect.anchorMax = referenceRect.anchorMax;
        upgradeRect.pivot = referenceRect.pivot;
        upgradeRect.sizeDelta = referenceRect.sizeDelta;
        upgradeRect.anchoredPosition = referenceRect.anchoredPosition + new Vector2(0, referenceRect.sizeDelta.y + 12f);

        heroUpgradeBg = upgradeObj.AddComponent<Image>();
        heroUpgradeButton = upgradeObj.AddComponent<Button>();
        heroUpgradeButton.onClick.AddListener(OnUpgradeClicked);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(upgradeRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        heroUpgradeText = textObj.AddComponent<TextMeshProUGUI>();
        heroUpgradeText.text = "Upgrade";
        heroUpgradeText.alignment = TextAlignmentOptions.Center;

        heroUpgradeButton.gameObject.SetActive(false);
    }

    // Счётчик + кнопка, ещё на 1 шаг выше Upgrade — потратить банк ascensionGems этого героя на Hero Voucher
    // (см. HeroCollectionManager.ConvertGemToVoucher). Только для Purple/Orange — у остальных редкостей
    // ascensionGems не копится вообще (см. HandleDuplicatePull).
    private void CreateGemConversionUIIfNeeded()
    {
        if (convertToVoucherButton != null) return;

        RectTransform referenceRect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        float stepY = referenceRect.sizeDelta.y + 12f;
        Vector2 rowBase = referenceRect.anchoredPosition + new Vector2(0, stepY * 2.3f);

        var countObj = new GameObject("AscensionGemCount", typeof(RectTransform));
        var countRect = (RectTransform)countObj.transform;
        countRect.SetParent(referenceRect.parent, false);
        countRect.anchorMin = referenceRect.anchorMin;
        countRect.anchorMax = referenceRect.anchorMax;
        countRect.pivot = referenceRect.pivot;
        countRect.sizeDelta = new Vector2(referenceRect.sizeDelta.x * 2.2f, referenceRect.sizeDelta.y * 0.6f);
        countRect.anchoredPosition = rowBase + new Vector2(0, stepY * 0.75f);
        gemCountText = countObj.AddComponent<TextMeshProUGUI>();
        gemCountText.alignment = TextAlignmentOptions.Center;
        gemCountText.fontSize = 18;
        gemCountText.color = Color.white;

        convertToVoucherButton = BuildGemConversionButton(referenceRect, rowBase, "+1 Voucher", OnConvertGemToVoucherClicked, out convertToVoucherText);
    }

    private Button BuildGemConversionButton(RectTransform referenceRect, Vector2 anchoredPos, string label, UnityEngine.Events.UnityAction onClick, out TMP_Text text)
    {
        var obj = new GameObject(label.Replace(" ", "").Replace("+", "") + "Button", typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(referenceRect.parent, false);
        rect.anchorMin = referenceRect.anchorMin;
        rect.anchorMax = referenceRect.anchorMax;
        rect.pivot = referenceRect.pivot;
        rect.sizeDelta = new Vector2(referenceRect.sizeDelta.x * 0.95f, referenceRect.sizeDelta.y);
        rect.anchoredPosition = anchoredPos;

        var bg = obj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(bg);
        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = 22;

        return btn;
    }

    private void OnConvertGemToVoucherClicked()
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        HeroCollectionManager.Instance.ConvertGemToVoucher(currentHero.heroId);
        Refresh();
    }

    private void RefreshGemConversionUI()
    {
        if (gemCountText == null || currentHero == null || currentOwnership == null) return;

        bool relevant = HeroAscensionUtility.GetMaxAscension(currentHero.rarity) > 0;
        gemCountText.gameObject.SetActive(relevant);
        convertToVoucherButton?.gameObject.SetActive(relevant && currentOwnership.voucherConversionUnlocked);
        if (!relevant) return;

        gemCountText.text = $"Ascension Gems: {currentOwnership.ascensionGems}";

        if (convertToVoucherButton != null)
            convertToVoucherButton.interactable = currentOwnership.ascensionGems >= 1;
    }

    private void OnAscendClicked()
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        bool success = HeroCollectionManager.Instance.AscendHero(currentHero.heroId);

        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            string message = success
                ? $"{currentHero.heroName} ascended! New level cap raised."
                : "Not enough ascension gems (or already at max ascension).";
            ConfirmationDialog.ShowInfo(canvas.transform, message);
        }

        Refresh();
    }

    // Клик по самому значку вознесения (не отдельная кнопка — см. project_hero_card_unification_plan).
    // Значок остаётся видимым всегда (см. HeroAscensionUtility.ApplyOverlay). Три состояния подсказки:
    // недоступно вообще (макс. вознесение/раса без него) — обводка и свечение скрыты;
    // доступно, но гемов не хватает — тонкая обводка + маленькое статичное свечение;
    // хватает гемов прямо сейчас — свечение крупнее и мерцает (см. Update/UpdateAscendGlowPulse).
    // Числового счётчика гемов на карточке больше нет — подсказка полностью визуальная.
    private bool ascendReadyNow;

    private void RefreshAscendButton()
    {
        if (currentHero == null || currentOwnership == null) return;

        int maxAscension = HeroAscensionUtility.GetMaxAscension(currentHero.rarity);
        bool relevant = maxAscension > 0 && currentOwnership.ascensionLevel < maxAscension;
        bool canAscendNow = relevant && currentOwnership.ascensionGems >= HeroAscensionUtility.GemsPerAscension;

        if (ascendButton != null)
            ascendButton.interactable = canAscendNow;

        if (ascendOutline != null)
            ascendOutline.gameObject.SetActive(relevant);

        if (ascendGlow != null)
        {
            ascendGlow.gameObject.SetActive(relevant);
            if (relevant && !canAscendNow)
            {
                // Просто доступно, гемов ещё не хватает — маленькое, статичное, без мерцания
                ascendGlow.rectTransform.localScale = Vector3.one;
                Color c = ascendGlow.color;
                c.a = AscendGlowIdleAlpha;
                ascendGlow.color = c;
            }
        }

        ascendReadyNow = canAscendNow; // дальше подхватывает Update() для мерцания/крупного размера
    }

    private const float AscendGlowIdleAlpha = 0.35f;    // "свечение 1" — просто доступно
    private const float AscendGlowReadyMinAlpha = 0.45f; // "свечение 3" — гемов хватает, мерцает между этими двумя
    private const float AscendGlowReadyMaxAlpha = 0.95f;
    private const float AscendGlowReadyScale = 1.7f;     // крупнее, чем статичное состояние
    private const float AscendGlowPulseSpeed = 2.5f;

    private void Update()
    {
        UpdateAscendGlowPulse();
    }

    private void UpdateAscendGlowPulse()
    {
        if (ascendGlow == null || !ascendReadyNow) return;

        ascendGlow.rectTransform.localScale = new Vector3(AscendGlowReadyScale, AscendGlowReadyScale, 1f);

        float t = (Mathf.Sin(Time.time * AscendGlowPulseSpeed) + 1f) / 2f;
        Color c = ascendGlow.color;
        c.a = Mathf.Lerp(AscendGlowReadyMinAlpha, AscendGlowReadyMaxAlpha, t);
        ascendGlow.color = c;
    }

    private static Sprite radialGlowSprite;

    // Мягкое радиальное пятно (белое, непрозрачное в центре → прозрачное к краю, квадратичный спад) —
    // тонируется цветом снаружи через Image.color. Тот же приём, что и CastleUI.GetRadialGlowSprite,
    // сгенерировано один раз и закэшировано (см. feedback_unity6_no_builtin_ui_sprites — почему не builtin).
    private static Sprite GetRadialGlowSprite()
    {
        if (radialGlowSprite != null) return radialGlowSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(1f - dist / radius);
                a *= a; // квадратичный спад — плотный центр, мягкий длинный хвост к краю
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return radialGlowSprite;
    }

    private static Sprite ringOutlineSprite;

    // Тонкое кольцо (белое на прозрачном) — обводка вокруг значка, отдельная от свечения. Не используем
    // штатный компонент Outline — он дублирует спрайт со сдвигом, а не обводит по силуэту (см. CastleUI).
    private static Sprite GetRingOutlineSprite()
    {
        if (ringOutlineSprite != null) return ringOutlineSprite;

        const int size = 128;
        const float thickness = 8f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f - 2f;
        float innerRadius = outerRadius - thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                bool onRing = dist <= outerRadius && dist >= innerRadius;
                tex.SetPixel(x, y, onRing ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }
        tex.Apply();

        ringOutlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return ringOutlineSprite;
    }

    private void RefreshUpgradeButtonTheme()
    {
        if (heroUpgradeBg != null) ConfirmationDialog.StyleAsButton(heroUpgradeBg);
        if (heroUpgradeText != null) heroUpgradeText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void RefreshUpgradeButtonVisibility()
    {
        if (heroUpgradeButton == null || currentHero == null
            || HeroCollectionManager.Instance == null || PlayerCurrencies.Instance == null) return;

        int needed = HeroCollectionManager.Instance.GetExperienceNeededToCap(currentHero.heroId);
        int balance = PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience);

        heroUpgradeButton.gameObject.SetActive(needed > 0 && balance > 0);
        if (heroUpgradeText != null) heroUpgradeText.text = $"Upgrade ({Mathf.Min(needed, balance)})";
    }
}
