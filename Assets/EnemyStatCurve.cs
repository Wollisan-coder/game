using UnityEngine;

// Процедурная кривая силы врага по (территория, номер ноды 1-18) — см. project_campaign_difficulty_curve.
// Заменяет авторство 108 отдельных EnemyData/override-полей одной формулой: EnemyData остаётся архетипом
// (портрет/скиллы/лут-правила), а числа HP/атаки/наградного опыта здесь считаются на лету.
//
// Базовый якорь (T1Reg.maxHP) пересчитан 2026-08-02 с 80 до 360 — старое значение калибровалось без
// сверки с реальным уроном по геммам (BattleManager.aliveDamagePerGem=8/матч), из-за чего живой плейтест
// (2 героя без уровня/шмота) убивал босса орков за 8 ходов при 90%+ HP отряда. Новый якорь считается от
// целевой длины рядового боя (10-15 ходов, взято 12) и оценки урона/ход подходящего этапу отряда (~30,
// экстраполяция от замера ~20/ход на голом 2-героическом отряде — см. project_campaign_difficulty_curve).
//
// Тир-якоря (конец полосы) — рядовые растут ×1.20 территория-к-территории (форма не менялась, только
// стартовое значение). Босс = рядовые той же территории × множитель длины боссфайта, подтверждённый
// пользователем 2026-08-02: Elves/Fairy ×2, Orcs/Beastfolk/Demons/Dragonkin ×3 (Demons/Dragonkin были ×5
// до 2026-08-09 — снижены после того, как фикс скейлинга урона по геммам всё равно не закрыл "стену",
// см. project_campaign_difficulty_curve). Демоны разбиты на 3 полосы внутри себя: лёгкая(1-9, ×1.25 от
// рядовых Зверолюдов) → средняя(10-17, ×1.50 от лёгкой) → босс(18, ×3 от средней). Драконоиды растут от
// Демонов-СРЕДНИХ (не от босса — намеренная передышка после стены), не Демонов.
public static class EnemyStatCurve
{
    public struct Stats
    {
        public int maxHP;
        public int minAttack;
        public int maxAttack;
    }

    private static readonly Stats T1Reg = new Stats { maxHP = 360, minAttack = 5, maxAttack = 12 };
    private static readonly Stats T1Boss = Scale(T1Reg, 2f);

    private static readonly Stats T2Reg = Scale(T1Reg, 1.20f);
    private static readonly Stats T2Boss = Scale(T2Reg, 2f);

    private static readonly Stats T3Reg = Scale(T2Reg, 1.20f);
    private static readonly Stats T3Boss = Scale(T3Reg, 3f);

    private static readonly Stats T4Reg = Scale(T3Reg, 1.20f);
    private static readonly Stats T4Boss = Scale(T4Reg, 3f);

    private static readonly Stats DemonsLight = Scale(T4Reg, 1.25f);
    private static readonly Stats DemonsMedium = Scale(DemonsLight, 1.50f);
    // Босс-множитель снижен 5 -> 3 (2026-08-09) — после фикса скейлинга урона по геммам
    // (GetColorPowerMultiplier, см. project_campaign_difficulty_curve) симуляция всё равно давала
    // ~102-153 хода на Demons-босса, "стена" никуда не делась. ×3 — тот же множитель, что уже
    // подтверждён живым плейтестом на Orcs/Beastfolk (T3Boss), а не новая непроверенная цифра.
    private static readonly Stats DemonsBoss = Scale(DemonsMedium, 3f);

    private static readonly Stats DragonReg = Scale(DemonsMedium, 1.20f);
    private static readonly Stats DragonBoss = Scale(DragonReg, 3f);

    // Ангелы/Люди (7-8): контент ещё не спроектирован (см. project_campaign_difficulty_curve — открытый
    // вопрос), это НЕ подтверждённые цифры — просто продолжение того же +20% паттерна (и того же ×3
    // босса, для единообразия с Demons/Dragonkin), чтобы кривая хотя бы не проваливалась вниз, если
    // кто-то по ошибке создаст ноду с territory=Angels/Humans раньше времени.
    private static readonly Stats AngelsReg = Scale(DragonReg, 1.20f);
    private static readonly Stats AngelsBoss = Scale(AngelsReg, 3f);
    private static readonly Stats HumansReg = Scale(AngelsReg, 1.20f);
    private static readonly Stats HumansBoss = Scale(HumansReg, 3f);

    private const float RampStart = 0.85f; // нода 1 полосы = 85% от значения на конце полосы

    private static Stats Scale(Stats s, float mult) => new Stats
    {
        maxHP = Mathf.RoundToInt(s.maxHP * mult),
        minAttack = Mathf.RoundToInt(s.minAttack * mult),
        maxAttack = Mathf.RoundToInt(s.maxAttack * mult),
    };

    private static Stats Lerp(Stats from, Stats to, float t) => new Stats
    {
        maxHP = Mathf.RoundToInt(Mathf.Lerp(from.maxHP, to.maxHP, t)),
        minAttack = Mathf.RoundToInt(Mathf.Lerp(from.minAttack, to.minAttack, t)),
        maxAttack = Mathf.RoundToInt(Mathf.Lerp(from.maxAttack, to.maxAttack, t)),
    };

    // Рампа "от 85% до 100% от ceiling" на count нод (используется для обычных территорий и для
    // лёгкой полосы Демонов — у них нет continuity с предыдущей территорией, каждая начинается заново).
    private static Stats RampToCeiling(Stats ceiling, int node, int count)
    {
        float t = count > 1 ? (node - 1) / (float)(count - 1) : 1f;
        Stats start = Scale(ceiling, RampStart);
        return Lerp(start, ceiling, t);
    }

    public static Stats GetStats(Race territory, int nodeIndex)
    {
        nodeIndex = Mathf.Clamp(nodeIndex, 1, 18);

        if (territory == Race.Demons)
        {
            if (nodeIndex <= 9) return RampToCeiling(DemonsLight, nodeIndex, 9);
            if (nodeIndex <= 17)
            {
                float t = (nodeIndex - 10) / 7f; // ноды 10..17, continuity от DemonsLight (не с 85%)
                return Lerp(DemonsLight, DemonsMedium, t);
            }
            return DemonsBoss; // нода 18
        }

        (Stats reg, Stats boss) = territory switch
        {
            Race.Elves => (T1Reg, T1Boss),
            Race.Fairy => (T2Reg, T2Boss),
            Race.Orcs => (T3Reg, T3Boss),
            Race.Beastfolk => (T4Reg, T4Boss),
            Race.Dragonkin => (DragonReg, DragonBoss),
            Race.Angels => (AngelsReg, AngelsBoss),
            Race.Humans => (HumansReg, HumansBoss),
            _ => (T1Reg, T1Boss),
        };

        if (nodeIndex >= 18) return boss;
        return RampToCeiling(reg, nodeIndex, 17);
    }

    // heroExperience растёт тем же коэффициентом, что и HP относительно T1-рядовых (baseHP=80) — см.
    // "Вариант C" в памяти: accountExperience остаётся плоским (не отсюда), героям/луту — растёт по кривой.
    private const int BaseHeroXp = 20;

    public static int GetHeroExperience(Race territory, int nodeIndex)
    {
        var stats = GetStats(territory, nodeIndex);
        float ratio = stats.maxHP / (float)T1Reg.maxHP;
        return Mathf.RoundToInt(BaseHeroXp * ratio);
    }

    // Общий множитель ценности лута (Wood/Stone/Shards и т.п.) — применять к базовым суммам, авторимым
    // вручную на EnemyData.loot.currency[], та же логика, что и heroExperience.
    public static float GetLootValueMultiplier(Race territory, int nodeIndex)
    {
        var stats = GetStats(territory, nodeIndex);
        return stats.maxHP / (float)T1Reg.maxHP;
    }

    // Базовый Wood/Stone за T1-ноду 1 (см. project_campaign_difficulty_curve) — растёт тем же множителем,
    // что и всё остальное. Шарды сюда намеренно НЕ входят — только с Shard Mine, чтобы Forge/Altar
    // оставались завязаны на здание, а не фармились с обычных боёв.
    private const int BaseWood = 15;
    private const int BaseStone = 10;

    public static (int wood, int stone) GetCurrency(Race territory, int nodeIndex)
    {
        float ratio = GetLootValueMultiplier(territory, nodeIndex);
        return (Mathf.RoundToInt(BaseWood * ratio), Mathf.RoundToInt(BaseStone * ratio));
    }
}
