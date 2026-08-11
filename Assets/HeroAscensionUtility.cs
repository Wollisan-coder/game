using UnityEngine;
using UnityEngine.UI;

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

    // Достигнута ли ПОСЛЕДНЯЯ ступень вознесения (не путать с "вознесение доступно вообще" — Green/Blue
    // всегда вернут false, у них GetMaxAscension=0). Используется для финальных наград — см.
    // project_hero_ascension_system: смена портрета (все редкости с вознесением) + апгрейд пассивки (только Orange).
    public static bool IsMaxAscension(Rarity rarity, int ascensionLevel)
    {
        int max = GetMaxAscension(rarity);
        return max > 0 && ascensionLevel >= max;
    }

    // Портрет для отображения — ascendedPortrait, если герой на полном вознесении И такой арт назначен,
    // иначе обычный portrait. Общий метод, чтобы карточка коллекции/инвентарь/слот отряда не могли разойтись.
    public static Sprite GetDisplayPortrait(HeroData hero, HeroOwnershipData ownership)
    {
        if (hero == null) return null;

        bool maxed = ownership != null && IsMaxAscension(hero.rarity, ownership.ascensionLevel);
        if (maxed && hero.ascendedPortrait != null)
            return hero.ascendedPortrait;

        return hero.portrait;
    }

    // Крупный портрет для окна инвентаря — свой отдельный арт (hero.inventoryPortrait), если назначен,
    // иначе тот же результат, что и на маленькой карточке (см. GetDisplayPortrait).
    public static Sprite GetInventoryPortrait(HeroData hero, HeroOwnershipData ownership)
    {
        if (hero == null) return null;
        if (hero.inventoryPortrait != null) return hero.inventoryPortrait;
        return GetDisplayPortrait(hero, ownership);
    }

    // Общая подстановка картинки вознесения (трикветра медальонов) — используется в карточке коллекции,
    // инвентаре героя и слоте отряда. Если у редкости нет вознесения (Green/Blue/White) или спрайта для
    // текущего уровня нет в наборе — просто скрывает Image, как и RarityUtility.ApplyFrame.
    public static void ApplyOverlay(Image overlayImage, AscensionOverlaySet overlaySet, Rarity rarity, int ascensionLevel)
    {
        if (overlayImage == null) return;

        Sprite sprite = overlaySet != null ? overlaySet.GetOverlay(rarity, ascensionLevel) : null;
        overlayImage.sprite = sprite;
        overlayImage.enabled = sprite != null;
    }
}
