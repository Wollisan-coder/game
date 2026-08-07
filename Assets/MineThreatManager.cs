using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Одна активная угроза — раса, чьи 3 шахты территории заблокированы (см. TerritoryMinesUI/BuildingManager),
// и список heroId'ов, чья жертва в неё внесла вклад (задел на будущее — сам факт, что боссы-нежить будут
// как-то опираться на личность героя, ещё не построен, просто не теряем ID).
[System.Serializable]
public class MineThreat
{
    public Race race;
    public List<string> sacrificedHeroIds = new List<string>();
}

// Кусок 5 Death Dungeon (см. project_death_dungeon_concept) — принесённые в жертву на ретрите герои
// возвращаются как "порченые" боссы, блокирующие шахты своей расы (см. project_territory_mines), пока
// игрок не победит их в обычном бою (см. BattleManager.isMineDefense). Очередь угроз копится с каждого
// успешного ретрита (см. HeroCollectionManager.ExecuteRetreatSacrifice -> DeathDungeonRetreatSelectionUI),
// но "созревают" по одной раз в SpawnIntervalDays — не все сразу, если ретрит принёс в жертву нескольких
// героев одной волной.
public class MineThreatManager : MonoBehaviour
{
    public static MineThreatManager Instance { get; private set; }

    private const int SpawnIntervalDays = 1;

    private const string QueueKey = "mine_threat_queue";
    private const string ActiveKey = "mine_threat_active";
    private const string NextSpawnKey = "mine_threat_next_spawn";

    // Герои, чья угроза ещё не "созрела" — FIFO, один спавнится за раз (см. ProcessQueue).
    private readonly List<string> queuedHeroIds = new List<string>();

    // Уже активные угрозы, максимум одна на расу — если для расы уже есть активная угроза, а в очереди
    // "созрел" ещё один герой этой же расы, его heroId просто присоединяется к уже существующей записи
    // (шахты и так уже заблокированы, второй отдельный блок не нужен).
    public readonly List<MineThreat> activeThreats = new List<MineThreat>();

    private long nextSpawnAtTicks;

    public bool HasAnyActiveThreat => activeThreats.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ProcessQueue();
    }

    // Вызывается после успешного ретрита (см. DeathDungeonRetreatSelectionUI.OnConfirmClicked) —
    // добавляет всех причастных героев в очередь. Не сбрасывает уже тикающий таймер, если очередь не
    // была пуста — "день кулдаун" это ОБЩИЙ темп появления угроз, а не персональный таймер каждого героя.
    public void EnqueueThreats(List<string> heroIds)
    {
        if (heroIds == null || heroIds.Count == 0) return;

        bool wasIdle = queuedHeroIds.Count == 0 && System.DateTime.UtcNow.Ticks >= nextSpawnAtTicks;

        queuedHeroIds.AddRange(heroIds);

        if (wasIdle)
            nextSpawnAtTicks = System.DateTime.UtcNow.AddDays(SpawnIntervalDays).Ticks;

        Save();
    }

    // Продвигает очередь настолько, насколько прошло реального времени — может "созреть" сразу несколько
    // угроз подряд, если игрок не заходил в игру много дней. Безопасно звать многократно (например при
    // каждом открытии карты мира/экрана шахт) — no-op, если рано или очередь пуста.
    // Максимум одновременно АКТИВНЫХ угроз на всю карту — даже если в очереди накопилось больше
    // принесённых в жертву героев (например, игрок долго не заходил), лишние ждут, пока не освободится
    // место (см. project_gem_economy_v2_redesign_pending — 2026-08-08, отдельный балансный фикс).
    private const int MaxActiveThreats = 3;

    public void ProcessQueue()
    {
        bool changed = false;

        while (queuedHeroIds.Count > 0 && System.DateTime.UtcNow.Ticks >= nextSpawnAtTicks)
        {
            string heroId = queuedHeroIds[0];

            var hero = HeroCollectionManager.Instance != null
                ? HeroCollectionManager.Instance.allHeroes.FirstOrDefault(h => h != null && h.heroId == heroId)
                : null;

            if (hero != null)
            {
                var existing = activeThreats.FirstOrDefault(t => t.race == hero.race);

                // Мерджиться в уже активную угрозу можно всегда (число активных угроз не растёт) — кап
                // блокирует только СОЗДАНИЕ новой, когда на карте уже максимум.
                if (existing == null && activeThreats.Count >= MaxActiveThreats)
                    break;

                queuedHeroIds.RemoveAt(0);

                if (existing != null)
                {
                    if (!existing.sacrificedHeroIds.Contains(heroId))
                        existing.sacrificedHeroIds.Add(heroId);
                }
                else
                {
                    activeThreats.Add(new MineThreat { race = hero.race, sacrificedHeroIds = new List<string> { heroId } });
                }
            }
            else
            {
                queuedHeroIds.RemoveAt(0); // герой не найден (битая запись) — отбрасываем, кап тут ни при чём
            }

            nextSpawnAtTicks = System.DateTime.UtcNow.AddDays(SpawnIntervalDays).Ticks;
            changed = true;
        }

        if (changed) Save();
    }

    public bool IsRaceBlocked(Race race) => activeThreats.Any(t => t.race == race);

    public MineThreat GetActiveThreat(Race race) => activeThreats.FirstOrDefault(t => t.race == race);

    // Вызывается после победы в бою за шахты (см. BattleManager.EndMineDefenseVictory).
    public void ResolveThreat(Race race)
    {
        activeThreats.RemoveAll(t => t.race == race);
        Save();
    }

    // Босс территории этой расы (нода 18, тот же "Overseer", что и в обычной кампании) — переиспользуем
    // готовый контент вместо авторства нового врага под угрозу (см. project_death_dungeon_concept
    // content-optimization plan). Null, если у территории этой расы ещё нет боевого контента вообще
    // (некоторые территории пока не доавторены — см. project_battle_economy_progression) — тогда бой
    // просто нельзя начать, шахты остаются заблокированы до тех пор, пока контент не появится.
    public EnemyData GetBossEnemyForRace(Race race)
    {
        var node = WorldMapManager.Instance?.allNodes?.FirstOrDefault(
            n => n != null && n.enemy != null && n.territory == race && n.nodeIndex == 18);
        return node?.enemy;
    }

    private void Save()
    {
        PlayerPrefs.SetString(QueueKey, string.Join(";", queuedHeroIds));

        string active = string.Join("|", activeThreats.Select(t =>
            $"{(int)t.race}:{string.Join(",", t.sacrificedHeroIds)}"));
        PlayerPrefs.SetString(ActiveKey, active);

        PlayerPrefs.SetString(NextSpawnKey, nextSpawnAtTicks.ToString());
        PlayerPrefs.Save();
    }

    private void Load()
    {
        queuedHeroIds.Clear();
        string queueRaw = PlayerPrefs.GetString(QueueKey, "");
        if (!string.IsNullOrEmpty(queueRaw))
            queuedHeroIds.AddRange(queueRaw.Split(';').Where(s => !string.IsNullOrEmpty(s)));

        activeThreats.Clear();
        string activeRaw = PlayerPrefs.GetString(ActiveKey, "");
        if (!string.IsNullOrEmpty(activeRaw))
        {
            foreach (var entry in activeRaw.Split('|'))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int raceIndex)) continue;

                var heroIds = parts[1].Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();
                activeThreats.Add(new MineThreat { race = (Race)raceIndex, sacrificedHeroIds = heroIds });
            }
        }

        nextSpawnAtTicks = long.TryParse(PlayerPrefs.GetString(NextSpawnKey, "0"), out long ticks) ? ticks : 0;
    }
}
