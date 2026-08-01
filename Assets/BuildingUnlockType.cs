// Чем гейтится разблокировка здания — см. BuildingManager.IsUnlocked.
public enum BuildingUnlockType
{
    AccountLevel,        // BuildingData.requiredAccountLevel
    TerritoryOpened,      // первая нода BuildingData.requiredTerritory уже открыта (WorldMapManager.IsUnlocked)
    TerritoryCompleted    // босс (нода 18) BuildingData.requiredTerritory уже пройден
}
