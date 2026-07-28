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

// Общий сервис призыва для Алтаря (герои) и Кузни (предметы).
// Гарант считается отдельно на каждый пул и действует только при оплате PremiumGems.
public class SummonService : MonoBehaviour
{
    public static SummonService Instance { get; private set; }

    [Header("Состояние гаранта по каждому пулу (только премиум-оплата)")]
    public List<SummonPityState> pityStates = new List<SummonPityState>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    // Призыв героя. usePremium — оплата PremiumGems (с гарантом) или SummonShards (без гаранта).
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

    // Призыв предмета. usePremium — оплата PremiumGems (с гарантом) или SummonShards (без гаранта).
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

    // Многократный призыв героев (например, x10). Останавливается раньше, если не хватает валюты —
    // возвращает столько результатов, сколько удалось фактически оплатить.
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

    // Многократный призыв предметов (например, x10). Останавливается раньше, если не хватает валюты.
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

    // Общая логика счётчика гаранта — не зависит от того, герой это или предмет
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
