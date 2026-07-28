using UnityEngine;

// Общая логика цвета редкости — используется и предметами, и героями
public static class RarityUtility
{
    public static Color GetColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.White: return Color.white;
            case Rarity.Green: return new Color(0.2f, 0.85f, 0.2f);
            case Rarity.Blue: return new Color(0.25f, 0.55f, 1f);
            case Rarity.Purple: return new Color(0.65f, 0.25f, 0.9f);
            case Rarity.Orange: return new Color(1f, 0.55f, 0.1f);
            default: return Color.white;
        }
    }
}
