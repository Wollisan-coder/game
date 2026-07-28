using UnityEngine;

[System.Serializable]
public class BoardEffectDefinition
{
    public BoardEffectType effectType;

    [Header("Визуал (показывается до шаффла, пока варианты не заблюрены)")]
    public Sprite icon;
    [TextArea(2, 4)] public string description; // если пусто — используется автотекст BoardEffectOption.GetDisplayName()

    public int amount = 10;
    public int turns = 2;
    public float multiplier = 1.5f;
}

// Пул баффов/дебаффов для панели переворота доски (гемблинг-колесо).
// Разделён на позитивные и негативные — при открытии панели случайно берётся
// 4 уникальных позитивных + 4 уникальных негативных (см. BoardFlipShuffleGate.AssignRandomEffects).
// Каждый список может содержать сколько угодно вариантов (больше 4).
[CreateAssetMenu(fileName = "BoardEffectPool", menuName = "Battle/Board Effect Pool")]
public class BoardEffectPoolData : ScriptableObject
{
    [Header("Позитивные баффы")]
    public BoardEffectDefinition[] positiveEffects;

    [Header("Негативные дебаффы")]
    public BoardEffectDefinition[] negativeEffects;
}
