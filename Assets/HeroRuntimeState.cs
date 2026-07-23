[System.Serializable]
public class HeroRuntimeState
{
    public HeroData data;
    public int currentResource;

    public HeroRuntimeState(HeroData heroData)
    {
        data = heroData;
        currentResource = 0;
    }
}