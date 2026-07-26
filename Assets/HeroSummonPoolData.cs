using UnityEngine;

[System.Serializable]
public class HeroSummonEntry
{
    public HeroData hero;
    public float weightViaShards = 1f;  // вага при оплаті SummonShards
    public float weightViaPremium = 1f; // вага при оплаті PremiumGems (зазвичай вищі шанси на топ-рідкість)
}

[CreateAssetMenu(fileName = "NewHeroSummonPool", menuName = "Castle/Hero Summon Pool")]
public class HeroSummonPoolData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string poolId;

    [Header("Можливі результати")]
    public HeroSummonEntry[] entries;

    [Header("Вартість одного призову")]
    public int shardCost = 100;
    public int premiumCost = 10;

    [Header("Гарант (лише для оплати PremiumGems)")]
    public bool hasPity = true;
    public int pityThreshold = 50;               // після стількох призовів поспіль без топ-рідкості — гарантія
    public Rarity pityGuaranteedRarity = Rarity.Orange;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(poolId))
            poolId = name;
    }

    public HeroSummonEntry RollWeighted(bool usePremium, System.Func<float> random01)
    {
        if (entries == null || entries.Length == 0) return null;

        float totalWeight = 0f;
        foreach (var entry in entries)
            totalWeight += Mathf.Max(0f, usePremium ? entry.weightViaPremium : entry.weightViaShards);

        if (totalWeight <= 0f) return entries[0];

        float roll = random01() * totalWeight;
        float cumulative = 0f;

        foreach (var entry in entries)
        {
            cumulative += Mathf.Max(0f, usePremium ? entry.weightViaPremium : entry.weightViaShards);
            if (roll <= cumulative)
                return entry;
        }

        return entries[entries.Length - 1];
    }

    public HeroSummonEntry PickHighestOfRarityOrAbove(Rarity minRarity)
    {
        HeroSummonEntry best = null;

        foreach (var entry in entries)
        {
            if (entry.hero == null || entry.hero.rarity < minRarity) continue;
            if (best == null || entry.hero.rarity > best.hero.rarity) best = entry;
        }

        return best;
    }
}
