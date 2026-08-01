using UnityEngine;

// Одна точка на карте мира — бой, который она открывает, плюс от чего зависит её разблокировка.
[CreateAssetMenu(fileName = "NewMapNode", menuName = "World Map/Map Node")]
public class MapNodeData : ScriptableObject
{
    [Header("Идентификатор (не менять после релиза!)")]
    public string nodeId;

    public string nodeName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Бой, который открывает эта нода")]
    public EnemyData enemy;

    [Header("Позиция в кривой сложности (см. EnemyStatCurve) — territory+nodeIndex перекрывают HP/атаку/опыт из enemy выше")]
    public Race territory;
    [Range(1, 18)] public int nodeIndex = 1;

    [Header("Ноды, которые нужно пройти, чтобы эта разблокировалась (пусто = открыта с начала)")]
    public MapNodeData[] requiredNodes;

    // Чисто информационный флаг — само повторное прохождение уже работает для ЛЮБОЙ ноды без него
    // (WorldMapManager.SelectNode ничего не блокирует после первого прохождения, ОП и так выдаётся только
    // один раз). Нужен для UI/фильтрации — например, чтобы визуально отметить ноду как фарм-подземелье
    // или собрать список "куда идти фармить" на карте.
    [Header("Фарм-нода (повторяемая — открыта после территории, не разовый story-уровень)")]
    public bool isFarmNode;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(nodeId))
            nodeId = name;
    }
}
