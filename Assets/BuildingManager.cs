using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    [Header("Каталог зданий")]
    public BuildingData[] allBuildings;

    [Header("Состояние владения (заполняется при загрузке сохранения)")]
    public List<BuildingOwnershipData> ownership = new List<BuildingOwnershipData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public BuildingOwnershipData GetOwnership(string buildingId)
    {
        return ownership.FirstOrDefault(o => o.buildingId == buildingId);
    }

    public bool IsBuilt(string buildingId)
    {
        var data = GetOwnership(buildingId);
        return data != null && data.isBuilt;
    }

    public bool IsUnlocked(BuildingData building)
    {
        if (building == null) return false;

        switch (building.unlockType)
        {
            case BuildingUnlockType.TerritoryOpened:
                return WorldMapManager.Instance != null && WorldMapManager.Instance.IsTerritoryOpened(building.requiredTerritory);
            case BuildingUnlockType.TerritoryCompleted:
                return WorldMapManager.Instance != null && WorldMapManager.Instance.IsTerritoryCompleted(building.requiredTerritory);
            default:
                return AccountManager.Instance == null || AccountManager.Instance.level >= building.requiredAccountLevel;
        }
    }

    // Первое возведение здания — списывает ресурсы на постройку
    public bool Build(BuildingData building)
    {
        if (building == null || IsBuilt(building.buildingId)) return false;
        if (!IsUnlocked(building)) return false;
        if (PlayerCurrencies.Instance == null) return false;

        if (!PlayerCurrencies.Instance.Spend(CurrencyType.Wood, building.buildCostWood)) return false;
        if (!PlayerCurrencies.Instance.Spend(CurrencyType.Stone, building.buildCostStone))
        {
            PlayerCurrencies.Instance.Add(CurrencyType.Wood, building.buildCostWood); // возвращаем дерево — транзакционность
            return false;
        }

        var data = GetOwnership(building.buildingId);
        if (data == null)
        {
            data = new BuildingOwnershipData { buildingId = building.buildingId };
            ownership.Add(data);
        }

        data.isBuilt = true;
        data.level = 1;
        data.lastCollectedAtTicks = System.DateTime.UtcNow.Ticks;

        Save();
        return true;
    }

    public bool UpgradeBuilding(BuildingData building)
    {
        var data = GetOwnership(building.buildingId);
        if (data == null || !data.isBuilt || PlayerCurrencies.Instance == null) return false;
        if (data.level >= building.maxLevel) return false;

        CollectProduction(building); // забираем накопленное по старой мощности/складу, прежде чем они изменятся

        var (wood, stone) = building.GetUpgradeCost(data.level + 1);

        if (!PlayerCurrencies.Instance.Spend(CurrencyType.Wood, wood)) return false;
        if (!PlayerCurrencies.Instance.Spend(CurrencyType.Stone, stone))
        {
            PlayerCurrencies.Instance.Add(CurrencyType.Wood, wood);
            return false;
        }

        data.level++;
        Save();
        return true;
    }

    // Сколько продукции накоплено сейчас (без списания) — для UI. Ограничивается складом.
    public float GetPendingAmount(BuildingData building)
    {
        var data = GetOwnership(building.buildingId);
        if (data == null || !data.isBuilt) return 0f;

        double hoursElapsed = (System.DateTime.UtcNow.Ticks - data.lastCollectedAtTicks) / (double)System.TimeSpan.TicksPerHour;
        float produced = (float)(hoursElapsed * building.GetProductionPerHour(data.level));

        return Mathf.Clamp(produced, 0f, building.GetStorageCap(data.level));
    }

    // Забирает накопленную продукцию на баланс игрока. Возвращает сколько именно зачислено.
    public int CollectProduction(BuildingData building)
    {
        var data = GetOwnership(building.buildingId);
        if (data == null || !data.isBuilt || PlayerCurrencies.Instance == null) return 0;

        int amount = Mathf.FloorToInt(GetPendingAmount(building));

        if (amount > 0)
            PlayerCurrencies.Instance.Add(building.producedCurrency, amount);

        data.lastCollectedAtTicks = System.DateTime.UtcNow.Ticks;
        Save();

        return amount;
    }

    private void Save()
    {
        string serialized = string.Join(";", ownership.Select(o =>
            $"{o.buildingId}:{(o.isBuilt ? 1 : 0)}:{o.level}:{o.lastCollectedAtTicks}"));
        PlayerPrefs.SetString("building_ownership", serialized);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        ownership.Clear();

        string saved = PlayerPrefs.GetString("building_ownership", "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var entry in saved.Split(';'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 4) continue;

            ownership.Add(new BuildingOwnershipData
            {
                buildingId = parts[0],
                isBuilt = parts[1] == "1",
                level = int.Parse(parts[2], CultureInfo.InvariantCulture),
                lastCollectedAtTicks = long.Parse(parts[3], CultureInfo.InvariantCulture)
            });
        }
    }
}
