using UnityEngine;

[System.Serializable]
public class BoardEffectDefinition
{
    public BoardEffectType effectType;
    public int amount = 10;
    public int turns = 2;
    public float multiplier = 1.5f;
}

// Пул усіх можливих баффів/дебаффів для панелі перевороту дошки.
// Може містити скільки завгодно варіантів (більше 8) — при відкритті панелі
// випадково обирається 8 унікальних штук із цього пулу.
[CreateAssetMenu(fileName = "BoardEffectPool", menuName = "Battle/Board Effect Pool")]
public class BoardEffectPoolData : ScriptableObject
{
    public BoardEffectDefinition[] effects;
}
