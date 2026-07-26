using UnityEngine;

public class SquadUI : MonoBehaviour
{
    public HeroCollectionManager collectionManager;
    public MainMenuUI mainMenuUI;
    public SquadSlotUI[] slots;

        private void Awake()
    {
        Debug.Log($"SquadUI.Awake() викликано, slots.Length = {slots.Length}");

        for (int i = 0; i < slots.Length; i++)
        {
            Debug.Log($"Призначаю slotIndex={i} для {slots[i].gameObject.name}");
            slots[i].slotIndex = i;
            slots[i].Initialize(this);
        }
    }

    private void OnEnable()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        int capacity = collectionManager != null ? collectionManager.MaxSquadSize : slots.Length;

        for (int i = 0; i < slots.Length; i++)
        {
            bool unlocked = i < capacity;
            slots[i].gameObject.SetActive(unlocked);
            if (!unlocked) continue;

            if (i < collectionManager.squad.Count && collectionManager.squad[i] != null)
                slots[i].SetHero(collectionManager.squad[i], this);
            else
                slots[i].SetEmpty();
        }
    }
}