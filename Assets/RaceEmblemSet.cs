using UnityEngine;

// Общий набор эмблем рас — один ассет на всю игру, по аналогии с RarityFrameSet.
[CreateAssetMenu(fileName = "RaceEmblemSet", menuName = "UI/Race Emblem Set")]
public class RaceEmblemSet : ScriptableObject
{
    // Индекс = (int)Race (Elves/Fairy/Orcs/Beastfolk/Demons/Dragonkin/Angels/Humans)
    public Sprite[] emblems;

    public Sprite GetEmblem(Race race)
    {
        int index = (int)race;
        return emblems != null && index >= 0 && index < emblems.Length ? emblems[index] : null;
    }
}
