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

    // UI (CastleSummonUI) читает это сразу после PullHero/PullHeroMultiple/PullItem, чтобы отличить
    // настоящую нехватку валюты от пула, который вернул пустой ролл (entries[] пуст либо все hero/item
    // ссылки битые) — раньше обе причины показывали игроку одинаковое "Not enough currency for this pull.",
    // даже когда у него объективно хватало денег (найдено 2026-08-20 — Altar_HeroPool.asset ссылался на
    // 4 давно удалённых плейсхолдер-героя). Тот же приём, что LastPullWasJackpot. Сбрасывается в начале
    // каждого вызова PullHero/PullItem, чтобы не тянуть значение от предыдущего, не связанного вызова.
    public bool LastPullFailedFromBadPoolData { get; private set; }

    // Призыв героя. usePremium — оплата PremiumGems (с гарантом) или SummonShards (без гаранта).
    // wasNewUnlock — герой получен ВПЕРВЫЕ (не дубликат); нужно SummonRevealUI, чтобы решить, показывать
    // ли катсцену первого получения (см. GrantHero).
    public (HeroData hero, bool wasNewUnlock) PullHero(HeroSummonPoolData pool, bool usePremium)
    {
        LastPullFailedFromBadPoolData = false;

        if (pool == null || PlayerCurrencies.Instance == null || HeroCollectionManager.Instance == null) return (null, false);

        CurrencyType currency = usePremium ? CurrencyType.PremiumGems : CurrencyType.SummonShards;
        int cost = usePremium ? pool.premiumCost : pool.shardCost;
        if (!PlayerCurrencies.Instance.Spend(currency, cost)) return (null, false);

        HeroSummonEntry result = usePremium && pool.hasPity
            ? RollWithPity(pool.poolId, pool.pityThreshold,
                forcedPick: () => pool.PickHighestOfRarityOrAbove(pool.pityGuaranteedRarity) ?? pool.RollWeighted(true, Random01),
                normalRoll: () => pool.RollWeighted(true, Random01),
                hitsGuaranteeRarity: entry => entry?.hero != null && entry.hero.rarity >= pool.pityGuaranteedRarity)
            : pool.RollWeighted(usePremium, Random01);

        if (result?.hero == null)
        {
            // Пул пуст или сконфигурирован некорректно (entries[] пуст, либо запись без назначенного hero) —
            // вернуть валюту, а не списать её впустую за ничего.
            PlayerCurrencies.Instance.Add(currency, cost);
            LastPullFailedFromBadPoolData = true;
            return (null, false);
        }

        bool wasNewUnlock = GrantHero(result.hero);
        return (result.hero, wasNewUnlock);
    }

    // Если герой уже разблокирован — это дубликат: конвертируется в гем вознесения (или в универсальную
    // награду, если герой уже на максимуме вознесения), а не пропадает молча/повторно "разблокирует" уже открытое.
    // Возвращает true, если это была ПЕРВАЯ разблокировка (не дубликат) — сигнал для катсцены первого получения.
    private bool GrantHero(HeroData hero)
    {
        if (hero == null || HeroCollectionManager.Instance == null) return false;

        if (HeroCollectionManager.Instance.IsUnlocked(hero))
        {
            HeroCollectionManager.Instance.HandleDuplicatePull(hero);
            return false;
        }

        HeroCollectionManager.Instance.UnlockHero(hero);
        return true;
    }

    // Призыв предмета. usePremium — оплата PremiumGems (с гарантом) или SummonShards (без гаранта).
    public ItemData PullItem(ItemSummonPoolData pool, bool usePremium)
    {
        LastPullFailedFromBadPoolData = false;

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

        if (result == null || result.item == null)
        {
            PlayerCurrencies.Instance.Add(currency, cost); // пул пуст/сконфигурирован некорректно — валюта не сгорает впустую
            LastPullFailedFromBadPoolData = true;
            return null;
        }

        ItemCollectionManager.Instance.AddItemCopy(result.item);
        return result.item;
    }

    // Бесплатный ролл из пула БЕЗ трат валюты и БЕЗ пити — для гарантированной награды фарм-данжей
    // (см. MapNodeData.farmLootPool), не для покупки в Кузне/Алтаре. Пул сам гарантирует нужную редкость
    // (например PurpleFarmPool содержит только фиолетовые предметы) — рандом только по тому, какой именно.
    public ItemData RollFreeItem(ItemSummonPoolData pool)
    {
        if (pool == null || ItemCollectionManager.Instance == null) return null;

        ItemSummonEntry result = pool.RollWeighted(false, Random01);
        if (result?.item == null) return null;

        ItemCollectionManager.Instance.AddItemCopy(result.item);
        return result.item;
    }

    // Заполняется PullHeroMultiple — джекпот сработал (или нет) на последнем вызове. UI (CastleSummonUI)
    // читает это сразу после вызова, чтобы показать особое сообщение вместо обычного списка.
    public bool LastPullWasJackpot { get; private set; }

    private const int JackpotMinPullCount = 10;

    // Многократный призыв героев (например, x10). Останавливается раньше, если не хватает валюты —
    // возвращает столько результатов, сколько удалось фактически оплатить. count >= 10 — один шанс
    // на джекпот (см. HeroSummonPoolData.jackpotChance) на весь этот пул призывов.
    public List<(HeroData hero, bool wasNewUnlock)> PullHeroMultiple(HeroSummonPoolData pool, bool usePremium, int count)
    {
        LastPullWasJackpot = false;

        if (pool != null && count >= JackpotMinPullCount && pool.jackpotChance > 0f && Random01() < pool.jackpotChance)
        {
            var jackpotResults = TryPullJackpotBatch(pool, usePremium, count);
            if (jackpotResults != null) return jackpotResults;
            // Валюты не хватило на весь x10, либо в пуле нет 2 разных Orange-героев одной расы —
            // джекпот молча отменяется, ниже идёт обычный путь (с ранней остановкой при нехватке валюты).
        }

        var results = new List<(HeroData hero, bool wasNewUnlock)>();
        for (int i = 0; i < count; i++)
        {
            var pull = PullHero(pool, usePremium);
            if (pull.hero == null) break;
            results.Add(pull);
        }
        return results;
    }

    private List<(HeroData hero, bool wasNewUnlock)> TryPullJackpotBatch(HeroSummonPoolData pool, bool usePremium, int count)
    {
        // 2 РАЗНЫХ Orange-героя (выбор без возвращения) — раньше каждый выбирался независимо и мог выпасть
        // дважды один и тот же, расходясь с собственным текстом "2 different Orange heroes"/"2 Legendary
        // heroes guaranteed" (см. UX-правку). Раса каждого должна быть уже открыта игроку
        // (WorldMapManager.IsTerritoryOpened) — джекпот не выдаёт героя расы, до которой игрок ещё не дошёл.
        var eligibleOrangeHeroes = pool.entries
            .Where(e => e.hero != null && e.hero.rarity == Rarity.Orange)
            .Select(e => e.hero)
            .Distinct()
            // Fail-closed — если WorldMapManager почему-то ещё не проинициализирован, лучше отказать
            // джекпоту (некому подтвердить, что раса открыта), чем выдать героя расы, до которой игрок
            // мог ещё не дойти.
            .Where(h => WorldMapManager.Instance != null && WorldMapManager.Instance.IsTerritoryOpened(h.race))
            .ToList();
        // < 2, а не == 0 — джекпот обещает 2 РАЗНЫХ героя; с одним кандидатом это обещание всё равно не
        // выполнить, лучше молча отменить джекпот (обычный x10 ниже), чем отдать дубль под видом джекпота.
        if (eligibleOrangeHeroes.Count < 2) return null;

        CurrencyType currency = usePremium ? CurrencyType.PremiumGems : CurrencyType.SummonShards;
        int perPullCost = usePremium ? pool.premiumCost : pool.shardCost;
        int totalCost = perPullCost * count;
        if (!PlayerCurrencies.Instance.Spend(currency, totalCost)) return null; // не хватает на весь x10 разом

        var pickPool = new List<HeroData>(eligibleOrangeHeroes);
        var jackpotHeroes = new List<HeroData>();
        for (int i = 0; i < 2; i++)
        {
            int index = Mathf.Clamp((int)(Random01() * pickPool.Count), 0, pickPool.Count - 1);
            jackpotHeroes.Add(pickPool[index]);
            pickPool.RemoveAt(index); // без возвращения — гарантирует различность (Count>=2 уже проверен выше)
        }

        var results = new List<(HeroData hero, bool wasNewUnlock)>();
        foreach (var hero in jackpotHeroes)
            results.Add((hero, GrantHero(hero)));

        // Оставшиеся пуллы этого x10 — обычный взвешенный ролл (без повторной проверки пити/джекпота,
        // валюта на них уже списана выше единым платежом).
        for (int i = results.Count; i < count; i++)
        {
            var entry = pool.RollWeighted(usePremium, Random01);
            if (entry?.hero == null)
            {
                // Пустая запись пула (entries[] без назначенного hero) — тот же принцип, что и в PullHero:
                // возвращаем стоимость именно этого пулла, а не сжигаем валюту молча (найдено на аудите
                // 2026-08-20 — до этой правки джекпотный x10 списывал полную стоимость, даже если один из
                // "рядовых" роллов молча проваливался).
                PlayerCurrencies.Instance.Add(currency, perPullCost);
                continue;
            }
            results.Add((entry.hero, GrantHero(entry.hero)));
        }

        if (usePremium && pool.hasPity)
            ResetPity(pool.poolId); // джекпот гарантированно выдал Orange — считается как попадание в гарант

        LastPullWasJackpot = true;
        Save();
        return results;
    }

    private void ResetPity(string poolId)
    {
        var pity = pityStates.FirstOrDefault(p => p.poolId == poolId);
        if (pity == null)
        {
            pity = new SummonPityState { poolId = poolId, pullsSincePity = 0 };
            pityStates.Add(pity);
        }
        else
        {
            pity.pullsSincePity = 0;
        }
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
