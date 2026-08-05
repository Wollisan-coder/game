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

    [Header("Стартовая нода — где стоит маркер игрока, пока ничего не пройдено")]
    public MapNodeData startingNode;

    [Header("ID пройденных нод (заполняется при загрузке сохранения)")]
    public List<string> completedNodeIds = new List<string>();

    // ID ноды, с которой запущен текущий бой — выставляется в SelectNode() перед переходом на боевую сцену
    public string currentNodeId;

    // Сама нода (не только id) — MapNodeData это ассет, а не объект сцены, так что ссылку хранить безопасно
    // (в отличие от lastActiveMapPanelName ниже). Нужна BattleManager'у для EnemyStatCurve.GetStats(territory, nodeIndex).
    public MapNodeData currentNode;

    // Имя UI-панели (WorldMapPanel или конкретная CityMap_...), в которой лежала нода запущенного боя —
    // выставляется в MapNodeUI.OnClicked() перед переходом на боевую сцену. GameObject-ссылку хранить нельзя
    // (старая сцена уничтожается при перезагрузке), поэтому храним просто имя и ищем панель по нему заново.
    public string lastActiveMapPanelName;

    // Дёргается при любом изменении прогресса (сейчас — только CompleteCurrentNode) — на это подписывается маркер игрока
    public System.Action OnProgressChanged;

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

    // Территория "открыта" — доступна её первая нода (nodeIndex 1). Используется гейтингом зданий
    // (BuildingUnlockType.TerritoryOpened), см. project_campaign_difficulty_curve. Фильтр по enemy!=null
    // отсекает ноды-врата городов (MapNodeUI с cityMapPanel вместо боя) — у них enemy не назначен, но
    // им ничего не мешает по ошибке получить тот же territory+nodeIndex=1, что и настоящей первой боевой ноде.
    public bool IsTerritoryOpened(Race territory)
    {
        var firstNode = allNodes?.FirstOrDefault(n => n != null && n.enemy != null && n.territory == territory && n.nodeIndex == 1);
        return firstNode != null && IsUnlocked(firstNode);
    }

    // Территория "пройдена" — её босс (nodeIndex 18) пройден.
    public bool IsTerritoryCompleted(Race territory)
    {
        var bossNode = allNodes?.FirstOrDefault(n => n != null && n.enemy != null && n.territory == territory && n.nodeIndex == 18);
        return bossNode != null && IsCompleted(bossNode);
    }

    // Вызывать по клику на ноду перед переходом в боевую сцену — выбирает врага ноды и запоминает саму ноду
    public bool SelectNode(MapNodeData node)
    {
        if (!IsUnlocked(node) || node.enemy == null) return false;
        if (EnemyCollectionManager.Instance == null || !EnemyCollectionManager.Instance.SelectEnemy(node.enemy))
            return false;

        currentNodeId = node.nodeId;
        currentNode = node;
        return true;
    }

    // Вызывается BattleManager'ом при победе (см. OnEnemyDefeated) — отмечает текущую ноду пройденной
    public void CompleteCurrentNode()
    {
        if (string.IsNullOrEmpty(currentNodeId)) return;

        if (!completedNodeIds.Contains(currentNodeId))
        {
            completedNodeIds.Add(currentNodeId);
            AchievementManager.Instance?.ReportMapNodeCleared();

            // Босс территории — всегда nodeIndex 18, см. IsTerritoryCompleted выше.
            if (currentNode != null && currentNode.nodeIndex == 18)
                AchievementManager.Instance?.ReportTerritoryCleared();
        }

        SaveProgress();
        OnProgressChanged?.Invoke();
    }

    // Где сейчас "находится" игрок на карте — маркер стоит на СЛЕДУЮЩЕЙ доступной ноде (открыта, но ещё
    // не пройдена), а не на последней пройденной: игрок отправляется ТУДА, а не остаётся на месте, где уже был.
    // Фарм-ноды и ноды-врата городов (enemy == null) намеренно исключены — маркер держится основного пути.
    // Если всё открытое уже пройдено (или нод вообще нет) — откатываемся к последней пройденной, либо на старт.
    public string GetCurrentPlayerNodeId()
    {
        var nextNode = allNodes?
            .Where(n => n != null && n.enemy != null && !n.isFarmNode && IsUnlocked(n) && !IsCompleted(n))
            .OrderBy(n => (int)n.territory)
            .ThenBy(n => n.nodeIndex)
            .FirstOrDefault();

        if (nextNode != null) return nextNode.nodeId;

        if (completedNodeIds.Count > 0)
            return completedNodeIds[completedNodeIds.Count - 1];

        return startingNode != null ? startingNode.nodeId : null;
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
