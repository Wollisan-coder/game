using UnityEngine;

public class SquadUI : MonoBehaviour
{
    public HeroCollectionManager collectionManager;
    public MainMenuUI mainMenuUI;
    public HeroInventoryUI inventoryUI; // спільний попап на всю сцену — той самий, що й у HeroCollectionUI
    public SquadSlotUI[] slots;

        private void Awake()
    {
        Debug.Log($"SquadUI.Awake() викликано, slots.Length = {slots.Length}");

        for (int i = 0; i < slots.Length; i++)
        {
            Debug.Log($"Призначаю slotIndex={i} для {slots[i].gameObject.name}");
            slots[i].slotIndex = i;
            slots[i].Initialize(this, inventoryUI);
        }
    }

    private void OnEnable()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        // Кількість слотів завжди фіксована (4) — Бараки піднімають ліміт ваги загону, а не кількість слотів
        for (int i = 0; i < slots.Length; i++)
        {
            if (collectionManager != null && i < collectionManager.squad.Count && collectionManager.squad[i] != null)
                slots[i].SetHero(collectionManager.squad[i], this);
            else
                slots[i].SetEmpty();
        }
    }
}