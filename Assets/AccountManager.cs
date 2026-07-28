using UnityEngine;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    [Header("Уровень аккаунта")]
    public int level = 1;
    public int experience = 0;

    [Header("Энергия")]
    public int baseMaxEnergy = 10;   // максимум на 1-м уровне аккаунта, +1 за каждый следующий уровень
    public int currentEnergy;

    private const float EnergyRegenIntervalMinutes = 5f; // 1 энергия за 5 реальных минут

    private long lastEnergyRegenTicks; // DateTime.UtcNow.Ticks на момент последнего начисления

    public int MaxEnergy => baseMaxEnergy + (level - 1);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        level = 20; // ВРЕМЕННО для теста зданий — разблокирует все здания сразу
        RegenerateEnergyFromElapsedTime();
    }

    // Сколько опыта нужно на указанном уровне, чтобы подняться на следующий (плейсхолдер — легко поменять)
    public int ExperienceToNextLevel(int lvl) => lvl * 200;

    public void GrantExperience(int amount)
    {
        if (amount <= 0) return;

        experience += amount;

        while (experience >= ExperienceToNextLevel(level))
        {
            experience -= ExperienceToNextLevel(level);
            level++;
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

    private void Save()
    {
        PlayerPrefs.SetInt("account_level", level);
        PlayerPrefs.SetInt("account_experience", experience);
        PlayerPrefs.SetInt("account_current_energy", currentEnergy);
        PlayerPrefs.SetString("account_last_energy_regen_ticks", lastEnergyRegenTicks.ToString());
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
    }
}
