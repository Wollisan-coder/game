using UnityEngine;
using UnityEngine.UI;

public class HeroCollectionCardUI : MonoBehaviour
{
    public Image portrait;
    public Image lockOverlay;
    public Button selectButton;

    [Header("Рамка редкости — общий ассет RarityFrameSet на всю игру")]
    public Image rarityFrame;
    public RarityFrameSet rarityFrameSet;

    [Header("Эмблема расы — общий ассет RaceEmblemSet на всю игру")]
    public Image raceEmblem;
    public RaceEmblemSet raceEmblemSet;

    private HeroInventoryUI inventoryUI; // общий на всю сцену, передаётся из HeroCollectionUI при спавне

    private HeroData heroData;
    private HeroCollectionManager collectionManager;

    public void Setup(HeroData data, HeroCollectionManager manager, HeroInventoryUI inventory)
    {
        heroData = data;
        collectionManager = manager;
        inventoryUI = inventory;

        var ownership = manager.ownership.Find(o => o.heroId == data.heroId);
        if (portrait != null) portrait.sprite = HeroAscensionUtility.GetDisplayPortrait(data, ownership);

        bool unlocked = manager.IsUnlocked(data);
        if (lockOverlay != null) lockOverlay.gameObject.SetActive(!unlocked);
        if (selectButton != null) selectButton.interactable = unlocked;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelected);
        }

        RarityUtility.ApplyFrame(rarityFrame, rarityFrameSet, data.rarity);

        if (raceEmblem != null)
        {
            Sprite emblem = raceEmblemSet != null ? raceEmblemSet.GetEmblem(data.race) : null;
            raceEmblem.sprite = emblem;
            raceEmblem.enabled = emblem != null;
        }
    }

    private void OnSelected()
    {
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
}