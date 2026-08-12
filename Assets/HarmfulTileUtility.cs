using UnityEngine;

// Текстовые описания + иконки для вредных фишек поля (см. HarmfulTileSpawnRule) — используется
// BattlePrepPopup (иконка + попап-описание перед боем) и годится для переиспользования где угодно ещё,
// где нужно объяснить игроку конкретный тип вредной фишки.
// Реальный арт — Resources/UI/HarmfulTiles/<Type>.png (см. GetIcon), скопирован 2026-08-12 из
// "Assets/3inrow base/" и переключён на Sprite (см. Editor/HarmfulTileIconImporter, Tools/Import Harmful
// Tile Icons) — GetColor/GetShortLabel остаются как fallback на случай, если Resources.Load не найдёт
// спрайт (например для будущих типов, под которые своего арта ещё не завезли).
public static class HarmfulTileUtility
{
    private static readonly System.Collections.Generic.Dictionary<HarmfulTileType, Sprite> IconCache =
        new System.Collections.Generic.Dictionary<HarmfulTileType, Sprite>();

    public static Sprite GetIcon(HarmfulTileType type)
    {
        if (IconCache.TryGetValue(type, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>($"UI/HarmfulTiles/{type}");
        IconCache[type] = sprite; // кэшируем и null — чтобы не повторять Resources.Load на каждый показ попапа для типов без арта
        return sprite;
    }

    public static string GetDescription(HarmfulTileType type, int value)
    {
        return type switch
        {
            HarmfulTileType.Ice => $"Ice — this tile is frozen and can't be swapped for {value} of your turns (or until a match next to it breaks it early).",
            HarmfulTileType.Spike => $"Spike — deals {value} damage to your team every turn while it's on the field.",
            HarmfulTileType.Trap => "Trap — looks like a normal gem, but matching it damages your team instead of helping.",
            HarmfulTileType.Anchor => $"Anchor — can't be cleared by an ordinary match. Needs {value} matches next to it in a row, or a specific hero skill.",
            HarmfulTileType.Cursed => $"Cursed — if not cleared within {value} turns, it infects a random neighboring tile and resets.",
            HarmfulTileType.BloodMark => $"Blood Mark — while at least one is on the field, all damage your team takes is increased by {value}%.",
            HarmfulTileType.Rotten => "Rotten — infects a random neighboring tile into another Rotten tile every turn until cleared.",
            HarmfulTileType.Chaos => "Chaos — when matched, randomizes the color of all surviving neighboring tiles.",
            _ => "",
        };
    }

    public static Color GetColor(HarmfulTileType type) => type switch
    {
        HarmfulTileType.Ice => new Color(0.6f, 0.85f, 1f),
        HarmfulTileType.Spike => new Color(0.9f, 0.3f, 0.3f),
        HarmfulTileType.Trap => new Color(0.7f, 0.3f, 0.9f),
        HarmfulTileType.Anchor => new Color(0.6f, 0.5f, 0.3f),
        HarmfulTileType.Cursed => new Color(0.3f, 0.7f, 0.3f),
        HarmfulTileType.BloodMark => new Color(0.8f, 0.1f, 0.2f),
        HarmfulTileType.Rotten => new Color(0.45f, 0.38f, 0.15f),
        HarmfulTileType.Chaos => new Color(0.9f, 0.6f, 0.1f),
        _ => Color.gray,
    };

    public static string GetShortLabel(HarmfulTileType type) => type switch
    {
        HarmfulTileType.Ice => "ICE",
        HarmfulTileType.Spike => "SPK",
        HarmfulTileType.Trap => "TRP",
        HarmfulTileType.Anchor => "ANC",
        HarmfulTileType.Cursed => "CRS",
        HarmfulTileType.BloodMark => "BLD",
        HarmfulTileType.Rotten => "ROT",
        HarmfulTileType.Chaos => "CHA",
        _ => "?",
    };
}
