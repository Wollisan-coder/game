using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Battle/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string enemyId;

    [Header("Основные параметры (всё в одном блоке)")]
    public string enemyName;
    public int maxHP = 80;
    public int maxMana = 50;            // про запас — если скиллы врага станут платными
    public int minAttack = 5;
    public int maxAttack = 12;
    public float damageMultiplier = 1f; // личный множитель урона этого врага
    public int price = 100;             // цена/сложность за разблокировку
    public EnemySkillData[] skills;     // количество умений = длина массива

    [Header("Визуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(enemyId))
            enemyId = name;
    }
}
