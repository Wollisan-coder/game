using UnityEngine;

[System.Serializable]
public class ItemSummonEntry
{
    public ItemData item;
    public float weightViaShards = 1f;  // вес при оплате SummonShards
    public float weightViaPremium = 1f; // вес при оплате PremiumGems (обычно выше шансы на топ-редкость)
}

[CreateAssetMenu(fileName = "NewItemSummonPool", menuName = "Castle/Item Summon Pool")]
public class ItemSummonPoolData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string poolId;

    [Header("Возможные результаты")]
    public ItemSummonEntry[] entries;

    [Header("Стоимость одного призыва")]
    public int shardCost = 100;
    public int premiumCost = 10;

    [Header("Гарант (только для оплаты PremiumGems)")]
    public bool hasPity = true;
    public int pityThreshold = 50;               // после стольких призывов подряд без топ-редкости — гарантия
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
