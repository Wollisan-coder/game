using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class SummonPityState
{
    public string poolId;
    public int pullsSincePity;
}

// Спільний сервіс призову для Алтаря (герої) і Кузні (предмети).
// Гарант рахується окремо на кожен пул і діє лише при оплаті PremiumGems.
public class SummonService : MonoBehaviour
{
    public static SummonService Instance { get; private set; }

    [Header("Стан гаранта по кожному пулу (лише преміум-оплата)")]
    public List<SummonPityState> pityStates = new List<SummonPityState>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    // Призов героя. usePremium — оплата PremiumGems (з гарантом) чи SummonShards (без гаранта).
    public HeroData PullHero(HeroSummonPoolData pool, bool usePremium)
    {
        if (pool == null || PlayerCurrencies.Instance == null || HeroCollectionManager.Instance == null) return null;

        CurrencyType currency = usePremium ? CurrencyType.PremiumGems : CurrencyType.SummonShards;
        int cost = usePremium ? pool.premiumCost : pool.shardCost;
        if (!PlayerCurrencies.Instance.Spend(currency, cost)) return null;

        HeroSummonEntry result = usePremium && pool.hasPity
            ? RollWithPity(pool.poolId, pool.pityThreshold,
                forcedPick: () => pool.PickHighestOfRarityOrAbove(pool.pityGuaranteedRarity) ?? pool.RollWeighted(true, Random01),
                normalRoll: () => pool.RollWeighted(true, Random01),
                hitsGuaranteeRarity: entry => entry?.hero != null && entry.hero.rarity >= pool.pityGuaranteedRarity)
            : pool.RollWeighted(usePremium, Random01);

        if (result?.hero == null) return null;

        HeroCollectionManager.Instance.UnlockHero(result.hero);
        return result.hero;
    }

    // Призов предмета. usePremium — оплата PremiumGems (з гарантом) чи SummonShards (без гаранта).
    public ItemData PullItem(ItemSummonPoolData pool, bool usePremium)
    {
        if (pool == null || PlayerCurrencies.Instance == null || ItemCollectionManager.Instance == null) return null;

        CurrencyType currency = usePremium ? CurrencyType.PremiumGems : CurrencyType.SummonShards;
        int cost = usePremium ? pool.premiumCost : pool.shardCost;
        if (!PlayerCurrencies.Instance.Spend(currency, cost)) return null;

        ItemSummonEntry result = usePremium && pool.hasPity
            ? RollWithPity(pool.poolId, pool.pityThreshold,
                forcedPick: () => pool.PickHighestOfRarityOrAbove(pool.pityGuaranteedRarity) ?? pool.RollWeighted(true, Random01),
                normalRoll: () => pool.RollWeighted(true, Random01),
                hitsGuaranteeRarity: entry => entry?.item != null && entry.item.rarity >= pool.pityGuaranteedRarity)
            : pool.RollWeighted(usePremium, Random01);

        if (result == null || result.item == null) return null;

        ItemCollectionManager.Instance.AddItemCopy(result.item);
        return result.item;
    }

    // Багаторазовий призов героїв (наприклад, x10). Зупиняється раніше, якщо не вистачає валюти —
    // повертає стільки результатів, скільки вдалося фактично оплатити.
    public List<HeroData> PullHeroMultiple(HeroSummonPoolData pool, bool usePremium, int count)
    {
        var results = new List<HeroData>();
        for (int i = 0; i < count; i++)
        {
            var hero = PullHero(pool, usePremium);
            if (hero == null) break;
            results.Add(hero);
        }
        return results;
    }

    // Багаторазовий призов предметів (наприклад, x10). Зупиняється раніше, якщо не вистачає валюти.
    public List<ItemData> PullItemMultiple(ItemSummonPoolData pool, bool usePremium, int count)
    {
        var results = new List<ItemData>();
        for (int i = 0; i < count; i++)
        {
            var item = PullItem(pool, usePremium);
            if (item == null) break;
            results.Add(item);
        }
        return results;
    }

    // Спільна логіка лічильника гаранта — незалежна від того, герой це чи предмет
    private T RollWithPity<T>(string poolId, int pityThreshold, System.Func<T> forcedPick, System.Func<T> normalRoll, System.Func<T, bool> hitsGuaranteeRarity)
    {
        var pity = pityStates.FirstOrDefault(p => p.poolId == poolId);
        if (pity == null)
        {
            pity = new SummonPityState { poolId = poolId, pullsSincePity = 0 };
            pityStates.Add(pity);
        }

        T result;

        if (pity.pullsSincePity + 1 >= pityThreshold)
        {
            result = forcedPick();
            pity.pullsSincePity = 0;
        }
        else
        {
            result = normalRoll();
            pity.pullsSincePity = hitsGuaranteeRarity(result) ? 0 : pity.pullsSincePity + 1;
        }

        Save();
        return result;
    }

    private float Random01() => Random.value;

    private void Save()
    {
        string serialized = string.Join(";", pityStates.Select(p => $"{p.poolId}:{p.pullsSincePity}"));
        PlayerPrefs.SetString("summon_pity_states", serialized);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        pityStates.Clear();

        string saved = PlayerPrefs.GetString("summon_pity_states", "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var entry in saved.Split(';'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 2) continue;

            pityStates.Add(new SummonPityState
            {
                poolId = parts[0],
                pullsSincePity = int.Parse(parts[1], CultureInfo.InvariantCulture)
            });
        }
    }
}
