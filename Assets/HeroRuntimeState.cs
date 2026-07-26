using UnityEngine;

[System.Serializable]
public class HeroRuntimeState
{
    public HeroData data;
    public int currentResource;

    public int level;
    public int maxHealth;
    public int currentHealth;
    public int maxResource;         // копія heroData.maxResource + бонуси від екіпіровки
    public float damageMultiplier;  // копія heroData.damageMultiplier + бонуси від екіпіровки

    public bool blockManaGainThisTurn; // true після використання навички — пропускає наступне нарахування мани

    public HeroRuntimeState(HeroData heroData, int heroLevel = 1)
    {
        data = heroData;
        currentResource = 0;

        level = heroLevel;
        maxHealth = heroData.maxHealth;
        currentHealth = maxHealth;
        maxResource = heroData.maxResource;
        damageMultiplier = heroData.damageMultiplier;
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
