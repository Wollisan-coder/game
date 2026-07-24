using UnityEngine;

public enum EnemySkillEffectType
{
    Damage,       // урон гравцю
    ShieldSelf,   // щит ворогу — % від власного максимального HP
    WeakenHeroes  // зниження урону героїв на N% на M ходів
}

[CreateAssetMenu(fileName = "NewEnemySkill", menuName = "Battle/Enemy Skill")]
public class EnemySkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    public EnemySkillEffectType effectType;
    public int effectValue; // урон, для Damage

    [Header("Параметри для ShieldSelf")]
    [Range(0f, 1f)] public float shieldPercentOfMaxHP = 0.2f;

    [Header("Параметри для WeakenHeroes")]
    [Range(0f, 1f)] public float damageReductionPercent = 0.5f;
    public int debuffDurationTurns = 1;

    [Header("Вага для випадкового вибору ворогом")]
    public float weight = 1f;
}