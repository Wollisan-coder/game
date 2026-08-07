using UnityEngine;

[System.Serializable]
public class HeroSummonEntry
{
    public HeroData hero;
    public float weightViaShards = 1f;  // вес при оплате SummonShards
    public float weightViaPremium = 1f; // вес при оплате PremiumGems (обычно выше шансы на топ-редкость)
}

[CreateAssetMenu(fileName = "NewHeroSummonPool", menuName = "Castle/Hero Summon Pool")]
public class HeroSummonPoolData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string poolId;

    [Header("Возможные результаты")]
    public HeroSummonEntry[] entries;

    [Header("Стоимость одного призыва")]
    public int shardCost = 100;
    public int premiumCost = 10;

    [Header("Гарант (только для оплаты PremiumGems)")]
    public bool hasPity = true;
    public int pityThreshold = 50;               // после стольких призывов подряд без топ-редкости — гарантия
    public Rarity pityGuaranteedRarity = Rarity.Orange;

    [Header("Джекпот на x10 (см. project_game_design_concept — \"shareable moment\")")]
    // Шанс проверяется один раз на весь x10-пул (не на каждый отдельный призыв). При срабатывании 2 из 10
    // результатов гарантированно — РАЗНЫЕ герои редкости Orange одной случайно выбранной расы; остальные 8 —
    // обычный ролл. Нужно минимум 2 разных Orange-героя одной расы в пуле, иначе джекпот невозможен (SummonService
    // тихо откатывается на обычные пуллы). 0 — джекпот выключен для этого пула.
    public float jackpotChance = 0.0005f;

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
