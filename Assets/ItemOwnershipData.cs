[System.Serializable]
public class ItemOwnershipData
{
    public string itemId;
    public int level = 1;
    public int experience = 0; // накопичений досвід у межах поточного рівня
    public int quantity = 1;   // скільки однакових копій цього предмета зараз у гравця (для стекування в UI)
}
