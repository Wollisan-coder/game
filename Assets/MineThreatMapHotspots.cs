using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Кликабельный маркер угрозы шахтам на 3D-карте мира (кусок 5, см. MineThreatManager,
// project_death_dungeon_concept). НЕ MapNodeData/MapNodeUI — сознательно отдельный лёгкий компонент:
// обычный MapNodeUI.OnClicked идёт через WorldMapManager.SelectNode (ОП, completedNodeIds, номер ноды
// кампании) — ничего из этого тут не нужно, смешивать было бы неправильно. Спавнится программно рядом с
// уже существующим босс-узлом (nodeIndex 18) расы — никакой ручной правки 3D-сцены, только Transform.position
// (карта не Canvas, см. project_world_map_system). ПОЗИЦИЯ/РАЗМЕР НЕ ПРОВЕРЕНЫ в редакторе — тот же
// eyeballed-статус, что и у прочих ручных размещений на этой карте в проекте, почти наверняка потребует подгонки.
public class MineThreatMapHotspots : MonoBehaviour
{
    private static readonly Dictionary<Race, string> CityPanelNames = new Dictionary<Race, string>
    {
        { Race.Elves, "CityMap_Elfs" },
        { Race.Fairy, "CityMap_Fairies" },
        { Race.Orcs, "CityMap_Orcs" },
        { Race.Beastfolk, "CityMap_Beasts" },
        { Race.Demons, "CityMap_Demons" },
        { Race.Dragonkin, "CityMap_Dragons" },
        { Race.Angels, "CityMap_Angels" },
        { Race.Humans, "CityMap_Humans" },
    };

    private readonly Dictionary<Race, GameObject> spawnedHotspots = new Dictionary<Race, GameObject>();
    private MainMenuUI owner;

    public void RefreshAll(MainMenuUI mainMenuUI)
    {
        owner = mainMenuUI;
        if (MineThreatManager.Instance == null || owner == null || owner.cityMapPanels == null) return;

        MineThreatManager.Instance.ProcessQueue(); // подхватить угрозы, "созревшие" пока карта была закрыта

        foreach (var race in CityPanelNames.Keys)
        {
            bool blocked = MineThreatManager.Instance.IsRaceBlocked(race);
            bool exists = spawnedHotspots.TryGetValue(race, out GameObject existing) && existing != null;

            if (blocked && !exists)
                SpawnHotspot(race);
            else if (!blocked && exists)
            {
                Destroy(existing);
                spawnedHotspots.Remove(race);
            }
        }
    }

    private void SpawnHotspot(Race race)
    {
        if (!CityPanelNames.TryGetValue(race, out string panelName)) return;
        GameObject panel = owner.cityMapPanels.FirstOrDefault(p => p != null && p.name == panelName);
        if (panel == null) return;

        MapNodeUI bossNode = panel.GetComponentsInChildren<MapNodeUI>(true)
            .FirstOrDefault(n => n.node != null && n.node.nodeIndex == 18);
        if (bossNode == null) return; // территория этой расы ещё не доавторена (нет ноды 18) — угроза копится, бой пока недоступен

        var hotspotObj = new GameObject($"MineThreat_{race}", typeof(RectTransform));
        hotspotObj.transform.SetParent(panel.transform, false);
        hotspotObj.transform.position = bossNode.transform.position + new Vector3(60f, 60f, 0f);

        var rect = (RectTransform)hotspotObj.transform;
        rect.sizeDelta = new Vector2(90, 90);

        var icon = hotspotObj.AddComponent<Image>();
        icon.color = new Color(0.8f, 0.1f, 0.1f, 0.95f);

        var btn = hotspotObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OnHotspotClicked(race, panelName));

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(hotspotObj.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "!";
        label.fontSize = 48;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        spawnedHotspots[race] = hotspotObj;
    }

    private void OnHotspotClicked(Race race, string panelName)
    {
        if (AccountManager.Instance == null || WorldMapManager.Instance == null) return;

        // Тот же приём, что и MapNodeUI — записываем, куда вернуться, но НЕ трогаем currentNodeId
        // (это не настоящая нода кампании, см. класс-комментарий выше).
        WorldMapManager.Instance.lastActiveMapPanelName = panelName;
        AccountManager.Instance.pendingMineDefense = true;
        AccountManager.Instance.pendingMineDefenseRace = race;

        SceneManager.LoadScene(owner != null ? owner.battleSceneName : "SampleScene");
    }
}
