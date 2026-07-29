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

    [Header("Ноды, которые нужно пройти, чтобы эта разблокировалась (пусто = открыта с начала)")]
    public MapNodeData[] requiredNodes;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(nodeId))
            nodeId = name;
    }
}
