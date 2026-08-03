using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Battle/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string enemyId;

    [Header("Основные параметры (всё в одном блоке)")]
    public string enemyName;

    [Tooltip("ФОЛБЭК, не реальные боевые статы! Если эту EnemyData запускает нода карты мира с заданными " +
        "territory+nodeIndex (MapNodeData), BattleManager перезапишет это значение цифрой из EnemyStatCurve " +
        "для той территории/ноды — то, что здесь написано, используется только для debug-боёв без ноды " +
        "(случайный пул possibleEnemies и т.п.).")]
    public int maxHP = 80;
    public int maxMana = 50;            // про запас — если скиллы врага станут платными
    [Tooltip("ФОЛБЭК — см. подсказку у Max HP, тот же принцип (перезаписывается EnemyStatCurve, если есть нода).")]
    public int minAttack = 5;
    [Tooltip("ФОЛБЭК — см. подсказку у Max HP, тот же принцип (перезаписывается EnemyStatCurve, если есть нода).")]
    public int maxAttack = 12;
    public float damageMultiplier = 1f; // личный множитель урона этого врага
    public int price = 100;             // цена/сложность за разблокировку
    public EnemySkillData[] skills;     // количество умений = длина массива
    public EnemyPassiveData[] passives; // всегда активны, без хода — см. EnemyPassiveData

    [Header("Вредные фишки поля (спавнятся на старте боя)")]
    public HarmfulTileSpawnRule[] harmfulTileSpawns;

    [Header("Добыча за победу")]
    public LootReward loot = new LootReward();

    [Header("Визуал")]
    public Sprite portrait;
    public Color themeColor = Color.white;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(enemyId))
            enemyId = name;
    }
}
