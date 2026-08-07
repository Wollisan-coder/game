using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Экран сбора с шахт территорий рас (см. project_death_dungeon_concept — кусок 5 позже будет атаковать
// именно эти шахты). Минимальная версия: по 3 маленьких TerritoryMine_<Race>_<Resource> здания на расу
// (Wood/Stone/Shard, переиспользуют существующую BuildingData/BuildingManager инфраструктуру целиком —
// см. Assets/TerritoryMines/). Появляются автоматически, как только территория открыта
// (BuildingUnlockType.TerritoryOpened) — без ручной постройки (buildCost=0, Build() вызывается лениво
// прямо тут при первом заходе на экран после открытия территории). Апгрейда пока нет (maxLevel=1) —
// ждёт будущей отдельной системы квестов рас, см. память project_gem_economy_docs_todo-style TODO.
// Структура экрана скопирована с AchievementUI (оверлей + ScrollRect + ProgressCardUI на каждую строку).
public class TerritoryMinesUI : MonoBehaviour
{
    private static readonly (string suffix, string label)[] ResourceSlots =
    {
        ("Wood", "Wood"),
        ("Stone", "Stone"),
        ("Shard", "Summon Shards"),
    };

    private Transform canvasRoot;
    private GameObject overlayRoot;
    private RectTransform content;
    private ScrollRect scrollRect;

    public void Open(Transform canvasRoot)
    {
        this.canvasRoot = canvasRoot;
        EnsureOverlay();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh(resetScroll: true);
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("TerritoryMinesOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.75f);
        var dimBtn = overlayRoot.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(950, 1100);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);
        var windowBlocker = windowObj.AddComponent<Button>();
        windowBlocker.transition = Selectable.Transition.None;

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 72);
        titleRect.anchoredPosition = new Vector2(0, -30);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Territory Mines";
        titleText.fontSize = 44;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        var scrollViewObj = new GameObject("ScrollView", typeof(RectTransform));
        var scrollViewRect = (RectTransform)scrollViewObj.transform;
        scrollViewRect.SetParent(windowRect, false);
        scrollViewRect.anchorMin = new Vector2(0, 0);
        scrollViewRect.anchorMax = new Vector2(1, 1);
        scrollViewRect.offsetMin = new Vector2(40, 150);
        scrollViewRect.offsetMax = new Vector2(-40, -130);
        scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewportObj = new GameObject("Viewport", typeof(RectTransform));
        var viewportRect = (RectTransform)viewportObj.transform;
        viewportRect.SetParent(scrollViewRect, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObj.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        viewportObj.AddComponent<RectMask2D>();

        var contentObj = new GameObject("Content", typeof(RectTransform));
        content = (RectTransform)contentObj.transform;
        content.SetParent(viewportRect, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 100);

        scrollRect.viewport = viewportRect;
        scrollRect.content = content;

        var emptyLabelObj = new GameObject("EmptyLabel", typeof(RectTransform));
        var emptyLabelRect = (RectTransform)emptyLabelObj.transform;
        emptyLabelRect.SetParent(windowRect, false);
        emptyLabelRect.anchorMin = new Vector2(0, 0.35f);
        emptyLabelRect.anchorMax = new Vector2(1, 0.65f);
        emptyLabelRect.offsetMin = new Vector2(40, 0);
        emptyLabelRect.offsetMax = new Vector2(-40, 0);
        emptyLabelText = emptyLabelObj.AddComponent<TextMeshProUGUI>();
        emptyLabelText.text = "Open a race's territory on the World Map to unlock its mines.";
        emptyLabelText.alignment = TextAlignmentOptions.Center;
        emptyLabelText.color = new Color(1, 1, 1, 0.6f);
        emptyLabelText.fontSize = 20;
        emptyLabelObj.SetActive(false);

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(320, 90);
        closeBtnRect.anchoredPosition = new Vector2(0, 30);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBtnImg);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);

        var closeTextObj = new GameObject("Text", typeof(RectTransform));
        var closeTextRect = (RectTransform)closeTextObj.transform;
        closeTextRect.SetParent(closeBtnRect, false);
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
        var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "Close";
        closeText.fontSize = 32;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = ConfirmationDialog.ButtonTextColor;

        overlayRoot.SetActive(false);
    }

    private TMP_Text emptyLabelText;

    private void Refresh(bool resetScroll = false)
    {
        var buildingManager = BuildingManager.Instance;
        var worldMapManager = WorldMapManager.Instance;
        if (buildingManager == null || worldMapManager == null || content == null) return;

        float previousScroll = scrollRect.verticalNormalizedPosition;

        foreach (Transform child in content)
            Destroy(child.gameObject);

        float yTop = 0f;
        bool anyRaceOpen = false;

        foreach (Race race in System.Enum.GetValues(typeof(Race)))
        {
            if (!worldMapManager.IsTerritoryOpened(race)) continue;
            anyRaceOpen = true;

            var headerObj = new GameObject($"{race}Header", typeof(RectTransform));
            var headerRect = (RectTransform)headerObj.transform;
            headerRect.SetParent(content, false);
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 40);
            headerRect.anchoredPosition = new Vector2(0, yTop);
            var headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = race.ToString();
            headerText.fontSize = 26;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.MidlineLeft;
            headerText.color = new Color(1f, 0.85f, 0.5f);
            yTop -= 40f + 8f;

            foreach (var (suffix, label) in ResourceSlots)
            {
                string buildingId = $"TerritoryMine_{race}_{suffix}";
                BuildingData building = buildingManager.allBuildings.FirstOrDefault(b => b != null && b.buildingId == buildingId);
                if (building == null) continue;
                if (!buildingManager.IsBuilt(buildingId) && buildingManager.IsUnlocked(building))
                    buildingManager.Build(building);
                if (!buildingManager.IsBuilt(buildingId)) continue;

                int pending = Mathf.FloorToInt(buildingManager.GetPendingAmount(building));
                int cap = building.GetStorageCap(buildingManager.GetOwnership(buildingId).level);
                bool ready = pending > 0;

                ProgressCardUI.Create(content, yTop, $"{race} {label}", pending, 0, cap, false, ready,
                    () => { buildingManager.CollectProduction(building); Refresh(); });
                yTop -= ProgressCardUI.CardHeight + ProgressCardUI.CardSpacing;
            }

            yTop -= 12f; // доп. отступ между расами
        }

        if (emptyLabelText != null)
            emptyLabelText.gameObject.SetActive(!anyRaceOpen);

        content.sizeDelta = new Vector2(0, -yTop);
        scrollRect.verticalNormalizedPosition = resetScroll ? 1f : previousScroll;
    }
}
