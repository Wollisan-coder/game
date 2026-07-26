using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BattleManager : MonoBehaviour
{
    [Header("Гравець")]
    public int playerMaxHP = 100;
    public int playerHP;
    public int playerShield = 0;

    [Header("Ворог")]
    public int enemyMaxHP = 80;
    public int enemyHP;
    public int enemyMinAttack = 5;
    public int enemyMaxAttack = 12;
    public int enemyShield = 0;

    [Header("Дебафф урону героїв від скіла ворога")]
    public float heroDamageMultiplier = 1f;
    public int heroDamageMultiplierTurnsRemaining = 0;

    [Header("Герої в бою")]
    public HeroData[] heroRoster; // призначити в Inspector усіх героїв, що беруть участь у бою
    public List<HeroRuntimeState> activeHeroes = new List<HeroRuntimeState>();

    [Header("Урон за фішку (базовий — якщо героя цього кольору немає або він загинув)")]
    public int[] baseDamagePerGem = { 5, 5, 5, 5, 5 };

    [Header("Урон за фішку (якщо герой цього кольору живий)")]
    public int[] aliveDamagePerGem = { 8, 8, 8, 8, 8 };

    [Header("Лікування за фішку Pink")]
    public int pinkHealPerGem = 3;

    [Header("Тимчасовий баф урона")]
    public float damageMultiplier = 1f;
    public int damageMultiplierTurnsRemaining = 0;

    [Header("Посилання на сітку")]
    public GridManager gridManager;

    [Header("Вороги (для цієї сутички)")]
    public EnemyData[] possibleEnemies;   // наперед заданий пул для випадкового бою
    public bool forceRandomEnemy = false; // true = ігнорувати вибір з колекції, завжди рандом
    public EnemyData currentEnemy;        // фактично обраний ворог цього бою

    [Header("Нагорода за перемогу")]
    public int accountExperienceReward = 20; // плейсхолдер — легко змінити в інспекторі

    public System.Action OnStateChanged;

    private HeroRuntimeState lastAttackedHero;
    private int consecutiveHitsOnLastHero;

        private EnemyData ResolveEnemy()
    {
        // 1. Конкретний ворог, обраний гравцем у колекції (якщо не форсуємо рандом)
        if (!forceRandomEnemy &&
            EnemyCollectionManager.Instance != null &&
            EnemyCollectionManager.Instance.selectedEnemy != null)
        {
            return EnemyCollectionManager.Instance.selectedEnemy;
        }

        // 2. Інакше — випадковий з наперед заданого масиву
        if (possibleEnemies != null && possibleEnemies.Length > 0)
            return possibleEnemies[Random.Range(0, possibleEnemies.Length)];

        return null; // жодного джерела — залишаються значення з інспектора
    }


            private void Awake()
    {
        playerHP = playerMaxHP;

        currentEnemy = ResolveEnemy();
        if (currentEnemy != null)
        {
            enemyMaxHP = currentEnemy.maxHP;
            enemyMinAttack = currentEnemy.minAttack;
            enemyMaxAttack = currentEnemy.maxAttack;
        }
        enemyHP = enemyMaxHP;

        // Беремо тільки реально вибраних героїв (без порожніх слотів null)
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

            // Бонуси від екіпірованих предметів (не змінюють сам ассет HeroData, лише цю копію на бій)
            if (ownership != null && ItemCollectionManager.Instance != null)
            {
                foreach (var equipped in ownership.equippedItems)
                {
                    var equippedItem = ItemCollectionManager.Instance.GetItemById(equipped.itemId);
                    if (equippedItem == null) continue;

                    float levelMultiplier = ItemCollectionManager.Instance.GetLevelMultiplier(equipped.itemId);
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
        int absorbed = Mathf.Min(enemyShield, amount);
        enemyShield -= absorbed;
        enemyHP = Mathf.Max(0, enemyHP - (amount - absorbed));
    }

    // Базовий урон, якщо живого героя цього кольору немає; підвищений — якщо є
    private int GetDamagePerGem(int type)
    {
        bool hasAliveHeroOfColor = activeHeroes.Any(h => h.currentHealth > 0 && (int)h.data.resourceType == type);
        return hasAliveHeroOfColor ? aliveDamagePerGem[type] : baseDamagePerGem[type];
    }

    public void ResolvePlayerTurn(Dictionary<int, int> matchedTypeCounts)
    {
        foreach (var pair in matchedTypeCounts)
        {
            int type = pair.Key;
            int count = pair.Value;

            if (type == 5) // Pink — лікування + мана всім живим героям 0-4
            {
                Heal(count * pinkHealPerGem);

                foreach (var hero in activeHeroes)
                {
                    if (hero.currentHealth > 0 && !hero.blockManaGainThisTurn && (int)hero.data.resourceType <= 4)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.maxResource);
                }
            }
            else if (type >= 0 && type <= 4) // Red/Blue/Green/Yellow/Violet
            {
                int baseDamage = count * GetDamagePerGem(type);
                int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier * heroDamageMultiplier);
                DealDamageToEnemy(finalDamage);

                // Кожен живий активний герой цього кольору отримує повну порцію ресурсу
                // (окрім тих, хто щойно використав навичку цього ходу — їм мана не нараховується)
                foreach (var hero in activeHeroes)
                {
                    if (hero.currentHealth > 0 && !hero.blockManaGainThisTurn && (int)hero.data.resourceType == type)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.maxResource);
                }
            }
        }

        // Блокування мани діяло рівно один хід — знімаємо його для наступного
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

        OnStateChanged?.Invoke();

        if (enemyHP > 0)
            StartCoroutine(EnemyTurnRoutine());
        else
            OnEnemyDefeated();
    }

    public void Heal(int amount) => playerHP = Mathf.Min(playerMaxHP, playerHP + amount);
    public void AddShield(int amount) => playerShield += amount;

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        EnemySkillData skill = PickEnemySkill();
        if (skill != null)
            UseEnemySkill(skill);
        else
            BasicEnemyAttack(); // якщо скіли не задані — стара проста атака

        // Щит діє від активації до наступного ходу гравця — після ходу ворога знімається
        playerShield = 0;

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
                // Скіл б'є по випадковому живому герою, але НЕ враховується у забороні "3 рази поспіль"
                ApplyDamageToHero(GetRandomAliveHero(), Mathf.RoundToInt(skill.effectValue * currentEnemy.damageMultiplier));
                break;

            case EnemySkillEffectType.ShieldSelf:
                enemyShield += Mathf.RoundToInt(enemyMaxHP * skill.shieldPercentOfMaxHP);
                break;

            case EnemySkillEffectType.WeakenHeroes:
                heroDamageMultiplier = 1f - skill.damageReductionPercent;
                heroDamageMultiplierTurnsRemaining = skill.debuffDurationTurns;
                break;
        }
    }

    private void BasicEnemyAttack()
    {
        int rawDamage = Random.Range(enemyMinAttack, enemyMaxAttack + 1);
        float multiplier = currentEnemy != null ? currentEnemy.damageMultiplier : 1f;
        int finalDamage = Mathf.RoundToInt(rawDamage * multiplier);

        ApplyDamageToHero(PickBasicAttackTarget(), finalDamage);
    }

    // Випадковий живий герой; exclude дозволяє прибрати конкретного героя з вибірки
    private HeroRuntimeState GetRandomAliveHero(HeroRuntimeState exclude = null)
    {
        var candidates = activeHeroes.Where(h => h.currentHealth > 0 && h != exclude).ToList();

        if (candidates.Count == 0)
            candidates = activeHeroes.Where(h => h.currentHealth > 0).ToList();

        if (candidates.Count == 0) return null; // усі герої загинули

        return candidates[Random.Range(0, candidates.Count)];
    }

    // Звичайна атака ворога не може влучити в одного героя 3 рази поспіль
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
        if (hero == null) return; // усі герої загинули — атакувати нікого

        int absorbed = Mathf.Min(playerShield, rawDamage);
        playerShield -= absorbed;
        hero.TakeDamage(rawDamage - absorbed);

        if (hero.currentHealth <= 0)
            OnHeroDefeated(hero);
    }

    private void OnHeroDefeated(HeroRuntimeState hero)
    {
        Debug.Log($"Герой {hero.data.heroName} загинув!");

        if (activeHeroes.All(h => h.currentHealth <= 0))
            OnPlayerDefeated();
    }

    private void OnEnemyDefeated()
    {
        Debug.Log("Ворог переможений!");
        AccountManager.Instance?.GrantExperience(accountExperienceReward);
    }
    private void OnPlayerDefeated() => Debug.Log("Гравець програв бій.");

    // Тепер прив'язано до конкретного героя, а не до глобального ресурсу
    public bool TryUseSkill(HeroRuntimeState hero, SkillData skill)
    {
        if (hero.currentHealth <= 0)
            return false;

        if (hero.currentResource < skill.cost)
            return false;

        hero.currentResource -= skill.cost;
        hero.blockManaGainThisTurn = true; // цього героя не можна поповнити маною на цьому ході

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
                int shieldAmount = Mathf.RoundToInt(playerMaxHP * skill.shieldPercentOfMaxHP);
                AddShield(shieldAmount);
                break;

            case SkillEffectType.DamageBuffTurns:
                damageMultiplier = skill.damageMultiplier;
                damageMultiplierTurnsRemaining = skill.buffDurationTurns;
                break;
        }

        OnStateChanged?.Invoke();

        if (skill.effectType != SkillEffectType.ConvertAndDestroyRed &&
            skill.effectType != SkillEffectType.DestroyRows)
        {
            if (enemyHP <= 0)
                OnEnemyDefeated();
            else
                StartCoroutine(EnemyTurnRoutine());
        }

        return true;
    }

    // Допоміжний метод — знайти стан конкретного героя за його HeroData
    public HeroRuntimeState GetHeroState(HeroData data)
    {
        return activeHeroes.Find(h => h.data == data);
    }
}