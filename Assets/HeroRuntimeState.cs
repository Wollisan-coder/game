using UnityEngine;

[System.Serializable]
public class HeroRuntimeState
{
    public HeroData data;
    public int currentResource;

    public int level;
    public int maxHealth;
    public int currentHealth;
    public int maxResource;         // heroData.maxResource * бонус от уровня + бонусы от экипировки
    public float damageMultiplier;  // heroData.damageMultiplier * бонус от уровня + бонусы от экипировки
    public int armor;               // heroData.armor * бонус от уровня + бонусы от экипировки

    public bool blockManaGainThisTurn; // true после использования навыка — пропускает следующее начисление маны

    // Для скиллов расы Людей
    public SkillData lastUsedSkill;              // последний использованный скилл этого героя (для CopyAllyLastSkill)
    [Range(0f, 1f)] public float costReductionPercent; // скидка на стоимость следующего скилла (ReduceAllyNextSkillCost), тратится сразу после использования
    public SkillData borrowedSkill;               // временно одолженный legendary-скилл другой расы (BorrowAllyLegendarySkill)
    public int borrowedSkillTurnsRemaining;

    // Для негативных эффектов гемблинг-колеса
    public int stunnedTurnsRemaining;      // герой полностью "пропускает" ходы — его цвет не даёт урон/ману (StunRandomHero)
    public int skillBlockedTurnsRemaining; // герой не может использовать скилл, но матчи всё ещё работают (BlockHeroSkill)

    public HeroRuntimeState(HeroData heroData, int heroLevel = 1)
    {
        data = heroData;
        currentResource = 0;

        level = heroLevel;

        var baseStats = HeroStatUtility.CalculateBaseStats(heroData, heroLevel);
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
