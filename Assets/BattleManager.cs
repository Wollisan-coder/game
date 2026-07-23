using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Герої в бою")]
    public HeroData[] heroRoster; // призначити в Inspector усіх героїв, що беруть участь у бою
    public List<HeroRuntimeState> activeHeroes = new List<HeroRuntimeState>();

    [Header("Урон за фішку (Red/Blue/Green/Yellow/Violet)")]
    public int[] damagePerGem = { 4, 4, 4, 4, 4 };

    [Header("Лікування за фішку Pink")]
    public int pinkHealPerGem = 3;

    [Header("Тимчасовий баф урона")]
    public float damageMultiplier = 1f;
    public int damageMultiplierTurnsRemaining = 0;

    [Header("Посилання на сітку")]
    public GridManager gridManager;

    public System.Action OnStateChanged;

    private void Awake()
    {
        playerHP = playerMaxHP;
        enemyHP = enemyMaxHP;

        activeHeroes.Clear();
        foreach (var hero in heroRoster)
        {
            if (hero != null)
                activeHeroes.Add(new HeroRuntimeState(hero));
        }
    }

    public void ResolvePlayerTurn(Dictionary<int, int> matchedTypeCounts)
    {
        foreach (var pair in matchedTypeCounts)
        {
            int type = pair.Key;
            int count = pair.Value;

            if (type == 5) // Pink — лікування + мана всім героям 0-4
            {
                Heal(count * pinkHealPerGem);

                foreach (var hero in activeHeroes)
                {
                    if ((int)hero.data.resourceType <= 4)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.data.maxResource);
                }
            }
            else if (type >= 0 && type <= 4) // Red/Blue/Green/Yellow/Violet
            {
                int baseDamage = count * damagePerGem[type];
                int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);
                DealDamageToEnemy(finalDamage);

                // Кожен активний герой цього кольору отримує повну порцію ресурсу
                foreach (var hero in activeHeroes)
                {
                    if ((int)hero.data.resourceType == type)
                        hero.currentResource = Mathf.Min(hero.currentResource + count, hero.data.maxResource);
                }
            }
        }

        if (damageMultiplierTurnsRemaining > 0)
        {
            damageMultiplierTurnsRemaining--;
            if (damageMultiplierTurnsRemaining <= 0)
                damageMultiplier = 1f;
        }

        OnStateChanged?.Invoke();

        if (enemyHP > 0)
            StartCoroutine(EnemyTurnRoutine());
        else
            OnEnemyDefeated();
    }

    public void DealDamageToEnemy(int amount) => enemyHP = Mathf.Max(0, enemyHP - amount);
    public void Heal(int amount) => playerHP = Mathf.Min(playerMaxHP, playerHP + amount);
    public void AddShield(int amount) => playerShield += amount;

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        int rawDamage = Random.Range(enemyMinAttack, enemyMaxAttack + 1);
        int absorbed = Mathf.Min(playerShield, rawDamage);
        playerShield -= absorbed;
        playerHP = Mathf.Max(0, playerHP - (rawDamage - absorbed));

        OnStateChanged?.Invoke();

        if (playerHP <= 0)
            OnPlayerDefeated();
    }

    private void OnEnemyDefeated() => Debug.Log("Ворог переможений!");
    private void OnPlayerDefeated() => Debug.Log("Гравець програв бій.");

    // Тепер прив'язано до конкретного героя, а не до глобального ресурсу
    public bool TryUseSkill(HeroRuntimeState hero, SkillData skill)
    {
        if (hero.currentResource < skill.cost)
            return false;

        hero.currentResource -= skill.cost;

        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                DealDamageToEnemy(Mathf.RoundToInt(skill.effectValue * damageMultiplier));
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