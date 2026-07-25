using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Battle/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string enemyId;

    [Header("Основні параметри (все в одному блоці)")]
    public string enemyName;
    public int maxHP = 80;
    public int maxMana = 50;            // про запас — якщо скіли ворога стануть коштовними
    public int minAttack = 5;
    public int maxAttack = 12;
    public float damageMultiplier = 1f; // особистий множник урону цього ворога
    public int price = 100;             // ціна/складність за розблокування
    public EnemySkillData[] skills;     // кількість умінь = довжина масиву

    [Header("Візуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(enemyId))
            enemyId = name;
    }
}