using System.Collections.Generic;
using UnityEngine;

// Тип узла на карте Death Dungeon (см. project_death_dungeon_concept/project_death_dungeon_implementation) —
// КАЖДЫЙ узел это настоящий бой (статы уравнены, см. BattleManager.isDeathDungeon), тип узла определяет
// только бонус/усложнение ВОКРУГ этого боя, не заменяет бой:
public enum DeathDungeonNodeType
{
    Heal,    // после победы лечим отряд на % макс. HP
    Gamble,  // бесплатный доп-спин гамбл-колеса в этом бою (сверх обычного лимита раз-в-бой)
    Elite,   // усложнённый бой — враг на тир сильнее / наложен harmful-tile дебафф с начала
    Reward,  // после победы гарантированный доп-предмет
    Buff,    // после победы выбор одного из 2-3 баффов (DeathDungeonBuffData) на остаток забега
    Boss,    // финальный уникальный босс — забег завершается победой
}

// Постоянный менеджер состояния текущего забега Death Dungeon. Один активный забег одновременно —
// currentNodeIndex==-1 значит "забега сейчас нет".
public class DeathDungeonManager : MonoBehaviour
{
    public static DeathDungeonManager Instance { get; private set; }

    // Порядок узлов снизу вверх по DeathDungMap.png: теплица, террариум, обсерватория, мастерская,
    // трофейная, кристаллы+пентаграмма, библиотека, Boss Arena. Фиксировано на "сезон" — пользователь сам
    // предложил на будущее перемешивать этот порядок раз в сезон для разнообразия; сделано обычным
    // массивом (не захардкожено по позициям в UI карты) именно для того, чтобы это было легко добавить
    // позже — просто шаффлить currentRunNodes перед StartNewRun(), когда появится система сезонов.
    public static readonly DeathDungeonNodeType[] DefaultNodeSequence =
    {
        DeathDungeonNodeType.Heal,
        DeathDungeonNodeType.Heal,
        DeathDungeonNodeType.Gamble,
        DeathDungeonNodeType.Elite,
        DeathDungeonNodeType.Reward,
        DeathDungeonNodeType.Buff,
        DeathDungeonNodeType.Buff,
        DeathDungeonNodeType.Boss,
    };

    public DeathDungeonNodeType[] currentRunNodes = DefaultNodeSequence;
    public int currentNodeIndex = -1;
    public bool IsRunActive => currentNodeIndex >= 0 && currentNodeIndex < currentRunNodes.Length;
    public DeathDungeonNodeType CurrentNodeType => IsRunActive ? currentRunNodes[currentNodeIndex] : DeathDungeonNodeType.Boss;

    // Баффы, выбранные на узлах Buff в ЭТОМ забеге — применяются к каждому следующему бою
    // (см. BattleManager), сбрасываются при старте нового забега.
    public List<DeathDungeonBuffData> activeBuffs = new List<DeathDungeonBuffData>();

    [Header("Пул баффов для узлов типа Buff — назначить в Inspector (см. Assets/DeathDungeonBuffs/)")]
    public DeathDungeonBuffData[] buffPool;

    // HP каждого героя, перенесённое из предыдущего узла этого забега (heroId -> currentHealth на момент
    // победы) — иначе каждый следующий бой стартовал бы со свежим полным HP, и весь смысл гаунтлета
    // (урон копится от боя к бою, Heal-узлы/баффы реально нужны) пропадал бы. Пусто = ещё не было ни
    // одного боя в этом забеге, герой стартует на своём полном HP.
    private readonly Dictionary<string, int> carriedHeroHP = new Dictionary<string, int>();

    public void SetCarriedHP(string heroId, int hp) => carriedHeroHP[heroId] = hp;
    public int GetCarriedHP(string heroId, int fallbackFullHP) =>
        carriedHeroHP.TryGetValue(heroId, out int hp) ? hp : fallbackFullHP;

    // Та же логика переноса, что и у HP выше, только для маны — не потратил ману в прошлом бою, она
    // остаётся с тобой в следующем (и наоборот, спустил всё в ноль — начинаешь следующий узел с нуля).
    private readonly Dictionary<string, int> carriedHeroMana = new Dictionary<string, int>();

    public void SetCarriedMana(string heroId, int mana) => carriedHeroMana[heroId] = mana;
    public int GetCarriedMana(string heroId, int fallbackFullMana) =>
        carriedHeroMana.TryGetValue(heroId, out int mana) ? mana : fallbackFullMana;

    // Последствия ретрита (см. project_death_dungeon_concept): 1 неделя -20% урон/HP в ОБЫЧНЫХ боях
    // (см. BattleManager.Awake) + 1 неделя блокировки повторного входа в данж (см. CastleUI). Персистится
    // через PlayerPrefs (не просто в памяти) — иначе рестарт игры тривиально снимал бы недельный дебафф.
    private const string DebuffExpiryKey = "dd_retreat_debuff_expiry";
    private const string LockoutExpiryKey = "dd_retreat_lockout_expiry";
    private const int RetreatDebuffDays = 5; // 7 -> 5, компромиссная цифра финального ребаланса (см. project_gem_economy_v2_redesign_pending)
    private const int RetreatLockoutDays = 7;

    public long retreatDebuffExpiryTicks;
    public long retreatLockoutExpiryTicks;

    public bool IsRetreatDebuffActive => System.DateTime.UtcNow.Ticks < retreatDebuffExpiryTicks;
    public bool IsRetreatLockoutActive => System.DateTime.UtcNow.Ticks < retreatLockoutExpiryTicks;

    public void ApplyRetreatConsequence()
    {
        retreatDebuffExpiryTicks = System.DateTime.UtcNow.AddDays(RetreatDebuffDays).Ticks;
        retreatLockoutExpiryTicks = System.DateTime.UtcNow.AddDays(RetreatLockoutDays).Ticks;

        PlayerPrefs.SetString(DebuffExpiryKey, retreatDebuffExpiryTicks.ToString());
        PlayerPrefs.SetString(LockoutExpiryKey, retreatLockoutExpiryTicks.ToString());
        PlayerPrefs.Save();
    }

    // heroId'ы, причастные к последнему ретриту (см. HeroCollectionManager.ExecuteRetreatSacrifice) —
    // и те, с кого списаны гемы (могут повторяться, если с одного героя ушло больше 1 гема), и те, кто
    // целиком принесён в жертву как "карта" — читает MineThreatManager.EnqueueThreats (куск 5, см.
    // project_death_dungeon_implementation).
    public List<string> lastSacrificedHeroIds = new List<string>();

    // Сезонная награда за прохождение гаунтлета (куск 6, см. project_death_dungeon_concept) — сезон 2
    // недели, попыток внутри сезона неограничено, но НАГРАДА (скин + валюта) засчитывается только за
    // ПЕРВУЮ чистую победу за сезон. Персистится через PlayerPrefs, тот же приём, что и retreat-таймеры
    // выше — 0 (никогда не клеймилось) значит "сезон доступен".
    private const string SeasonRewardKey = "dd_season_reward_claimed";
    private const int SeasonRewardCooldownDays = 14;

    public long lastSeasonRewardClaimedAtTicks;

    // ВРЕМЕННО ОТКЛЮЧЕНО ДЛЯ ТЕСТА — лимит на прохождение (14-дневный кулдаун сезонной награды) снят,
    // чтобы можно было гонять Death Dungeon повторно без ожидания. Перед релизом раскомментировать
    // строку ниже и удалить "return true".
    public bool CanClaimSeasonReward => true;
    // public bool CanClaimSeasonReward => System.DateTime.UtcNow.Ticks >= lastSeasonRewardClaimedAtTicks + System.TimeSpan.FromDays(SeasonRewardCooldownDays).Ticks;

    public void ClaimSeasonReward()
    {
        lastSeasonRewardClaimedAtTicks = System.DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(SeasonRewardKey, lastSeasonRewardClaimedAtTicks.ToString());
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        retreatDebuffExpiryTicks = long.TryParse(PlayerPrefs.GetString(DebuffExpiryKey, "0"), out long d) ? d : 0;
        retreatLockoutExpiryTicks = long.TryParse(PlayerPrefs.GetString(LockoutExpiryKey, "0"), out long l) ? l : 0;
        lastSeasonRewardClaimedAtTicks = long.TryParse(PlayerPrefs.GetString(SeasonRewardKey, "0"), out long s) ? s : 0;
    }

    public void StartNewRun()
    {
        currentRunNodes = DefaultNodeSequence;
        currentNodeIndex = 0;
        activeBuffs.Clear();
        carriedHeroHP.Clear();
        carriedHeroMana.Clear();
    }

    public bool IsNodeCompleted(int index) => IsRunActive && index < currentNodeIndex;
    public bool IsNodeCurrent(int index) => IsRunActive && index == currentNodeIndex;

    // Вызывать после победы в бою текущего узла (см. BattleManager.EndBattleVictory). Возвращает true,
    // если это был последний узел (босс) — забег завершён победой, currentNodeIndex сбрасывается в -1.
    public bool AdvanceToNextNode()
    {
        if (!IsRunActive) return false;

        currentNodeIndex++;
        if (currentNodeIndex >= currentRunNodes.Length)
        {
            currentNodeIndex = -1;
            return true;
        }
        return false;
    }

    // Забег брошен/провален (вайп отряда и т.п.) — сама логика решения "что теперь с героями" ещё не
    // построена (permadeath — отдельный кусок, см. project_death_dungeon_implementation), пока это просто
    // сброс состояния забега.
    public void AbandonRun()
    {
        currentNodeIndex = -1;
        activeBuffs.Clear();
        carriedHeroHP.Clear();
        carriedHeroMana.Clear();
    }

    // count случайных РАЗНЫХ баффов из пула, исключая уже активные в этом забеге (не предлагать то же
    // самое дважды за один забег). Может вернуть меньше count, если пул уже почти исчерпан.
    public List<DeathDungeonBuffData> DrawBuffChoices(int count)
    {
        var available = new List<DeathDungeonBuffData>();
        if (buffPool != null)
        {
            foreach (var buff in buffPool)
                if (buff != null && !activeBuffs.Contains(buff))
                    available.Add(buff);
        }

        var result = new List<DeathDungeonBuffData>();
        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int index = Random.Range(0, available.Count);
            result.Add(available[index]);
            available.RemoveAt(index);
        }
        return result;
    }
}
