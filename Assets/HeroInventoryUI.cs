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

    private HeroData currentHero;
    private HeroOwnershipData currentOwnership;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

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
        if (levelText != null) levelText.text = $"Рівень: {(currentOwnership != null ? currentOwnership.level : 1)}";
        if (descriptionText != null) descriptionText.text = currentHero.description;

        PopulateSkills();
        PopulateItems();
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

    public void EquipItem(EquipmentSlotType slotType, string itemId)
    {
        if (currentOwnership == null) return;

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
}
