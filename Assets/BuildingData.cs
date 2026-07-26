using UnityEngine;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "Castle/Building")]
public class BuildingData : ScriptableObject
{
    [Header("Ідентифікатор (не змінювати після релізу!)")]
    public string buildingId;

    [Header("Основні параметри")]
    public string buildingName;
    public BuildingType buildingType;
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Розблокування")]
    public int requiredAccountLevel = 1;

    [Header("Максимальний рівень будівлі (апгрейд припиняється)")]
    public int maxLevel = 100;

    [Header("Постройка (перше спорудження)")]
    public int buildCostWood = 0;
    public int buildCostStone = 0;

    [Header("Виробництво (лише для *Production)")]
    public CurrencyType producedCurrency = CurrencyType.Wood;
    public float baseProductionPerHour = 10f;
    public int baseStorageCap = 100;

    [Header("Призов (лише для Forge/Altar)")]
    public HeroSummonPoolData heroSummonPool; // для Altar
    public ItemSummonPoolData itemSummonPool; // для Forge

    [Header("Місткість загону (лише для SquadCapacity)")]
    public int baseSquadCapacityBonus = 1; // скільки додаткових слотів дає кожен рівень

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(buildingId))
            buildingId = name;
    }

    // Продукція за годину на вказаному рівні (+20% за кожен рівень понад 1-й) — плейсхолдер, легко змінити
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

    // Вартість апгрейду на вказаний цільовий рівень
    public (int wood, int stone) GetUpgradeCost(int targetLevel)
    {
        int wood = Mathf.RoundToInt(buildCostWood * Mathf.Pow(targetLevel, 1.5f));
        int stone = Mathf.RoundToInt(buildCostStone * Mathf.Pow(targetLevel, 1.5f));
        return (wood, stone);
    }

    // Додаткові слоти загону на вказаному рівні (0, якщо ще не збудовано)
    public int GetSquadCapacityBonus(int level) => baseSquadCapacityBonus * Mathf.Max(0, level);
}
