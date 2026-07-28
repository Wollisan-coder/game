using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

    [Header("Награда за победу")]
    public int accountExperienceReward = 20; // плейсхолдер — легко поменять в инспекторе

    public System.Action OnStateChanged;
    public System.Action<string> OnBattleLog; // вызов с текстом строки при каждом нанесённом/полученном уроне

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

        currentEnemy = ResolveEnemy();
        if (currentEnemy != null)
        {
            enemyMaxHP = currentEnemy.maxHP;
            enemyMinAttack = currentEnemy.minAttack;
            enemyMaxAttack = currentEnemy.maxAttack;
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
            HeroOwnershipData ownership = null;
            if (HeroCollectionManager.Instance != null)
            {
                ownership = HeroCollectionManager.Instance.ownership.Find(o => o.heroId == hero.heroId);
                if (ownership != null) level = ownership.level;
            }

            var heroState = new HeroRuntimeState(hero, level);

            // Бонусы от экипированных предметов (не меняют сам ассет HeroData, только эту копию на бой)
            if (ownership != null && ItemCollectionManager.Instance != null)
            {
                foreach (var equipped in ownership.equippedItems)
                {
                    var equippedStack = ItemCollectionManager.Instance.GetStackByInstanceId(equipped.itemInstanceId);
                    if (equippedStack == null) continue;

                    var equippedItem = ItemCollectionManager.Instance.GetItemById(equippedStack.itemId);
                    if (equippedItem == null) continue;

                    float levelMultiplier = ItemCollectionManager.Instance.GetLevelMultiplierForLevel(equippedStack.level);
                    int bonusHealth = Mathf.RoundToInt(equippedItem.bonusHealth * levelMultiplier);
                    int bonusMana = Mathf.RoundToInt(equippedItem.bonusMana * levelMultiplier);

                    heroState.maxHealth += bonusHealth;
                    heroState.currentHealth += bonusHealth;
                    heroState.maxResource += bonusMana;
                    heroState.damageMultiplier += equippedItem.bonusDamageMultiplier * levelMultiplier;
                }
            }

            activeHeroes.Add(heroState);
        }
    }

        public void DealDamageToEnemy(int amount)
    {
        float multiplier = enemyIncomingDamageMultiplier * nextHitDamageMultiplier;
        int scaledAmount = Mathf.RoundToInt(amount * multiplier);
        nextHitDamageMultiplier = 1f; // метка слабости — одноразовая

        int absorbed = Mathf.Min(enemyShield, scaledAmount);
        enemyShield -= absorbed;
        int applied = scaledAmount - absorbed;
        enemyHP = Mathf.Max(0, enemyHP - applied);

        if (applied > 0)
            OnBattleLog?.Invoke($"Enemy took {applied} damage");
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
                DealDamageToEnemy(finalDamage);

                // Каждый живой активный герой этого цвета получает полную порцию ресурса
                // (кроме тех, кто только что использовал навык в этот ход — им мана не начисляется)
                foreach (var hero in activeHeroes)
                {
                    if (hero.currentHealth > 0 && hero.stunnedTurnsRemaining <= 0 && !hero.blockManaGainThisTurn && (int)hero.data.resourceType == type)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.maxResource);
                }
            }
        }

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

        OnStateChanged?.Invoke();

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

    public void DamageRandomHero(int amount)
    {
        ApplyDamageToHero(GetRandomAliveHero(), amount);
        OnStateChanged?.Invoke();
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

    public void StunRandomHero(int turns)
    {
        var hero = GetRandomAliveHero();
        if (hero != null) hero.stunnedTurnsRemaining = turns;
        OnStateChanged?.Invoke();
    }

    public void BlockRandomHeroSkill(int turns)
    {
        var hero = GetRandomAliveHero();
        if (hero != null) hero.skillBlockedTurnsRemaining = turns;
        OnStateChanged?.Invoke();
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
                ApplyDamageToHero(GetRandomAliveHero(), Mathf.RoundToInt(skill.effectValue * currentEnemy.damageMultiplier * enemyDamageMultiplier));
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
        float multiplier = (currentEnemy != null ? currentEnemy.damageMultiplier : 1f) * enemyDamageMultiplier;
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

        int absorbed = Mathf.Min(playerShield, rawDamage);
        playerShield -= absorbed;
        int applied = rawDamage - absorbed;
        hero.TakeDamage(applied);

        if (applied > 0)
        {
            OnBattleLog?.Invoke($"{hero.data.heroName} took {applied} damage");

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
        AccountManager.Instance?.GrantExperience(accountExperienceReward);
    }
    private void OnPlayerDefeated() => Debug.Log("Игрок проиграл бой.");

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
            if (enemyHP <= 0)
                OnEnemyDefeated();
            else
                StartCoroutine(EnemyTurnRoutine());
        }

        return true;
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
