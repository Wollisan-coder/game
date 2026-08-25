using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroRuntimeState
{
    public HeroData data;
    public int currentResource;

    public int level;
    // Нужен в бою, не только при расчёте статов на старте — см. BattleManager.HasLivingMaxAscendedHeroOfRace
    // (3я ступень вознесения усиливает расовую пассивку, см. project_hero_ascension_system).
    public int ascensionLevel;
    public int maxHealth;
    public int currentHealth;
    public int maxResource;         // heroData.maxResource * бонус от уровня + бонусы от экипировки
    public float damageMultiplier;  // heroData.damageMultiplier * бонус от уровня + бонусы от экипировки
    public int armor;               // heroData.armor * бонус от уровня + бонусы от экипировки

    public bool blockManaGainThisTurn; // true после использования навыка — пропускает следующее начисление маны

    // Скиллы, уже скастованные этим героем с последнего РЕАЛЬНОГО хода (свайпа) — скилл больше не завершает
    // ход сам (см. BattleManager.TryUseSkill, правка 2026-08-19), поэтому до свайпа можно скастовать несколько
    // РАЗНЫХ скиллов подряд, но не один и тот же дважды (иначе, например, FullManaRefill кастуется на себя
    // бесконечно и бесплатно — баг найден на еженедельном аудите 2026-08-20). Очищается в AdvanceTurnTimers().
    public HashSet<SkillData> skillsCastSinceLastRealTurn = new HashSet<SkillData>();

    // Скопировано из HeroOwnershipData.racePassiveEnabled на момент входа в бой — сама пассивка расы
    // применяется только если это true (см. BattleManager.HasLivingHeroOfRace/GetLivingHeroOfRace).
    public bool racePassiveEnabled;

    // Для скиллов расы Людей
    public SkillData lastUsedSkill;              // последний использованный скилл этого героя (для CopyAllyLastSkill)
    [Range(0f, 1f)] public float costReductionPercent; // скидка на стоимость следующего скилла (ReduceAllyNextSkillCost), тратится сразу после использования
    public SkillData borrowedSkill;               // временно одолженный legendary-скилл другой расы (BorrowAllyLegendarySkill)
    public int borrowedSkillTurnsRemaining;

    // Для негативных эффектов гемблинг-колеса
    public int stunnedTurnsRemaining;      // герой полностью "пропускает" ходы — его цвет не даёт урон/ману (StunRandomHero)
    public int skillBlockedTurnsRemaining; // герой не может использовать скилл, но матчи всё ещё работают (BlockHeroSkill)

    // Death Dungeon уравнивает статы — сохранённый игроком активный скилл (HeroOwnershipData.activeSkillIndex)
    // мог стать недоступен без маны-бонуса от диковинки (Trinket). Если да, BattleManager.SetupDeathDungeonNode
    // кладёт сюда замену НА ЭТОТ КОНКРЕТНЫЙ БОЙ — ownership.activeSkillIndex (сохранённый выбор игрока для
    // обычных боёв) не трогается. null = нет переопределения, брать обычным путём (HeroCardUI.ResolveActiveSkill).
    public SkillData effectiveActiveSkillOverride;

    public HeroRuntimeState(HeroData heroData, int heroLevel = 1, int ascensionLevel = 0)
    {
        data = heroData;
        currentResource = 0;

        level = heroLevel;
        this.ascensionLevel = ascensionLevel;

        var baseStats = HeroStatUtility.CalculateBaseStats(heroData, heroLevel, ascensionLevel);
        maxHealth = baseStats.health;
        currentHealth = maxHealth;
        maxResource = baseStats.mana;
        damageMultiplier = baseStats.damageMultiplier;
        armor = baseStats.armor;
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }
}
