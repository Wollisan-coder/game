using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Прогресс по карте мира — какие ноды пройдены, какая нода сейчас выбрана для боя.
// Синглтон с DontDestroyOnLoad + PlayerPrefs, по той же схеме, что HeroCollectionManager/EnemyCollectionManager.
public class WorldMapManager : MonoBehaviour
{
    public static WorldMapManager Instance { get; private set; }

    [Header("Все ноды карты")]
    public MapNodeData[] allNodes;

    [Header("ID пройденных нод (заполняется при загрузке сохранения)")]
    public List<string> completedNodeIds = new List<string>();

    // ID ноды, с которой запущен текущий бой — выставляется в SelectNode() перед переходом на боевую сцену
    public string currentNodeId;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadProgress();
    }

    public bool IsCompleted(MapNodeData node) => node != null && completedNodeIds.Contains(node.nodeId);

    // Нода открыта, если у неё нет обязательных предыдущих нод, либо все они уже пройдены
    public bool IsUnlocked(MapNodeData node)
    {
        if (node == null) return false;
        if (node.requiredNodes == null || node.requiredNodes.Length == 0) return true;

        return node.requiredNodes.All(IsCompleted);
    }

    // Вызывать по клику на ноду перед переходом в боевую сцену — выбирает врага ноды и запоминает саму ноду
    public bool SelectNode(MapNodeData node)
    {
        if (!IsUnlocked(node) || node.enemy == null) return false;
        if (EnemyCollectionManager.Instance == null || !EnemyCollectionManager.Instance.SelectEnemy(node.enemy))
            return false;

        currentNodeId = node.nodeId;
        return true;
    }

    // Вызывается BattleManager'ом при победе (см. OnEnemyDefeated) — отмечает текущую ноду пройденной
    public void CompleteCurrentNode()
    {
        if (string.IsNullOrEmpty(currentNodeId)) return;

        if (!completedNodeIds.Contains(currentNodeId))
            completedNodeIds.Add(currentNodeId);

        SaveProgress();
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetString("completed_node_ids", string.Join(",", completedNodeIds));
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        completedNodeIds.Clear();

        string saved = PlayerPrefs.GetString("completed_node_ids", "");
        if (string.IsNullOrEmpty(saved)) return;

        completedNodeIds.AddRange(saved.Split(',').Where(id => !string.IsNullOrEmpty(id)));
    }
}
