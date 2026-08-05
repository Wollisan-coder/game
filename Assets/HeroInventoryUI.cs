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

    private Button heroUpgradeButton; // строится программно — прокачать героя предметом опыта
    private Image heroUpgradeBg;
    private TMP_Text heroUpgradeText;
    private HeroExperienceItemPickerUI experienceItemPickerUI;

    private Button heroAscendButton; // строится программно — потратить гемы вознесения, поднять потолок уровня
    private Image heroAscendBg;
    private TMP_Text heroAscendText;

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

        if (activeSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(activeSkillInfoBg);
        if (passiveSkillInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(passiveSkillInfoBg);
        if (racePassiveInfoBg != null) ConfirmationDialog.StyleAsDescriptionPanel(racePassiveInfoBg);

        CacheSkillButtonBaseWidths();
        CreateUpgradeButtonIfNeeded();
        CreateAscendButtonIfNeeded();

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

        if (portraitImage != null) portraitImage.sprite = HeroAscensionUtility.GetDisplayPortrait(currentHero, currentOwnership);
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
        if (experienceItemPickerUI == null || currentHero == null) return;

        experienceItemPickerUI.Open(currentHero.heroId, Refresh);
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

        experienceItemPickerUI = gameObject.AddComponent<HeroExperienceItemPickerUI>();
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

    // Кнопку "Ascend" строим программно над Upgrade (та уже стоит над Close), той же техникой копирования трансформа.
    private void CreateAscendButtonIfNeeded()
    {
        if (heroAscendButton != null) return;

        RectTransform referenceRect = heroUpgradeButton != null ? heroUpgradeButton.GetComponent<RectTransform>()
            : closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (referenceRect == null) return;

        var ascendObj = new GameObject("HeroAscendButton", typeof(RectTransform));
        var ascendRect = (RectTransform)ascendObj.transform;
        ascendRect.SetParent(referenceRect.parent, false);
        ascendRect.anchorMin = referenceRect.anchorMin;
        ascendRect.anchorMax = referenceRect.anchorMax;
        ascendRect.pivot = referenceRect.pivot;
        ascendRect.sizeDelta = referenceRect.sizeDelta;
        ascendRect.anchoredPosition = referenceRect.anchoredPosition + new Vector2(0, referenceRect.sizeDelta.y + 12f);

        heroAscendBg = ascendObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(heroAscendBg);
        heroAscendButton = ascendObj.AddComponent<Button>();
        heroAscendButton.onClick.AddListener(OnAscendClicked);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(ascendRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        heroAscendText = textObj.AddComponent<TextMeshProUGUI>();
        heroAscendText.alignment = TextAlignmentOptions.Center;
        heroAscendText.color = ConfirmationDialog.ButtonTextColor;
        heroAscendText.enableAutoSizing = true; // "Ascend\nX/Y gems (Z/W)" — длина плавает от чисел
        heroAscendText.fontSizeMin = 14;
        heroAscendText.fontSizeMax = 22;

        heroAscendButton.gameObject.SetActive(false);
    }

    // Видна только для Purple/Orange (Green/Blue вознесение не требуют — см. HeroAscensionUtility) и только
    // пока не достигнут максимум. Подпись показывает гемы этого героя и сколько нужно для следующей ступени.
    private void RefreshAscendButton()
    {
        if (heroAscendButton == null || currentHero == null || currentOwnership == null) return;

        int maxAscension = HeroAscensionUtility.GetMaxAscension(currentHero.rarity);
        bool relevant = maxAscension > 0 && currentOwnership.ascensionLevel < maxAscension;

        heroAscendButton.gameObject.SetActive(relevant);
        if (!relevant) return;

        heroAscendButton.interactable = currentOwnership.ascensionGems >= HeroAscensionUtility.GemsPerAscension;
        if (heroAscendText != null)
        {
            heroAscendText.text = $"Ascend\n{currentOwnership.ascensionGems}/{HeroAscensionUtility.GemsPerAscension} gems " +
                $"({currentOwnership.ascensionLevel}/{maxAscension})";
        }
    }

    private void RefreshUpgradeButtonTheme()
    {
        if (heroUpgradeBg != null) ConfirmationDialog.StyleAsButton(heroUpgradeBg);
        if (heroUpgradeText != null) heroUpgradeText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void RefreshUpgradeButtonVisibility()
    {
        if (heroUpgradeButton == null || ItemCollectionManager.Instance == null) return;

        bool hasExperienceItems = ItemCollectionManager.Instance.ownership.Any(o =>
            o.quantity > 0 && ItemCollectionManager.Instance.GetItemById(o.itemId)?.category == ItemCategory.HeroExperience);

        heroUpgradeButton.gameObject.SetActive(hasExperienceItems);
    }
}
