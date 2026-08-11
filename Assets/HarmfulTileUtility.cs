using UnityEngine;

// Текстовые описания + цвет-заглушка для вредных фишек поля (см. HarmfulTileSpawnRule) — используется
// BattlePrepPopup (иконка + попап-описание перед боем) и годится для переиспользования где угодно ещё,
// где нужно объяснить игроку конкретный тип вредной фишки. Реального 2D-арта под иконки пока нет —
// цветная плашка с коротким лейблом, тот же приём, что и другие процедурные заглушки в проекте
// (см. CastleUI.CreateMinesIconButton до того, как появился реальный арт).
public static class HarmfulTileUtility
{
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
