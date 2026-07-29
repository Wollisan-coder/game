public enum HarmfulTileType
{
    None,
    Ice,    // блокирует свайп, пока не разморозится сама или матчем по соседству (переиспользует Item.Freeze/isFrozen)
    Spike,  // фиксированный урон герою каждый ход, пока фишка на поле
    Trap,   // выглядит как обычный гем, но матч по ней наносит урон герою вместо пользы
    Anchor  // не уничтожается обычным матчем — снимается только спец-скиллом (Elves T2) или N матчей подряд по соседству
}

[System.Serializable]
public class HarmfulTileSpawnRule
{
    public HarmfulTileType type;
    public int count = 1;          // сколько таких фишек заспавнить в начале боя
    public int value = 1;          // Ice/Anchor: длительность/порог в ходах; Spike/Trap: урон герою
}
