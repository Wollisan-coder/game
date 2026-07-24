using UnityEngine;
using UnityEngine.UI;

public class HeroCollectionCardUI : MonoBehaviour
{
    public Image portrait;
    public Image lockOverlay;
    public Button selectButton;

    private HeroData heroData;
    private HeroCollectionManager collectionManager;

    public void Setup(HeroData data, HeroCollectionManager manager)
    {
        heroData = data;
        collectionManager = manager;

        portrait.sprite = data.portrait;

        bool unlocked = manager.IsUnlocked(data);
        lockOverlay.gameObject.SetActive(!unlocked);
        selectButton.interactable = unlocked;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelected);
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
            return;
        }

        Debug.Log("Клікніть на слот у загоні, щоб вибрати героя для нього.");
    }
}