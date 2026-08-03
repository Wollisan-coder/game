using UnityEngine;

// В отличие от EnemySkillData (разовое действие вместо хода) — пассивка работает всегда,
// без хода: тикает каждый вражеский ход (Regen) или модифицирует урон при каждом попадании (остальные).
public enum EnemyPassiveEffectType
{
    Regen,           // лечит себя на % от maxHP в начале каждого своего хода
    DamageReduction, // снижает весь входящий урон на % (применяется в BattleManager.DealDamageToEnemy)
    Reflect,         // отражает % полученного урона случайному живому герою
    ColorAbsorb      // если в отряде есть живой герой цвета absorbColorType — собственный урон врага повышен на %
}

[CreateAssetMenu(fileName = "NewEnemyPassive", menuName = "Battle/Enemy Passive")]
public class EnemyPassiveData : ScriptableObject
{
    public string passiveName;
    [TextArea] public string description;

    public EnemyPassiveEffectType effectType;

    [Header("Regen / DamageReduction / Reflect — доля 0..1")]
    [Range(0f, 1f)] public float percent = 0.1f;

    [Header("ColorAbsorb — тот же индекс цвета, что и HeroData.resourceType/тип гемма (0-4)")]
    public int absorbColorType;
    [Range(0f, 3f)] public float absorbDamageBonus = 0.5f; // +50% к урону врага, пока в отряде жив герой этого цвета
}
