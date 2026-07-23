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

    [Header("Ресурси (мана для навичок)")]
    // Індекси: 0=Red,1=Blue,2=Green,3=Yellow,4=Violet,5=Pink
    public int[] resources = new int[6];
    public int[] maxResources = { 50, 50, 50, 50, 50, 50 };

    [Header("Урон за фішку (Red/Blue/Green/Yellow/Violet)")]
    public int[] damagePerGem = { 4, 4, 4, 4, 4 }; // індекси 0-4, відповідають кольорам пошкодження

    [Header("Лікування за фішку Pink")]
    public int pinkHealPerGem = 3;

    [Header("Посилання на сітку")]
    public GridManager gridManager;

    public System.Action OnStateChanged;

    private void Awake()
    {
        playerHP = playerMaxHP;
        enemyHP = enemyMaxHP;
    }

    // Викликається з GridManager, коли всі каскади ходу гравця завершились
    public void ResolvePlayerTurn(Dictionary<int, int> matchedTypeCounts)
    {
        foreach (var pair in matchedTypeCounts)
        {
            int type = pair.Key;
            int count = pair.Value;

            if (type == 5) // Pink — лікування + мана всім кольорам пошкодження
            {
                Heal(count * pinkHealPerGem);

                for (int i = 0; i < 5; i++)
                    resources[i] = Mathf.Min(resources[i] + count, maxResources[i]);
            }
            else if (type >= 0 && type <= 4) // Red/Blue/Green/Yellow/Violet — урон + мана свого кольору
            {
                DealDamageToEnemy(count * damagePerGem[type]);
                resources[type] = Mathf.Min(resources[type] + count, maxResources[type]);
            }
        }

        OnStateChanged?.Invoke();

        if (enemyHP > 0)
            StartCoroutine(EnemyTurnRoutine());
        else
            OnEnemyDefeated();
    }

    public void DealDamageToEnemy(int amount) => enemyHP = Mathf.Max(0, enemyHP - amount);
    public void Heal(int amount) => playerHP = Mathf.Min(playerMaxHP, playerHP + amount);
    public void AddShield(int amount) => playerShield += amount; // тепер тільки з навичок

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

    public bool TryUseSkill(SkillData skill)
    {
        if (resources[(int)skill.costType] < skill.cost)
            return false;

        resources[(int)skill.costType] -= skill.cost;

        switch (skill.effectType)
        {
            case SkillEffectType.Damage: DealDamageToEnemy(skill.effectValue); break;
            case SkillEffectType.Heal: Heal(skill.effectValue); break;
            case SkillEffectType.Shield: AddShield(skill.effectValue); break;
            case SkillEffectType.ConvertAndDestroyRed:
                StartCoroutine(gridManager.ExecuteConvertAndDestroySkill(skill.effectValue));
                break;
        }

        OnStateChanged?.Invoke();

        if (skill.effectType != SkillEffectType.ConvertAndDestroyRed)
        {
            if (enemyHP <= 0)
                OnEnemyDefeated();
            else
                StartCoroutine(EnemyTurnRoutine());
        }

        return true;
    }
}