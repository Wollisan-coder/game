using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HeroInventoryUI : MonoBehaviour
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

    private HeroData currentHero;
    private HeroOwnershipData currentOwnership;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        CreateUpgradeButtonIfNeeded();

        gameObject.SetActive(false);
    }

    public void Open(HeroData hero)
    {
        if (hero == null || collectionManager == null) return;

        currentHero = hero;
        currentOwnership = collectionManager.ownership.Find(o => o.heroId == hero.heroId);

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

            string equippedId = currentOwnership != null ? currentOwnership.GetEquippedItemId(slotType) : null;
            ItemData equippedItem = (!string.IsNullOrEmpty(equippedId) && itemCollectionManager != null)
                ? itemCollectionManager.GetItemById(equippedId)
                : null;

            slot.Refresh(equippedItem);
        }
    }

    public void OpenItemPicker(EquipmentSlotType slotType)
    {
        if (itemPicker != null)
            itemPicker.Open(slotType, this);
    }

    // Поточний предмет, екіпірований у вказаний слот цього героя (null, якщо слот порожній)
    public string GetEquippedItemId(EquipmentSlotType slotType)
    {
        return currentOwnership != null ? currentOwnership.GetEquippedItemId(slotType) : null;
    }

    public void EquipItem(EquipmentSlotType slotType, string itemId)
    {
        if (currentOwnership == null) return;

        // Предмет унікальний — якщо він уже екіпірований на іншому герої, знімаємо його звідти ("переносимо" сюди)
        if (!string.IsNullOrEmpty(itemId) && collectionManager != null)
            collectionManager.UnequipItemFromAllHeroes(itemId, currentHero.heroId);

        currentOwnership.SetEquippedItem(slotType, itemId);
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
