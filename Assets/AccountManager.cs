using UnityEngine;

public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    [Header("Рівень акаунту")]
    public int level = 1;
    public int experience = 0;

    [Header("Енергія")]
    public int baseMaxEnergy = 10;   // максимум на 1-му рівні акаунту, +1 за кожен наступний рівень
    public int currentEnergy;

    private const float EnergyRegenIntervalMinutes = 5f; // 1 енергія за 5 реальних хвилин

    private long lastEnergyRegenTicks; // DateTime.UtcNow.Ticks на момент останнього нарахування

    public int MaxEnergy => baseMaxEnergy + (level - 1);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        level = 20; // ВРЕМЕННО для тесту будівель — розблоковує всі будівлі одразу
        RegenerateEnergyFromElapsedTime();
    }

    // Скільки досвіду потрібно на вказаному рівні, щоб піднятись на наступний (плейсхолдер — легко змінити)
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

    // Витрачає енергію (наприклад, на вхід у бій). Повертає false і нічого не змінює, якщо не вистачає.
    public bool SpendEnergy(int amount)
    {
        RegenerateEnergyFromElapsedTime();

        if (currentEnergy < amount) return false;

        currentEnergy -= amount;
        Save();
        return true;
    }

    // Дораховує енергію за реальний час, що минув відтоді, як рахунок оновлювався востаннє
    // (включно з часом, поки гра була закрита). Викликати перед показом UI енергії.
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

        // Залишок часу, що не встиг накопичити повну одиницю, переноситься на наступний тік (без "дрейфу")
        long consumedTicks = (long)(gained * EnergyRegenIntervalMinutes * System.TimeSpan.TicksPerMinute);
        lastEnergyRegenTicks += consumedTicks;

        Save();
    }

    // Скільки секунд лишилось до наступної одиниці енергії — для UI-таймера
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
