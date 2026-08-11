using UnityEngine;

// Текст-детализация для EnemyPassiveData — дополняет авторский passive.description точной цифрой,
// посчитанной из effectType/percent/absorbDamageBonus (тот же принцип, что и HarmfulTileUtility для
// вредных фишек: авторский текст — это флейвор, конкретные числа — отдельно, чтобы не разъезжались,
// если баланс поменяют, а описание в ассете забудут обновить).
public static class EnemyPassiveUtility
{
    private static readonly string[] ColorNames = { "Red", "Blue", "Green", "Yellow", "Violet" };

    public static string GetFullDescription(EnemyPassiveData passive)
    {
        if (passive == null) return "";

        string detail = passive.effectType switch
        {
            EnemyPassiveEffectType.Regen => $"Regenerates {passive.percent:P0} of its max HP at the start of each of its turns.",
            EnemyPassiveEffectType.DamageReduction => $"Reduces all incoming damage by {passive.percent:P0}.",
            EnemyPassiveEffectType.Reflect => $"Reflects {passive.percent:P0} of damage it takes back at a random living hero.",
            EnemyPassiveEffectType.ColorAbsorb =>
                $"Deals {passive.absorbDamageBonus:P0} more damage while your squad has a living " +
                $"{ColorNames[Mathf.Clamp(passive.absorbColorType, 0, ColorNames.Length - 1)]} hero.",
            _ => "",
        };

        if (string.IsNullOrEmpty(passive.description)) return detail;
        if (string.IsNullOrEmpty(detail)) return passive.description;
        return $"{passive.description}\n\n{detail}";
    }

    // Нет отдельного 2D-арта под пассивки врагов — плоский цвет + короткий лейблом по effectType,
    // тот же приём, что и HarmfulTileUtility, пока не появится реальный арт.
    public static Color GetColor(EnemyPassiveEffectType type) => type switch
    {
        EnemyPassiveEffectType.Regen => new Color(0.3f, 0.8f, 0.4f),
        EnemyPassiveEffectType.DamageReduction => new Color(0.5f, 0.5f, 0.55f),
        EnemyPassiveEffectType.Reflect => new Color(0.8f, 0.7f, 0.2f),
        EnemyPassiveEffectType.ColorAbsorb => new Color(0.8f, 0.3f, 0.7f),
        _ => Color.gray,
    };

    public static string GetShortLabel(EnemyPassiveEffectType type) => type switch
    {
        EnemyPassiveEffectType.Regen => "REG",
        EnemyPassiveEffectType.DamageReduction => "DEF",
        EnemyPassiveEffectType.Reflect => "RFL",
        EnemyPassiveEffectType.ColorAbsorb => "ABS",
        _ => "?",
    };
}
