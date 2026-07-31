using UnityEngine;

// Система вознесения карт героев — ОТДЕЛЬНАЯ от системы редкости предметов (свои уровни/кап).
// Green/Blue вознесения не требуют вообще (кап фиксирован), Purple/Orange раскрывают уровень поэтапно.
public static class HeroAscensionUtility
{
    // Одно вознесение = один жетон (гем) этого героя
    public const int GemsPerAscension = 1;

    // Максимальное число ступеней вознесения для редкости (0 = вознесение не нужно/недоступно)
    public static int GetMaxAscension(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Purple: return 2;
            case Rarity.Orange: return 3;
            default: return 0; // White/Green/Blue
        }
    }

    // Потолок уровня героя на текущей ступени вознесения (ascensionLevel: 0 = база, до GetMaxAscension включительно)
    public static int GetLevelCap(Rarity rarity, int ascensionLevel)
    {
        switch (rarity)
        {
            case Rarity.Green: return 20;
            case Rarity.Blue: return 40;

            case Rarity.Purple:
                switch (Mathf.Clamp(ascensionLevel, 0, 2))
                {
                    case 0: return 60;
                    case 1: return 80;
                    default: return 100;
                }

            case Rarity.Orange:
                switch (Mathf.Clamp(ascensionLevel, 0, 3))
                {
                    case 0: return 80;
                    case 1: return 100;
                    case 2: return 120;
                    default: return 160;
                }

            default: return 20; // White — не выдаётся игрокам, но пусть не крашится, если где-то останется
        }
    }

    // Сколько гемов опыта героя ЭТОЙ редкости выдаётся за дубликат, если герой уже на максимуме вознесения.
    // Плейсхолдер-масштаб: Orange (самая дорогая карта) даёт вдвое больше, чем остальные — легко поменять.
    public static int GetOverflowGemCount(Rarity rarity)
    {
        return rarity == Rarity.Orange ? 2 : 1;
    }
}
