using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HeroInventoryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Посилання")]
    public HeroCollectionManager collectionManager;

    [Header("Картка героя")]
    public Image portraitImage;
    public TMP_Text heroNameText;
    public TMP_Text healthText;
    public TMP_Text levelText;
    public TMP_Text descriptionText;

    [Header("Навички")]
    public Transform skillsContainer;
    public GameObject skillEntryPrefab;

    [Header("Інвентар (предмети)")]
    public Transform itemsContainer;
    public GameObject itemSlotPrefab; // слот з компонентом ItemSlotUI
    public ItemPickerUI itemPicker;
    public ItemCollectionManager itemCollectionManager;

    private static readonly EquipmentSlotType[] AllSlotTypes =
    {
        EquipmentSlotType.Weapon,
        EquipmentSlotType.Armor,
        EquipmentSlotType.Accessory,
        EquipmentSlotType.Trinket
    };

    [Header("Кнопка закриття")]
    public Button closeButton;

    private Button heroUpgradeButton; // будується програмно — прокачати героя предметом досвіду
    private Image heroUpgradeBg;
    private TMP_Text heroUpgradeText;
    private HeroExperienceItemPickerUI experienceItemPickerUI;

    private const float SwipeThreshold = 80f; // мінімальна довжина свайпу по X (пікселі), щоб зарахувати перемикання героя

    private HeroData currentHero;
    private HeroOwnershipData currentOwnership;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        CreateUpgradeButtonIfNeeded();
        CreateHeroNavButtonsIfNeeded();

        // Панель вже збережена вимкненою в сцені (m_IsActive: 0) — не гасимо її тут ще раз:
        // якщо викликати SetActive(false) з Awake(), а Awake() вперше запускається САМЕ під час
        // першого Open()->SetActive(true), цей виклик одразу скасовує щойно виконану активацію.
    }

    // IBeginDragHandler/IDragHandler навмисно порожні — нам потрібен лише сумарний зсув на OnEndDrag,
    // але Unity визначає ціль перетягування саме через IBeginDragHandler, тож без нього OnEndDrag не спрацює.
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }

    // Свайп по X всередині панелі — перемикає на сусіднього героя (вліво = попередній, вправо = наступний)
    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - eventData.pressPosition.x;
        if (Mathf.Abs(deltaX) < SwipeThreshold) return;

        NavigateHero(deltaX > 0 ? -1 : 1);
    }

    // Переходить до попереднього/наступного (за напрямком) розблокованого героя в тому ж порядку,
    // що й у колекції (collectionManager.allHeroes), з переходом по колу.
    private void NavigateHero(int direction)
    {
        if (currentHero == null || collectionManager == null) return;

        var unlocked = collectionManager.allHeroes.Where(h => collectionManager.IsUnlocked(h)).ToList();
        if (unlocked.Count < 2) return;

        int index = unlocked.FindIndex(h => h.heroId == currentHero.heroId);
        if (index < 0) return;

        int nextIndex = (index + direction + unlocked.Count) % unlocked.Count;
        Open(unlocked[nextIndex]);
    }

    public void Open(HeroData hero)
    {
        if (hero == null || collectionManager == null) return;

        currentHero = hero;
        currentOwnership = collectionManager.ownership.Find(o => o.heroId == hero.heroId);

        transform.SetAsLastSibling(); // піднімаємо над іншими панелями (Squad, Collection тощо), інакше вони перехоплюють клік
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

        if (portraitImage != null) portraitImage.sprite = currentHero.portrait;
        if (heroNameText != null) heroNameText.text = currentHero.heroName;
        if (healthText != null) healthText.text = $"HP: {currentHero.maxHealth}";
        if (descriptionText != null) descriptionText.text = currentHero.description;

        if (levelText != null)
        {
            int level = currentOwnership != null ? currentOwnership.level : 1;

            if (currentOwnership != null && collectionManager != null)
            {
                int nextThreshold = collectionManager.ExperienceToNextLevel(level);
                levelText.text = $"Рівень: {level} ({currentOwnership.experience}/{nextThreshold} Exp)";
            }
            else
            {
                levelText.text = $"Рівень: {level}";
            }
        }

        PopulateSkills();
        PopulateItems();
        RefreshUpgradeButtonTheme();
        RefreshUpgradeButtonVisibility();
    }

    private void PopulateSkills()
    {
        if (skillsContainer == null || skillEntryPrefab == null) return;

        foreach (Transform child in skillsContainer)
            Destroy(child.gameObject);

        if (currentHero.skills == null) return;

        int activeIndex = currentOwnership != null ? currentOwnership.activeSkillIndex : 0;
        int passiveIndex = currentOwnership != null ? currentOwnership.passiveSkillIndex : -1;

        for (int i = 0; i < currentHero.skills.Length; i++)
        {
            GameObject entryObj = Instantiate(skillEntryPrefab, skillsContainer);
            SkillEntryUI entry = entryObj.GetComponent<SkillEntryUI>();
            entry.Setup(currentHero.skills[i], i, this);
            entry.RefreshMarkers(activeIndex, passiveIndex);
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
            ItemOwnershipData equippedStack = (!string.IsNullOrEmpty(equippedInstanceId) && itemCollectionManager != null)
                ? itemCollectionManager.GetStackByInstanceId(equippedInstanceId)
                : null;
            ItemData equippedItem = equippedStack != null ? itemCollectionManager.GetItemById(equippedStack.itemId) : null;

            slot.Refresh(equippedItem);
        }
    }

    public void OpenItemPicker(EquipmentSlotType slotType)
    {
        if (itemPicker != null)
            itemPicker.Open(slotType, this);
    }

    // Instance ID стека, екіпірованого у вказаний слот цього героя (null, якщо слот порожній)
    public string GetEquippedItemInstanceId(EquipmentSlotType slotType)
    {
        return currentOwnership != null ? currentOwnership.GetEquippedItemInstanceId(slotType) : null;
    }

    public void EquipItem(EquipmentSlotType slotType, string itemInstanceId)
    {
        if (currentOwnership == null) return;

        // Конкретний стек унікальний — якщо він уже екіпірований на іншому герої, знімаємо його звідти ("переносимо" сюди)
        if (!string.IsNullOrEmpty(itemInstanceId) && collectionManager != null)
            collectionManager.UnequipItemFromAllHeroes(itemInstanceId, currentHero.heroId);

        currentOwnership.SetEquippedItem(slotType, itemInstanceId);
        PopulateItems();
    }

    public void SetActiveSkill(int index)
    {
        if (currentOwnership == null) return;
        currentOwnership.activeSkillIndex = index;
        PopulateSkills();
    }

    public void SetPassiveSkill(int index)
    {
        if (currentOwnership == null) return;
        // Повторний клік по тій самій навичці знімає позначку пасивної
        currentOwnership.passiveSkillIndex = currentOwnership.passiveSkillIndex == index ? -1 : index;
        PopulateSkills();
    }

    private void OnUpgradeClicked()
    {
        if (experienceItemPickerUI == null || currentHero == null) return;

        experienceItemPickerUI.Open(currentHero.heroId, Refresh);
    }

    // Кнопку "Upgrade" будуємо програмно поруч із Close (копіюючи його трансформ),
    // щоб не редагувати вручну розмітку панелі героя у сцені.
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

    // Стрілки "<" / ">" по боках від портрета — будуються програмно, щоб не редагувати вручну розмітку панелі.
    // Прив'язані до parent'а портрета, зліва/справа по центру висоти — якщо портрет не займає всю ширину
    // картки, позицію можна буде підправити в Inspector (це прості RectTransform-и, а не частина коду).
    private void CreateHeroNavButtonsIfNeeded()
    {
        if (portraitImage == null) return;

        Transform parent = portraitImage.rectTransform.parent;
        if (parent == null) return;

        CreateNavButton(parent, "PrevHeroButton", "<", new Vector2(0, 0.5f), () => NavigateHero(-1));
        CreateNavButton(parent, "NextHeroButton", ">", new Vector2(1, 0.5f), () => NavigateHero(1));
    }

    private void CreateNavButton(Transform parent, string name, string label, Vector2 anchor, System.Action onClick)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(48, 48);
        rect.anchoredPosition = Vector2.zero;

        var bg = obj.AddComponent<Image>();
        bg.color = ConfirmationDialog.ButtonColor;
        var button = obj.AddComponent<Button>();
        button.onClick.AddListener(() => onClick());

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 28;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor;
    }

    private void RefreshUpgradeButtonTheme()
    {
        if (heroUpgradeBg != null) heroUpgradeBg.color = ConfirmationDialog.ButtonColor;
        if (heroUpgradeText != null) heroUpgradeText.color = ConfirmationDialog.ButtonTextColor;
    }

    private void RefreshUpgradeButtonVisibility()
    {
        if (heroUpgradeButton == null || itemCollectionManager == null) return;

        bool hasExperienceItems = itemCollectionManager.ownership.Any(o =>
            o.quantity > 0 && itemCollectionManager.GetItemById(o.itemId)?.category == ItemCategory.HeroExperience);

        heroUpgradeButton.gameObject.SetActive(hasExperienceItems);
    }
}
