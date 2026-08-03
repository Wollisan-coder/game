using UnityEngine;

// Универсальный набор скиллов босса (игрока) в режиме Boss Training — один общий кит на всех
// тренируемых героев (не по расам, см. project_boss_training_mechanic_concept). Игрок не трогает
// доску сам — доской играет ИИ (тренируемый герой), игрок только применяет эти защитные скиллы.
public enum BossTrainingSkillEffectType
{
    Shield,          // щит боссу — % от собственного максимального HP
    Heal,            // лечит боссу % от максимального HP
    WeakenAIDamage,  // снижает урон ИИ-героя от геммов на N% на M ходов (тот же heroDamageMultiplier, что и у врагов)
    ReduceIncoming,  // снижает весь входящий урон боссу на N% на M ходов
    CastTrap         // ставит вредную клетку на случайную свободную клетку поля ИИ-героя
}

[CreateAssetMenu(fileName = "NewBossTrainingSkill", menuName = "Battle/Boss Training Skill")]
public class BossTrainingSkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    public BossTrainingSkillEffectType effectType;

    [Header("Shield / Heal — доля от максимального HP босса (0..1)")]
    [Range(0f, 1f)] public float percentOfMaxHP = 0.2f;

    [Header("WeakenAIDamage / ReduceIncoming — доля снижения (0..1) и длительность в ходах")]
    [Range(0f, 1f)] public float reductionPercent = 0.3f;
    public int durationTurns = 2;

    [Header("CastTrap — какую вредную клетку ставить и её value (см. HarmfulTileSpawnRule)")]
    public HarmfulTileType trapType = HarmfulTileType.Ice;
    public int trapValue = 2;

    [Header("Стоимость — сколько раз можно использовать за одну тренировку (0 = без лимита)")]
    public int usesPerTraining = 3;
}
