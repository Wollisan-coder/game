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
                // Fail-closed, как и ветки выше (TerritoryOpened/Completed) — раньше отсутствующий
                // AccountManager.Instance считался "разблокировано", в отличие от остальных веток.
                return AccountManager.Instance != null && AccountManager.Instance.level >= building.requiredAccountLevel;
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
        if (data.level >= GetEffectiveMaxLevel(building)) return false;

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
        AchievementManager.Instance?.ReportBuildingUpgraded();
        return true;
    }

    // Обычно просто building.maxLevel — но для TerritoryMine_<Race>_<Resource> (см. Assets/TerritoryMines/,
    // project_territory_mines) он захардкожен в 1 на самом ассете (общие данные, мутировать напрямую
    // нельзя — не переживёт перезапуск, в отличие от per-игрок ownership) и поднимается ЗДЕСЬ во время
    // выполнения, как только игрок закрывает все 3 расовых квеста этой расы (см. RaceQuestManager).
    public int GetEffectiveMaxLevel(BuildingData building)
    {
        if (building != null && building.buildingId.StartsWith("TerritoryMine_")
            && RaceQuestManager.Instance != null && RaceQuestManager.Instance.IsRaceQuestsComplete(building.requiredTerritory))
        {
            return Mathf.Max(building.maxLevel, RaceQuestManager.UnlockedMineMaxLevel);
        }

        return building.maxLevel;
    }

    // TerritoryMine_<Race>_<Resource> (см. Assets/TerritoryMines/, project_territory_mines) — если для
    // этой расы сейчас активна угроза шахтам (см. MineThreatManager, project_death_dungeon_concept piece 5),
    // все 3 её здания блокируются целиком: не копят и не выдают продукцию, пока угроза не будет побеждена.
    private bool IsBlockedByMineThreat(BuildingData building)
    {
        if (building == null || MineThreatManager.Instance == null) return false;

        string[] parts = building.buildingId.Split('_');
        if (parts.Length != 3 || parts[0] != "TerritoryMine") return false;
        if (!System.Enum.TryParse(parts[1], out Race race)) return false;

        return MineThreatManager.Instance.IsRaceBlocked(race);
    }

    // Сколько продукции накоплено сейчас (без списания) — для UI. Ограничивается складом.
    public float GetPendingAmount(BuildingData building)
    {
        var data = GetOwnership(building.buildingId);
        if (data == null || !data.isBuilt) return 0f;

        if (IsBlockedByMineThreat(building))
        {
            data.lastCollectedAtTicks = System.DateTime.UtcNow.Ticks; // простой не копится молча
            return 0f;
        }

        double hoursElapsed = (System.DateTime.UtcNow.Ticks - data.lastCollectedAtTicks) / (double)System.TimeSpan.TicksPerHour;
        float produced = (float)(hoursElapsed * building.GetProductionPerHour(data.level));

        return Mathf.Clamp(produced, 0f, building.GetStorageCap(data.level));
    }

    // Забирает накопленную продукцию на баланс игрока. Возвращает сколько именно зачислено.
    public int CollectProduction(BuildingData building)
    {
        var data = GetOwnership(building.buildingId);
        if (data == null || !data.isBuilt || PlayerCurrencies.Instance == null) return 0;

        if (IsBlockedByMineThreat(building))
        {
            data.lastCollectedAtTicks = System.DateTime.UtcNow.Ticks;
            Save();
            return 0;
        }

        float rate = building.GetProductionPerHour(data.level);
        float storageCap = building.GetStorageCap(data.level);

        double hoursElapsed = (System.DateTime.UtcNow.Ticks - data.lastCollectedAtTicks) / (double)System.TimeSpan.TicksPerHour;
        float producedRaw = (float)(hoursElapsed * rate);
        bool cappedByStorage = producedRaw > storageCap;
        int amount = Mathf.FloorToInt(Mathf.Clamp(producedRaw, 0f, storageCap));

        if (amount > 0)
            PlayerCurrencies.Instance.Add(building.producedCurrency, amount);

        if (cappedByStorage || rate <= 0f)
        {
            // Копилось дольше, чем вмещает склад (или производство сейчас нулевое) — лишнее время
            // законно "сгорает", сброс отметки на текущий момент здесь корректен.
            data.lastCollectedAtTicks = System.DateTime.UtcNow.Ticks;
        }
        else
        {
            // Переносим остаток времени, не успевший накопить целую единицу, на следующий сбор — без
            // "дрейфа" к нулю при частых сборах (тот же приём, что и
            // AccountManager.RegenerateEnergyFromElapsedTime.consumedTicks).
            long consumedTicks = (long)(amount / rate * System.TimeSpan.TicksPerHour);
            data.lastCollectedAtTicks += consumedTicks;
        }

        Save();

        return amount;
    }

    // Хотя бы у одной построенной шахты территории (TerritoryMine_*) склад уже заполнен под завязку —
    // используется бейджем-уведомлением у кнопки Mines (см. MainMenuUI), чтобы игрок не терял продукцию,
    // простаивающую на капе.
    public bool HasAnyMineAtCap()
    {
        foreach (var building in allBuildings)
        {
            if (building == null || !building.buildingId.StartsWith("TerritoryMine_")) continue;
            if (!IsBuilt(building.buildingId)) continue;

            var data = GetOwnership(building.buildingId);
            if (data == null) continue;

            if (GetPendingAmount(building) >= building.GetStorageCap(data.level)) return true;
        }
        return false;
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
