// Правило-мутация поля, применяемое к ОДНОМУ узлу нового roguelike-режима (см. MutationDungeonNodeType) —
// в отличие от вредных фишек (HarmfulTileSpawnRule, авторится вручную на EnemyData), мутация — свойство
// САМОГО УЗЛА забега, не врага, и живёт ровно один бой. Список ограничен типами, дёшево реализуемыми
// поверх существующего движка матч-3 (см. GridManager) — без переписывания детекции матчей:
// FrozenStart/Overrun переиспользуют HarmfulTileSpawnRule-инфраструктуру, MissingColor/LockedAffinity —
// GridManager.excludedItemType, CrampedBoard — GridManager.width/height (уже голые public-поля),
// PowerSurge/Weakened — существующий BattleManager.damageMultiplier.
public enum BoardMutationType
{
    FrozenStart,    // N случайных фишек стартуют замороженными
    MissingColor,   // один случайный цвет исключён из генерации/респавна на весь бой
    CrampedBoard,   // уменьшенное поле (сложнее находить ходы)
    PowerSurge,     // весь урон отряда +N% (единственная ПОЗИТИВНАЯ мутация — см. MutationUtility.IsPositive)
    Weakened,       // весь урон отряда -N% (штраф — узел с ней даёт лучшую награду)
    Overrun,        // N доп. Spike-фишек поверх обычных вредных фишек врага
    LockedAffinity, // конкретный цвет заблокирован (механически = MissingColor, отдельная запись ради флейвора/UI)
}

// Дефолтные величины + текстовые описания для каждой мутации — используется UI выбора узла (превью
// карточки) и BattleManager (реальное применение эффекта), одна таблица, чтобы цифры не могли разойтись.
public static class MutationUtility
{
    public static int GetDefaultValue(BoardMutationType type) => type switch
    {
        BoardMutationType.FrozenStart => 4,     // фишек
        BoardMutationType.MissingColor => 0,    // не используется (цвет выбирается случайно в рантайме)
        BoardMutationType.CrampedBoard => 6,    // ширина/высота поля (дефолт 8x8)
        BoardMutationType.PowerSurge => 25,     // %
        BoardMutationType.Weakened => 20,       // %
        BoardMutationType.Overrun => 3,         // доп. Spike-фишек
        BoardMutationType.LockedAffinity => 0,  // как MissingColor
        _ => 0,
    };

    public static string GetShortLabel(BoardMutationType type) => type switch
    {
        BoardMutationType.FrozenStart => "Frozen Start",
        BoardMutationType.MissingColor => "Missing Color",
        BoardMutationType.CrampedBoard => "Cramped Board",
        BoardMutationType.PowerSurge => "Power Surge",
        BoardMutationType.Weakened => "Weakened",
        BoardMutationType.Overrun => "Overrun",
        BoardMutationType.LockedAffinity => "Locked Affinity",
        _ => "",
    };

    public static string GetDescription(BoardMutationType type, int value) => type switch
    {
        BoardMutationType.FrozenStart => $"The board starts with {value} tiles already frozen.",
        BoardMutationType.MissingColor => "One random gem color is removed from this fight's board entirely.",
        BoardMutationType.CrampedBoard => $"The board is shrunk to {value}x{value} for this fight.",
        BoardMutationType.PowerSurge => $"Your team's damage is increased by {value}% for this fight.",
        BoardMutationType.Weakened => $"Your team's damage is reduced by {value}% for this fight — better reward.",
        BoardMutationType.Overrun => $"{value} extra Spike tiles litter the board from the start.",
        BoardMutationType.LockedAffinity => "One random gem color is locked out of this fight's board entirely.",
        _ => "",
    };

    // PowerSurge — единственная мутация, которая помогает игроку, а не усложняет бой; используется UI
    // выбора узла, чтобы подсветить карточку иначе (зелёным, а не красным/нейтральным).
    public static bool IsPositive(BoardMutationType type) => type == BoardMutationType.PowerSurge;
}
