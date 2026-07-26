using UnityEngine;
using UnityEngine.UI;

public class HeroCollectionCardUI : MonoBehaviour
{
    public Image portrait;
    public Image lockOverlay;
    public Button selectButton;
    private HeroInventoryUI inventoryUI; // спільний на всю сцену, передається з HeroCollectionUI при спавні

    private HeroData heroData;
    private HeroCollectionManager collectionManager;

    public void Setup(HeroData data, HeroCollectionManager manager, HeroInventoryUI inventory)
    {
        heroData = data;
        collectionManager = manager;
        inventoryUI = inventory;

        if (portrait != null) portrait.sprite = data.portrait;

        bool unlocked = manager.IsUnlocked(data);
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!unlocked);
        if (selectButton != null) selectButton.interactable = unlocked;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelected);
        }
    }

    private void OnSelected()
    {
        // Якщо зараз активний режим вибору героя для конкретного слота — призначаємо туди
        if (collectionManager.slotBeingEdited >= 0)
        {
            bool assigned = collectionManager.AssignToSlot(heroData);
            if (assigned)
            {
                // Повертаємось до екрану загону після вибору
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

        // Звичайний перегляд колекції — відкриваємо вікно інвентаря героя
        if (inventoryUI != null)
            inventoryUI.Open(heroData);
    }
}