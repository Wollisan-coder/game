using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class BattleManager : MonoBehaviour
{
    [Header("Игрок")]
    public int playerMaxHP = 100;
    public int playerHP;
    public int playerShield = 0;

    [Header("Враг")]
    public int enemyMaxHP = 80;
    public int enemyHP;
    public int enemyMinAttack = 5;
    public int enemyMaxAttack = 12;
    public int enemyShield = 0;

    [Header("Дебафф урона героев от скилла врага")]
    public float heroDamageMultiplier = 1f;
    public int heroDamageMultiplierTurnsRemaining = 0;

    // Пассивка Демонов — накопительно, без таймера (в отличие от enemyIncomingDamageMultiplier/
    // enemyArmorDebuffTurnsRemaining, которые сбрасываются по истечении скилловых дебаффов брони) —
    // отдельное поле, чтобы не конфликтовать с ними. См. ApplyRacePassivesPerTurn.
    private float demonsResistanceShredBonus = 0f;

    [Header("Герои в бою")]
    public HeroData[] heroRoster; // назначить в Inspector всех героев, участвующих в бою
    public List<HeroRuntimeState> activeHeroes = new List<HeroRuntimeState>();

    [Header("Урон за фишку (базовый — если героя этого цвета нет или он погиб)")]
    public int[] baseDamagePerGem = { 5, 5, 5, 5, 5 };

    [Header("Урон за фишку (если герой этого цвета жив)")]
    public int[] aliveDamagePerGem = { 8, 8, 8, 8, 8 };

    [Header("Лечение за фишку Pink")]
    public int pinkHealPerGem = 3;

    [Header("Временный бафф урона")]
    public float damageMultiplier = 1f;
    public int damageMultiplierTurnsRemaining = 0;

    [Header("Ссылка на сетку")]
    public GridManager gridManager;

    [Header("Враги (для этой стычки)")]
    public EnemyData[] possibleEnemies;   // заранее заданный пул для случайного боя
    public bool forceRandomEnemy = false; // true = игнорировать выбор из коллекции, всегда рандом
    public EnemyData currentEnemy;        // фактически выбранный враг этого боя

    [Header("Награда за победу (используется, только если у currentEnemy не задан свой loot)")]
    public int accountExperienceReward = 20; // плейсхолдер — легко поменять в инспекторе

    [Header("Сцена, куда возвращаемся после боя")]
    public string mainMenuSceneName = "MainMenuScene";

    private bool battleEnded; // защита от повторного срабатывания (реальная победа + debug-кнопка одновременно)

    public System.Action OnStateChanged;
    public System.Action<string> OnBattleLog; // вызов с текстом строки при каждом нанесённом/полученном уроне
    public System.Action<int> OnEnemyDamaged; // фактически применённый урон (после щита/множителей) — для всплывающих цифр
    public System.Action<HeroRuntimeState, int> OnHeroDamaged; // конкретный герой + фактически применённый урон

    private HeroRuntimeState lastAttackedHero;
    private int consecutiveHitsOnLastHero;

    [Header("Переворот доски (эффекты) — раз за бой")]
    public bool boardFlipUsedThisBattle = false;

    [Header("Гномы — броня врага / невязвимость / отражение урона")]
    public float enemyIncomingDamageMultiplier = 1f; // >1 = враг получает больше урона (ReduceEnemyArmor)
    public int enemyArmorDebuffTurnsRemaining = 0;
    public bool playerInvulnerableNextEnemyTurn = false;
    public float damageReflectPercent = 0f;
    public int damageReflectTurnsRemaining = 0;

    [Header("Зверолюди / общее — оглушение, количество матчей за ход, доп. ходы")]
    public bool enemyStunnedNextTurn = false;
    public int lastTurnMatchCount = 0;
    public int freeExtraTurnsRemaining = 0; // ExtraTurn/DoubleFreeTurn — следующий(е) ход(ы) без ответа врага

    [Header("Демоны — точность врага / слабость / перенос дебаффа")]
    public float enemyMissChancePercent = 0f;
    public int enemyMissChanceTurnsRemaining = 0;
    public float nextHitDamageMultiplier = 1f; // WeaknessMarkNextHit — одноразовый, тратится в DealDamageToEnemy
    public float enemyDamageMultiplier = 1f;   // дебафф собственного урона врага (TransferDebuffToEnemy)
    public int enemyDamageMultiplierTurnsRemaining = 0;

    [Header("Ангелы — иммунитет команды к дебаффам")]
    public int teamDebuffImmuneTurnsRemaining = 0;

    [Header("Гемблинг-колесо — щит команды на N ходов, оглушение/блок скилла героя")]
    public int extendedShieldTurnsRemaining = 0; // пока > 0, щит НЕ сбрасывается после хода врага (ShieldTeamTurns)

    [Header("Boss Training (см. project_boss_training_mechanic_concept) — игрок управляет боссом, ИИ играет доску за тренируемого героя")]
    public bool isBossTraining;
    // ОЦЕНКА, не измерено живым тестом — см. project_campaign_difficulty_curve для методологии перекалибровки после плейтеста.
    public int bossTrainingMaxHP = 300;
    public int bossTrainingTotalTurns = 15;
    public int baseTrainingXp = 1600; // = ценность одного HeroExpGemPurple — см. project_campaign_difficulty_curve
    public BossTrainingSkillData[] bossTrainingSkillKit; // один общий набор на всех тренируемых героев — см. BattleUI

    // Прямая ссылка на ассет, а не heroId-строка — при запуске SampleScene напрямую (Play без похода через
    // MainMenuScene) HeroCollectionManager.Instance ещё не существует (DontDestroyOnLoad не успел создаться
    // в этом Play-сеансе), так что искать героя по id через менеджер тут не получится.
    [Header("Debug — тест Boss Training без похода через Castle/пикер героя (жать Play прямо в SampleScene)")]
    public bool debugForceBossTraining;
    public HeroData debugTrainingHero;
    public float bossTrainingPlayerWindowSeconds = 5f; // сколько ждать бездействия игрока, прежде чем сходить ИИ сама (пасс по таймауту)
    private int bossTrainingTurnsRemaining;
    private string bossTrainingHeroId;
    private Coroutine bossTrainingWindowCoroutine;
    private readonly Dictionary<BossTrainingSkillData, int> bossTrainingSkillUses = new Dictionary<BossTrainingSkillData, int>();

    [System.Serializable]
    private class PendingDamage
    {
        public int turnsRemaining;
        public int amount;
    }

    private List<PendingDamage> pendingEnemyDamage = new List<PendingDamage>();

        private EnemyData ResolveEnemy()
    {
        // 1. Конкретный враг, выбранный игроком в коллекции (если не форсируем рандом)
        if (!forceRandomEnemy &&
            EnemyCollectionManager.Instance != null &&
            EnemyCollectionManager.Instance.selectedEnemy != null)
        {
            return EnemyCollectionManager.Instance.selectedEnemy;
        }

        // 2. Иначе — случайный из заранее заданного массива
        if (possibleEnemies != null && possibleEnemies.Length > 0)
            return possibleEnemies[Random.Range(0, possibleEnemies.Length)];

        return null; // нет ни одного источника — остаются значения из инспектора
    }


            private void Awake()
    {
        playerHP = playerMaxHP;
        boardFlipUsedThisBattle = false;

        isBossTraining = debugForceBossTraining ||
            (AccountManager.Instance != null && AccountManager.Instance.pendingBossTraining);
        if (isBossTraining)
        {
            InitializeBossTraining();
            return; // отдельная инициализация — обычный путь (currentEnemy/EnemyStatCurve/сквод) не нужен
        }

        currentEnemy = ResolveEnemy();
        if (currentEnemy != null)
        {
            enemyMaxHP = currentEnemy.maxHP;
            enemyMinAttack = currentEnemy.minAttack;
            enemyMaxAttack = currentEnemy.maxAttack;
        }

        // Нода на карте перекрывает числа из EnemyData процедурной кривой (EnemyStatCurve) — EnemyData
        // остаётся архетипом (портрет/скиллы/лут), а HP/атака считаются из (территория, номер ноды).
        // Не трогает FarmNode/повторные бои без node-контекста и debug-тесты без WorldMapManager — тогда
        // просто остаются сырые значения EnemyData/инспектора, как раньше.
        var node = WorldMapManager.Instance != null ? WorldMapManager.Instance.currentNode : null;
        if (node != null)
        {
            var curveStats = EnemyStatCurve.GetStats(node.territory, node.nodeIndex);
            enemyMaxHP = curveStats.maxHP;
            enemyMinAttack = curveStats.minAttack;
            enemyMaxAttack = curveStats.maxAttack;
        }

        enemyHP = enemyMaxHP;

        // Берём только реально выбранных героев (без пустых слотов null)
        if (HeroCollectionManager.Instance != null)
            heroRoster = HeroCollectionManager.Instance.squad.Where(h => h != null).ToArray();

        activeHeroes.Clear();
        foreach (var hero in heroRoster)
        {
            if (hero == null) continue;

            int level = 1;
            int ascensionLevel = 0;
            HeroOwnershipData ownership = null;
            if (HeroCollectionManager.Instance != null)
            {
                ownership = HeroCollectionManager.Instance.ownership.Find(o => o.heroId == hero.heroId);
                if (ownership != null)
                {
                    level = ownership.level;
                    ascensionLevel = ownership.ascensionLevel;
                }
            }

            var heroState = new HeroRuntimeState(hero, level, ascensionLevel);

            // Бонусы от экипированных предметов (не меняют сам ассет HeroData, только эту копию на бой)
            var bonuses = HeroStatUtility.CalculateEquipmentBonuses(ownership);
            heroState.maxHealth += bonuses.health;
            heroState.currentHealth += bonuses.health;
            heroState.maxResource += bonuses.mana;
            heroState.damageMultiplier += bonuses.damageMultiplier;
            heroState.armor += bonuses.armor;

            // Пассивный навык "стоит" маны — выбранная пассивка навсегда съедает часть максимума маны героя в этом бою
            if (ownership != null && ownership.passiveSkillIndex >= 0 && hero.skills != null
                && ownership.passiveSkillIndex < hero.skills.Length)
            {
                var passiveSkill = hero.skills[ownership.passiveSkillIndex];
                if (passiveSkill != null)
                    heroState.maxResource = Mathf.Max(1, heroState.maxResource - passiveSkill.cost);
            }

            activeHeroes.Add(heroState);
        }
    }

    // Отдельная инициализация Boss Training: тренируется ОДИН выбранный герой (не весь отряд), "враг" (enemyHP)
    // на самом деле представляет собственный HP-пул игрока-босса — гемм-матчи ИИ (см. GridManager.AutoPlayLoop)
    // бьют по нему через уже существующий DealDamageToEnemy, никакой новой боевой математики не нужно.
    private void InitializeBossTraining()
    {
        var accountManager = AccountManager.Instance;
        HeroData trainedHero;

        if (debugForceBossTraining)
        {
            trainedHero = debugTrainingHero;
            bossTrainingHeroId = trainedHero != null ? trainedHero.heroId : null;
        }
        else
        {
            bossTrainingHeroId = accountManager != null ? accountManager.pendingBossTrainingHeroId : null;
            if (accountManager != null) accountManager.pendingBossTraining = false; // одноразовый флаг, гасим сразу

            trainedHero = HeroCollectionManager.Instance != null
                ? HeroCollectionManager.Instance.allHeroes.FirstOrDefault(h => h != null && h.heroId == bossTrainingHeroId)
                : null;
        }

        heroRoster = trainedHero != null ? new[] { trainedHero } : new HeroData[0];

        enemyMaxHP = bossTrainingMaxHP;
        enemyHP = enemyMaxHP;
        enemyShield = 0;
        bossTrainingTurnsRemaining = bossTrainingTotalTurns;

        activeHeroes.Clear();
        foreach (var hero in heroRoster)
        {
            if (hero == null) continue;

            int level = 1;
            int ascensionLevel = 0;
            HeroOwnershipData ownership = HeroCollectionManager.Instance?.ownership.Find(o => o.heroId == hero.heroId);
            if (ownership != null)
            {
                level = ownership.level;
                ascensionLevel = ownership.ascensionLevel;
            }

            // Тренируется герой в своём реальном виде — экипировка работает как обычно (это не Death Dungeon
            // с уравниванием статов, тут смысл именно прокачать существующего героя таким, какой он есть).
            var heroState = new HeroRuntimeState(hero, level, ascensionLevel);
            var bonuses = HeroStatUtility.CalculateEquipmentBonuses(ownership);
            heroState.maxHealth += bonuses.health;
            heroState.currentHealth += bonuses.health;
            heroState.maxResource += bonuses.mana;
            heroState.damageMultiplier += bonuses.damageMultiplier;
            heroState.armor += bonuses.armor;

            activeHeroes.Add(heroState);
        }

        if (gridManager != null)
            gridManager.SetTrainingMode(true);
    }

    // raiseEvent=false — для вызовов, которые сами суммируют несколько попаданий за один ход
    // (см. ResolvePlayerTurn) и поднимают OnEnemyDamaged один раз с итоговой суммой, а не по кускам.
    public int DealDamageToEnemy(int amount, bool raiseEvent = true)
    {
        float multiplier = enemyIncomingDamageMultiplier * nextHitDamageMultiplier * GetPassiveDamageReductionMultiplier() * (1f + demonsResistanceShredBonus);
        int scaledAmount = Mathf.RoundToInt(amount * multiplier);
        nextHitDamageMultiplier = 1f; // метка слабости — одноразовая

        int absorbed = Mathf.Min(enemyShield, scaledAmount);
        enemyShield -= absorbed;
        int applied = scaledAmount - absorbed;
        enemyHP = Mathf.Max(0, enemyHP - applied);

        if (applied > 0)
        {
            OnBattleLog?.Invoke($"Enemy took {applied} damage");
            if (raiseEvent)
                OnEnemyDamaged?.Invoke(applied);

            ApplyReflectPassives(applied);
        }

        return applied;
    }

    // Базовый урон, если живого героя этого цвета нет; повышенный — если есть
    // Оглушённый (stunnedTurnsRemaining) герой считается недоступным — как будто его цвета нет на поле
    private int GetDamagePerGem(int type)
    {
        bool hasAliveHeroOfColor = activeHeroes.Any(h => h.currentHealth > 0 && h.stunnedTurnsRemaining <= 0 && (int)h.data.resourceType == type);
        return hasAliveHeroOfColor ? aliveDamagePerGem[type] : baseDamagePerGem[type];
    }

    public void ResolvePlayerTurn(Dictionary<int, int> matchedTypeCounts)
    {
        lastTurnMatchCount = matchedTypeCounts.Values.Sum();
        int totalEnemyDamageThisTurn = 0;

        foreach (var pair in matchedTypeCounts)
        {
            int type = pair.Key;
            int count = pair.Value;

            if (type == 5) // Pink — лечение + мана всем живым героям 0-4
            {
                Heal(count * pinkHealPerGem);

                foreach (var hero in activeHeroes)
                {
                    if (hero.currentHealth > 0 && hero.stunnedTurnsRemaining <= 0 && !hero.blockManaGainThisTurn && (int)hero.data.resourceType <= 4)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.maxResource);
                }
            }
            else if (type >= 0 && type <= 4) // Red/Blue/Green/Yellow/Violet
            {
                int baseDamage = count * GetDamagePerGem(type);
                int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier * heroDamageMultiplier);

                // Орки — пассивка: 10% шанс утроить урон этого попадания ("3х удар"), любой цвет.
                if (HasLivingHeroOfRace(Race.Orcs) && Random.value < 0.10f)
                    finalDamage *= 3;

                totalEnemyDamageThisTurn += DealDamageToEnemy(finalDamage, raiseEvent: false);

                // Каждый живой активный герой этого цвета получает полную порцию ресурса
                // (кроме тех, кто только что использовал навык в этот ход — им мана не начисляется)
                foreach (var hero in activeHeroes)
                {
                    if (hero.currentHealth > 0 && hero.stunnedTurnsRemaining <= 0 && !hero.blockManaGainThisTurn && (int)hero.data.resourceType == type)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.maxResource);
                }
            }
        }

        // Эльфы — пассивка: 10% шанс оглушить врага при обычном матче (любой цвет). enemyStunnedNextTurn —
        // bool, не счётчик, поэтому одной попытки за ход достаточно (несколько цветов в одном ходу не дают
        // накопления оглушения).
        if (matchedTypeCounts.Count > 0 && HasLivingHeroOfRace(Race.Elves) && Random.value < 0.10f)
            enemyStunnedNextTurn = true;

        if (totalEnemyDamageThisTurn > 0)
            OnEnemyDamaged?.Invoke(totalEnemyDamageThisTurn);

        // Блокировка маны действовала ровно один ход — снимаем её для следующего
        foreach (var hero in activeHeroes)
            hero.blockManaGainThisTurn = false;

        if (damageMultiplierTurnsRemaining > 0)
        {
            damageMultiplierTurnsRemaining--;
            if (damageMultiplierTurnsRemaining <= 0)
                damageMultiplier = 1f;
        }
                if (heroDamageMultiplierTurnsRemaining > 0)
        {
            heroDamageMultiplierTurnsRemaining--;
            if (heroDamageMultiplierTurnsRemaining <= 0)
                heroDamageMultiplier = 1f;
        }

        if (enemyArmorDebuffTurnsRemaining > 0)
        {
            enemyArmorDebuffTurnsRemaining--;
            if (enemyArmorDebuffTurnsRemaining <= 0)
                enemyIncomingDamageMultiplier = 1f;
        }

        if (damageReflectTurnsRemaining > 0)
        {
            damageReflectTurnsRemaining--;
            if (damageReflectTurnsRemaining <= 0)
                damageReflectPercent = 0f;
        }

        if (enemyMissChanceTurnsRemaining > 0)
        {
            enemyMissChanceTurnsRemaining--;
            if (enemyMissChanceTurnsRemaining <= 0)
                enemyMissChancePercent = 0f;
        }

        if (enemyDamageMultiplierTurnsRemaining > 0)
        {
            enemyDamageMultiplierTurnsRemaining--;
            if (enemyDamageMultiplierTurnsRemaining <= 0)
                enemyDamageMultiplier = 1f;
        }

        if (teamDebuffImmuneTurnsRemaining > 0)
            teamDebuffImmuneTurnsRemaining--;

        foreach (var hero in activeHeroes)
        {
            if (hero.borrowedSkillTurnsRemaining > 0)
            {
                hero.borrowedSkillTurnsRemaining--;
                if (hero.borrowedSkillTurnsRemaining <= 0)
                    hero.borrowedSkill = null;
            }

            if (hero.stunnedTurnsRemaining > 0)
                hero.stunnedTurnsRemaining--;

            if (hero.skillBlockedTurnsRemaining > 0)
                hero.skillBlockedTurnsRemaining--;
        }

        for (int i = pendingEnemyDamage.Count - 1; i >= 0; i--)
        {
            pendingEnemyDamage[i].turnsRemaining--;
            if (pendingEnemyDamage[i].turnsRemaining <= 0)
            {
                DealDamageToEnemy(pendingEnemyDamage[i].amount);
                pendingEnemyDamage.RemoveAt(i);
            }
        }

        ApplyRacePassivesPerTurn();

        OnStateChanged?.Invoke();

        if (isBossTraining)
        {
            // ИИ-гемм-матч — это и есть "атака героя", отдельного хода-ответа врага (=босса-игрока) нет:
            // босс защищается своими скиллами по клику игрока, не автоматически. Сессия кончается по HP
            // босса до нуля ИЛИ по истечении фиксированного числа ходов — что раньше.
            bossTrainingTurnsRemaining--;
            if (enemyHP <= 0 || bossTrainingTurnsRemaining <= 0)
                EndBossTraining();
            else
                OpenBossTrainingPlayerWindow();

            return;
        }

        if (enemyHP <= 0)
        {
            OnEnemyDefeated();
        }
        else if (freeExtraTurnsRemaining > 0)
        {
            freeExtraTurnsRemaining--;
            OnBattleLog?.Invoke("Extra turn — no enemy response!");
        }
        else
        {
            StartCoroutine(EnemyTurnRoutine());
        }
    }

    // "Ход игрока" между автоходами ИИ: игрок может использовать РОВНО один скилл босса (клик сразу
    // обрывает ожидание и передаёт ход дальше — см. UseBossTrainingSkill), либо ничего не делать и
    // дождаться таймаута — тогда ход переходит к ИИ сам. Так нельзя заспамить один скилл несколько
    // раз подряд между ходами ИИ (баг, который поймал пользователь с Set Trap).
    private void OpenBossTrainingPlayerWindow()
    {
        if (bossTrainingWindowCoroutine != null) StopCoroutine(bossTrainingWindowCoroutine);
        bossTrainingWindowCoroutine = StartCoroutine(BossTrainingPlayerWindowRoutine());
    }

    private IEnumerator BossTrainingPlayerWindowRoutine()
    {
        yield return new WaitForSeconds(bossTrainingPlayerWindowSeconds);
        bossTrainingWindowCoroutine = null;
        gridManager?.RequestNextAiMove();
    }

    // FindAnyObjectByType<Canvas>() ненадёжен, если в сцене больше одного Canvas (в SampleScene их минимум
    // два) — может найти не тот, и попап рисуется криво/не по центру. BattleUI уже решает ровно эту же
    // проблему через enemyPortrait.GetComponentInParent<Canvas>() — переиспользуем то же самое решение
    // здесь, а не гадаем заново с FindAnyObjectByType<Canvas>().
    private Transform ResolveDialogRoot()
    {
        var battleUI = FindAnyObjectByType<BattleUI>();
        if (battleUI != null && battleUI.enemyPortrait != null)
        {
            var uiCanvas = battleUI.enemyPortrait.GetComponentInParent<Canvas>();
            if (uiCanvas != null) return uiCanvas.transform;
        }

        var anyCanvas = FindAnyObjectByType<Canvas>();
        return anyCanvas != null ? anyCanvas.transform : transform;
    }

    // Итог тренировки: XP тренируемому герою пропорционален доле оставшегося HP босса (минимум 10%, чтобы
    // полный разгром не ощущался как "впустую") — см. project_boss_training_mechanic_concept про анти-абьюз принцип.
    public void EndBossTraining()
    {
        if (battleEnded) return;
        battleEnded = true;

        gridManager?.SetTrainingMode(false);

        float hpFraction = enemyMaxHP > 0 ? (float)enemyHP / enemyMaxHP : 0f;
        int heroXp = Mathf.RoundToInt(baseTrainingXp * Mathf.Max(0.1f, hpFraction));

        if (!string.IsNullOrEmpty(bossTrainingHeroId))
            HeroCollectionManager.Instance?.GrantExperience(bossTrainingHeroId, heroXp);

        AccountManager.Instance?.MarkBossTrainingDone();

        string summary = $"Training complete!\nBoss HP held: {Mathf.RoundToInt(hpFraction * 100)}%\nHero XP +{heroXp}";
        OnBattleLog?.Invoke(summary.Replace("\n", " | "));

        Transform root = ResolveDialogRoot();
        ConfirmationDialog.ShowInfo(root, summary, 220, () =>
        {
            if (AccountManager.Instance != null)
                AccountManager.Instance.returningFromBossTraining = true;

            SceneManager.LoadScene(mainMenuSceneName);
        });
    }

    // Универсальный набор скиллов босса (игрока) в Boss Training — все эффекты переиспользуют уже
    // существующие поля/методы врага (enemyShield/enemyHP/heroDamageMultiplier/enemyIncomingDamageMultiplier),
    // просто с ролями наоборот: тут игрок нажимает кнопку, а не AI выбирает скилл случайно.
    public void UseBossTrainingSkill(BossTrainingSkillData skill)
    {
        if (!isBossTraining || skill == null) return;
        if (bossTrainingWindowCoroutine == null) return; // не "окно игрока" — ход сейчас не у босса

        int used = bossTrainingSkillUses.TryGetValue(skill, out int u) ? u : 0;
        if (skill.usesPerTraining > 0 && used >= skill.usesPerTraining)
        {
            OnBattleLog?.Invoke($"{skill.skillName}: no uses left this training.");
            return;
        }

        bossTrainingSkillUses[skill] = used + 1;

        switch (skill.effectType)
        {
            case BossTrainingSkillEffectType.Shield:
                enemyShield += Mathf.RoundToInt(enemyMaxHP * skill.percentOfMaxHP);
                break;

            case BossTrainingSkillEffectType.Heal:
                enemyHP = Mathf.Min(enemyMaxHP, enemyHP + Mathf.RoundToInt(enemyMaxHP * skill.percentOfMaxHP));
                break;

            case BossTrainingSkillEffectType.WeakenAIDamage:
                heroDamageMultiplier = 1f - skill.reductionPercent;
                heroDamageMultiplierTurnsRemaining = skill.durationTurns;
                break;

            case BossTrainingSkillEffectType.ReduceIncoming:
                enemyIncomingDamageMultiplier = 1f - skill.reductionPercent;
                enemyArmorDebuffTurnsRemaining = skill.durationTurns;
                break;

            case BossTrainingSkillEffectType.CastTrap:
                gridManager?.CastHarmfulTileRandom(skill.trapType, skill.trapValue);
                break;
        }

        OnStateChanged?.Invoke();

        // Игрок потратил своё единственное действие в этом окне — сразу закрываем окно и передаём ход ИИ,
        // не дожидаясь bossTrainingPlayerWindowSeconds (иначе можно было бы использовать ещё один скилл до таймаута).
        if (bossTrainingWindowCoroutine != null)
        {
            StopCoroutine(bossTrainingWindowCoroutine);
            bossTrainingWindowCoroutine = null;
        }
        gridManager?.RequestNextAiMove();
    }

    // Остаток кликов скилла в текущей тренировке — для счётчика на кнопке в BattleUI. -1 = без лимита (счётчик не показываем).
    public int GetBossTrainingSkillUsesRemaining(BossTrainingSkillData skill)
    {
        if (skill == null || skill.usesPerTraining <= 0) return -1;
        int used = bossTrainingSkillUses.TryGetValue(skill, out int u) ? u : 0;
        return Mathf.Max(0, skill.usesPerTraining - used);
    }

    public void Heal(int amount) => playerHP = Mathf.Min(playerMaxHP, playerHP + amount);
    public void AddShield(int amount) => playerShield += amount;

    // Эффекты для панели переворота доски (BoardEffectOption)
    public void ApplyDamageBuff(float multiplier, int turns)
    {
        damageMultiplier = multiplier;
        damageMultiplierTurnsRemaining = turns;
        OnStateChanged?.Invoke();
    }

    public void ApplyWeakenHeroes(float multiplier, int turns)
    {
        if (teamDebuffImmuneTurnsRemaining > 0) return; // команда иммунна к дебаффам (Ангелы)

        heroDamageMultiplier = multiplier;
        heroDamageMultiplierTurnsRemaining = turns;
        OnStateChanged?.Invoke();
    }

    public void AddEnemyShield(int amount)
    {
        enemyShield += amount;
        OnStateChanged?.Invoke();
    }

    public void BlockManaForAllHeroes()
    {
        foreach (var hero in activeHeroes)
            hero.blockManaGainThisTurn = true;
        OnStateChanged?.Invoke();
    }

    public HeroRuntimeState DamageRandomHero(int amount)
    {
        var hero = GetRandomAliveHero();
        ApplyDamageToHero(hero, amount);
        OnStateChanged?.Invoke();
        return hero;
    }

    public void FullManaAllHeroes()
    {
        foreach (var hero in activeHeroes)
            hero.currentResource = hero.maxResource;
        OnStateChanged?.Invoke();
    }

    public void HealTeamPercent(float percent)
    {
        Heal(Mathf.RoundToInt(playerMaxHP * percent));
    }

    public void ApplyTeamShieldForTurns(int amount, int turns)
    {
        AddShield(amount);
        extendedShieldTurnsRemaining = turns;
        OnStateChanged?.Invoke();
    }

    public HeroRuntimeState StunRandomHero(int turns)
    {
        var hero = GetRandomAliveHero();
        if (hero != null) hero.stunnedTurnsRemaining = turns;
        OnStateChanged?.Invoke();
        return hero;
    }

    public HeroRuntimeState BlockRandomHeroSkill(int turns)
    {
        var hero = GetRandomAliveHero();
        if (hero != null) hero.skillBlockedTurnsRemaining = turns;
        OnStateChanged?.Invoke();
        return hero;
    }

    public void ApplyEnemyDamageBuff(float multiplier, int turns)
    {
        enemyDamageMultiplier = multiplier;
        enemyDamageMultiplierTurnsRemaining = turns;
        OnStateChanged?.Invoke();
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        ApplyRegenPassives(); // тикает независимо от оглушения — это не действие, а пассивный эффект

        if (enemyStunnedNextTurn)
        {
            enemyStunnedNextTurn = false;
            OnBattleLog?.Invoke("Enemy is stunned and skips its turn!");
        }
        else
        {
            EnemySkillData skill = PickEnemySkill();
            if (skill != null)
                UseEnemySkill(skill);
            else
                BasicEnemyAttack(); // если скиллы не заданы — старая простая атака
        }

        // Щит действует от активации до следующего хода игрока — после хода врага снимается,
        // если только не активен продлённый щит (ShieldTeamTurns) на несколько ходов вперёд
        if (extendedShieldTurnsRemaining > 0)
            extendedShieldTurnsRemaining--;
        else
            playerShield = 0;

        playerInvulnerableNextEnemyTurn = false; // действовала ровно один ход врага

        OnStateChanged?.Invoke();

        if (playerHP <= 0)
            OnPlayerDefeated();
    }

    // --- Пассивки врага (EnemyPassiveData) — всегда активны, без хода ---

    private void ApplyRegenPassives()
    {
        if (currentEnemy?.passives == null) return;

        foreach (var p in currentEnemy.passives)
        {
            if (p.effectType != EnemyPassiveEffectType.Regen) continue;

            int healAmount = Mathf.RoundToInt(enemyMaxHP * p.percent);
            if (healAmount <= 0 || enemyHP >= enemyMaxHP) continue;

            enemyHP = Mathf.Min(enemyMaxHP, enemyHP + healAmount);
            OnBattleLog?.Invoke($"Enemy regenerates {healAmount} HP");
        }
    }

    private float GetPassiveDamageReductionMultiplier()
    {
        float mult = 1f;
        if (currentEnemy?.passives == null) return mult;

        foreach (var p in currentEnemy.passives)
            if (p.effectType == EnemyPassiveEffectType.DamageReduction)
                mult *= Mathf.Clamp01(1f - p.percent);

        return mult;
    }

    private void ApplyReflectPassives(int damageTaken)
    {
        if (currentEnemy?.passives == null || damageTaken <= 0) return;

        foreach (var p in currentEnemy.passives)
        {
            if (p.effectType != EnemyPassiveEffectType.Reflect) continue;

            int reflected = Mathf.RoundToInt(damageTaken * p.percent);
            if (reflected > 0)
                ApplyDamageToHero(GetRandomAliveHero(), reflected);
        }
    }

    // +% к урону врага, пока в отряде жив хотя бы один герой "поглощаемого" цвета (design intent из
    // project_game_design_concept: AbsorbedColors) — форсирует разнообразие состава вместо одного лучшего отряда.
    private float GetPassiveOutgoingDamageMultiplier()
    {
        float mult = 1f;
        if (currentEnemy?.passives == null) return mult;

        foreach (var p in currentEnemy.passives)
        {
            if (p.effectType == EnemyPassiveEffectType.ColorAbsorb &&
                activeHeroes.Any(h => h.currentHealth > 0 && (int)h.data.resourceType == p.absorbColorType))
            {
                mult += p.absorbDamageBonus;
            }
        }

        return mult;
    }

    private EnemySkillData PickEnemySkill()
    {
        if (currentEnemy == null || currentEnemy.skills == null || currentEnemy.skills.Length == 0)
            return null;

        float totalWeight = currentEnemy.skills.Sum(s => Mathf.Max(0.0001f, s.weight));
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var skill in currentEnemy.skills)
        {
            cumulative += Mathf.Max(0.0001f, skill.weight);
            if (roll <= cumulative)
                return skill;
        }

        return currentEnemy.skills[currentEnemy.skills.Length - 1];
    }

    private void UseEnemySkill(EnemySkillData skill)
    {
        switch (skill.effectType)
        {
            case EnemySkillEffectType.Damage:
                // Скилл бьёт по случайному живому герою, но НЕ учитывается в запрете "3 раза подряд"
                if (RollEnemyMiss())
                {
                    OnBattleLog?.Invoke("Enemy missed!");
                    break;
                }
                ApplyDamageToHero(GetRandomAliveHero(), Mathf.RoundToInt(skill.effectValue * currentEnemy.damageMultiplier * enemyDamageMultiplier * GetPassiveOutgoingDamageMultiplier()));
                break;

            case EnemySkillEffectType.ShieldSelf:
                enemyShield += Mathf.RoundToInt(enemyMaxHP * skill.shieldPercentOfMaxHP);
                break;

            case EnemySkillEffectType.WeakenHeroes:
                if (teamDebuffImmuneTurnsRemaining > 0) break; // команда иммунна к дебаффам (Ангелы)
                heroDamageMultiplier = 1f - skill.damageReductionPercent;
                heroDamageMultiplierTurnsRemaining = skill.debuffDurationTurns;
                break;
        }
    }

    private void BasicEnemyAttack()
    {
        if (RollEnemyMiss())
        {
            OnBattleLog?.Invoke("Enemy missed!");
            return;
        }

        int rawDamage = Random.Range(enemyMinAttack, enemyMaxAttack + 1);
        float multiplier = (currentEnemy != null ? currentEnemy.damageMultiplier : 1f) * enemyDamageMultiplier * GetPassiveOutgoingDamageMultiplier();
        int finalDamage = Mathf.RoundToInt(rawDamage * multiplier);

        ApplyDamageToHero(PickBasicAttackTarget(), finalDamage);
    }

    // Случайный живой герой; exclude позволяет убрать конкретного героя из выборки
    private HeroRuntimeState GetRandomAliveHero(HeroRuntimeState exclude = null)
    {
        var candidates = activeHeroes.Where(h => h.currentHealth > 0 && h != exclude).ToList();

        if (candidates.Count == 0)
            candidates = activeHeroes.Where(h => h.currentHealth > 0).ToList();

        if (candidates.Count == 0) return null; // все герои погибли

        return candidates[Random.Range(0, candidates.Count)];
    }

    // Жив, не оглушён (та же логика, что и в GetDamagePerGem — оглушённый герой считается недоступным)
    private bool HasLivingHeroOfRace(Race race) =>
        activeHeroes.Any(h => h.currentHealth > 0 && h.stunnedTurnsRemaining <= 0 && h.data.race == race);

    private HeroRuntimeState GetLivingHeroOfRace(Race race, HeroRuntimeState exclude = null) =>
        activeHeroes.FirstOrDefault(h => h.currentHealth > 0 && h.stunnedTurnsRemaining <= 0 && h.data.race == race && h != exclude);

    // Пассивки рас — по одной на расу, включаются, пока в отряде жив (не оглушён) хотя бы один герой этой
    // расы. Проки Эльфов/Орков (завязаны на конкретное попадание) — прямо в цикле ResolvePlayerTurn, здесь —
    // тикающие раз в ход. Список и проценты — по прямому ТЗ пользователя, не выведены из дизайн-доков.
    private void ApplyRacePassivesPerTurn()
    {
        // Феи (Fairy, "Гномы" в разговоре) — щит на 15% макс. HP случайного героя, каждый ход. Щит в этом
        // проекте общий на отряд (playerShield), не per-hero — случайный герой определяет только % базу.
        if (HasLivingHeroOfRace(Race.Fairy))
        {
            var randomHero = GetRandomAliveHero();
            if (randomHero != null)
                AddShield(Mathf.RoundToInt(randomHero.maxHealth * 0.15f));
        }

        // Драконы (Dragonkin) — периодический урон врагу, 1% от его максимального HP, каждый ход.
        if (HasLivingHeroOfRace(Race.Dragonkin))
            DealDamageToEnemy(Mathf.RoundToInt(enemyMaxHP * 0.01f));

        // Демоны — сопротивление врага снижается на 2% каждый ход, накопительно (см. demonsResistanceShredBonus).
        if (HasLivingHeroOfRace(Race.Demons))
            demonsResistanceShredBonus += 0.02f;

        // Ангелы — лечение всей команде, 5% от playerMaxHP каждый ход.
        if (HasLivingHeroOfRace(Race.Angels))
            Heal(Mathf.RoundToInt(playerMaxHP * 0.05f));

        // Зверолюди (Beastfolk) — 10% шанс не потратить ход на этом матче (лишний бесплатный ход).
        if (HasLivingHeroOfRace(Race.Beastfolk) && Random.value < 0.10f)
            freeExtraTurnsRemaining++;
    }

    // Обычная атака врага не может попасть в одного героя 3 раза подряд
    private HeroRuntimeState PickBasicAttackTarget()
    {
        HeroRuntimeState exclude = consecutiveHitsOnLastHero >= 2 ? lastAttackedHero : null;
        HeroRuntimeState target = GetRandomAliveHero(exclude);

        if (target != null && target == lastAttackedHero)
            consecutiveHitsOnLastHero++;
        else
            consecutiveHitsOnLastHero = 1;

        lastAttackedHero = target;
        return target;
    }

    private void ApplyDamageToHero(HeroRuntimeState hero, int rawDamage)
    {
        if (hero == null) return; // все герои погибли — атаковать некого

        if (playerInvulnerableNextEnemyTurn)
        {
            OnBattleLog?.Invoke("Attack blocked — invulnerable!");
            return;
        }

        // Проклятая клетка BloodMark на поле — увеличивает весь входящий урон игроку, пока не убрана.
        if (gridManager != null)
            rawDamage = Mathf.RoundToInt(rawDamage * gridManager.GetBloodMarkDamageMultiplier());

        int absorbed = Mathf.Min(playerShield, rawDamage);
        playerShield -= absorbed;
        int afterShield = rawDamage - absorbed;

        // Броня (целиком от бижутерии) снижает урон плоским значением после щита — минимум 1,
        // если урон вообще прошёл щит, чтобы броня смягчала, но не давала полную неуязвимость
        int applied = afterShield > 0 ? Mathf.Max(1, afterShield - hero.armor) : 0;
        hero.TakeDamage(applied);

        if (applied > 0)
        {
            OnBattleLog?.Invoke($"{hero.data.heroName} took {applied} damage");
            OnHeroDamaged?.Invoke(hero, applied);

            if (damageReflectTurnsRemaining > 0 && damageReflectPercent > 0f)
            {
                int reflected = Mathf.RoundToInt(applied * damageReflectPercent);
                if (reflected > 0)
                    DealDamageToEnemy(reflected);
            }
        }

        if (hero.currentHealth <= 0)
            OnHeroDefeated(hero);
    }

    // Шанс промаха врага (Демоны: ReduceEnemyAccuracy) — проверять перед применением урона от врага
    private bool RollEnemyMiss()
    {
        return enemyMissChanceTurnsRemaining > 0 && Random.value < enemyMissChancePercent;
    }

    private void OnHeroDefeated(HeroRuntimeState hero)
    {
        Debug.Log($"Герой {hero.data.heroName} погиб!");

        if (activeHeroes.All(h => h.currentHealth <= 0))
            OnPlayerDefeated();
    }

    private void OnEnemyDefeated()
    {
        Debug.Log("Враг побеждён!");
        EndBattleVictory();
    }

    // Публичный — можно повесить прямо на debug-кнопку в инспекторе, чтобы протестировать
    // экран победы и выдачу добычи, не проходя бой целиком.
    public void EndBattleVictory()
    {
        if (battleEnded) return;
        battleEnded = true;

        LootReward loot = currentEnemy != null ? currentEnemy.loot : null;
        int accountXp = loot != null ? loot.accountExperience : accountExperienceReward; // ОСТАЁТСЯ плоским — см. "Вариант C"
        int heroXp = loot != null ? loot.heroExperience : 0;

        // heroExperience растёт по той же кривой, что и HP врага (EnemyStatCurve), когда есть node-контекст —
        // перекрывает статичное значение из LootReward, как и статы врага выше в Awake().
        var rewardNode = WorldMapManager.Instance != null ? WorldMapManager.Instance.currentNode : null;
        if (rewardNode != null)
            heroXp = EnemyStatCurve.GetHeroExperience(rewardNode.territory, rewardNode.nodeIndex);

        var rewardLines = new List<string>();

        AccountManager.Instance?.GrantExperience(accountXp);
        rewardLines.Add($"Account XP +{accountXp}");

        if (heroXp > 0)
        {
            foreach (var hero in activeHeroes)
                HeroCollectionManager.Instance?.GrantExperience(hero.data.heroId, heroXp);

            rewardLines.Add($"Hero XP +{heroXp} each");
        }

        if (loot != null && loot.currency != null)
        {
            foreach (var entry in loot.currency)
            {
                if (entry.amount <= 0) continue;
                // Wood/Stone перекрываются EnemyStatCurve ниже, когда есть node-контекст — тот же принцип,
                // что и для heroXp/статов врага. Остальные типы валюты (например Shards) идут как заавторено.
                if (rewardNode != null && (entry.type == CurrencyType.Wood || entry.type == CurrencyType.Stone))
                    continue;
                PlayerCurrencies.Instance?.Add(entry.type, entry.amount);
                rewardLines.Add($"{entry.type} +{entry.amount}");
            }
        }

        if (rewardNode != null)
        {
            var (wood, stone) = EnemyStatCurve.GetCurrency(rewardNode.territory, rewardNode.nodeIndex);
            PlayerCurrencies.Instance?.Add(CurrencyType.Wood, wood);
            PlayerCurrencies.Instance?.Add(CurrencyType.Stone, stone);
            rewardLines.Add($"Wood +{wood}");
            rewardLines.Add($"Stone +{stone}");
        }

        // Гарантированный дроп фарм-данжа (Purple/Orange farm pool) — поверх обычного лута, не вместо него.
        if (rewardNode != null && rewardNode.isFarmNode && rewardNode.farmLootPool != null)
        {
            var farmItem = SummonService.Instance?.RollFreeItem(rewardNode.farmLootPool);
            if (farmItem != null)
                rewardLines.Add($"{farmItem.itemName} +1");
        }

        if (loot != null && loot.items != null)
        {
            foreach (var entry in loot.items)
            {
                if (entry.item == null || entry.count <= 0) continue;
                ItemCollectionManager.Instance?.AddItemCopy(entry.item, entry.count);
                rewardLines.Add($"{entry.item.itemName} x{entry.count}");
            }
        }

        // ОП начисляется только за ПЕРВОЕ прохождение ноды ("1 story level = 30 ОП") — проверяем ДО того,
        // как CompleteCurrentNode() пометит её пройденной, иначе фарм уже открытых уровней давал бы ОП бесконечно.
        int progressPoints = loot != null ? loot.progressPoints : 0;
        bool alreadyCompleted = WorldMapManager.Instance != null
            && WorldMapManager.Instance.completedNodeIds.Contains(WorldMapManager.Instance.currentNodeId);

        if (progressPoints > 0 && !alreadyCompleted)
        {
            PlayerCurrencies.Instance?.Add(CurrencyType.ProgressPoints, progressPoints);
            rewardLines.Add($"Progress Points +{progressPoints}");
        }

        WorldMapManager.Instance?.CompleteCurrentNode();

        string summary = "Victory!\n" + string.Join("\n", rewardLines);
        OnBattleLog?.Invoke(summary.Replace("\n", " | "));

        Transform root = ResolveDialogRoot();
        ConfirmationDialog.ShowInfo(root, summary, 260, () => SceneManager.LoadScene(mainMenuSceneName));
    }
    private void OnPlayerDefeated()
    {
        Debug.Log("Игрок проиграл бой.");
        EndBattleDefeat();
    }

    // Публичный — можно повесить прямо на debug-кнопку в инспекторе, аналогично EndBattleVictory().
    // Добычи и отметки ноды карты пройденной нет — только возврат в меню.
    public void EndBattleDefeat()
    {
        if (battleEnded) return;
        battleEnded = true;

        const string summary = "Defeat...";
        OnBattleLog?.Invoke(summary);

        Transform root = ResolveDialogRoot();
        ConfirmationDialog.ShowInfo(root, summary, 170, () => SceneManager.LoadScene(mainMenuSceneName));
    }

    // Теперь привязано к конкретному герою, а не к глобальному ресурсу
    public bool TryUseSkill(HeroRuntimeState hero, SkillData skill)
    {
        if (hero.currentHealth <= 0)
            return false;

        if (hero.stunnedTurnsRemaining > 0 || hero.skillBlockedTurnsRemaining > 0)
            return false; // герой оглушён (StunRandomHero) или скилл заблокирован (BlockHeroSkill)

        int actualCost = Mathf.RoundToInt(skill.cost * (1f - hero.costReductionPercent));

        if (hero.currentResource < actualCost)
            return false;

        hero.costReductionPercent = 0f; // скидка (ReduceAllyNextSkillCost) тратится вместе с этим использованием
        hero.currentResource -= actualCost;
        hero.blockManaGainThisTurn = true; // этому герою нельзя пополнить ману в этот ход
        hero.lastUsedSkill = skill; // для CopyAllyLastSkill

        ApplySkillEffect(hero, skill);

        // Люди — пассивка: 15% от потраченной маны возвращается живому герою-Человеку рядом (не самому
        // кастующему — если кастующий и есть тот единственный Человек, возвращать некому).
        var humanAlly = GetLivingHeroOfRace(Race.Humans, exclude: hero);
        if (humanAlly != null)
            humanAlly.currentResource = Mathf.Min(humanAlly.maxResource, humanAlly.currentResource + Mathf.RoundToInt(actualCost * 0.15f));

        OnStateChanged?.Invoke();

        bool skipsImmediateEnemyTurn =
            skill.effectType == SkillEffectType.ConvertAndDestroyRed ||
            skill.effectType == SkillEffectType.DestroyRows ||
            skill.effectType == SkillEffectType.DestroyRandomGems ||
            skill.effectType == SkillEffectType.DestroyHarmfulTile ||
            skill.effectType == SkillEffectType.FavorableReshuffle ||
            skill.effectType == SkillEffectType.ExtraTurn ||
            skill.effectType == SkillEffectType.DoubleFreeTurn;

        if (!skipsImmediateEnemyTurn)
        {
            // В Boss Training нет обычного врага (currentEnemy == null) — EnemyTurnRoutine там бессмысленна
            // (PickEnemySkill вернёт null и отработает BasicEnemyAttack по playerHP, чего быть не должно).
            // Вместо этого — та же ветка, что и после гемм-матча в ResolvePlayerTurn.
            if (isBossTraining)
            {
                bossTrainingTurnsRemaining--;
                if (enemyHP <= 0 || bossTrainingTurnsRemaining <= 0)
                    EndBossTraining();
                else
                    OpenBossTrainingPlayerWindow();
            }
            else if (enemyHP <= 0)
            {
                OnEnemyDefeated();
            }
            else
            {
                StartCoroutine(EnemyTurnRoutine());
            }
        }

        return true;
    }

    // ИИ тренируемого героя пытается применить свой активный навык вместо хода по доске — аналог клика
    // игрока по HeroCardUI.OnActivateClicked, но автоматически (см. GridManager.PlayOneAiMove).
    public bool TryUseTrainedHeroSkillIfPossible()
    {
        if (!isBossTraining || activeHeroes.Count == 0) return false;

        HeroRuntimeState hero = activeHeroes[0];
        SkillData skill = ResolveTrainedHeroActiveSkill(hero);
        return skill != null && TryUseSkill(hero, skill);
    }

    // Тот же выбор активного навыка, что и HeroCardUI.ResolveActiveSkill — выбранный игроком в инвентаре, иначе первый.
    private SkillData ResolveTrainedHeroActiveSkill(HeroRuntimeState hero)
    {
        if (hero.data.skills == null || hero.data.skills.Length == 0) return null;

        int activeIndex = 0;
        var ownership = HeroCollectionManager.Instance?.ownership.Find(o => o.heroId == hero.data.heroId);
        if (ownership != null)
            activeIndex = Mathf.Clamp(ownership.activeSkillIndex, 0, hero.data.skills.Length - 1);

        return hero.data.skills[activeIndex];
    }

    // Случайный живой герой, кроме exclude (без фолбэка на самого exclude — в отличие от GetRandomAliveHero)
    private HeroRuntimeState GetRandomOtherLivingHero(HeroRuntimeState exclude)
    {
        var candidates = activeHeroes.Where(h => h.currentHealth > 0 && h != exclude).ToList();
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // Вся логика эффектов скилла — отдельно от оплаты/пометки хода, чтобы CopyAllyLastSkill могло
    // бесплатно применить скилл союзника, вызвав этот же метод напрямую
    private void ApplySkillEffect(HeroRuntimeState hero, SkillData skill)
    {
        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                DealDamageToEnemy(Mathf.RoundToInt(skill.effectValue * damageMultiplier * hero.damageMultiplier));
                break;

            case SkillEffectType.Heal:
                Heal(skill.effectValue);
                break;

            case SkillEffectType.Shield:
                AddShield(skill.effectValue);
                break;

            case SkillEffectType.ConvertAndDestroyRed:
                StartCoroutine(gridManager.ExecuteConvertAndDestroySkill(skill.effectValue));
                break;

            case SkillEffectType.DestroyRows:
                StartCoroutine(gridManager.ExecuteDestroyRowsSkill(skill.rowStart, skill.rowEnd));
                break;

            case SkillEffectType.ShieldPercent:
                AddShield(Mathf.RoundToInt(playerMaxHP * skill.shieldPercentOfMaxHP));
                break;

            case SkillEffectType.DamageBuffTurns:
                damageMultiplier = skill.damageMultiplier;
                damageMultiplierTurnsRemaining = skill.buffDurationTurns;
                break;

            // --- Эльфы ---
            case SkillEffectType.DestroyRandomGems:
                StartCoroutine(gridManager.ExecuteDestroyRandomGemsSkill(skill.effectValue));
                break;

            case SkillEffectType.DestroyHarmfulTile:
                StartCoroutine(gridManager.ExecuteDestroyHarmfulTileSkill());
                break;

            case SkillEffectType.ConvertCellToJoker:
                gridManager.ConvertRandomCellToJoker();
                break;

            case SkillEffectType.FavorableReshuffle:
                StartCoroutine(gridManager.ExecuteFavorableReshuffleSkill());
                break;

            // --- Гномы/феи ---
            case SkillEffectType.ReduceEnemyArmor:
                enemyIncomingDamageMultiplier = 1f + skill.shieldPercentOfMaxHP;
                enemyArmorDebuffTurnsRemaining = skill.buffDurationTurns;
                break;

            case SkillEffectType.Invulnerability:
                playerInvulnerableNextEnemyTurn = true;
                break;

            case SkillEffectType.ShieldAndReflect:
                AddShield(skill.effectValue);
                damageReflectPercent = skill.shieldPercentOfMaxHP;
                damageReflectTurnsRemaining = skill.buffDurationTurns;
                break;

            // --- Орки ---
            case SkillEffectType.MultiHit:
                for (int i = 0; i < skill.hitCount; i++)
                    DealDamageToEnemy(Mathf.RoundToInt(skill.effectValue * hero.damageMultiplier));
                break;

            case SkillEffectType.DamagePercentCurrentEnemyHP:
                DealDamageToEnemy(Mathf.RoundToInt(enemyHP * skill.shieldPercentOfMaxHP));
                break;

            case SkillEffectType.SacrificeForDamage:
                int sacrificeAmount = Mathf.RoundToInt(playerHP * skill.shieldPercentOfMaxHP);
                playerHP = Mathf.Max(1, playerHP - sacrificeAmount); // не убивает игрока самим скиллом
                DealDamageToEnemy(Mathf.RoundToInt(skill.effectValue * skill.damageMultiplier));
                break;

            // --- Звероlюди ---
            case SkillEffectType.ExtraTurn:
                break; // само использование не провоцирует ответ врага (см. TryUseSkill)

            case SkillEffectType.StunEnemy:
                enemyStunnedNextTurn = true;
                break;

            case SkillEffectType.DamageScalingWithMatches:
                DealDamageToEnemy(skill.effectValue * Mathf.Max(1, lastTurnMatchCount));
                break;

            case SkillEffectType.DoubleFreeTurn:
                freeExtraTurnsRemaining += 1; // + само использование тоже без ответа врага
                break;

            // --- Дракониды ---
            case SkillEffectType.DelayedDamageMark:
                pendingEnemyDamage.Add(new PendingDamage { turnsRemaining = skill.buffDurationTurns, amount = skill.effectValue });
                break;

            case SkillEffectType.DamagePercentMaxEnemyHP:
                DealDamageToEnemy(Mathf.RoundToInt(enemyMaxHP * skill.shieldPercentOfMaxHP));
                break;

            case SkillEffectType.CleanseDebuffsAndDamage:
                heroDamageMultiplier = 1f;
                heroDamageMultiplierTurnsRemaining = 0;
                DealDamageToEnemy(skill.effectValue);
                break;

            // --- Демоны ---
            case SkillEffectType.ReduceEnemyAccuracy:
                enemyMissChancePercent = skill.shieldPercentOfMaxHP;
                enemyMissChanceTurnsRemaining = skill.buffDurationTurns;
                break;

            case SkillEffectType.StealEnemyShield:
                int stolenShield = enemyShield;
                enemyShield = 0;
                AddShield(stolenShield);
                break;

            case SkillEffectType.WeaknessMarkNextHit:
                nextHitDamageMultiplier = skill.damageMultiplier;
                break;

            case SkillEffectType.TransferDebuffToEnemy:
                if (heroDamageMultiplierTurnsRemaining > 0)
                {
                    enemyDamageMultiplier = heroDamageMultiplier;
                    enemyDamageMultiplierTurnsRemaining = heroDamageMultiplierTurnsRemaining;
                    heroDamageMultiplier = 1f;
                    heroDamageMultiplierTurnsRemaining = 0;
                }
                break;

            // --- Ангелы ---
            case SkillEffectType.FullManaRefill:
                hero.currentResource = hero.maxResource;
                break;

            case SkillEffectType.TeamDebuffImmunity:
                teamDebuffImmuneTurnsRemaining = skill.buffDurationTurns;
                break;

            case SkillEffectType.ReviveHero:
                var deadHero = activeHeroes.FirstOrDefault(h => h.currentHealth <= 0);
                if (deadHero != null)
                    deadHero.currentHealth = Mathf.Max(1, Mathf.RoundToInt(deadHero.maxHealth * skill.shieldPercentOfMaxHP));
                break;

            // --- Люди ---
            case SkillEffectType.ManaTransfer:
                var manaTarget = GetRandomOtherLivingHero(hero);
                if (manaTarget != null)
                    manaTarget.currentResource = Mathf.Min(manaTarget.maxResource, manaTarget.currentResource + skill.effectValue);
                break;

            case SkillEffectType.ReduceAllyNextSkillCost:
                var costTarget = GetRandomOtherLivingHero(hero);
                if (costTarget != null)
                    costTarget.costReductionPercent = skill.shieldPercentOfMaxHP;
                break;

            case SkillEffectType.CopyAllyLastSkill:
                var copyTarget = activeHeroes.FirstOrDefault(h => h != hero && h.currentHealth > 0 && h.lastUsedSkill != null);
                if (copyTarget != null && copyTarget.lastUsedSkill.effectType != SkillEffectType.CopyAllyLastSkill)
                    ApplySkillEffect(hero, copyTarget.lastUsedSkill);
                break;

            case SkillEffectType.BorrowAllyLegendarySkill:
                var legendaryDonor = activeHeroes.FirstOrDefault(h => h != hero && h.currentHealth > 0 && h.data.skills != null && h.data.skills.Length >= 4);
                if (legendaryDonor != null)
                {
                    hero.borrowedSkill = legendaryDonor.data.skills[3];
                    hero.borrowedSkillTurnsRemaining = skill.buffDurationTurns;
                }
                break;
        }
    }

    // Вспомогательный метод — найти состояние конкретного героя по его HeroData
    public HeroRuntimeState GetHeroState(HeroData data)
    {
        return activeHeroes.Find(h => h.data == data);
    }
}
