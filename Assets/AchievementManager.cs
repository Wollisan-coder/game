using System.Collections.Generic;
using UnityEngine;

// Пожизненные (никогда не сбрасываются) многоуровневые ачивки — 14 категорий x 5 уровней каждая.
// Прогресс копится вечно; награда НЕ выдаётся автоматически по достижению порога — уровень становится
// "готов к клейму" (см. IsReadyToClaim), игрок жмёт Claim (по категории или Claim All), см. Claim*.
// Прогресс репортится точечными хуками из тех же мест, что и DailyQuestManager, плюс отдельные —
// WorldMapManager.CompleteCurrentNode (только на новую ноду) и HeroCollectionManager.UnlockHero
// (только на первую разблокировку, не на дубликаты).
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    // Награда (SummonShards) за уровень 1..5 — одинаковая для всех категорий, растёт x2 за уровень.
    private static readonly int[] TierRewards = { 10, 20, 40, 80, 160 };

    private static readonly Dictionary<AchievementCategory, int[]> Thresholds = new Dictionary<AchievementCategory, int[]>
    {
        { AchievementCategory.DefeatEnemies, new[] { 20, 50, 100, 250, 500 } },
        { AchievementCategory.CompleteQuests, new[] { 5, 15, 30, 60, 120 } },
        { AchievementCategory.ClearMapNodes, new[] { 5, 15, 30, 60, 100 } },
        { AchievementCategory.CollectHeroes, new[] { 3, 6, 10, 15, 25 } },
        { AchievementCategory.UseSkills, new[] { 20, 50, 100, 200, 500 } },
        { AchievementCategory.CompleteSummons, new[] { 5, 15, 30, 60, 120 } },
        { AchievementCategory.Ascension, new[] { 1, 3, 6, 10, 15 } },
        { AchievementCategory.BuildingUpgrades, new[] { 5, 15, 30, 60, 100 } },
        { AchievementCategory.BossTrainingTotal, new[] { 5, 15, 30, 60, 100 } },
        { AchievementCategory.TerritoriesCleared, new[] { 1, 2, 4, 6, 8 } },
        { AchievementCategory.AccountLevel, new[] { 5, 10, 20, 35, 50 } },
        { AchievementCategory.GambleSpins, new[] { 5, 15, 30, 60, 100 } },
        { AchievementCategory.LoginStreak, new[] { 3, 7, 14, 30, 60 } },
        { AchievementCategory.FarmDungeonsCompleted, new[] { 5, 15, 50, 100, 250 } },
    };

    public static string GetDescription(AchievementCategory category) => category switch
    {
        AchievementCategory.DefeatEnemies => "Defeat enemies",
        AchievementCategory.CompleteQuests => "Complete daily quests",
        AchievementCategory.ClearMapNodes => "Clear map nodes",
        AchievementCategory.CollectHeroes => "Collect unique heroes",
        AchievementCategory.UseSkills => "Use hero skills",
        AchievementCategory.CompleteSummons => "Perform summons",
        AchievementCategory.Ascension => "Ascend heroes",
        AchievementCategory.BuildingUpgrades => "Upgrade buildings",
        AchievementCategory.BossTrainingTotal => "Complete Boss Training sessions",
        AchievementCategory.TerritoriesCleared => "Clear territories",
        AchievementCategory.AccountLevel => "Reach account level",
        AchievementCategory.GambleSpins => "Spin the board gamble wheel",
        AchievementCategory.LoginStreak => "Login streak (consecutive days)",
        AchievementCategory.FarmDungeonsCompleted => "Complete farm dungeon runs",
        _ => category.ToString(),
    };

    // progress — сырое накопленное (или "максимум когда-либо достигнутого" для AccountLevel/LoginStreak,
    // см. ReportValue) значение. tierClaimedCount — сколько уровней РЕАЛЬНО заклеймлено (не то же самое,
    // что "сколько порогов уже пройдено по progress" — см. GetTierReached/IsReadyToClaim).
    private readonly Dictionary<AchievementCategory, int> progress = new Dictionary<AchievementCategory, int>();
    private readonly Dictionary<AchievementCategory, int> tierClaimedCount = new Dictionary<AchievementCategory, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    // Start(), а не Awake() — гарантированно выполняется ПОСЛЕ Awake() всех менеджеров сцены (порядок
    // Awake() между разными GameObject'ами не определён), так что AccountManager.Instance тут уже точно
    // готов. Ловит и стартовое состояние (уровень/стрик уже были ненулевыми при первом запуске сессии),
    // и не даёт пропасть стрику, если AccountManager успел бы обновить его до того, как этот менеджер ожил.
    private void Start()
    {
        if (AccountManager.Instance != null)
        {
            ReportValue(AchievementCategory.AccountLevel, AccountManager.Instance.level);
            ReportValue(AchievementCategory.LoginStreak, AccountManager.Instance.currentLoginStreak);
        }
    }

    public int GetProgress(AchievementCategory category) => progress.TryGetValue(category, out int v) ? v : 0;
    public int GetTierClaimed(AchievementCategory category) => tierClaimedCount.TryGetValue(category, out int v) ? v : 0;
    public int[] GetThresholds(AchievementCategory category) => Thresholds[category];

    // Сколько порогов уже пройдено по накопленному progress, вне зависимости от того, заклеймлены они или нет.
    public int GetTierReached(AchievementCategory category)
    {
        var thresholds = Thresholds[category];
        int current = GetProgress(category);
        int reached = 0;
        while (reached < thresholds.Length && current >= thresholds[reached])
            reached++;
        return reached;
    }

    public bool IsReadyToClaim(AchievementCategory category) => GetTierReached(category) > GetTierClaimed(category);

    public bool HasAnyClaimable()
    {
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
            if (IsReadyToClaim(category)) return true;
        return false;
    }

    public void ReportEnemyDefeated() => ReportProgress(AchievementCategory.DefeatEnemies);
    public void ReportQuestCompleted() => ReportProgress(AchievementCategory.CompleteQuests);
    public void ReportMapNodeCleared() => ReportProgress(AchievementCategory.ClearMapNodes);
    public void ReportHeroCollected() => ReportProgress(AchievementCategory.CollectHeroes);
    public void ReportSkillUsed() => ReportProgress(AchievementCategory.UseSkills);
    public void ReportSummonsCompleted(int count) => ReportProgress(AchievementCategory.CompleteSummons, count);
    public void ReportAscension() => ReportProgress(AchievementCategory.Ascension);
    public void ReportBuildingUpgraded() => ReportProgress(AchievementCategory.BuildingUpgrades);
    public void ReportBossTrainingCompleted() => ReportProgress(AchievementCategory.BossTrainingTotal);
    public void ReportTerritoryCleared() => ReportProgress(AchievementCategory.TerritoriesCleared);
    public void ReportGambleSpin() => ReportProgress(AchievementCategory.GambleSpins);
    public void ReportFarmDungeonCompleted() => ReportProgress(AchievementCategory.FarmDungeonsCompleted);

    // AccountLevel/LoginStreak вызывают именно это, не ReportProgress — прогресс тут "самое высокое
    // когда-либо достигнутое значение", а не сумма (уровень аккаунта не "накапливается", стрик может
    // упасть обратно к 1, но уже пройденный однажды порог в 7 дней подряд не должен отбираться).
    public void ReportValue(AchievementCategory category, int value)
    {
        if (value <= GetProgress(category)) return;
        progress[category] = value;
        Save();
    }

    private void ReportProgress(AchievementCategory category, int amount = 1)
    {
        progress[category] = GetProgress(category) + amount;
        Save();
    }

    // Клеймит все уровни категории, что накопились между "сколько заклеймлено" и "сколько реально
    // достигнуто" — один большой скачок прогресса (например, x10 призыв) может перепрыгнуть сразу
    // несколько порогов, каждый уровень выдаёт свою награду отдельно.
    public int ClaimCategory(AchievementCategory category)
    {
        int reached = GetTierReached(category);
        int claimed = GetTierClaimed(category);
        if (claimed >= reached) return 0;

        int total = 0;
        while (claimed < reached)
        {
            total += TierRewards[claimed];
            claimed++;
        }

        tierClaimedCount[category] = claimed;
        PlayerCurrencies.Instance?.Add(CurrencyType.SummonShards, total);
        Save();
        return total;
    }

    public int ClaimAll()
    {
        int total = 0;
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
            total += ClaimCategory(category);
        return total;
    }

    private void Save()
    {
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
        {
            PlayerPrefs.SetInt($"ach_{category}_progress", GetProgress(category));
            PlayerPrefs.SetInt($"ach_{category}_tier", GetTierClaimed(category));
        }
        PlayerPrefs.Save();
    }

    private void Load()
    {
        foreach (AchievementCategory category in System.Enum.GetValues(typeof(AchievementCategory)))
        {
            progress[category] = PlayerPrefs.GetInt($"ach_{category}_progress", 0);
            tierClaimedCount[category] = PlayerPrefs.GetInt($"ach_{category}_tier", 0);
        }
    }
}
