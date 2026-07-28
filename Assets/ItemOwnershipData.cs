[System.Serializable]
public class ItemOwnershipData
{
    public string instanceId; // уникальный ID конкретного стека — itemId сам по себе больше НЕ уникален,
                               // потому что один и тот же предмет может существовать несколькими стеками разного уровня
    public string itemId;
    public int level = 1;
    public int experience = 0; // накопленный опыт в пределах текущего уровня
    public int quantity = 1;   // сколько одинаковых копий (тот же itemId И уровень) сейчас у игрока в этом стеке
}
