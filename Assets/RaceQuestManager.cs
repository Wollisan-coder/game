using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Расовые квесты — разблокируют апгрейд территориальных шахт (TerritoryMine_<Race>_*, см.
// project_territory_mines / BuildingManager.GetEffectiveMaxLevel), которые до этого были жёстко
// заперты на maxLevel=1. У каждой расы свой набор из 3 квестов, завязанных на её же уникальную
// механику (не один универсальный квест на всех): побеждать бои с героем этой расы в отряде,
// натриггерить расовую пассивку нужное число раз (см. BattleManager.ApplyRacePassivesPerTurn и
// инлайн-проки Эльфов/Орков/Людей), прокачать любого героя этой расы до целевого уровня.
// Одноразовые/накопительные — НЕ сбрасываются, в отличие от DailyQuestManager.
public class RaceQuestManager : MonoBehaviour
{
    public static RaceQuestManager Instance { get; private set; }

    public const int WinBattlesTarget = 10;
    public const int TriggerPassiveTarget = 15;
    public const int HeroLevelTarget = 10;

    // maxLevel территориальных шахт этой расы после выполнения всех 3 квестов (было 1) — даёт реальный
    // простор для апгрейда через уже существующую BuildingManager.UpgradeBuilding/GetUpgradeCost.
    public const int UnlockedMineMaxLevel = 5;

    private readonly Dictionary<Race, int> battlesWon = new Dictionary<Race, int>();
    private readonly Dictionary<Race, int> passiveTriggers = new Dictionary<Race, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public int GetBattlesWon(Race race) => battlesWon.TryGetValue(race, out int v) ? v : 0;
    public int GetPassiveTriggers(Race race) => passiveTriggers.TryGetValue(race, out int v) ? v : 0;

    public bool IsWinBattlesQuestComplete(Race race) => GetBattlesWon(race) >= WinBattlesTarget;
    public bool IsTriggerPassiveQuestComplete(Race race) => GetPassiveTriggers(race) >= TriggerPassiveTarget;

    // Не событие, а живая проверка текущего состояния — не требует отдельного хука на level-up, просто
    // смотрим, есть ли СЕЙЧАС у игрока (владение, не обязательно в отряде) герой этой расы нужного уровня.
    public bool IsHeroLevelQuestComplete(Race race)
    {
        var manager = HeroCollectionManager.Instance;
        if (manager == null || manager.allHeroes == null) return false;

        return manager.allHeroes.Any(hero => hero != null && hero.race == race
            && manager.ownership.Any(o => o.heroId == hero.heroId && o.level >= HeroLevelTarget));
    }

    public bool IsRaceQuestsComplete(Race race) =>
        IsWinBattlesQuestComplete(race) && IsTriggerPassiveQuestComplete(race) && IsHeroLevelQuestComplete(race);

    public void ReportBattleWon(Race race)
    {
        if (IsWinBattlesQuestComplete(race)) return;
        battlesWon[race] = GetBattlesWon(race) + 1;
        Save();
    }

    public void ReportPassiveTriggered(Race race)
    {
        if (IsTriggerPassiveQuestComplete(race)) return;
        passiveTriggers[race] = GetPassiveTriggers(race) + 1;
        Save();
    }

    private void Save()
    {
        foreach (Race race in System.Enum.GetValues(typeof(Race)))
        {
            PlayerPrefs.SetInt($"race_quest_battles_{race}", GetBattlesWon(race));
            PlayerPrefs.SetInt($"race_quest_passive_{race}", GetPassiveTriggers(race));
        }
        PlayerPrefs.Save();
    }

    private void Load()
    {
        battlesWon.Clear();
        passiveTriggers.Clear();

        foreach (Race race in System.Enum.GetValues(typeof(Race)))
        {
            battlesWon[race] = PlayerPrefs.GetInt($"race_quest_battles_{race}", 0);
            passiveTriggers[race] = PlayerPrefs.GetInt($"race_quest_passive_{race}", 0);
        }
    }
}
