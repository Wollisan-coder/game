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

    // Викликається один раз при ініціалізації слотів у SquadUI
    public void Initialize(SquadUI squadUI)
    {
        parentUI = squadUI;
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
            Debug.Log($"Клікнуто на слот з slotIndex = {slotIndex}, ім'я об'єкта: {gameObject.name}");

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