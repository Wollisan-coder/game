using UnityEngine;

// Общий набор картинок вознесения — один ассет на всю игру (Collection card, Hero inventory, Squad slot),
// по образцу RarityFrameSet. Индекс массива = текущий ascensionLevel героя (0 = ещё не возносился).
// Green/Blue/White сюда не попадают — у них HeroAscensionUtility.GetMaxAscension == 0.
[CreateAssetMenu(fileName = "AscensionOverlaySet", menuName = "UI/Ascension Overlay Set")]
public class AscensionOverlaySet : ScriptableObject
{
    public Sprite[] orangeOverlays; // индекс 0..3
    public Sprite[] purpleOverlays; // индекс 0..2

    public Sprite GetOverlay(Rarity rarity, int ascensionLevel)
    {
        Sprite[] set;
        switch (rarity)
        {
            case Rarity.Orange: set = orangeOverlays; break;
            case Rarity.Purple: set = purpleOverlays; break;
            default: return null;
        }

        return set != null && ascensionLevel >= 0 && ascensionLevel < set.Length ? set[ascensionLevel] : null;
    }
}
