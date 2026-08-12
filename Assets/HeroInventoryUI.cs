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

    [Header("Дивная броня — общий ассет WondrousArmorSkinSet на всю игру")]
    public Image wondrousArmorOverlay;
    public WondrousArmorSkinSet wondrousArmorSkinSet;

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

    [Header("Своя картинка пустого слота на каждый тип экипировки (иначе — общий спрайт с itemSlotPrefab)")]
    public Sprite emptySlotWeaponSprite;
    public Sprite emptySlotArmorSprite;
    public Sprite emptySlotAccessorySprite;
    public Sprite emptySlotTrinketSprite;

    private static readonly EquipmentSlotType[] AllSlotTypes =
    {
        EquipmentSlotType.Weapon,
        EquipmentSlotType.Armor,
        EquipmentSlotType.Accessory,
        EquipmentSlotType.Trinket
    };

    [Header("Кнопка закрытия")]
    public Button closeButton;

    // Три шага прокачки за CurrencyType.HeroExperience — +1/+10/до потолка текущей ступени вознесения
    // (см. OnUpgradeStepClicked). int.MaxValue как маркер "до конца", клампится в OnUpgradeStepClicked/
    // RefreshUpgradeButtonVisibility. Раньше была одна кнопка "Upgrade", тратящая весь баланс разом —
    // добавлено 2026-08-12 по явному запросу: пошаговая прокачка с превью стоимости на каждой кнопке.
    private static readonly int[] UpgradeSteps = { 1, 10, int.MaxValue };
    private static readonly string[] UpgradeStepLabels = { "+1", "+10", "Max" };
    private readonly Button[] heroUpgradeButtons = new Button[UpgradeSteps.Length];
    private readonly Image[] heroUpgradeBgs = new Image[UpgradeSteps.Length];
    private readonly TMP_Text[] heroUpgradeTexts = new TMP_Text[UpgradeSteps.Length];

    // Ручной выбор портрета для маркера карты мира (см. HeroCollectionManager.mapAvatarHeroId /
    // PlayerMapMarkerUI) — кнопка видна только для героя на максимальном вознесении, тот же стек, ещё
    // на шаг выше Upgrade.
    private Button mapAvatarButton;
    private TMP_Text mapAvatarButtonText;

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
            ascendOutline.sprite = GetSquareOutlineSprite();
            ascendOutline.type = Image.Type.Sliced;
            ascendOutline.color = new Color(1f, 0.9f, 0.55f, 1f);
            ascendOutline.raycastTarget = false;
            AlignAscendOutlineToButton();
        }

        if (activeSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(activeSkillInfoBg);
        if (passiveSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(passiveSkillInfoBg);
        if (racePassiveInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(racePassiveInfoBg);

        CacheSkillButtonBaseWidths();
        CreateUpgradeButtonIfNeeded();

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
        WondrousArmorSkinSet.Apply(wondrousArmorOverlay, wondrousArmorSkinSet, currentHero.heroId, currentOwnership != null && currentOwnership.wondrousArmorWorn);

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
        CreateMapAvatarButtonIfNeeded();
        RefreshMapAvatarButton();
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
            bool maxAscended = currentOwnership != null
                && HeroAscensionUtility.IsMaxAscension(currentHero.rarity, currentOwnership.ascensionLevel);
            racePassiveInfoText.text = $"{RacePassiveUtility.GetDescription(currentHero.race, maxAscended)}\n" +
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
            slot.Setup(slotType, this, GetEmptySlotSprite(slotType));

            string equippedInstanceId = currentOwnership != null ? currentOwnership.GetEquippedItemInstanceId(slotType) : null;
            ItemOwnershipData equippedStack = (!string.IsNullOrEmpty(equippedInstanceId) && ItemCollectionManager.Instance != null)
                ? ItemCollectionManager.Instance.GetStackByInstanceId(equippedInstanceId)
                : null;
            ItemData equippedItem = equippedStack != null ? ItemCollectionManager.Instance.GetItemById(equippedStack.itemId) : null;

            slot.Refresh(equippedItem);
        }

        RefreshStats();
    }

    private Sprite GetEmptySlotSprite(EquipmentSlotType type) => type switch
    {
        EquipmentSlotType.Weapon => emptySlotWeaponSprite,
        EquipmentSlotType.Armor => emptySlotArmorSprite,
        EquipmentSlotType.Accessory => emptySlotAccessorySprite,
        EquipmentSlotType.Trinket => emptySlotTrinketSprite,
        _ => null
    };

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

    // levelStep — сколько уровней прокачать за клик (1, 10, или int.MaxValue как маркер "до потолка
    // текущей ступени вознесения"). Тратит РОВНО столько CurrencyType.HeroExperience, сколько нужно, чтобы
    // дойти до целевого уровня без остатка (см. GetExperienceNeededForLevel) — либо весь доступный баланс,
    // если его не хватает на полный шаг (тот же принцип частичной траты, что был у старой единой кнопки).
    private void OnUpgradeStepClicked(int levelStep)
    {
        if (currentHero == null || currentOwnership == null || HeroCollectionManager.Instance == null || PlayerCurrencies.Instance == null) return;

        int levelCap = HeroAscensionUtility.GetLevelCap(currentHero.rarity, currentOwnership.ascensionLevel);
        int targetLevel = levelStep >= levelCap ? levelCap : Mathf.Min(currentOwnership.level + levelStep, levelCap);
        int needed = HeroCollectionManager.Instance.GetExperienceNeededForLevel(currentHero.heroId, targetLevel);
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

    // 3 кнопки в один ряд (тот же слот, что раньше занимала одна "Upgrade") строим программно рядом с
    // Close (копируя его трансформ, поделённый на 3 колонки), чтобы не редактировать вручную разметку
    // панели героя в сцене.
    private void CreateUpgradeButtonIfNeeded()
    {
        if (heroUpgradeButtons[0] != null) return;

        RectTransform referenceRect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        Vector2 rowPos = referenceRect.anchoredPosition + new Vector2(0, referenceRect.sizeDelta.y + 12f);
        const float gap = 8f;
        float btnWidth = (referenceRect.sizeDelta.x - gap * 2f) / 3f;
        float height = referenceRect.sizeDelta.y;

        for (int i = 0; i < UpgradeSteps.Length; i++)
        {
            int step = UpgradeSteps[i]; // локальная копия — безопасна для замыкания кнопки ниже
            float xOffset = (i - 1) * (btnWidth + gap);

            var upgradeObj = new GameObject($"HeroUpgradeButton_{UpgradeStepLabels[i]}", typeof(RectTransform));
            var upgradeRect = (RectTransform)upgradeObj.transform;
            upgradeRect.SetParent(referenceRect.parent, false);
            upgradeRect.anchorMin = referenceRect.anchorMin;
            upgradeRect.anchorMax = referenceRect.anchorMax;
            upgradeRect.pivot = referenceRect.pivot;
            upgradeRect.sizeDelta = new Vector2(btnWidth, height);
            upgradeRect.anchoredPosition = rowPos + new Vector2(xOffset, 0);

            heroUpgradeBgs[i] = upgradeObj.AddComponent<Image>();
            heroUpgradeButtons[i] = upgradeObj.AddComponent<Button>();
            heroUpgradeButtons[i].onClick.AddListener(() => OnUpgradeStepClicked(step));

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(upgradeRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            heroUpgradeTexts[i] = textObj.AddComponent<TextMeshProUGUI>();
            heroUpgradeTexts[i].text = UpgradeStepLabels[i];
            heroUpgradeTexts[i].alignment = TextAlignmentOptions.Center;
            heroUpgradeTexts[i].enableAutoSizing = true;
            heroUpgradeTexts[i].fontSizeMin = 10;
            heroUpgradeTexts[i].fontSizeMax = 20;

            upgradeObj.SetActive(false);
        }
    }

    // Общий билдер подписанной кнопки в том же вертикальном стеке над Upgrade — использовался и гемом-в-
    // ваучер (кнопка теперь на плитке AscensionGemTile, см. project_hero_experience_audit_2026_08_11 /
    // AscensionGemTileUI.OnClicked), и Map Avatar ниже, остаётся общим ради второго.
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

    // Ручной выбор портрета для маркера карты мира — доступно только на максимальном вознесении (см.
    // HeroCollectionManager.mapAvatarHeroId / PlayerMapMarkerUI.RefreshHeroVisual). Переиспользует
    // BuildGemConversionButton — тот на самом деле общий билдер подписанной кнопки, не только для гемов.
    // Позиция (2.3 шага) — та же, что раньше занимала кнопка "гем -> Voucher" (см. BuildGemConversionButton),
    // освободившаяся после переноса той кнопки на плитку AscensionGemTile.
    private void CreateMapAvatarButtonIfNeeded()
    {
        if (mapAvatarButton != null) return;

        RectTransform referenceRect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        float stepY = referenceRect.sizeDelta.y + 12f;
        Vector2 pos = referenceRect.anchoredPosition + new Vector2(0, stepY * 2.3f);

        mapAvatarButton = BuildGemConversionButton(referenceRect, pos, "Set as Map Avatar", OnMapAvatarClicked, out mapAvatarButtonText);
    }

    private void OnMapAvatarClicked()
    {
        if (currentHero == null || HeroCollectionManager.Instance == null) return;

        bool isCurrent = HeroCollectionManager.Instance.mapAvatarHeroId == currentHero.heroId;
        HeroCollectionManager.Instance.SetMapAvatarHero(isCurrent ? "" : currentHero.heroId);
        RefreshMapAvatarButton();
    }

    private void RefreshMapAvatarButton()
    {
        if (mapAvatarButton == null || currentHero == null) return;

        bool maxed = currentOwnership != null
            && HeroAscensionUtility.IsMaxAscension(currentHero.rarity, currentOwnership.ascensionLevel);
        mapAvatarButton.gameObject.SetActive(maxed);
        if (!maxed) return;

        bool isCurrent = HeroCollectionManager.Instance != null
            && HeroCollectionManager.Instance.mapAvatarHeroId == currentHero.heroId;
        if (mapAvatarButtonText != null)
            mapAvatarButtonText.text = isCurrent ? "Unset Map Avatar" : "Set as Map Avatar";
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

    private static Sprite squareOutlineSprite;

    // Прямоугольная рамка (белая на прозрачном), 9-слайс — обводка вокруг кнопки вознесения. Раньше тут
    // был круг: ascendButton — это Image БЕЗ спрайта (просто залитый цветом прямоугольник, m_Sprite:
    // fileID 0 в сцене), круг его реальные (прямые) края не обводил. 9-слайс нужен, чтобы толщина рамки
    // не "плыла" при любом размере RectTransform (см. AlignAscendOutlineToButton — размер берётся от
    // самой кнопки + отступ). Не используем штатный компонент Outline — он дублирует спрайт со сдвигом,
    // а не обводит по силуэту (та же оговорка, что и в CastleUI).
    private static Sprite GetSquareOutlineSprite()
    {
        if (squareOutlineSprite != null) return squareOutlineSprite;

        const int size = 32;
        const int thickness = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onBorder = x < thickness || x >= size - thickness || y < thickness || y >= size - thickness;
                tex.SetPixel(x, y, onBorder ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }
        tex.Apply();

        squareOutlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(thickness, thickness, thickness, thickness));
        return squareOutlineSprite;
    }

    private const float AscendOutlinePadding = 6f; // отступ рамки НАРУЖУ от реальных краёв кнопки, на сторону

    // Копирует anchor/pivot/anchoredPosition кнопки вознесения на рамку и раздувает sizeDelta на
    // отступ — раньше эти два RectTransform были заданы независимо и по факту не совпадали (другой pivot,
    // другой размер), поэтому даже прямоугольная рамка не легла бы точно на кнопку без этого шага.
    private void AlignAscendOutlineToButton()
    {
        if (ascendOutline == null || ascendButton == null) return;

        var buttonRect = ascendButton.GetComponent<RectTransform>();
        var outlineRect = ascendOutline.rectTransform;
        if (buttonRect == null) return;

        outlineRect.anchorMin = buttonRect.anchorMin;
        outlineRect.anchorMax = buttonRect.anchorMax;
        outlineRect.pivot = buttonRect.pivot;
        outlineRect.anchoredPosition = buttonRect.anchoredPosition;
        outlineRect.sizeDelta = buttonRect.sizeDelta + new Vector2(AscendOutlinePadding * 2f, AscendOutlinePadding * 2f);
    }

    private void RefreshUpgradeButtonTheme()
    {
        foreach (var bg in heroUpgradeBgs)
            if (bg != null) ConfirmationDialog.StyleAsButton(bg);
        foreach (var text in heroUpgradeTexts)
            if (text != null) text.color = ConfirmationDialog.ButtonTextColor;
    }

    // Каждая из 3 кнопок показывает собственную цену (сколько HeroExperience нужно для ЭТОГО шага) и
    // скрывается, если шаг уже недостижим (герой уже на потолке текущей ступени вознесения) — интерактивность
    // отдельно от видимости, чтобы игрок видел точную цену, даже если валюты пока не хватает на неё.
    private void RefreshUpgradeButtonVisibility()
    {
        if (heroUpgradeButtons[0] == null || currentHero == null || currentOwnership == null
            || HeroCollectionManager.Instance == null || PlayerCurrencies.Instance == null) return;

        int levelCap = HeroAscensionUtility.GetLevelCap(currentHero.rarity, currentOwnership.ascensionLevel);
        int balance = PlayerCurrencies.Instance.GetBalance(CurrencyType.HeroExperience);

        for (int i = 0; i < UpgradeSteps.Length; i++)
        {
            int targetLevel = UpgradeSteps[i] >= levelCap ? levelCap : Mathf.Min(currentOwnership.level + UpgradeSteps[i], levelCap);
            int needed = HeroCollectionManager.Instance.GetExperienceNeededForLevel(currentHero.heroId, targetLevel);

            bool visible = needed > 0;
            heroUpgradeButtons[i].gameObject.SetActive(visible);
            if (!visible) continue;

            heroUpgradeButtons[i].interactable = balance > 0;
            if (heroUpgradeTexts[i] != null)
                heroUpgradeTexts[i].text = $"{UpgradeStepLabels[i]}\n({Mathf.Min(needed, balance)}/{needed})";
        }
    }
}
