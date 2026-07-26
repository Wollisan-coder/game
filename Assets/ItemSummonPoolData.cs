using UnityEngine;

[System.Serializable]
public class ItemSummonEntry
{
    public ItemData item;
    public float weightViaShards = 1f;  // вага при оплаті SummonShards
    public float weightViaPremium = 1f; // вага при оплаті PremiumGems (зазвичай вищі шанси на топ-рідкість)
}

[CreateAssetMenu(fileName = "NewItemSummonPool", menuName = "Castle/Item Summon Pool")]
public class ItemSummonPoolData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string poolId;

    [Header("Можливі результати")]
    public ItemSummonEntry[] entries;

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

    public ItemSummonEntry RollWeighted(bool usePremium, System.Func<float> random01)
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

    public ItemSummonEntry PickHighestOfRarityOrAbove(Rarity minRarity)
    {
        ItemSummonEntry best = null;

        foreach (var entry in entries)
        {
            if (entry.item == null || entry.item.rarity < minRarity) continue;
            if (best == null || entry.item.rarity > best.item.rarity) best = entry;
        }

        return best;
    }
}
