using UnityEngine;

public enum SkillEffectType { Damage, Heal, Shield, ConvertAndDestroyRed }
public enum ResourceType { Red = 0, Blue = 1, Green = 2, Yellow = 3, Violet = 4, Pink = 5 }

[CreateAssetMenu(fileName = "NewSkill", menuName = "Battle/Skill")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    public ResourceType costType;
    public int cost;

    public SkillEffectType effectType;
    public int effectValue; // для ConvertAndDestroyRed — сколько фишек конвертувати (наприклад 5)
}