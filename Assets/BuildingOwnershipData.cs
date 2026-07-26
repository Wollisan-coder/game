[System.Serializable]
public class BuildingOwnershipData
{
    public string buildingId;
    public bool isBuilt;
    public int level = 1;
    public long lastCollectedAtTicks; // DateTime.UtcNow.Ticks на момент останнього збору продукції
}
