using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Battle/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string enemyId;

    [Header("Основна інформація")]
    public string enemyName;



    [Header("Візуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;

    [Header("Бойові характеристики")]
    public int maxHP = 80;
    public int minAttack = 5;
    public int maxAttack = 12;

        [Header("Уміння ворога")]
    public EnemySkillData[] skills;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(enemyId))
            enemyId = name;
    }
}