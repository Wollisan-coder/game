using UnityEngine;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "Castle/Building")]
public class BuildingData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string buildingId;

    [Header("Основные параметры")]
    public string buildingName;
    public BuildingType buildingType;
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Разблокировка")]
    public int requiredAccountLevel = 1;

    [Header("Максимальный уровень здания (апгрейд прекращается)")]
    public int maxLevel = 100;

    [Header("Постройка (первое возведение)")]
    public int buildCostWood = 0;
    public int buildCostStone = 0;

    [Header("Производство (только для *Production)")]
    public CurrencyType producedCurrency = CurrencyType.Wood;
    public float baseProductionPerHour = 10f;
    public int baseStorageCap = 100;

    [Header("Призыв (только для Forge/Altar)")]
    public HeroSummonPoolData heroSummonPool; // для Altar
    public ItemSummonPoolData itemSummonPool; // для Forge

    [Header("Вместимость отряда (только для SquadCapacity)")]
    public int baseSquadWeightBonus = 1; // сколько дополнительного веса отряда даёт каждый уровень (количество слотов всегда фиксировано — 4)

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(buildingId))
            buildingId = name;
    }

    // Продукция в час на указанном уровне (+20% за каждый уровень сверх 1-го) — плейсхолдер, легко поменять
    public float GetProductionPerHour(int level)
    {
        int lvl = Mathf.Max(1, level);
        return baseProductionPerHour * (1f + 0.2f * (lvl - 1));
    }

    public int GetStorageCap(int level)
    {
        int lvl = Mathf.Max(1, level);
        return Mathf.RoundToInt(baseStorageCap * (1f + 0.3f * (lvl - 1)));
    }

    // Стоимость апгрейда до указанного целевого уровня
    public (int wood, int stone) GetUpgradeCost(int targetLevel)
    {
        int wood = Mathf.RoundToInt(buildCostWood * Mathf.Pow(targetLevel, 1.5f));
        int stone = Mathf.RoundToInt(buildCostStone * Mathf.Pow(targetLevel, 1.5f));
        return (wood, stone);
    }

    // Дополнительный вес отряда на указанном уровне (0, если ещё не построено)
    public int GetSquadWeightBonus(int level) => baseSquadWeightBonus * Mathf.Max(0, level);
}
