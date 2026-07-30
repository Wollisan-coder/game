using UnityEngine;

public class SquadUI : MonoBehaviour
{
    public MainMenuUI mainMenuUI;
    public HeroInventoryUI inventoryUI; // общий попап на всю сцену — тот же, что и в HeroCollectionUI
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
        var collectionManager = HeroCollectionManager.Instance;

        // Количество слотов всегда фиксировано (4) — Бараки поднимают лимит веса отряда, а не количество слотов
        for (int i = 0; i < slots.Length; i++)
        {
            if (collectionManager != null && i < collectionManager.squad.Count && collectionManager.squad[i] != null)
                slots[i].SetHero(collectionManager.squad[i], this);
            else
                slots[i].SetEmpty();
        }
    }
}