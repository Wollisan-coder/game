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
    private Transform buildingHotspotsContainer;
    private GameObject buildingDetailPopupRoot;
    private TMP_Text currencyText;
    private TMP_Text accountText;

    [Header("2D-сцена базы")]
    public Image baseBackground;

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

        // --- 2D-сцена базы: фон (пока без спрайта — просто затемнённая заливка, подставишь свой Image
        // через поле baseBackground в Inspector, либо дальше по коду) + контейнер хотспотов зданий ---
        var backgroundObj = new GameObject("BaseBackground", typeof(RectTransform));
        var backgroundRect = (RectTransform)backgroundObj.transform;
        backgroundRect.SetParent(panelRect, false);
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        baseBackground = backgroundObj.AddComponent<Image>();
        var bgSprite = Resources.Load<Sprite>("UI/Castle/BaseBackground");
        if (bgSprite != null)
        {
            baseBackground.sprite = bgSprite;
            baseBackground.color = Color.white;
        }
        else
        {
            baseBackground.color = new Color(0.15f, 0.15f, 0.2f, 1f); // фолбэк, если арт ещё не заведён
        }
        baseBackground.preserveAspect = false; // тянем ровно под канвас (1080x1920) — позиции хотспотов посчитаны под это же растяжение

        var hotspotsObj = new GameObject("BuildingHotspots", typeof(RectTransform));
        var hotspotsRect = (RectTransform)hotspotsObj.transform;
        hotspotsRect.SetParent(panelRect, false);
        hotspotsRect.anchorMin = Vector2.zero;
        hotspotsRect.anchorMax = Vector2.one;
        hotspotsRect.offsetMin = Vector2.zero;
        hotspotsRect.offsetMax = Vector2.zero;
        buildingHotspotsContainer = hotspotsRect;

        // --- Навигация — так же, как кнопки внизу SquadPanel: якорь (0.5,0), размер 200x100, y=50 ---
        CreateNavButton(panelRect, "Squad", new Vector2(6, 55), () => { owner?.ShowSquad(); });
        CreateNavButton(panelRect, "Inventory", new Vector2(-425, 55), () => { owner?.ShowItemCollection(); });
        CreateNavButton(panelRect, "Collection", new Vector2(-210, 55), () => { owner?.ShowCollection(); });
        CreateNavButton(panelRect, "Castle", new Vector2(221, 55), () => { owner?.ShowCastle(); });
        CreateNavButton(panelRect, "Map", new Vector2(436, 55), () => { owner?.ShowWorldMap(); });
        CreateDailyRewardButton(panelRect, new Vector2(330, 250));
        // Second row above the first — keeps the already-working bottom row untouched instead of
        // squeezing a 5th button into it. Boss Training moved off this row onto its own map hotspot
        // (Training zone.png, see CreateTrainingZoneHotspot) once the user provided real art for it.
        CreateNavButton(panelRect, "Exchange", new Vector2(0, 160), () => { exchangeUI?.Open(canvasRoot, Refresh); });

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
        btnRect.sizeDelta = new Vector2(220, 100);
        btnRect.anchoredPosition = anchoredPosition;

        var img = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(img);
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
        text.fontSize = 30;
        text.alignment = TextAlignmentOptions.Center;
        text.color = ConfirmationDialog.ButtonTextColor; // тёмный текст был под старую светлую заливку, на новой тёмно-синей рамке нужен светлый
    
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
        dailyRewardButtonText.color = ConfirmationDialog.ButtonTextColor; // тёмный текст был под старую светлую заливку, на новой тёмно-синей рамке нужен светлый
    }

    // Открывает Collection как пикер героя для тренировки (тот же приём, что и выбор героя в слот отряда —
    // HeroCollectionManager.pickingForBossTraining, см. HeroCollectionCardUI.OnSelected).
    // Лимит 1/день отключён по просьбе пользователя — AccountManager.HasDoneBossTrainingToday()/
    // MarkBossTrainingDone() оставлены нетронутыми на случай, если лимит понадобится вернуть.
    private void OnBossTrainingClicked()
    {
        if (AccountManager.Instance == null || HeroCollectionManager.Instance == null) return;

        HeroCollectionManager.Instance.pickingForBossTraining = true;
        owner?.ShowCollection();
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
        {
            ConfirmationDialog.StyleAsButton(dailyRewardButtonBg);
            dailyRewardButtonBg.color = claimed ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Color.white; // тускло-серый поверх той же рамки, пока не получена
        }
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
        if (buildingHotspotsContainer == null || BuildingManager.Instance == null) return;

        foreach (Transform child in buildingHotspotsContainer)
            Destroy(child.gameObject);

        var allBuildings = BuildingManager.Instance.allBuildings;
        for (int i = 0; i < allBuildings.Length; i++)
        {
            var building = allBuildings[i];
            if (building == null) continue;

            CreateBuildingHotspot(building, building.mapPosition);
        }

        CreateTrainingZoneHotspot();
    }

    // Не BuildingData — Boss Training не участвует в экономике (нет стоимости постройки/апгрейда,
    // всегда доступен), поэтому вместо полноценного здания это отдельный хотспот с тем же артом
    // (Training zone.png), клик сразу открывает пикер героя вместо попапа со статусом/действиями —
    // тот же OnBossTrainingClicked, что раньше висел на нав-кнопке "Boss Training" (см. EnsurePanel).
    private void CreateTrainingZoneHotspot()
    {
        var sprite = Resources.Load<Sprite>("UI/Castle/Training zone");
        if (sprite == null) return;

        var hotspotObj = new GameObject("TrainingZone", typeof(RectTransform));
        var hotspotRect = (RectTransform)hotspotObj.transform;
        hotspotRect.SetParent(buildingHotspotsContainer, false);
        hotspotRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotspotRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotspotRect.pivot = new Vector2(0.5f, 0.5f);
        hotspotRect.sizeDelta = new Vector2(312, 292);
        hotspotRect.anchoredPosition = new Vector2(-316, -278);

        var img = hotspotObj.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;

        var btn = hotspotObj.AddComponent<Button>();
        btn.onClick.AddListener(OnBossTrainingClicked);
    }

    // Маленький кликабельный маркер здания прямо на фоне сцены (вместо целой карточки в сетке) —
    // клик открывает попап с полным статусом/действиями, см. OpenBuildingDetailPopup.
    private void CreateBuildingHotspot(BuildingData building, Vector2 position)
    {
        var manager = BuildingManager.Instance;
        bool unlocked = manager.IsUnlocked(building);
        bool built = manager.IsBuilt(building.buildingId);

        // Здание уже нарисовано прямо на BaseBackground.png (сейчас — Altar) — никакого маркера поверх,
        // только невидимая клик-зона в том месте, где оно и так видно на фоне.
        if (building.builtIntoBackground)
        {
            CreateInvisibleHotspot(building, position);
            return;
        }

        // Option B: если на BuildingData заведён прозрачный спрайт здания — хотспотом становится сам
        // спрайт (клик-зона по его размеру, без фоновой плашки/иконки/подписи). Пока mapSprite не задан —
        // старый маркер-заглушка ниже, чтобы ничего не ломалось до того как арт заведут в проект.
        Sprite mapSprite = built
            ? building.mapSprite
            : (building.mapSpriteNotBuilt != null ? building.mapSpriteNotBuilt : building.mapSprite);
        if (mapSprite != null)
        {
            CreateBuildingSpriteHotspot(building, position, mapSprite, unlocked);
            return;
        }

        var hotspotObj = new GameObject(building.buildingId, typeof(RectTransform));
        var hotspotRect = (RectTransform)hotspotObj.transform;
        hotspotRect.SetParent(buildingHotspotsContainer, false);
        hotspotRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotspotRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotspotRect.pivot = new Vector2(0.5f, 0.5f);
        hotspotRect.sizeDelta = new Vector2(150, 150);
        hotspotRect.anchoredPosition = position;

        var bg = hotspotObj.AddComponent<Image>();
        bg.color = unlocked ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.6f);
        var btn = hotspotObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OpenBuildingDetailPopup(building));

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(hotspotRect, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(14, 30);
        iconRect.offsetMax = new Vector2(-14, -14);
        var icon = iconObj.AddComponent<Image>();
        icon.sprite = building.icon;
        icon.preserveAspect = true;
        icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);

        var nameObj = new GameObject("Name", typeof(RectTransform));
        var nameRect = (RectTransform)nameObj.transform;
        nameRect.SetParent(hotspotRect, false);
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0);
        nameRect.pivot = new Vector2(0.5f, 0);
        nameRect.sizeDelta = new Vector2(0, 26);
        nameRect.anchoredPosition = new Vector2(0, 2);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = building.buildingName;
        nameText.fontSize = 15;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
    }

    // Option B-хотспот: сам building.mapSprite и есть маркер на базе, никакого бокса/подписи вокруг —
    // здание должно быть узнаваемо по самой картинке. Клик-зона = mapSpriteSize.
    private void CreateBuildingSpriteHotspot(BuildingData building, Vector2 position, Sprite sprite, bool unlocked)
    {
        var hotspotObj = new GameObject(building.buildingId, typeof(RectTransform));
        var hotspotRect = (RectTransform)hotspotObj.transform;
        hotspotRect.SetParent(buildingHotspotsContainer, false);
        hotspotRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotspotRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotspotRect.pivot = new Vector2(0.5f, 0.5f);
        hotspotRect.sizeDelta = building.mapSpriteSize;
        hotspotRect.anchoredPosition = position;

        var img = hotspotObj.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.4f); // затемнено, пока не разблокировано — та же конвенция, что у старого маркера

        var btn = hotspotObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OpenBuildingDetailPopup(building));
    }

    // builtIntoBackground-хотспот: сам building уже виден на BaseBackground.png — тут только Image с
    // alpha=0 (нужен как raycast target для Button, полностью прозрачный) поверх готового арта.
    private void CreateInvisibleHotspot(BuildingData building, Vector2 position)
    {
        var hotspotObj = new GameObject(building.buildingId, typeof(RectTransform));
        var hotspotRect = (RectTransform)hotspotObj.transform;
        hotspotRect.SetParent(buildingHotspotsContainer, false);
        hotspotRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotspotRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotspotRect.pivot = new Vector2(0.5f, 0.5f);
        hotspotRect.sizeDelta = building.mapSpriteSize;
        hotspotRect.anchoredPosition = position;

        var img = hotspotObj.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0f);

        var btn = hotspotObj.AddComponent<Button>();
        btn.onClick.AddListener(() => OpenBuildingDetailPopup(building));
    }

    // Попап с полным статусом/действиями одного здания — то же самое, что раньше показывала целая
    // карточка в сетке, просто по клику на хотспот вместо "всегда видно".
    private void OpenBuildingDetailPopup(BuildingData building)
    {
        var manager = BuildingManager.Instance;
        if (manager == null || canvasRoot == null) return;

        bool unlocked = manager.IsUnlocked(building);
        bool built = manager.IsBuilt(building.buildingId);

        string status;
        System.Action<RectTransform> addActions = null;

        if (!unlocked)
        {
            string requirement = building.unlockType switch
            {
                BuildingUnlockType.TerritoryOpened => $"Requires {building.requiredTerritory} territory opened",
                BuildingUnlockType.TerritoryCompleted => $"Requires {building.requiredTerritory} territory cleared",
                _ => $"Requires account level {building.requiredAccountLevel}",
            };
            status = $"Locked\n{requirement}";
        }
        else if (!built)
        {
            status = $"Not built\nCost: {building.buildCostWood} Wood, {building.buildCostStone} Stone";
            addActions = actionRow => CreateActionButton(actionRow, "Build", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 160), () =>
            {
                manager.Build(building);
                CloseBuildingDetailPopup();
                Refresh();
            });
        }
        else
        {
            var ownership = manager.GetOwnership(building.buildingId);
            int level = ownership != null ? ownership.level : 1;

            if (building.buildingType == BuildingType.Forge || building.buildingType == BuildingType.Altar)
            {
                status = $"Level {level}";
                addActions = actionRow => CreateActionButton(actionRow, "Summon", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 160), () =>
                {
                    CloseBuildingDetailPopup();
                    if (summonUI != null) summonUI.Open(building, canvasRoot, Refresh);
                });
            }
            else if (building.buildingType == BuildingType.SquadCapacity)
            {
                int squadWeight = HeroCollectionManager.BaseSquadWeight + building.GetSquadWeightBonus(level);
                status = $"Level {level}\nSquad weight capacity: {squadWeight}";

                if (level < building.maxLevel)
                {
                    var (wood, stone) = building.GetUpgradeCost(level + 1);
                    addActions = actionRow => CreateActionButton(actionRow, $"Upgrade\n({wood}W/{stone}S)", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 160), () =>
                    {
                        manager.UpgradeBuilding(building);
                        CloseBuildingDetailPopup();
                        Refresh();
                    });
                }
                else
                {
                    status += "\nMAX";
                }
            }
            else
            {
                float pending = manager.GetPendingAmount(building);
                int cap = building.GetStorageCap(level);
                status = $"Level {level}\n{Mathf.FloorToInt(pending)}/{cap} {building.producedCurrency}";

                var (wood, stone) = building.GetUpgradeCost(level + 1);
                addActions = actionRow =>
                {
                    CreateActionButton(actionRow, "Collect", new Vector2(0, 0), new Vector2(0.48f, 0), new Vector2(0, 160), () =>
                    {
                        manager.CollectProduction(building);
                        CloseBuildingDetailPopup();
                        Refresh();
                    });

                    CreateActionButton(actionRow, $"Upgrade\n({wood}W/{stone}S)", new Vector2(0.52f, 0), new Vector2(1, 0), new Vector2(0, 160), () =>
                    {
                        manager.UpgradeBuilding(building);
                        CloseBuildingDetailPopup();
                        Refresh();
                    });
                };
            }
        }

        BuildBuildingDetailPopupWindow(building, status, addActions);
    }

    private void BuildBuildingDetailPopupWindow(BuildingData building, string status, System.Action<RectTransform> addActions)
    {
        CloseBuildingDetailPopup();

        buildingDetailPopupRoot = new GameObject("BuildingDetailPopup", typeof(RectTransform));
        var overlayRect = (RectTransform)buildingDetailPopupRoot.transform;
        overlayRect.SetParent(canvasRoot, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        var dim = buildingDetailPopupRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        var dimBtn = buildingDetailPopupRoot.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(CloseBuildingDetailPopup);

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        // Ширина не буквально x2 (700→1400 вылезло бы за пределы канваса 1080 шириной) — расширено
        // настолько, насколько влезает с отступами; высота и весь остальной масштаб (текст/кнопки) — x2.
        windowRect.sizeDelta = new Vector2(1000, 1040);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);
        var windowBlocker = windowObj.AddComponent<Button>();
        windowBlocker.transition = Selectable.Transition.None;

        // Иконка здания убрана из попапа — само здание уже видно на карте базы (Option B mapSprite/фон),
        // дублировать его отдельной иконкой сверху попапа больше не нужно. Name/Status подняты вверх
        // на освободившееся место.
        var nameObj = new GameObject("Name", typeof(RectTransform));
        var nameRect = (RectTransform)nameObj.transform;
        nameRect.SetParent(windowRect, false);
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.sizeDelta = new Vector2(-80, 72);
        nameRect.anchoredPosition = new Vector2(0, -70);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = building.buildingName;
        nameText.fontSize = 56;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;

        var statusObj = new GameObject("Status", typeof(RectTransform));
        var statusRect = (RectTransform)statusObj.transform;
        statusRect.SetParent(windowRect, false);
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.pivot = new Vector2(0.5f, 1);
        statusRect.sizeDelta = new Vector2(-120, 440);
        statusRect.anchoredPosition = new Vector2(0, -180);
        var statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.text = status;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = new Color(1f, 1f, 1f, 0.85f);
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 28;
        statusText.fontSizeMax = 48;

        // Действия (Build/Summon/Collect/Upgrade) сидят в отдельном узком ряду выше кнопки Close, а не
        // прямо в windowRect — CreateActionButton всегда якорит себя к низу СВОЕГО parent'а с фиксированным
        // отступом, так что без этой прокладки кнопка легла бы поверх Close.
        var actionRowObj = new GameObject("ActionRow", typeof(RectTransform));
        var actionRowRect = (RectTransform)actionRowObj.transform;
        actionRowRect.SetParent(windowRect, false);
        actionRowRect.anchorMin = new Vector2(0, 0);
        actionRowRect.anchorMax = new Vector2(1, 0);
        actionRowRect.pivot = new Vector2(0.5f, 0);
        actionRowRect.sizeDelta = new Vector2(-120, 200);
        actionRowRect.anchoredPosition = new Vector2(0, 200);
        addActions?.Invoke(actionRowRect);

        var closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        var closeBtnRect = (RectTransform)closeBtnObj.transform;
        closeBtnRect.SetParent(windowRect, false);
        closeBtnRect.anchorMin = new Vector2(0.5f, 0);
        closeBtnRect.anchorMax = new Vector2(0.5f, 0);
        closeBtnRect.pivot = new Vector2(0.5f, 0);
        closeBtnRect.sizeDelta = new Vector2(440, 140);
        closeBtnRect.anchoredPosition = new Vector2(0, 40);
        var closeBtnImg = closeBtnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(closeBtnImg);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseBuildingDetailPopup);

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
    }

    private void CloseBuildingDetailPopup()
    {
        if (buildingDetailPopupRoot != null)
        {
            Destroy(buildingDetailPopupRoot);
            buildingDetailPopupRoot = null;
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
        btnRect.anchoredPosition = new Vector2(0, 12);

        var img = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(img);
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
        text.color = ConfirmationDialog.ButtonTextColor; // тёмный текст был под старую светлую заливку, на новой тёмно-синей рамке нужен светлый
        text.enableAutoSizing = true; // строка с ценой (Wood/Stone) разной длины в зависимости от уровня — фиксированный размер либо мелкий, либо не влезает
        text.fontSizeMin = 16;
        text.fontSizeMax = 28;
    }
}
