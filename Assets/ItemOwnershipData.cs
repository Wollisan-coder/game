[System.Serializable]
public class ItemOwnershipData
{
    public string instanceId; // унікальний ID конкретного стека — itemId сам по собі більше НЕ унікальний,
                               // бо один і той самий предмет може існувати кількома стеками різного рівня
    public string itemId;
    public int level = 1;
    public int experience = 0; // накопичений досвід у межах поточного рівня
    public int quantity = 1;   // скільки однакових копій (той самий itemId І рівень) зараз у гравця в цьому стеку
}
