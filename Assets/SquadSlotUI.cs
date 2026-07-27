using UnityEngine;
using UnityEngine.UI;

public class SquadSlotUI : MonoBehaviour
{
    public int slotIndex;

    public Image portrait;
    public Button removeButton;
    public Button selectButton;
    public GameObject emptyPlaceholder;

    private HeroData heroData;
    private SquadUI parentUI;
    private HeroInventoryUI inventoryUI;

    // Викликається один раз при ініціалізації слотів у SquadUI
    public void Initialize(SquadUI squadUI, HeroInventoryUI inventory)
    {
        parentUI = squadUI;
        inventoryUI = inventory;
    }

    public void SetHero(HeroData data, SquadUI squadUI)
    {
        heroData = data;
        parentUI = squadUI;

        portrait.gameObject.SetActive(true);
        portrait.sprite = data.portrait;
        emptyPlaceholder.SetActive(false);

        removeButton.gameObject.SetActive(true);
        removeButton.onClick.RemoveAllListeners();
        removeButton.onClick.AddListener(OnRemoveClicked);
    }

    public void SetEmpty()
    {
        heroData = null;
        portrait.gameObject.SetActive(false);
        emptyPlaceholder.SetActive(true);
        removeButton.gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private void OnSelectClicked()
    {
        // Якщо в слоті вже стоїть герой — клік по картинці відкриває його інвентар (екіпіровку), а не вибір нового
        if (heroData != null)
        {
            if (inventoryUI != null)
                inventoryUI.Open(heroData);
            else
                Debug.LogWarning("SquadSlotUI: inventoryUI не призначено!");
            return;
        }

        HeroCollectionManager.Instance.StartEditingSlot(slotIndex);

        if (parentUI != null && parentUI.mainMenuUI != null)
            parentUI.mainMenuUI.ShowCollection();
        else
            Debug.LogWarning("SquadSlotUI: parentUI або mainMenuUI не призначено!");
    }

    private void OnRemoveClicked()
    {
        HeroCollectionManager.Instance.RemoveFromSquad(slotIndex);
        if (parentUI != null)
            parentUI.RefreshSlots();
    }
}