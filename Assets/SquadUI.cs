using UnityEngine;
using UnityEngine.UI;

public class SquadUI : MonoBehaviour
{
    public HeroCollectionManager collectionManager;
    public SquadSlotUI[] slots; // рівно 4 елементи, призначити в Inspector

    private void OnEnable()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < collectionManager.squad.Count)
                slots[i].SetHero(collectionManager.squad[i], this);
            else
                slots[i].SetEmpty();
        }
    }
}


