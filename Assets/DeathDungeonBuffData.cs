using UnityEngine;

// Временные баффы забега Death Dungeon (см. project_death_dungeon_concept) — действуют только до конца
// текущего забега, не сохраняются. Маленький авторский пул (~5-6 штук на первый заход, легко дополнить
// позже) вместо большого набора — тот же принцип "маленький пул + переиспользование", что и у остального
// проекта. value трактуется по-разному в зависимости от type — см. комментарии на самих значениях enum.
public enum DeathDungeonBuffType
{
    DamageBoost,        // +value урона отряда (доля, 0.15 = +15%) весь остаток забега — см. BattleManager.damageMultiplier
    ExtraShield,        // щит в начале КАЖДОГО боя = value от суммарного макс. HP отряда (доля)
    ExtraMana,          // +value маны всем героям в начале каждого боя (целое число)
    HealEachBattle,      // лечить отряд на value от макс. HP в начале каждого боя (доля)
    ExtraGambleUse,      // +value доп. использований гамбл-колеса за бой (целое число)
    ReducedEnemyDamage,  // урон врага снижен на value весь остаток забега (доля)
}

[CreateAssetMenu(fileName = "NewDeathDungeonBuff", menuName = "Death Dungeon/Buff")]
public class DeathDungeonBuffData : ScriptableObject
{
    public string buffName;
    [TextArea] public string description;
    public DeathDungeonBuffType type;
    public float value;
}
