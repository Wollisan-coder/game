using UnityEngine;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    [Header("Уровень аккаунта")]
    public int level = 1;
    public int experience = 0;

    [Header("Энергия")]
    // Пересчитано с учётом рефилла энергии на левел-ап (см. GrantExperience): за 18 уровней Эльфов
    // игрок получает 18*20=360 аккаунт-опыта (LootReward.accountExperience по умолчанию), это пересекает
    // порог lvl1->2 (200 опыта, см. ExperienceToNextLevel) на ~10-й победе — и энергия в этот момент
    // полностью восстанавливается до нового (уже +1) максимума. Значит нужно продержаться только ДО
    // этого рефилла: 10 боёв впритык + ~5 про запас на поражения/ретраи = 15. Оставшиеся ~8 уровней
    // территории после рефилла покрываются с большим запасом.
    public int baseMaxEnergy = 15;   // максимум на 1-м уровне аккаунта, +1 за каждый следующий уровень
    public int currentEnergy;

    private const float EnergyRegenIntervalMinutes = 5f; // 1 энергия за 5 реальных минут

    private long lastEnergyRegenTicks; // DateTime.UtcNow.Ticks на момент последнего начисления

    public int MaxEnergy => baseMaxEnergy + (level - 1);

    // "Fast Start" дневная награда (ОП) — Day N считается от календарной даты ПЕРВОГО входа (не стрик,
    // не сбрасывается за пропуск дня): Day 1=400, Day 2=350, Day 3=300, Day 4=250, Day 5+=200 (см. match3-economy.md)
    private System.DateTime firstLoginDate;
    private System.DateTime? lastDailyClaimDate;
    private System.DateTime? lastBossTrainingDate;
    private const string DateFormat = "yyyyMMdd";

    // Стрик входов подряд (для AchievementManager.LoginStreak) — НЕ то же самое, что GetDailyRewardDay()
    // выше: тот считает календарные дни с первого входа и НЕ сбрасывается за пропуск, этот — наоборот,
    // именно последовательность без пропусков, сбрасывается в 1 при разрыве.
    public int currentLoginStreak = 1;
    private System.DateTime? lastLoginDate;

    // Одноразовый сигнал перед загрузкой боевой сцены (BattleManager.Awake читает и сразу гасит pendingBossTraining) —
    // не персистится, как и WorldMapManager.lastActiveMapPanelName.
    [Header("Boss Training")]
    public bool pendingBossTraining;
    public string pendingBossTrainingHeroId;

    // Тот же одноразовый флаг-сигнал, что и pendingBossTraining выше, но в обратную сторону — читается
    // MainMenuUI.Start() (и сразу гасится), чтобы после тренировки открылся замок, а не Collection по умолчанию.
    public bool returningFromBossTraining;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        level = 20; // ВРЕМЕННО для теста зданий — разблокирует все здания сразу
        RegenerateEnergyFromElapsedTime();
        UpdateLoginStreak();
    }

    // Вызывается один раз за запуск (Awake) — сегодня уже засчитан, ничего не делать; вчера — стрик
    // продолжается (+1); любой другой разрыв (или самый первый запуск) — стрик начинается заново с 1.
    private void UpdateLoginStreak()
    {
        var today = System.DateTime.UtcNow.Date;

        if (lastLoginDate == today) return; // уже открывали игру сегодня

        currentLoginStreak = lastLoginDate.HasValue && lastLoginDate.Value == today.AddDays(-1)
            ? currentLoginStreak + 1
            : 1;

        lastLoginDate = today;
        Save();
    }

    // Сколько опыта нужно на указанном уровне, чтобы подняться на следующий (плейсхолдер — легко поменять)
    public int ExperienceToNextLevel(int lvl) => lvl * 200;

    public void GrantExperience(int amount)
    {
        if (amount <= 0) return;

        experience += amount;
        int levelBefore = level;

        while (experience >= ExperienceToNextLevel(level))
        {
            experience -= ExperienceToNextLevel(level);
            level++;
        }

        // Левел-ап аккаунта полностью восстанавливает энергию (до НОВОГО, уже увеличенного MaxEnergy) —
        // это часть бюджета энергии на прохождение территории за одну сессию, см. AccountManager.baseMaxEnergy.
        if (level > levelBefore)
        {
            currentEnergy = MaxEnergy;
            AchievementManager.Instance?.ReportValue(AchievementCategory.AccountLevel, level);
        }

        Save();
    }

    // Тратит энергию (например, на вход в бой). Возвращает false и ничего не меняет, если не хватает.
    public bool SpendEnergy(int amount)
    {
        RegenerateEnergyFromElapsedTime();

        if (currentEnergy < amount) return false;

        currentEnergy -= amount;
        Save();
        DailyQuestManager.Instance?.ReportEnergySpent(amount);
        return true;
    }

    // Доначисляет энергию за реальное время, прошедшее с последнего обновления счёта
    // (включая время, пока игра была закрыта). Вызывать перед показом UI энергии.
    public void RegenerateEnergyFromElapsedTime()
    {
        if (currentEnergy >= MaxEnergy)
        {
            lastEnergyRegenTicks = System.DateTime.UtcNow.Ticks;
            return;
        }

        double minutesElapsed = (System.DateTime.UtcNow.Ticks - lastEnergyRegenTicks) / (double)System.TimeSpan.TicksPerMinute;
        int gained = Mathf.FloorToInt((float)(minutesElapsed / EnergyRegenIntervalMinutes));

        if (gained <= 0) return;

        currentEnergy = Mathf.Min(MaxEnergy, currentEnergy + gained);

        // Остаток времени, не успевший накопить полную единицу, переносится на следующий тик (без "дрейфа")
        long consumedTicks = (long)(gained * EnergyRegenIntervalMinutes * System.TimeSpan.TicksPerMinute);
        lastEnergyRegenTicks += consumedTicks;

        Save();
    }

    // Сколько секунд осталось до следующей единицы энергии — для UI-таймера
    public int SecondsUntilNextEnergy()
    {
        if (currentEnergy >= MaxEnergy) return 0;

        double minutesElapsed = (System.DateTime.UtcNow.Ticks - lastEnergyRegenTicks) / (double)System.TimeSpan.TicksPerMinute;
        double minutesRemaining = EnergyRegenIntervalMinutes - (minutesElapsed % EnergyRegenIntervalMinutes);
        return Mathf.CeilToInt((float)(minutesRemaining * 60));
    }

    // Day N (1-based) считается от календарной даты первого входа — не сбрасывается, если игрок пропустил день
    public int GetDailyRewardDay()
    {
        int days = (System.DateTime.UtcNow.Date - firstLoginDate).Days + 1;
        return Mathf.Max(1, days);
    }

    public int GetDailyRewardAmount()
    {
        switch (GetDailyRewardDay())
        {
            case 1: return 400;
            case 2: return 350;
            case 3: return 300;
            case 4: return 250;
            default: return 200;
        }
    }

    public bool HasClaimedDailyRewardToday() =>
        lastDailyClaimDate.HasValue && lastDailyClaimDate.Value == System.DateTime.UtcNow.Date;

    // Выдаёт ОП по текущей дневной ставке. Возвращает выданное количество, либо 0, если сегодня уже забирали.
    public int ClaimDailyReward()
    {
        if (HasClaimedDailyRewardToday()) return 0;

        int amount = GetDailyRewardAmount();
        PlayerCurrencies.Instance?.Add(CurrencyType.ProgressPoints, amount);

        lastDailyClaimDate = System.DateTime.UtcNow.Date;
        Save();

        return amount;
    }

    [Header("Debug")]
    public bool debugSkipBossTrainingCooldown; // тестовый флажок — временно снимает лимит 1/день, выключить перед релизом

    // 1 раз/день, тот же принцип и тот же клиент-часовой gotcha, что и Daily Reward (см. ClaimDailyReward).
    public bool HasDoneBossTrainingToday() =>
        !debugSkipBossTrainingCooldown &&
        lastBossTrainingDate.HasValue && lastBossTrainingDate.Value == System.DateTime.UtcNow.Date;

    public void MarkBossTrainingDone()
    {
        lastBossTrainingDate = System.DateTime.UtcNow.Date;
        Save();
    }

    private void Save()
    {
        PlayerPrefs.SetInt("account_level", level);
        PlayerPrefs.SetInt("account_experience", experience);
        PlayerPrefs.SetInt("account_current_energy", currentEnergy);
        PlayerPrefs.SetString("account_last_energy_regen_ticks", lastEnergyRegenTicks.ToString());
        PlayerPrefs.SetString("account_first_login_date", firstLoginDate.ToString(DateFormat));
        if (lastDailyClaimDate.HasValue)
            PlayerPrefs.SetString("account_last_daily_claim_date", lastDailyClaimDate.Value.ToString(DateFormat));
        if (lastBossTrainingDate.HasValue)
            PlayerPrefs.SetString("account_last_boss_training_date", lastBossTrainingDate.Value.ToString(DateFormat));
        PlayerPrefs.SetInt("account_login_streak", currentLoginStreak);
        if (lastLoginDate.HasValue)
            PlayerPrefs.SetString("account_last_login_date", lastLoginDate.Value.ToString(DateFormat));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        level = PlayerPrefs.GetInt("account_level", 1);
        experience = PlayerPrefs.GetInt("account_experience", 0);

        string savedTicks = PlayerPrefs.GetString("account_last_energy_regen_ticks", "");
        lastEnergyRegenTicks = !string.IsNullOrEmpty(savedTicks) && long.TryParse(savedTicks, out long parsedTicks)
            ? parsedTicks
            : System.DateTime.UtcNow.Ticks;

        currentEnergy = PlayerPrefs.HasKey("account_current_energy")
            ? PlayerPrefs.GetInt("account_current_energy", MaxEnergy)
            : MaxEnergy;

        string savedFirstLogin = PlayerPrefs.GetString("account_first_login_date", "");
        if (string.IsNullOrEmpty(savedFirstLogin) ||
            !System.DateTime.TryParseExact(savedFirstLogin, DateFormat, null, System.Globalization.DateTimeStyles.None, out firstLoginDate))
        {
            // Самый первый запуск — фиксируем сегодняшнюю дату как Day 1 и сразу сохраняем, чтобы не сбилась
            firstLoginDate = System.DateTime.UtcNow.Date;
            PlayerPrefs.SetString("account_first_login_date", firstLoginDate.ToString(DateFormat));
            PlayerPrefs.Save();
        }

        string savedLastClaim = PlayerPrefs.GetString("account_last_daily_claim_date", "");
        lastDailyClaimDate = !string.IsNullOrEmpty(savedLastClaim) &&
            System.DateTime.TryParseExact(savedLastClaim, DateFormat, null, System.Globalization.DateTimeStyles.None, out var parsedClaim)
            ? parsedClaim
            : (System.DateTime?)null;

        string savedLastTraining = PlayerPrefs.GetString("account_last_boss_training_date", "");
        lastBossTrainingDate = !string.IsNullOrEmpty(savedLastTraining) &&
            System.DateTime.TryParseExact(savedLastTraining, DateFormat, null, System.Globalization.DateTimeStyles.None, out var parsedTraining)
            ? parsedTraining
            : (System.DateTime?)null;

        currentLoginStreak = PlayerPrefs.GetInt("account_login_streak", 1);
        string savedLastLogin = PlayerPrefs.GetString("account_last_login_date", "");
        lastLoginDate = !string.IsNullOrEmpty(savedLastLogin) &&
            System.DateTime.TryParseExact(savedLastLogin, DateFormat, null, System.Globalization.DateTimeStyles.None, out var parsedLogin)
            ? parsedLogin
            : (System.DateTime?)null;
    }
}
