using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime-built castle menu: currency/energy header, navigation to Squad/Inventory/Collection,
// and a card per building (build / collect+upgrade production / open summon window for Forge & Altar).
// Built entirely in code — no scene/prefab edits.
public class CastleUI : MonoBehaviour
{
    private MainMenuUI owner;
    private Transform canvasRoot;

    private GameObject panelRoot;
    private Transform buildingGrid;
    private TMP_Text currencyText;
    private TMP_Text accountText;

    private Button dailyRewardButton;
    private Image dailyRewardButtonBg;
    private TMP_Text dailyRewardButtonText;

    private CastleSummonUI summonUI;
    private ProgressExchangeUI exchangeUI;

    public void Open(MainMenuUI mainMenu)
    {
        owner = mainMenu;

        if (canvasRoot == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRoot = canvas != null ? canvas.transform : transform;
        }

        EnsurePanel();
        panelRoot.transform.SetAsLastSibling();
        panelRoot.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void EnsurePanel()
    {
        if (panelRoot != null) return;

        panelRoot = new GameObject("CastlePanel", typeof(RectTransform));
        var panelRect = (RectTransform)panelRoot.transform;
        panelRect.SetParent(canvasRoot, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 1f);

        // --- Верхня панель: валюти + акаунт ---
        var topBarObj = new GameObject("TopBar", typeof(RectTransform));
        var topBarRect = (RectTransform)topBarObj.transform;
        topBarRect.SetParent(panelRect, false);
        topBarRect.anchorMin = new Vector2(0, 1);
        topBarRect.anchorMax = new Vector2(1, 1);
        topBarRect.pivot = new Vector2(0.5f, 1);
        topBarRect.sizeDelta = new Vector2(0, 70);
        var topBarBg = topBarObj.AddComponent<Image>();
        topBarBg.color = new Color(0, 0, 0, 0.35f);

        var currencyObj = new GameObject("CurrencyText", typeof(RectTransform));
        var currencyRect = (RectTransform)currencyObj.transform;
        currencyRect.SetParent(topBarRect, false);
        currencyRect.anchorMin = new Vector2(0, 0);
        currencyRect.anchorMax = new Vector2(0.6f, 1);
        currencyRect.offsetMin = new Vector2(16, 0);
        currencyRect.offsetMax = new Vector2(0, 0);
        currencyText = currencyObj.AddComponent<TextMeshProUGUI>();
        currencyText.fontSize = 20;
        currencyText.alignment = TextAlignmentOptions.MidlineLeft;
        currencyText.color = Color.white;

        var accountObj = new GameObject("AccountText", typeof(RectTransform));
        var accountRect = (RectTransform)accountObj.transform;
        accountRect.SetParent(topBarRect, false);
        accountRect.anchorMin = new Vector2(0.6f, 0);
        accountRect.anchorMax = new Vector2(1, 1);
        accountRect.offsetMin = new Vector2(0, 0);
        accountRect.offsetMax = new Vector2(-16, 0);
        accountText = accountObj.AddComponent<TextMeshProUGUI>();
        accountText.fontSize = 20;
        accountText.alignment = TextAlignmentOptions.MidlineRight;
        accountText.color = Color.white;

        // --- Навигация — так же, как кнопки внизу SquadPanel: якорь (0.5,0), размер 200x100, y=50 ---
        CreateNavButton(panelRect, "Squad", new Vector2(-330, 50), () => { owner?.ShowSquad(); });
        CreateNavButton(panelRect, "Inventory", new Vector2(-110, 50), () => { owner?.ShowItemCollection(); });
        CreateNavButton(panelRect, "Collection", new Vector2(110, 50), () => { owner?.ShowCollection(); });
        CreateDailyRewardButton(panelRect, new Vector2(330, 50));
        // Second row above the first — keeps the already-working bottom row untouched instead of
        // squeezing a 5th button into it.
        CreateNavButton(panelRect, "Exchange", new Vector2(0, 160), () => { exchangeUI?.Open(canvasRoot, Refresh); });

        // --- Сетка зданий ---
        var scrollObj = new GameObject("Scroll View", typeof(RectTransform));
        var scrollRect = (RectTransform)scrollObj.transform;
        scrollRect.SetParent(panelRect, false);
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(16, 220);
        scrollRect.offsetMax = new Vector2(-16, -80);

        var scroll = scrollObj.AddComponent<ScrollRect>();
        var scrollImg = scrollObj.AddComponent<Image>();
        scrollImg.color = new Color(1, 1, 1, 0.03f);
        var scrollMask = scrollObj.AddComponent<Mask>();
        scrollMask.showMaskGraphic = true;

        var contentObj = new GameObject("Content", typeof(RectTransform));
        var contentRect = (RectTransform)contentObj.transform;
        contentRect.SetParent(scrollRect, false);
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var grid = contentObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(330, 330);
        grid.spacing = new Vector2(16, 16);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Vertical;
        grid.childAlignment = TextAnchor.UpperLeft; 
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        buildingGrid = contentRect;

        summonUI = gameObject.AddComponent<CastleSummonUI>();
        exchangeUI = gameObject.AddComponent<ProgressExchangeUI>();

        panelRoot.SetActive(false);
    }

    private void CreateNavButton(RectTransform parent, string label, Vector2 anchoredPosition, System.Action onClick)
    {
        var btnObj = new GameObject(label, typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(200, 100);
        btnRect.anchoredPosition = anchoredPosition;

        var img = btnObj.AddComponent<Image>();
        img.color = ConfirmationDialog.ButtonColor;
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black; // текст кнопок замка — отдельно от глобального ConfirmationDialog.ButtonTextColor
    }

    // Отдельно от CreateNavButton — держит ссылки на bg/text, чтобы Refresh() мог менять подпись/цвет
    // в зависимости от того, забирали ли награду сегодня.
    private void CreateDailyRewardButton(RectTransform parent, Vector2 anchoredPosition)
    {
        var btnObj = new GameObject("DailyReward", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(200, 100);
        btnRect.anchoredPosition = anchoredPosition;

        dailyRewardButtonBg = btnObj.AddComponent<Image>();
        dailyRewardButton = btnObj.AddComponent<Button>();
        dailyRewardButton.onClick.AddListener(OnDailyRewardClicked);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        dailyRewardButtonText = textObj.AddComponent<TextMeshProUGUI>();
        dailyRewardButtonText.alignment = TextAlignmentOptions.Center;
        dailyRewardButtonText.fontSize = 24;
        dailyRewardButtonText.color = Color.black;
    }

    private void OnDailyRewardClicked()
    {
        if (AccountManager.Instance == null) return;

        int granted = AccountManager.Instance.ClaimDailyReward();
        if (granted > 0 && canvasRoot != null)
            ConfirmationDialog.ShowInfo(canvasRoot, $"Daily reward claimed!\n+{granted} Progress Points");

        Refresh();
    }

    private void RefreshDailyRewardButton()
    {
        if (dailyRewardButton == null || AccountManager.Instance == null) return;

        bool claimed = AccountManager.Instance.HasClaimedDailyRewardToday();
        int amount = AccountManager.Instance.GetDailyRewardAmount();

        dailyRewardButton.interactable = !claimed;
        if (dailyRewardButtonBg != null)
            dailyRewardButtonBg.color = claimed ? new Color(1, 1, 1, 0.15f) : ConfirmationDialog.ButtonColor;
        if (dailyRewardButtonText != null)
            dailyRewardButtonText.text = claimed ? "Daily reward\nclaimed" : $"Daily reward\n+{amount} PP";
    }

    public void Refresh()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        AccountManager.Instance?.RegenerateEnergyFromElapsedTime();

        if (currencyText != null && PlayerCurrencies.Instance != null)
        {
            currencyText.text =
                $"Wood: {PlayerCurrencies.Instance.GetBalance(CurrencyType.Wood)}   " +
                $"Stone: {PlayerCurrencies.Instance.GetBalance(CurrencyType.Stone)}   " +
                $"Shards: {PlayerCurrencies.Instance.GetBalance(CurrencyType.SummonShards)}   " +
                $"Gems: {PlayerCurrencies.Instance.GetBalance(CurrencyType.PremiumGems)}   " +
                $"PP: {PlayerCurrencies.Instance.GetBalance(CurrencyType.ProgressPoints)}";
        }

        if (accountText != null && AccountManager.Instance != null)
        {
            var acc = AccountManager.Instance;
            accountText.text = $"Lvl {acc.level} ({acc.experience}/{acc.ExperienceToNextLevel(acc.level)})   Energy: {acc.currentEnergy}/{acc.MaxEnergy}";
        }

        RefreshDailyRewardButton();

        PopulateBuildings();
    }

    private void PopulateBuildings()
    {
        if (buildingGrid == null || BuildingManager.Instance == null) return;

        foreach (Transform child in buildingGrid)
            Destroy(child.gameObject);

        foreach (var building in BuildingManager.Instance.allBuildings)
        {
            if (building != null)
                CreateBuildingCard(building);
        }
    }

    private void CreateBuildingCard(BuildingData building)
    {
        var manager = BuildingManager.Instance;

        var cardObj = new GameObject(building.buildingId, typeof(RectTransform));
        var cardRect = (RectTransform)cardObj.transform;
        cardRect.SetParent(buildingGrid, false);
        var cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(1, 1, 1, 0.06f);

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(cardRect, false);
        iconRect.anchorMin = new Vector2(0.5f, 1);
        iconRect.anchorMax = new Vector2(0.5f, 1);
        iconRect.pivot = new Vector2(0.5f, 1);
        iconRect.sizeDelta = new Vector2(96, 96);
        iconRect.anchoredPosition = new Vector2(0, -8);
        var icon = iconObj.AddComponent<Image>();
        icon.sprite = building.icon;
        icon.preserveAspect = true;

        var nameObj = new GameObject("Name", typeof(RectTransform));
        var nameRect = (RectTransform)nameObj.transform;
        nameRect.SetParent(cardRect, false);
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.sizeDelta = new Vector2(-8, 24);
        nameRect.anchoredPosition = new Vector2(0, -108);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = building.buildingName;
        nameText.fontSize = 16;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;

        var statusObj = new GameObject("Status", typeof(RectTransform));
        var statusRect = (RectTransform)statusObj.transform;
        statusRect.SetParent(cardRect, false);
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.pivot = new Vector2(0.5f, 1);
        statusRect.sizeDelta = new Vector2(-8, 40);
        statusRect.anchoredPosition = new Vector2(0, -132);
        var statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 13;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = new Color(1, 1, 1, 0.75f);

        bool unlocked = manager.IsUnlocked(building);
        bool built = manager.IsBuilt(building.buildingId);

        if (!unlocked)
        {
            string requirement = building.unlockType switch
            {
                BuildingUnlockType.TerritoryOpened => $"Requires {building.requiredTerritory} territory opened",
                BuildingUnlockType.TerritoryCompleted => $"Requires {building.requiredTerritory} territory cleared",
                _ => $"Requires account level {building.requiredAccountLevel}",
            };
            statusText.text = $"Locked\n{requirement}";
            cardBg.color = new Color(0, 0, 0, 0.25f);
            icon.color = new Color(1, 1, 1, 0.35f);
            return;
        }

        if (!built)
        {
            statusText.text = $"Not built\nCost: {building.buildCostWood} Wood, {building.buildCostStone} Stone";
            CreateActionButton(cardRect, "Build", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 34), () =>
            {
                manager.Build(building);
                Refresh();
            });
            return;
        }

        var ownership = manager.GetOwnership(building.buildingId);
        int level = ownership != null ? ownership.level : 1;

        if (building.buildingType == BuildingType.Forge || building.buildingType == BuildingType.Altar)
        {
            statusText.text = $"Level {level}";
            CreateActionButton(cardRect, "Summon", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 34), () =>
            {
                if (summonUI != null) summonUI.Open(building, canvasRoot, Refresh);
            });
        }
        else if (building.buildingType == BuildingType.SquadCapacity)
        {
            int squadWeight = HeroCollectionManager.BaseSquadWeight + building.GetSquadWeightBonus(level);
            statusText.text = $"Level {level}\nSquad weight capacity: {squadWeight}";

            if (level < building.maxLevel)
            {
                var (wood, stone) = building.GetUpgradeCost(level + 1);
                CreateActionButton(cardRect, $"Upgrade\n({wood}W/{stone}S)", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 34), () =>
                {
                    manager.UpgradeBuilding(building);
                    Refresh();
                });
            }
            else
            {
                statusText.text += "\nMAX";
            }
        }
        else
        {
            float pending = manager.GetPendingAmount(building);
            int cap = building.GetStorageCap(level);
            statusText.text = $"Level {level}\n{Mathf.FloorToInt(pending)}/{cap} {building.producedCurrency}";

            CreateActionButton(cardRect, "Collect", new Vector2(0, 0), new Vector2(0.48f, 0), new Vector2(0, 34), () =>
            {
                manager.CollectProduction(building);
                Refresh();
            });

            var (wood, stone) = building.GetUpgradeCost(level + 1);
            CreateActionButton(cardRect, $"Upgrade\n({wood}W/{stone}S)", new Vector2(0.52f, 0), new Vector2(1, 0), new Vector2(0, 34), () =>
            {
                manager.UpgradeBuilding(building);
                Refresh();
            });
        }
    }

    private void CreateActionButton(RectTransform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, System.Action onClick)
    {
        var btnObj = new GameObject("Action_" + label, typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = anchorMin;
        btnRect.anchorMax = anchorMax;
        btnRect.pivot = new Vector2(0.5f, 0);
        btnRect.sizeDelta = sizeDelta;
        btnRect.anchoredPosition = new Vector2(0, 6);

        var img = btnObj.AddComponent<Image>();
        img.color = ConfirmationDialog.ButtonColor;
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(btnRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black; // текст кнопок замка — отдельно от глобального ConfirmationDialog.ButtonTextColor
    }
}
