using UnityEngine;

public enum SkillEffectType
{
    Damage,
    Heal,
    Shield,
    ConvertAndDestroyRed,
    DestroyRows,       // 1. Знищення рядів
    ShieldPercent,     // 2. Щит у % від максимального HP
    DamageBuffTurns    // 3. Множник урону на N ходів
}

public enum ResourceType { Red = 0, Blue = 1, Green = 2, Yellow = 3, Violet = 4, Pink = 5 }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    public ResourceType costType;
    public int cost;

    public SkillEffectType effectType;
    public int effectValue; // загальне значення (урон/лікування/щит/тощо, залежно від типу)

    [Header("Параметри для DestroyRows")]
    public int rowStart; // перший ряд (0-based індекс, наприклад 1 = другий ряд)
    public int rowEnd;   // останній ряд включно (наприклад 5 = шостий ряд)

    [Header("Параметри для ShieldPercent")]
    [Range(0f, 1f)] public float shieldPercentOfMaxHP = 0.2f; // 20% за замовчуванням

    [Header("Параметри для DamageBuffTurns")]
    public float damageMultiplier = 2f;
    public int buffDurationTurns = 2;
}