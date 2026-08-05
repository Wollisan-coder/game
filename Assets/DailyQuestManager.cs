using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class DailyQuestSlot
{
    public DailyQuestType type;
    public int current;
    public int target;
    public bool claimed;

    public bool IsReady => current >= target && !claimed;
}

// Дневные задания: каждый день 3 случайных из пула 7 DailyQuestType (без повторов), фиксированные
// пороги/награда за каждый (см. GetTarget/PerQuestReward), плюс бонус за клейм всех трёх. Прогресс
// репортится точечными хуками из мест, где реально происходит нужное действие (BattleManager
// .EndBattleVictory/EndBossTraining, HeroCardUI скилл-клик, CastleSummonUI.Pull, AccountManager
// .SpendEnergy, HeroCollectionManager/ItemCollectionManager level-up) — см. Report*. Награда НЕ
// выдаётся автоматически по достижению порога — игрок жмёт Claim (по одному или Claim All), см. Claim*.
public class DailyQuestManager : MonoBehaviour
{
    public static DailyQuestManager Instance { get; private set; }

    public const int PerQuestReward = 200; // ProgressPoints за каждое выполненное задание
    public const int AllCompleteBonusShards = 20; // бонус SummonShards за клейм всех 3 за день

    public List<DailyQuestSlot> todaysQuests = new List<DailyQuestSlot>();
    public bool allBonusGranted;

    private System.DateTime setDate;
    private const string DateFormat = "yyyyMMdd";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();

        if (todaysQuests.Count == 0 || setDate != System.DateTime.UtcNow.Date)
            GenerateNewSet();
    }

    private void GenerateNewSet()
    {
        var pool = new List<DailyQuestType>((DailyQuestType[])System.Enum.GetValues(typeof(DailyQuestType)));
        var rng = new System.Random();

        // Fisher-Yates, берём первые 3 после перемешивания — без повторов по построению.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        todaysQuests = new List<DailyQuestSlot>();
        for (int i = 0; i < 3 && i < pool.Count; i++)
        {
            todaysQuests.Add(new DailyQuestSlot
            {
                type = pool[i],
                current = 0,
                target = GetTarget(pool[i]),
                claimed = false,
            });
        }

        allBonusGranted = false;
        setDate = System.DateTime.UtcNow.Date;
        Save();
    }

    private static int GetTarget(DailyQuestType type) => type switch
    {
        DailyQuestType.WinBattles => 5,
        DailyQuestType.UseSkills => 20,
        DailyQuestType.BossTraining => 1,
        DailyQuestType.Summon => 1,
        DailyQuestType.SpendEnergy => 15,
        DailyQuestType.HeroLevelUp => 1,
        DailyQuestType.ItemLevelUp => 1,
        _ => 1,
    };

    public static string GetDescription(DailyQuestType type) => type switch
    {
        DailyQuestType.WinBattles => "Win 5 battles",
        DailyQuestType.UseSkills => "Use 20 active skills in battle",
        DailyQuestType.BossTraining => "Complete 1 Boss Training session",
        DailyQuestType.Summon => "Make 1 summon pull (Altar or Forge)",
        DailyQuestType.SpendEnergy => "Spend 15 energy",
        DailyQuestType.HeroLevelUp => "Level up a hero",
        DailyQuestType.ItemLevelUp => "Level up a piece of equipment",
        _ => type.ToString(),
    };

    public void ReportBattleWon() => ReportProgress(DailyQuestType.WinBattles);
    public void ReportSkillUsed() => ReportProgress(DailyQuestType.UseSkills);
    public void ReportBossTrainingCompleted() => ReportProgress(DailyQuestType.BossTraining);
    public void ReportSummonPulled() => ReportProgress(DailyQuestType.Summon);
    public void ReportEnergySpent(int amount) => ReportProgress(DailyQuestType.SpendEnergy, amount);
    public void ReportHeroLeveledUp() => ReportProgress(DailyQuestType.HeroLevelUp);
    public void ReportItemLeveledUp() => ReportProgress(DailyQuestType.ItemLevelUp);

    private void ReportProgress(DailyQuestType type, int amount = 1)
    {
        var slot = todaysQuests.Find(s => s.type == type);
        if (slot == null || slot.claimed) return; // не в сегодняшней тройке, либо уже заклеймлено

        slot.current = Mathf.Min(slot.target, slot.current + amount);
        Save();
    }

    // Бонус доступен к клейму, когда все 3 задания заклеймлены по отдельности (не просто "выполнены") —
    // так "выполнение всех трёх" ощущается как явное завершение дня, а не тихий побочный эффект.
    public bool IsBonusReady => !allBonusGranted && todaysQuests.Count > 0 && todaysQuests.All(s => s.claimed);

    public bool HasAnyClaimable => todaysQuests.Any(s => s.IsReady) || IsBonusReady;

    public int ClaimQuest(DailyQuestType type)
    {
        var slot = todaysQuests.Find(s => s.type == type);
        if (slot == null || !slot.IsReady) return 0;

        slot.claimed = true;
        PlayerCurrencies.Instance?.Add(CurrencyType.ProgressPoints, PerQuestReward);
        AchievementManager.Instance?.ReportQuestCompleted();
        Save();
        return PerQuestReward;
    }

    public int ClaimBonus()
    {
        if (!IsBonusReady) return 0;

        allBonusGranted = true;
        PlayerCurrencies.Instance?.Add(CurrencyType.SummonShards, AllCompleteBonusShards);
        Save();
        return AllCompleteBonusShards;
    }

    // Клеймит все готовые задания разом, и бонус следом, если он стал доступен именно этим клеймом.
    public void ClaimAll()
    {
        foreach (var slot in todaysQuests.Where(s => s.IsReady).ToList())
            ClaimQuest(slot.type);

        if (IsBonusReady)
            ClaimBonus();
    }

    private void Save()
    {
        PlayerPrefs.SetString("dq_date", setDate.ToString(DateFormat));
        PlayerPrefs.SetInt("dq_bonus_granted", allBonusGranted ? 1 : 0);
        PlayerPrefs.SetInt("dq_count", todaysQuests.Count);

        for (int i = 0; i < todaysQuests.Count; i++)
        {
            var slot = todaysQuests[i];
            PlayerPrefs.SetInt($"dq_slot{i}_type", (int)slot.type);
            PlayerPrefs.SetInt($"dq_slot{i}_current", slot.current);
            PlayerPrefs.SetInt($"dq_slot{i}_target", slot.target);
            PlayerPrefs.SetInt($"dq_slot{i}_claimed", slot.claimed ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    private void Load()
    {
        string savedDate = PlayerPrefs.GetString("dq_date", "");
        if (!string.IsNullOrEmpty(savedDate) &&
            System.DateTime.TryParseExact(savedDate, DateFormat, null, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            setDate = parsed;
        }
        else
        {
            setDate = System.DateTime.MinValue; // гарантированно "не сегодня" — заставит Awake() перегенерировать
        }

        allBonusGranted = PlayerPrefs.GetInt("dq_bonus_granted", 0) == 1;

        int count = PlayerPrefs.GetInt("dq_count", 0);
        todaysQuests = new List<DailyQuestSlot>();
        for (int i = 0; i < count; i++)
        {
            if (!PlayerPrefs.HasKey($"dq_slot{i}_type")) continue;

            todaysQuests.Add(new DailyQuestSlot
            {
                type = (DailyQuestType)PlayerPrefs.GetInt($"dq_slot{i}_type", 0),
                current = PlayerPrefs.GetInt($"dq_slot{i}_current", 0),
                target = PlayerPrefs.GetInt($"dq_slot{i}_target", 1),
                claimed = PlayerPrefs.GetInt($"dq_slot{i}_claimed", 0) == 1,
            });
        }
    }
}
