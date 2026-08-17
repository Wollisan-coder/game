using UnityEngine;

public enum SkillEffectType
{
    Damage,
    Heal,
    Shield,
    ConvertAndDestroyRed,
    DestroyRows,       // 1. Уничтожение рядов
    ShieldPercent,     // 2. Щит в % от максимального HP
    DamageBuffTurns,   // 3. Множитель урона на N ходов

    // --- Эльфы ---
    DestroyRandomGems,      // T1: разрушить N случайных гемов
    DestroyHarmfulTile,     // T2: уничтожить "вредную" фишку на поле (заглушка под будущую систему дебаффов поля)
    ConvertCellToJoker,     // T3: превратить случайную клетку в джокер (матчится с любым цветом)
    FavorableReshuffle,     // T4: полный шафл доски с гарантированным крупным матчем в пользу игрока

    // --- Гномы/феи ---
    ReduceEnemyArmor,       // T2: снизить броню врага на N ходов (враг получает больше урона)
    Invulnerability,        // T3: невязвимость игрока на 1 ход врага
    ShieldAndReflect,       // T4: щит + отражение % урона одновременно

    // --- Орки ---
    MultiHit,               // T2: множественный удар (2-3 хита)
    DamagePercentCurrentEnemyHP, // T3: урон в % от текущего HP врага
    SacrificeForDamage,     // T4: жертва % своего HP -> урон x3 по врагу

    // --- Зверолюди ---
    ExtraTurn,              // T1: дополнительный ход (без ответа врага)
    StunEnemy,              // T2: оглушение врага на 1 ход
    DamageScalingWithMatches, // T3: урон, растущий от количества матчей за ход
    DoubleFreeTurn,         // T4: двойной ход подряд (два свапа без затрат)

    // --- Дракониды ---
    DelayedDamageMark,      // T2: метка отложенного урона (взрыв через N ходов)
    DamagePercentMaxEnemyHP,// T3: урон в % от максимального HP врага
    CleanseDebuffsAndDamage,// T4: снять все свои дебаффы + AoE-урон одновременно

    // --- Демоны ---
    ReduceEnemyAccuracy,    // T1: снизить точность врага (шанс промаха)
    StealEnemyShield,       // T2: украсть щит врага себе
    WeaknessMarkNextHit,    // T3: метка слабости (следующий удар усилен)
    TransferDebuffToEnemy,  // T4: провокация + перенос своего дебаффа на врага

    // --- Ангелы ---
    FullManaRefill,         // T2: полная заправка маны герою (себе)
    TeamDebuffImmunity,     // T3: иммунитет к дебаффам всей команде на N ходов
    ReviveHero,             // T4: воскрешение погибшего героя

    // --- Люди ---
    ManaTransfer,           // T1: передача маны между героями (случайному другому союзнику)
    ReduceAllyNextSkillCost,// T2: уменьшить стоимость следующего скилла союзника
    CopyAllyLastSkill,      // T3: копирование последнего скилла союзника бесплатно
    BorrowAllyLegendarySkill // T4: временно унаследовать legendary-скилл другой расы в отряде
}

public enum ResourceType { Red = 0, Blue = 1, Green = 2, Yellow = 3, Violet = 4, Pink = 5 }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;
    // Короткая версия для основного экрана (текстовая стена — то, чего просит избегать UX-бриф). Пусто —
    // код сам обрежет description до ~80 символов; заполнено — используется как есть. Полный текст всегда
    // доступен по тапу, см. HeroInventoryUI.SetSkillInfoText.
    [TextArea(1, 2)] public string shortSummary;

    public ResourceType costType;
    public int cost;

    public SkillEffectType effectType;
    public int effectValue; // общее значение (урон/лечение/щит/и т.д., в зависимости от типа)

    [Header("Параметры для DestroyRows")]
    public int rowStart; // первый ряд (0-based индекс, например 1 = второй ряд)
    public int rowEnd;   // последний ряд включительно (например 5 = шестой ряд)

    [Header("Параметры для ShieldPercent")]
    [Range(0f, 1f)] public float shieldPercentOfMaxHP = 0.2f; // 20% по умолчанию

    [Header("Параметры для DamageBuffTurns")]
    public float damageMultiplier = 2f;
    public int buffDurationTurns = 2;

    [Header("Параметры для MultiHit")]
    public int hitCount = 2; // сколько раз ударить (каждый раз на effectValue урона)
}
