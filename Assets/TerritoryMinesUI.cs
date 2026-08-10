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
// Точка входа — иконка Mines в Замке (см. CastleUI.CreateMinesIconButton).
//
// 3 здания одной расы визуально объединены в ОДНУ карточку (у игрока это одна "шахта", просто у
// BuildingData один producedCurrency на ассет, поэтому под капотом их всё равно три) — своя мини-полоска
// сейчас/кап на каждый ресурс внутри карточки, кнопка Collect на карточке забирает все 3 здания ЭТОЙ расы.
// Отдельная глобальная кнопка Collect All в шапке окна (см. CollectAllGlobal) забирает вообще все шахты
// всех открытых рас разом. Структура окна изначально скопирована с AchievementUI, карточки — своя вёрстка.
public class TerritoryMinesUI : MonoBehaviour
{
    private static readonly (string suffix, string label, string iconPath)[] ResourceSlots =
    {
        ("Wood", "Wood", "UI/Currency/Wood"),
        ("Stone", "Stone", "UI/Currency/Stone"),
        ("Shard", "Summon Shards", "UI/Currency/Shards"),
    };

    private const float CardHeight = 300f; // было 200 — +100 под строку расовых квестов/апгрейда на 28pt (см. BuildRaceQuestRow)
    private const float CardSpacing = 20f;
    private const float RowHeight = 36f;
    private const float RowSpacing = 6f;
    private const float QuestRowHeight = 90f; // текст здесь ConfirmationDialog.MinTextFontSize (28), может занять 2 строки

    private Transform canvasRoot;
    private GameObject overlayRoot;
    private RectTransform content;
    private ScrollRect scrollRect;
    private System.Action onChanged;

    private Image collectAllGlobalImg;
    private Button collectAllGlobalBtn;
    private TMP_Text collectAllGlobalText;

    // onChanged — дёргается после Collect All (см. MainMenuUI.RefreshMinesBadges), чтобы бейдж "склад
    // полон" у кнопки Mines погас сразу, не дожидаясь следующего захода на экран карты.
    public void Open(Transform canvasRoot, System.Action onChanged = null)
    {
        this.canvasRoot = canvasRoot;
        this.onChanged = onChanged;
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
        titleText.fontSize = ConfirmationDialog.TitleFontSize;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Собирает продукцию ВСЕХ построенных шахт всех открытых рас разом — отдельно от Collect на
        // каждой карточке расы (см. BuildCollectAllButton), которая забирает только эту одну расу.
        var collectAllObj = new GameObject("CollectAllGlobal", typeof(RectTransform));
        var collectAllRect = (RectTransform)collectAllObj.transform;
        collectAllRect.SetParent(windowRect, false);
        collectAllRect.anchorMin = new Vector2(1, 1);
        collectAllRect.anchorMax = new Vector2(1, 1);
        collectAllRect.pivot = new Vector2(1, 1);
        collectAllRect.sizeDelta = new Vector2(200, 60);
        collectAllRect.anchoredPosition = new Vector2(-30, -30);
        collectAllGlobalImg = collectAllObj.AddComponent<Image>();
        collectAllGlobalBtn = collectAllObj.AddComponent<Button>();
        // Кнопки, построенные в рантайме, не проходят через Selectable.OnValidate (тот авто-привязывает
        // targetGraphic, но только если !Application.isPlaying) — без явного назначения здесь Unity не
        // применит свой стандартный Disabled Color при interactable=false, см. RefreshCollectAllGlobal.
        collectAllGlobalBtn.targetGraphic = collectAllGlobalImg;
        collectAllGlobalBtn.onClick.AddListener(CollectAllGlobal);

        var collectAllTextObj = new GameObject("Text", typeof(RectTransform));
        var collectAllTextRect = (RectTransform)collectAllTextObj.transform;
        collectAllTextRect.SetParent(collectAllRect, false);
        collectAllTextRect.anchorMin = Vector2.zero;
        collectAllTextRect.anchorMax = Vector2.one;
        collectAllTextRect.offsetMin = Vector2.zero;
        collectAllTextRect.offsetMax = Vector2.zero;
        collectAllGlobalText = collectAllTextObj.AddComponent<TextMeshProUGUI>();
        collectAllGlobalText.text = "Collect All";
        collectAllGlobalText.fontSize = ConfirmationDialog.ButtonFontSize;
        collectAllGlobalText.alignment = TextAlignmentOptions.Center;

        var scrollViewObj = new GameObject("ScrollView", typeof(RectTransform));
        var scrollViewRect = (RectTransform)scrollViewObj.transform;
        scrollViewRect.SetParent(windowRect, false);
        scrollViewRect.anchorMin = new Vector2(0, 0);
        scrollViewRect.anchorMax = new Vector2(1, 1);
        scrollViewRect.offsetMin = new Vector2(ConfirmationDialog.WindowContentPaddingX, 150);
        scrollViewRect.offsetMax = new Vector2(-ConfirmationDialog.WindowContentPaddingX, -130);
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
        closeText.fontSize = ConfirmationDialog.ButtonFontSize;
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

            bool anyBuilt = false;
            foreach (var (suffix, _, _) in ResourceSlots)
            {
                string buildingId = $"TerritoryMine_{race}_{suffix}";
                BuildingData building = buildingManager.allBuildings.FirstOrDefault(b => b != null && b.buildingId == buildingId);
                if (building == null) continue;
                if (!buildingManager.IsBuilt(buildingId) && buildingManager.IsUnlocked(building))
                    buildingManager.Build(building);
                if (buildingManager.IsBuilt(buildingId)) anyBuilt = true;
            }
            if (!anyBuilt) continue;

            yTop = BuildRaceCard(content, yTop, race, buildingManager);
        }

        if (emptyLabelText != null)
            emptyLabelText.gameObject.SetActive(!anyRaceOpen);

        content.sizeDelta = new Vector2(0, -yTop);
        scrollRect.verticalNormalizedPosition = resetScroll ? 1f : previousScroll;

        RefreshCollectAllGlobal(buildingManager, worldMapManager);
    }

    // Активна, только если хоть у одной построенной шахты ЛЮБОЙ открытой расы что-то накопилось —
    // те же условия, что и у BuildingManager.HasAnyMineAtCap, просто pending>0 вместо pending>=cap.
    private void RefreshCollectAllGlobal(BuildingManager buildingManager, WorldMapManager worldMapManager)
    {
        if (collectAllGlobalImg == null) return;

        bool anyReady = false;
        foreach (Race race in System.Enum.GetValues(typeof(Race)))
        {
            if (!worldMapManager.IsTerritoryOpened(race)) continue;
            foreach (var (suffix, _, _) in ResourceSlots)
            {
                string buildingId = $"TerritoryMine_{race}_{suffix}";
                BuildingData building = buildingManager.allBuildings.FirstOrDefault(b => b != null && b.buildingId == buildingId);
                if (building == null || !buildingManager.IsBuilt(buildingId)) continue;
                if (buildingManager.GetPendingAmount(building) > 0) { anyReady = true; break; }
            }
            if (anyReady) break;
        }

        // Стиль кнопки (декоративная рамка) одинаковый в обоих состояниях — не подменяем спрайт на
        // плоскую серую заливку. Разница между "активна"/"не активна" — Button.interactable, а серый
        // вид неактивной кнопки даёт стандартный Disabled Color из Button.colors (Unity сам тонирует
        // targetGraphic), как у "Already Worn" на плитке Дивной брони (WondrousArmorTileUI).
        ConfirmationDialog.StyleAsButton(collectAllGlobalImg);
        collectAllGlobalBtn.interactable = anyReady;
        collectAllGlobalText.text = "Collect All";
        collectAllGlobalText.color = anyReady ? new Color(1f, 0.92f, 0.4f) : ConfirmationDialog.ButtonTextColor;
    }

    // Забирает продукцию всех построенных шахт всех открытых рас разом — глобальная версия
    // BuildCollectAllButton, привязанная к кнопке в шапке окна (см. EnsureOverlay/CollectAllGlobal-объект).
    private void CollectAllGlobal()
    {
        var buildingManager = BuildingManager.Instance;
        var worldMapManager = WorldMapManager.Instance;
        if (buildingManager == null || worldMapManager == null) return;

        foreach (Race race in System.Enum.GetValues(typeof(Race)))
        {
            if (!worldMapManager.IsTerritoryOpened(race)) continue;
            foreach (var (suffix, _, _) in ResourceSlots)
            {
                string buildingId = $"TerritoryMine_{race}_{suffix}";
                BuildingData building = buildingManager.allBuildings.FirstOrDefault(b => b != null && b.buildingId == buildingId);
                if (building == null || !buildingManager.IsBuilt(buildingId)) continue;
                buildingManager.CollectProduction(building);
            }
        }

        onChanged?.Invoke();
        Refresh();
    }

    // Одна карточка на расу — заголовок + кнопка Collect справа от него (забирает только эту расу — для
    // всех сразу см. глобальную Collect All в шапке окна, CollectAllGlobal), ниже 3 мини-строки
    // (Wood/Stone/Shard, у каждой своя полоска сейчас/кап). Возвращает новый курсор yTop для следующей карточки.
    private float BuildRaceCard(RectTransform parent, float yTop, Race race, BuildingManager buildingManager)
    {
        var cardObj = new GameObject($"{race}Card", typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(parent, false);
        cardRect.anchorMin = new Vector2(0, 1);
        cardRect.anchorMax = new Vector2(1, 1);
        cardRect.pivot = new Vector2(0.5f, 1);
        cardRect.sizeDelta = new Vector2(0, CardHeight);
        cardRect.anchoredPosition = new Vector2(0, yTop);

        var bg = cardObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(cardRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.offsetMin = new Vector2(16, 0);
        titleRect.offsetMax = new Vector2(-170, 0);
        titleRect.sizeDelta = new Vector2(0, 40);
        titleRect.anchoredPosition = new Vector2(0, -14);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = $"{race} Mines";
        titleText.fontSize = ConfirmationDialog.HeaderFontSize;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.color = new Color(1f, 0.85f, 0.5f);

        var raceBuildings = new BuildingData[ResourceSlots.Length];
        bool anyReady = false;

        float rowY = -14f - 40f - 6f;
        for (int i = 0; i < ResourceSlots.Length; i++)
        {
            var (suffix, label, iconPath) = ResourceSlots[i];
            string buildingId = $"TerritoryMine_{race}_{suffix}";
            BuildingData building = buildingManager.allBuildings.FirstOrDefault(b => b != null && b.buildingId == buildingId);
            raceBuildings[i] = building;

            if (building != null && buildingManager.IsBuilt(buildingId))
            {
                int pending = Mathf.FloorToInt(buildingManager.GetPendingAmount(building));
                int cap = building.GetStorageCap(buildingManager.GetOwnership(buildingId).level);
                if (pending > 0) anyReady = true;

                CreateResourceRow(cardRect, rowY, label, iconPath, pending, cap);
            }

            rowY -= RowHeight + RowSpacing;
        }

        BuildCollectAllButton(cardRect, buildingManager, raceBuildings, anyReady);
        BuildRaceQuestRow(cardRect, rowY - 6f, race, raceBuildings, buildingManager);

        return yTop - CardHeight - CardSpacing;
    }

    // Прогресс расовых квестов (см. RaceQuestManager) — пока не выполнены все 3, апгрейд шахт этой расы
    // невозможен (maxLevel заперт на 1, см. BuildingManager.GetEffectiveMaxLevel). Как только выполнены —
    // текст меняется на кнопку Upgrade, апгрейдящую все 3 здания расы разом (тот же принцип, что и Collect
    // выше) — суммарная стоимость проверяется ЦЕЛИКОМ до списания, чтобы 3 здания расы не разъехались по
    // уровню из-за нехватки ресурсов на середине цикла.
    private void BuildRaceQuestRow(RectTransform cardRect, float yTop, Race race, BuildingData[] raceBuildings, BuildingManager buildingManager)
    {
        var rowRect = (RectTransform)new GameObject("QuestRow", typeof(RectTransform)).transform;
        rowRect.SetParent(cardRect, false);
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.offsetMin = new Vector2(16, 0);
        rowRect.offsetMax = new Vector2(-16, 0);
        rowRect.sizeDelta = new Vector2(0, QuestRowHeight);
        rowRect.anchoredPosition = new Vector2(0, yTop);

        var manager = RaceQuestManager.Instance;
        bool complete = manager != null && manager.IsRaceQuestsComplete(race);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(rowRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = complete ? new Vector2(0.58f, 1) : Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = ConfirmationDialog.MinTextFontSize; // проект-вайд правило — нигде не меньше 28
        text.enableWordWrapping = true;

        if (manager == null)
        {
            text.text = "";
        }
        else if (complete)
        {
            text.color = new Color(0.5f, 0.9f, 0.5f);
            text.text = "Race Quests complete";
        }
        else
        {
            // "Yes"/"No" вместо ✓/✗ — у используемого TMP-шрифта нет этих глифов, символ рендерился
            // квадратом-заглушкой (тот же баг, что был с em-dash — см. ProgressCardUI/DailyQuestUI).
            text.color = new Color(1f, 1f, 1f, 0.75f);
            text.text = $"Race Quests: Battles {manager.GetBattlesWon(race)}/{RaceQuestManager.WinBattlesTarget} · " +
                $"Passive {manager.GetPassiveTriggers(race)}/{RaceQuestManager.TriggerPassiveTarget} · " +
                $"Hero Lv{RaceQuestManager.HeroLevelTarget} {(manager.IsHeroLevelQuestComplete(race) ? "Yes" : "No")}";
        }

        if (!complete) return;

        int currentLevel = raceBuildings.Where(b => b != null)
            .Select(b => buildingManager.GetOwnership(b.buildingId)?.level ?? 1)
            .DefaultIfEmpty(1).Min();
        var anyBuilding = raceBuildings.FirstOrDefault(b => b != null);
        int maxLevel = anyBuilding != null ? buildingManager.GetEffectiveMaxLevel(anyBuilding) : 1;
        bool canUpgradeMore = currentLevel < maxLevel;

        int totalWood = 0, totalStone = 0;
        foreach (var b in raceBuildings)
        {
            if (b == null) continue;
            var (w, s) = b.GetUpgradeCost(currentLevel + 1);
            totalWood += w;
            totalStone += s;
        }

        bool canAfford = PlayerCurrencies.Instance != null
            && PlayerCurrencies.Instance.GetBalance(CurrencyType.Wood) >= totalWood
            && PlayerCurrencies.Instance.GetBalance(CurrencyType.Stone) >= totalStone;

        var upgradeObj = new GameObject("Upgrade", typeof(RectTransform));
        var upgradeRect = (RectTransform)upgradeObj.transform;
        upgradeRect.SetParent(rowRect, false);
        upgradeRect.anchorMin = new Vector2(0.6f, 0);
        upgradeRect.anchorMax = Vector2.one;
        upgradeRect.offsetMin = Vector2.zero;
        upgradeRect.offsetMax = Vector2.zero;

        var upgradeImg = upgradeObj.AddComponent<Image>();
        var upgradeBtn = upgradeObj.AddComponent<Button>();
        upgradeBtn.targetGraphic = upgradeImg;
        ConfirmationDialog.StyleAsButton(upgradeImg);
        upgradeBtn.interactable = canUpgradeMore && canAfford;
        upgradeBtn.onClick.AddListener(() =>
        {
            if (PlayerCurrencies.Instance == null) return;
            if (PlayerCurrencies.Instance.GetBalance(CurrencyType.Wood) < totalWood
                || PlayerCurrencies.Instance.GetBalance(CurrencyType.Stone) < totalStone) return;

            foreach (var b in raceBuildings)
                if (b != null) buildingManager.UpgradeBuilding(b);
            Refresh();
        });

        var upgradeTextObj = new GameObject("Text", typeof(RectTransform));
        var upgradeTextRect = (RectTransform)upgradeTextObj.transform;
        upgradeTextRect.SetParent(upgradeRect, false);
        upgradeTextRect.anchorMin = Vector2.zero;
        upgradeTextRect.anchorMax = Vector2.one;
        upgradeTextRect.offsetMin = Vector2.zero;
        upgradeTextRect.offsetMax = Vector2.zero;
        var upgradeText = upgradeTextObj.AddComponent<TextMeshProUGUI>();
        upgradeText.alignment = TextAlignmentOptions.Center;
        upgradeText.fontSize = ConfirmationDialog.MinTextFontSize;
        upgradeText.enableWordWrapping = true;
        upgradeText.color = ConfirmationDialog.ButtonTextColor;
        upgradeText.text = canUpgradeMore ? $"Upgrade\n{totalWood}W {totalStone}S" : "Max Level";
    }

    private void BuildCollectAllButton(RectTransform cardRect, BuildingManager buildingManager, BuildingData[] raceBuildings, bool anyReady)
    {
        var collectObj = new GameObject("Collect", typeof(RectTransform));
        var collectRect = (RectTransform)collectObj.transform;
        collectRect.SetParent(cardRect, false);
        collectRect.anchorMin = new Vector2(1, 1);
        collectRect.anchorMax = new Vector2(1, 1);
        collectRect.pivot = new Vector2(1, 1);
        collectRect.sizeDelta = new Vector2(200, 50);
        collectRect.anchoredPosition = new Vector2(-16, -14);

        var collectImg = collectObj.AddComponent<Image>();
        var collectBtn = collectObj.AddComponent<Button>();
        // См. targetGraphic-комментарий у CollectAllGlobal в EnsureOverlay — рантайм-кнопки не проходят
        // через Selectable.OnValidate, без явного назначения Disabled Color из Button.colors не применится.
        collectBtn.targetGraphic = collectImg;
        collectBtn.onClick.AddListener(() =>
        {
            foreach (var b in raceBuildings)
                if (b != null) buildingManager.CollectProduction(b);
            onChanged?.Invoke();
            Refresh();
        });

        var collectTextObj = new GameObject("Text", typeof(RectTransform));
        var collectTextRect = (RectTransform)collectTextObj.transform;
        collectTextRect.SetParent(collectRect, false);
        collectTextRect.anchorMin = Vector2.zero;
        collectTextRect.anchorMax = Vector2.one;
        collectTextRect.offsetMin = Vector2.zero;
        collectTextRect.offsetMax = Vector2.zero;
        var collectText = collectTextObj.AddComponent<TextMeshProUGUI>();
        collectText.fontSize = ConfirmationDialog.ButtonFontSize;
        collectText.alignment = TextAlignmentOptions.Center;
        collectText.text = "Collect";

        // Не прячем кнопку и не подменяем её вид на плоскую серую заливку — рамка (StyleAsButton) всегда
        // та же, что и на активной кнопке, а серый некликабельный вид даёт стандартный Disabled Color из
        // Button.colors, ровно как у "Already Worn" на плитке Дивной брони (WondrousArmorTileUI).
        ConfirmationDialog.StyleAsButton(collectImg);
        collectBtn.interactable = anyReady;
        collectText.color = anyReady ? new Color(1f, 0.92f, 0.4f) : ConfirmationDialog.ButtonTextColor;
    }

    // Мини-полоска одного ресурса внутри карточки расы — иконка + бар сейчас/кап, без своей кнопки
    // (сбор теперь только через общий Collect All на карточке, см. BuildCollectAllButton).
    private void CreateResourceRow(RectTransform parent, float yTop, string label, string iconPath, int pending, int cap)
    {
        var rowRect = (RectTransform)new GameObject($"Row_{label}", typeof(RectTransform)).transform;
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0, 1);
        rowRect.anchorMax = new Vector2(1, 1);
        rowRect.pivot = new Vector2(0.5f, 1);
        rowRect.offsetMin = new Vector2(16, 0);
        rowRect.offsetMax = new Vector2(-16, 0);
        rowRect.sizeDelta = new Vector2(0, RowHeight);
        rowRect.anchoredPosition = new Vector2(0, yTop);

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(rowRect, false);
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(0, 0.5f);
        iconRect.sizeDelta = new Vector2(28, 28);
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = Resources.Load<Sprite>(iconPath);
        iconImg.preserveAspect = true;

        var trackObj = new GameObject("BarTrack", typeof(RectTransform));
        var trackRect = (RectTransform)trackObj.transform;
        trackRect.SetParent(rowRect, false);
        trackRect.anchorMin = new Vector2(0, 0.5f);
        trackRect.anchorMax = new Vector2(1, 0.5f);
        trackRect.pivot = new Vector2(0, 0.5f);
        // Left=36, Right=36, Pos Y=0, Height=30 (см. RowHeight-6) — тот же отступ с обеих сторон, а не
        // только слева. sizeDelta/anchoredPosition считаются напрямую (а не через offsetMin), потому что
        // Unity выводит offsetMin/offsetMax из anchoredPosition+sizeDelta+pivot — присваивание offsetMin,
        // а следом sizeDelta/anchoredPosition, просто затирало бы друг друга.
        trackRect.sizeDelta = new Vector2(-72, RowHeight - 6);
        trackRect.anchoredPosition = new Vector2(36, 0);
        var trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0f, 0f, 0f, 0.5f);

        var fillObj = new GameObject("BarFill", typeof(RectTransform));
        var fillRect = (RectTransform)fillObj.transform;
        fillRect.SetParent(trackRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fillObj.AddComponent<Image>();
        bool atCap = cap > 0 && pending >= cap;
        fillImg.color = atCap ? new Color(0.9f, 0.55f, 0.15f, 0.9f) : new Color(0.3f, 0.55f, 0.9f, 0.9f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = cap > 0 ? Mathf.Clamp01(pending / (float)cap) : 0f;

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(trackRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = atCap ? $"{label}: {pending}/{cap} (FULL)" : $"{label}: {pending}/{cap}";
        text.fontSize = ConfirmationDialog.BodyFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }
}
