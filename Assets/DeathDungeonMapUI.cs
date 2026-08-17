using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Экран карты забега Death Dungeon (см. project_death_dungeon_concept/project_death_dungeon_implementation) —
// фон DeathDungMap.png + 8 хотспотов поверх альковов, locked/current/completed берётся из
// DeathDungeonManager. Позиции хотспотов (NodeAnchors) — грубая оценка по картинке "на глаз", почти
// наверняка потребуют ручной подгонки в редакторе (та же оговорка, что и у хотспота в Замке).
public class DeathDungeonMapUI : MonoBehaviour
{
    // Якоря (доля ширины/высоты картинки, снизу-слева = 0,0) для каждого узла — по порядку
    // DeathDungeonManager.DefaultNodeSequence, путь снизу вверх по зигзагу лестницы на DeathDungMap.png:
    // теплица, террариум, обсерватория, мастерская, трофейная, кристаллы+пентаграмма, библиотека, Boss Arena.
    private static readonly Vector2[] NodeAnchors =
    {
        new Vector2(0.25f, 0.10f),
        new Vector2(0.75f, 0.2f),
        new Vector2(0.27f, 0.3f),
        new Vector2(0.76f, 0.42f),
        new Vector2(0.25f, 0.53f),
        new Vector2(0.80f, 0.67f),
        new Vector2(0.25f, 0.65f),
        new Vector2(0.52f, 0.85f),
    };

    private Transform canvasRoot;
    private MainMenuUI mainMenuUI;
    private GameObject overlayRoot;
    private RectTransform mapImageRect;
    private readonly List<Button> nodeButtons = new List<Button>();
    private readonly List<GameObject> lockedOverlays = new List<GameObject>();
    private readonly List<GameObject> completedMarks = new List<GameObject>();

    public void Open(Transform canvasRoot, MainMenuUI mainMenuUI)
    {
        this.canvasRoot = canvasRoot;
        this.mainMenuUI = mainMenuUI;
        BuildOverlayIfNeeded();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void BuildOverlayIfNeeded()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("DeathDungeonMapOverlay", typeof(RectTransform));
        var overlayRect = (RectTransform)overlayRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.9f);

        var mapImageObj = new GameObject("MapImage", typeof(RectTransform));
        mapImageRect = (RectTransform)mapImageObj.transform;
        mapImageRect.SetParent(overlayRect, false);
        mapImageRect.anchorMin = Vector2.zero;
        mapImageRect.anchorMax = Vector2.one;
        mapImageRect.offsetMin = Vector2.zero;
        mapImageRect.offsetMax = Vector2.zero;
        var mapImage = mapImageObj.AddComponent<Image>();
        mapImage.sprite = Resources.Load<Sprite>("UI/World/DeathDungMap");
        mapImage.preserveAspect = false; // тянем ровно под весь оверлей, как и CastleUI.baseBackground

        for (int i = 0; i < NodeAnchors.Length; i++)
            BuildNodeHotspot(i);

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(overlayRect, false);
        closeBtnRect.anchorMin = new Vector2(0.28f, 0);
        closeBtnRect.anchorMax = new Vector2(0.28f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(260, 80);
        closeBtnRect.anchoredPosition = new Vector2(0, 30);
        var closeImg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeImg);
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
        // НЕ выход из забега — просто прячет карту (например, чтобы глянуть отряд/инвентарь), забег продолжается.
        closeText.text = "Hide Map";
        closeText.fontSize = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        closeText.fontStyle = FontStyles.Bold;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = ConfirmationDialog.ButtonTextColor;

        var retreatBtnObj = new GameObject("RetreatButton", typeof(RectTransform));
        var retreatBtnRect = (RectTransform)retreatBtnObj.transform;
        retreatBtnRect.SetParent(overlayRect, false);
        retreatBtnRect.anchorMin = new Vector2(0.72f, 0);
        retreatBtnRect.anchorMax = new Vector2(0.72f, 0);
        retreatBtnRect.pivot = new Vector2(0.5f, 0);
        retreatBtnRect.sizeDelta = new Vector2(260, 80);
        retreatBtnRect.anchoredPosition = new Vector2(0, 30);
        var retreatImg = retreatBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(retreatImg);
        var retreatBtn = retreatBtnObj.AddComponent<Button>();
        retreatBtn.onClick.AddListener(OnRetreatClicked);

        var retreatTextObj = new GameObject("Text", typeof(RectTransform));
        var retreatTextRect = (RectTransform)retreatTextObj.transform;
        retreatTextRect.SetParent(retreatBtnRect, false);
        retreatTextRect.anchorMin = Vector2.zero;
        retreatTextRect.anchorMax = Vector2.one;
        retreatTextRect.offsetMin = Vector2.zero;
        retreatTextRect.offsetMax = Vector2.zero;
        var retreatText = retreatTextObj.AddComponent<TextMeshProUGUI>();
        retreatText.text = $"Retreat ({HeroCollectionManager.RetreatGemCost} pts)";
        retreatText.fontSize = 22;
        retreatText.fontStyle = FontStyles.Bold;
        retreatText.alignment = TextAlignmentOptions.Center;
        retreatText.color = ConfirmationDialog.ButtonTextColor;
        retreatText.enableAutoSizing = true;
        retreatText.fontSizeMin = 28; // жёсткий пол проекта — ConfirmationDialog.MinTextFontSize
        retreatText.fontSizeMax = 32;

        overlayRoot.SetActive(false);
    }

    // Ретрит доступен в любой момент забега (см. project_death_dungeon_concept) — открывает
    // DeathDungeonRetreatSelectionUI, где игрок САМ набирает HeroCollectionManager.RetreatGemCost (4)
    // очков смесью гемов вознесения (с любых героев) и целиком принесённых в жертву героев-"карт" (вне
    // текущего отряда). Спасает весь отряд, но НЕ бесплатно: недельный дебафф в обычных боях + недельная
    // блокировка повторного входа (см. DeathDungeonManager), плюс цена самой жертвы (упущенное вознесение
    // и/или потерянные герои).
    private void OnRetreatClicked()
    {
        var collectionManager = HeroCollectionManager.Instance;
        if (collectionManager == null || DeathDungeonManager.Instance == null) return;

        int need = HeroCollectionManager.RetreatGemCost;
        var squadIds = collectionManager.squad.Where(h => h != null).Select(h => h.heroId);
        int available = collectionManager.GetTotalAscensionGemCount() + collectionManager.GetAvailableSacrificeHeroCount(squadIds);

        if (available < need)
        {
            ConfirmationDialog.ShowInfo(canvasRoot, $"You need {need} points worth of Ascension Gems and/or spare heroes to retreat.\nDuplicate hero pulls grant Ascension Gems directly — see the Ascension Gems screen.");
            return;
        }

        var retreatUI = GetComponent<DeathDungeonRetreatSelectionUI>();
        retreatUI?.Open(canvasRoot, OnRetreated);
    }

    private void OnRetreated()
    {
        Close();
        ConfirmationDialog.ShowInfo(canvasRoot,
            $"You retreated safely. Your squad is shaken (-20% in normal battles for {DeathDungeonManager.RetreatDebuffDays} days) " +
            $"and the Death Dungeon is sealed to you for {DeathDungeonManager.RetreatLockoutDays} days.");
    }

    private void BuildNodeHotspot(int index)
    {
        var hotspotObj = new GameObject($"Node_{index}", typeof(RectTransform));
        var hotspotRect = (RectTransform)hotspotObj.transform;
        hotspotRect.SetParent(mapImageRect, false);
        hotspotRect.anchorMin = NodeAnchors[index];
        hotspotRect.anchorMax = NodeAnchors[index];
        hotspotRect.pivot = new Vector2(0.5f, 0.5f);
        hotspotRect.sizeDelta = new Vector2(90, 90);
        hotspotRect.anchoredPosition = Vector2.zero;

        var bg = hotspotObj.AddComponent<Image>();
        bg.color = new Color(1f, 0.85f, 0.4f, 0.55f); // тёплая подсветка узла — своей иконки под каждый тип пока нет

        var btn = hotspotObj.AddComponent<Button>();
        int capturedIndex = index;
        btn.onClick.AddListener(() => OnNodeClicked(capturedIndex));
        nodeButtons.Add(btn);

        var lockedObj = new GameObject("Locked", typeof(RectTransform));
        var lockedRect = (RectTransform)lockedObj.transform;
        lockedRect.SetParent(hotspotRect, false);
        lockedRect.anchorMin = Vector2.zero;
        lockedRect.anchorMax = Vector2.one;
        lockedRect.offsetMin = Vector2.zero;
        lockedRect.offsetMax = Vector2.zero;
        var lockedImg = lockedObj.AddComponent<Image>();
        lockedImg.color = new Color(0f, 0f, 0f, 0.65f);
        lockedOverlays.Add(lockedObj);

        var completedObj = new GameObject("Completed", typeof(RectTransform));
        var completedRect = (RectTransform)completedObj.transform;
        completedRect.SetParent(hotspotRect, false);
        completedRect.anchorMin = Vector2.zero;
        completedRect.anchorMax = Vector2.one;
        completedRect.offsetMin = Vector2.zero;
        completedRect.offsetMax = Vector2.zero;
        var completedText = completedObj.AddComponent<TextMeshProUGUI>();
        completedText.text = "✓";
        completedText.fontSize = 48;
        completedText.alignment = TextAlignmentOptions.Center;
        completedText.color = new Color(0.5f, 1f, 0.5f, 1f);
        completedMarks.Add(completedObj);
    }

    private void Refresh()
    {
        var dungeon = DeathDungeonManager.Instance;
        if (dungeon == null) return;

        for (int i = 0; i < nodeButtons.Count; i++)
        {
            bool completed = dungeon.IsNodeCompleted(i);
            bool current = dungeon.IsNodeCurrent(i);

            nodeButtons[i].interactable = current;
            lockedOverlays[i].SetActive(!completed && !current);
            completedMarks[i].SetActive(completed);
        }
    }

    private void OnNodeClicked(int index)
    {
        if (mainMenuUI == null || DeathDungeonManager.Instance == null) return;
        if (!DeathDungeonManager.Instance.IsNodeCurrent(index)) return;

        if (AccountManager.Instance == null || !AccountManager.Instance.SpendEnergy(mainMenuUI.battleEnergyCost))
        {
            ConfirmationDialog.ShowInfo(canvasRoot, "Not enough energy to start a battle.", iconPath: "UI/Currency/Energy");
            return;
        }

        // Одноразовый сигнал на КАЖДЫЙ узел гаунтлета, не только на первый — см. AccountManager.pendingDeathDungeon.
        AccountManager.Instance.pendingDeathDungeon = true;
        SceneManager.LoadScene(mainMenuUI.battleSceneName);
    }
}
