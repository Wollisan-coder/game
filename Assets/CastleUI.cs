using System.Collections.Generic;
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
    private readonly Dictionary<CurrencyType, TMP_Text> currencyTexts = new Dictionary<CurrencyType, TMP_Text>();
    private TMP_Text accountText;
    private TMP_Text energyText;

    [Header("2D-сцена базы")]
    public Image baseBackground;

    private CastleSummonUI summonUI;
    private ProgressExchangeUI exchangeUI;
    private DebugConstructorUI debugConstructorUI;
    private DailyQuestUI dailyQuestUI;
    private AchievementUI achievementUI;
    private DeathDungeonEntryUI deathDungeonEntryUI;
    private DeathDungeonMapUI deathDungeonMapUI;
    private TerritoryMinesUI territoryMinesUI;
    private GameObject questsBadge;
    private GameObject achieveBadge;
    private GameObject mineThreatBadge; // красный "!" — активная угроза шахтам (см. MineThreatManager)
    private GameObject mineCapBadge;    // оранжевый "MAX" — у хотя бы одной шахты переполнен склад (см. BuildingManager.HasAnyMineAtCap)

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

        var currencyAreaObj = new GameObject("CurrencyBar", typeof(RectTransform));
        var currencyAreaRect = (RectTransform)currencyAreaObj.transform;
        currencyAreaRect.SetParent(topBarRect, false);
        currencyAreaRect.anchorMin = new Vector2(0, 0);
        currencyAreaRect.anchorMax = new Vector2(0.6f, 1);
        currencyAreaRect.offsetMin = new Vector2(16, 0);
        currencyAreaRect.offsetMax = new Vector2(0, 0);
        CreateCurrencyBar(currencyAreaRect);

        // Energy — иконка+число, фиксированный слот у правого края topBar. accountText (Lvl/XP) занимает
        // остаток области 0.6-1 слева от этого слота, тоже правым выравниванием.
        const float energySlotWidth = 140f;
        var energyObj = new GameObject("Energy", typeof(RectTransform));
        var energyRect = (RectTransform)energyObj.transform;
        energyRect.SetParent(topBarRect, false);
        energyRect.anchorMin = new Vector2(1, 0.5f);
        energyRect.anchorMax = new Vector2(1, 0.5f);
        energyRect.pivot = new Vector2(1, 0.5f);
        energyRect.sizeDelta = new Vector2(energySlotWidth, 50);
        energyRect.anchoredPosition = new Vector2(-16, 0);

        var energyIconObj = new GameObject("Icon", typeof(RectTransform));
        var energyIconRect = (RectTransform)energyIconObj.transform;
        energyIconRect.SetParent(energyRect, false);
        energyIconRect.anchorMin = new Vector2(0, 0.5f);
        energyIconRect.anchorMax = new Vector2(0, 0.5f);
        energyIconRect.pivot = new Vector2(0, 0.5f);
        energyIconRect.sizeDelta = new Vector2(30, 44);
        energyIconRect.anchoredPosition = Vector2.zero;
        var energyIconImg = energyIconObj.AddComponent<Image>();
        energyIconImg.sprite = Resources.Load<Sprite>("UI/Currency/Energy");
        energyIconImg.preserveAspect = true;

        var energyTextObj = new GameObject("Text", typeof(RectTransform));
        var energyTextRect = (RectTransform)energyTextObj.transform;
        energyTextRect.SetParent(energyRect, false);
        energyTextRect.anchorMin = new Vector2(0, 0.5f);
        energyTextRect.anchorMax = new Vector2(1, 0.5f);
        energyTextRect.pivot = new Vector2(0, 0.5f);
        energyTextRect.offsetMin = new Vector2(36, -25);
        energyTextRect.offsetMax = new Vector2(0, 25);
        energyText = energyTextObj.AddComponent<TextMeshProUGUI>();
        energyText.fontSize = 20;
        energyText.alignment = TextAlignmentOptions.MidlineLeft;
        energyText.color = Color.white;

        var accountObj = new GameObject("AccountText", typeof(RectTransform));
        var accountRect = (RectTransform)accountObj.transform;
        accountRect.SetParent(topBarRect, false);
        accountRect.anchorMin = new Vector2(0.6f, 0);
        accountRect.anchorMax = new Vector2(1, 1);
        accountRect.offsetMin = new Vector2(0, 0);
        accountRect.offsetMax = new Vector2(-(energySlotWidth + 24), 0);
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

        // TopBar создаётся в коде раньше BaseBackground/хотспотов — без этого более поздний sibling
        // (фон базы) рисуется поверх и полностью перекрывает панель ресурсов.
        topBarRect.SetAsLastSibling();

        // --- Навигация — так же, как кнопки внизу SquadPanel: якорь (0.5,0), размер 200x100, y=50 ---
        CreateNavButton(panelRect, "Squad", new Vector2(6, 55), () => { owner?.ShowSquad(); });
        CreateNavButton(panelRect, "Inventory", new Vector2(-425, 55), () => { owner?.ShowItemCollection(); });
        CreateNavButton(panelRect, "Collection", new Vector2(-210, 55), () => { owner?.ShowCollection(); });
        CreateNavButton(panelRect, "Castle", new Vector2(221, 55), () => { owner?.ShowCastle(); });
        CreateNavButton(panelRect, "Map", new Vector2(436, 55), () => { owner?.ShowWorldMap(); });
        // Daily Reward перестал быть отдельной кнопкой — теперь это первая строка внутри попапа Quests
        // (DailyQuestUI), рядом с остальными дневными наградами.
        questsBadge = CreateBareIconButton(panelRect, "UI/Castle/DailyQIcon", new Vector2(482.9f, 723f), new Vector2(100, 200), () => { dailyQuestUI?.Open(canvasRoot, RefreshNotificationBadges); });
        achieveBadge = CreateBareIconButton(panelRect, "UI/Castle/AchivIcon", new Vector2(486f, 1526f), new Vector2(100, 200), () => { achievementUI?.Open(canvasRoot, RefreshNotificationBadges); });
        // Second row above the first — keeps the already-working bottom row untouched instead of
        // squeezing a 5th button into it. Boss Training moved off this row onto its own map hotspot
        // (Training zone.png, see CreateTrainingZoneHotspot) once the user provided real art for it.
        CreateNavButton(panelRect, "Shop", new Vector2(0, 160), () => { exchangeUI?.Open(canvasRoot, Refresh); });
        // Debug Constructor (бэклог 2026-08-10 #6) — тестовый отряд/шмот без гринда, см. DebugConstructorUI.
        CreateNavButton(panelRect, "Debug", new Vector2(220, 160), () => { debugConstructorUI?.Open(canvasRoot); });
        // Mines — обратно в Замок (была временно на World Map). Реальный арт — Resources/UI/Castle/Mines.png.
        CreateMinesIconButton(panelRect, new Vector2(486, 470));

        summonUI = gameObject.AddComponent<CastleSummonUI>();
        exchangeUI = gameObject.AddComponent<ProgressExchangeUI>();
        debugConstructorUI = gameObject.AddComponent<DebugConstructorUI>();
        dailyQuestUI = gameObject.AddComponent<DailyQuestUI>();
        achievementUI = gameObject.AddComponent<AchievementUI>();
        deathDungeonEntryUI = gameObject.AddComponent<DeathDungeonEntryUI>();
        deathDungeonMapUI = gameObject.AddComponent<DeathDungeonMapUI>();
        gameObject.AddComponent<DeathDungeonRetreatSelectionUI>();
        territoryMinesUI = gameObject.AddComponent<TerritoryMinesUI>();

        panelRoot.SetActive(false);
    }

    // Иконка+число на каждую валюту вместо одной текстовой строки "Wood: X   Stone: Y   ...".
    // Слоты идут подряд слева направо внутри currencyAreaRect (0.6 ширины topBar), фиксированная
    // ширина слота — 5 валют детерминированно, авто-layout тут не нужен.
    private void CreateCurrencyBar(RectTransform parent)
    {
        var entries = new (CurrencyType type, string iconResourcePath)[]
        {
            (CurrencyType.Wood, "UI/Currency/Wood"),
            (CurrencyType.Stone, "UI/Currency/Stone"),
            (CurrencyType.SummonShards, "UI/Currency/Shards"),
            (CurrencyType.PremiumGems, "UI/Currency/Gems"),
            (CurrencyType.ProgressPoints, "UI/Currency/PP"),
        };

        const float slotWidth = 126f;
        for (int i = 0; i < entries.Length; i++)
        {
            var (type, iconResourcePath) = entries[i];

            var slotObj = new GameObject($"Currency_{type}", typeof(RectTransform));
            var slotRect = (RectTransform)slotObj.transform;
            slotRect.SetParent(parent, false);
            slotRect.anchorMin = new Vector2(0, 0.5f);
            slotRect.anchorMax = new Vector2(0, 0.5f);
            slotRect.pivot = new Vector2(0, 0.5f);
            slotRect.sizeDelta = new Vector2(slotWidth, 50);
            slotRect.anchoredPosition = new Vector2(i * slotWidth, 0);

            var iconObj = new GameObject("Icon", typeof(RectTransform));
            var iconRect = (RectTransform)iconObj.transform;
            iconRect.SetParent(slotRect, false);
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(44, 44);
            iconRect.anchoredPosition = Vector2.zero;
            var iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>(iconResourcePath);
            iconImg.preserveAspect = true;

            var textObj = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(slotRect, false);
            textRect.anchorMin = new Vector2(0, 0.5f);
            textRect.anchorMax = new Vector2(0, 0.5f);
            textRect.pivot = new Vector2(0, 0.5f);
            textRect.sizeDelta = new Vector2(slotWidth - 48, 50);
            textRect.anchoredPosition = new Vector2(48, 0);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;

            currencyTexts[type] = text;
        }
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

    // Кнопка = сама иконка (Achiv.png/DailyQ.png), без рамки/подписи — preserveAspect не даёт исказиться
    // при непортретном sizeDelta. Возвращает GameObject бейджа-уведомления (красный кружок) — скрыт по
    // умолчанию, показывается/прячется снаружи (см. RefreshNotificationBadges) по DailyQuestManager/
    // AchievementManager HasAnyClaimable — сигнал "тут есть что забрать", без открытия самого попапа.
    private GameObject CreateBareIconButton(RectTransform parent, string iconResourcePath, Vector2 anchoredPosition, Vector2 size, System.Action onClick)
    {
        // Мягкое жёлтое "пятно" позади иконки — отдельный sibling ПЕРЕД кнопкой (рендерится раньше =
        // ниже по слоям), крупнее самой иконки, чтобы выступать за края и не сливаться с фоном базы.
        // Glow по силуэту самой иконки (GetSilhouetteGlowSprite), а не квадрат/круг позади неё — тот же
        // приём, что и у Mines (см. CreateMinesIconButton). Требует Read/Write Enabled на исходнике
        // (см. UIAssetImportSetup) — если текстура не readable, само тихо откатится на радиальное пятно.
        var glowObj = new GameObject("Glow", typeof(RectTransform));
        var glowRect = (RectTransform)glowObj.transform;
        glowRect.SetParent(parent, false);
        glowRect.anchorMin = new Vector2(0.5f, 0f);
        glowRect.anchorMax = new Vector2(0.5f, 0f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = size * 1.5f;
        glowRect.anchoredPosition = anchoredPosition;
        var glowImg = glowObj.AddComponent<Image>();
        glowImg.sprite = GetSilhouetteGlowSprite(iconResourcePath);
        glowImg.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        glowImg.raycastTarget = false;
        glowImg.preserveAspect = true; // та же пропорция, что и у иконки — иначе форма glow "поплывёт"

        var btnObj = new GameObject("Icon", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = size;
        btnRect.anchoredPosition = anchoredPosition;

        var iconImg = btnObj.AddComponent<Image>();
        iconImg.sprite = Resources.Load<Sprite>(iconResourcePath);
        iconImg.preserveAspect = true;
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var badgeObj = new GameObject("Badge", typeof(RectTransform));
        var badgeRect = (RectTransform)badgeObj.transform;
        badgeRect.SetParent(btnRect, false);
        badgeRect.anchorMin = new Vector2(1, 1);
        badgeRect.anchorMax = new Vector2(1, 1);
        badgeRect.pivot = new Vector2(1, 1);
        badgeRect.sizeDelta = new Vector2(32, 32);
        badgeRect.anchoredPosition = new Vector2(0, -15);
        var badgeImg = badgeObj.AddComponent<Image>();
        badgeImg.sprite = GetRoundBadgeSprite();
        badgeImg.color = new Color(0.85f, 0.15f, 0.15f, 1f);

        var badgeTextObj = new GameObject("Text", typeof(RectTransform));
        var badgeTextRect = (RectTransform)badgeTextObj.transform;
        badgeTextRect.SetParent(badgeRect, false);
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.offsetMin = Vector2.zero;
        badgeTextRect.offsetMax = Vector2.zero;
        var badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        badgeText.text = "!";
        badgeText.fontSize = 22;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = Color.white;

        badgeObj.SetActive(false);
        return badgeObj;
    }

    // Иконка Mines — тот же приём, что и CreateBareIconButton (жёлтый glow позади + сама иконка), но
    // отдельным методом, а не через CreateBareIconButton, т.к. у Mines два независимых бейджа (threat/cap
    // от TerritoryMinesUI), а не один общий "есть что забрать" из CreateBareIconButton.
    private void CreateMinesIconButton(RectTransform parent, Vector2 anchoredPosition)
    {
        var size = new Vector2(100f, 200f);

        var glowObj = new GameObject("Glow", typeof(RectTransform));
        var glowRect = (RectTransform)glowObj.transform;
        glowRect.SetParent(parent, false);
        glowRect.anchorMin = new Vector2(0.5f, 0f);
        glowRect.anchorMax = new Vector2(0.5f, 0f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = size * 1.5f;
        glowRect.anchoredPosition = anchoredPosition;
        var glowImg = glowObj.AddComponent<Image>();
        glowImg.sprite = GetSilhouetteGlowSprite("UI/Castle/Mines");
        glowImg.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        glowImg.raycastTarget = false;
        // preserveAspect обязателен: glow-текстура генерируется из исходника 1:1, у неё та же пропорция,
        // что и у иконки. Без этого при sizeDelta glow != sizeDelta иконки * ровно тот же аспект картинка
        // растянется не туда и свечение перестанет совпадать с силуэтом.
        glowImg.preserveAspect = true;

        var btnObj = new GameObject("MinesButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = size;
        btnRect.anchoredPosition = anchoredPosition;

        var bg = btnObj.AddComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("UI/Castle/Mines");
        bg.preserveAspect = true;
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => territoryMinesUI?.Open(canvasRoot, RefreshNotificationBadges));

        mineThreatBadge = CreateAlertBadge(btnRect, new Vector2(8, 10), new Color(0.85f, 0.1f, 0.1f, 0.95f), "!");
        mineCapBadge = CreateAlertBadge(btnRect, new Vector2(-30, 10), new Color(0.9f, 0.6f, 0.1f, 0.95f), "MAX");
    }

    // Минимальный самодостаточный маркер — круглый спрайт-заглушка + текст, без внешнего арта.
    // "!" = активная угроза шахтам (MineThreatManager.HasAnyActiveThreat), "MAX" = у хотя бы одной шахты
    // переполнен склад (BuildingManager.HasAnyMineAtCap) — оба обновляются вместе в RefreshNotificationBadges.
    private GameObject CreateAlertBadge(RectTransform parent, Vector2 anchoredPosition, Color color, string label)
    {
        var badgeObj = new GameObject("Badge_" + label, typeof(RectTransform));
        var badgeRect = (RectTransform)badgeObj.transform;
        badgeRect.SetParent(parent, false);
        badgeRect.anchorMin = new Vector2(1, 1);
        badgeRect.anchorMax = new Vector2(1, 1);
        badgeRect.pivot = new Vector2(1, 1);
        badgeRect.sizeDelta = new Vector2(38, 38);
        badgeRect.anchoredPosition = anchoredPosition;

        var bg = badgeObj.AddComponent<Image>();
        bg.sprite = GetRoundBadgeSprite();
        bg.color = color;

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(badgeRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = label.Length > 1 ? 12 : 24;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        badgeObj.SetActive(false);
        return badgeObj;
    }

    private static Sprite roundBadgeSprite;

    // Круглый спрайт для бейджа, сгенерированный на лету — в этой версии Unity 6 Resources
    // .GetBuiltinResource для встроенных UI-спрайтов (Knob, Checkmark и т.п.) молча возвращает null при
    // загрузке из кода (см. feedback_unity6_no_builtin_ui_sprites), так что вместо ссылки на builtin
    // рисуем свою текстуру с кругом один раз и кэшируем.
    private static Sprite GetRoundBadgeSprite()
    {
        if (roundBadgeSprite != null) return roundBadgeSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                tex.SetPixel(x, y, dist <= radius ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }
        tex.Apply();

        roundBadgeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return roundBadgeSprite;
    }

    private static Sprite radialGlowSprite;

    // Мягкое радиальное пятно (белое, непрозрачное в центре → прозрачное к краю, квадратичный спад) —
    // тонируется цветом снаружи через Image.color. Сгенерировано один раз и закэшировано.
    private static Sprite GetRadialGlowSprite()
    {
        if (radialGlowSprite != null) return radialGlowSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(1f - dist / radius);
                a *= a; // квадратичный спад — плотный центр, мягкий длинный хвост к краю
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return radialGlowSprite;
    }

    private static readonly Dictionary<string, Sprite> silhouetteGlowCache = new Dictionary<string, Sprite>();

    // Glow "по силуэту" иконки, а не квадратом/кругом позади неё (в отличие от GetRadialGlowSprite) —
    // берёт альфа-канал исходной текстуры (та должна быть Read/Write Enabled, см. UIAssetImportSetup),
    // "растит" непрозрачную область на dilate пикселей (макс-фильтр), затем размывает край на blur
    // пикселей (box blur) — получается мягкое свечение той же формы, что и сама иконка, просто крупнее.
    // Component Outline тут не подходит — он обводит bounding box текстуры целиком, а не силуэт по альфе.
    private static Sprite GetSilhouetteGlowSprite(string resourcePath, int dilate = 14, int blur = 16)
    {
        if (silhouetteGlowCache.TryGetValue(resourcePath, out var cached) && cached != null) return cached;

        var source = Resources.Load<Texture2D>(resourcePath);
        if (source == null || !source.isReadable)
        {
            Debug.LogWarning($"CastleUI: '{resourcePath}' missing or not Read/Write Enabled, falling back to radial glow");
            return GetRadialGlowSprite();
        }

        int w = source.width, h = source.height;
        var pixels = source.GetPixels();
        var alpha = new float[w * h];
        for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;

        var dilated = SeparableMaxFilter(alpha, w, h, dilate);
        var glowAlpha = SeparableBoxBlur(dilated, w, h, blur);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var outPixels = new Color[w * h];
        for (int i = 0; i < outPixels.Length; i++)
            outPixels[i] = new Color(1f, 1f, 1f, glowAlpha[i]);
        tex.SetPixels(outPixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        silhouetteGlowCache[resourcePath] = sprite;
        return sprite;
    }

    // Разделяемый макс-фильтр (горизонтальный проход, затем вертикальный) — O(w*h*radius) вместо
    // O(w*h*radius^2) у честного 2D-варианта. "Растит" непрозрачные области наружу (дилейт).
    private static float[] SeparableMaxFilter(float[] src, int w, int h, int radius)
    {
        if (radius <= 0) return (float[])src.Clone();

        var temp = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float max = 0f;
                int xMin = Mathf.Max(0, x - radius), xMax = Mathf.Min(w - 1, x + radius);
                for (int xx = xMin; xx <= xMax; xx++) max = Mathf.Max(max, src[y * w + xx]);
                temp[y * w + x] = max;
            }
        }

        var result = new float[w * h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float max = 0f;
                int yMin = Mathf.Max(0, y - radius), yMax = Mathf.Min(h - 1, y + radius);
                for (int yy = yMin; yy <= yMax; yy++) max = Mathf.Max(max, temp[yy * w + x]);
                result[y * w + x] = max;
            }
        }
        return result;
    }

    // Разделяемый box blur — смягчает жёсткий край после дилейта в градиент затухания свечения.
    private static float[] SeparableBoxBlur(float[] src, int w, int h, int radius)
    {
        if (radius <= 0) return (float[])src.Clone();

        var temp = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                int xMin = Mathf.Max(0, x - radius), xMax = Mathf.Min(w - 1, x + radius);
                for (int xx = xMin; xx <= xMax; xx++) sum += src[y * w + xx];
                temp[y * w + x] = sum / (xMax - xMin + 1);
            }
        }

        var result = new float[w * h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                int yMin = Mathf.Max(0, y - radius), yMax = Mathf.Min(h - 1, y + radius);
                for (int yy = yMin; yy <= yMax; yy++) sum += temp[yy * w + x];
                result[y * w + x] = sum / (yMax - yMin + 1);
            }
        }
        return result;
    }

    // Отдельно от CreateNavButton — держит ссылки на bg/text, чтобы Refresh() мог менять подпись/цвет
    // в зависимости от того, забирали ли награду сегодня.
    // Открывает Collection как пикер героя для тренировки (тот же приём, что и выбор героя в слот отряда —
    // HeroCollectionManager.pickingForBossTraining, см. HeroCollectionUI.OnCardSelected).
    // Лимит 1/день отключён по просьбе пользователя — AccountManager.HasDoneBossTrainingToday()/
    // MarkBossTrainingDone() оставлены нетронутыми на случай, если лимит понадобится вернуть.
    private void OnBossTrainingClicked()
    {
        if (AccountManager.Instance == null || HeroCollectionManager.Instance == null) return;

        HeroCollectionManager.Instance.pickingForBossTraining = true;
        owner?.ShowCollection();
    }

    public void Refresh()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        AccountManager.Instance?.RegenerateEnergyFromElapsedTime();

        if (PlayerCurrencies.Instance != null)
        {
            foreach (var kvp in currencyTexts)
                kvp.Value.text = PlayerCurrencies.Instance.GetBalance(kvp.Key).ToString();
        }

        if (accountText != null && AccountManager.Instance != null)
        {
            var acc = AccountManager.Instance;
            accountText.text = $"Lvl {acc.level} ({acc.experience}/{acc.ExperienceToNextLevel(acc.level)})";
            if (energyText != null)
                energyText.text = $"{acc.currentEnergy}/{acc.MaxEnergy}";
        }

        RefreshNotificationBadges();
        PopulateBuildings();
    }

    private void RefreshNotificationBadges()
    {
        if (questsBadge != null)
            questsBadge.SetActive(DailyQuestManager.Instance != null && DailyQuestManager.Instance.HasAnyClaimable);
        if (achieveBadge != null)
            achieveBadge.SetActive(AchievementManager.Instance != null && AchievementManager.Instance.HasAnyClaimable());
        if (mineThreatBadge != null)
            mineThreatBadge.SetActive(MineThreatManager.Instance != null && MineThreatManager.Instance.HasAnyActiveThreat);
        if (mineCapBadge != null)
            mineCapBadge.SetActive(BuildingManager.Instance != null && BuildingManager.Instance.HasAnyMineAtCap());
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
            // TerritoryMine_* — экономика шахт территорий, управляется только через свой отдельный экран
            // (см. TerritoryMinesUI), никогда не должны показываться хотспотом на базе Замка (без этого
            // все 24 маленьких здания рисовались тут заглушками-боксами друг на друге в (0,0), т.к. у них
            // нет mapSprite/mapPosition — те просто не заводились для этой цели).
            if (building.buildingId.StartsWith("TerritoryMine_")) continue;

            CreateBuildingHotspot(building, building.mapPosition);
        }

        CreateTrainingZoneHotspot();
        CreateDeathDungeonHotspot();
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

    // Первый кусок Death Dungeon (см. project_death_dungeon_concept/project_death_dungeon_implementation) —
    // клик открывает экран подготовки (DeathDungeonEntryUI: отряд/жертвы/предупреждение), сам гаунтлет/
    // баффы/permadeath/ретрит/награды — ещё впереди отдельными шагами.
    private void CreateDeathDungeonHotspot()
    {
        var sprite = Resources.Load<Sprite>("UI/Castle/DeathDung");
        if (sprite == null) return;

        var hotspotObj = new GameObject("DeathDungeon", typeof(RectTransform));
        var hotspotRect = (RectTransform)hotspotObj.transform;
        hotspotRect.SetParent(buildingHotspotsContainer, false);
        hotspotRect.anchorMin = new Vector2(0.5f, 0.5f);
        hotspotRect.anchorMax = new Vector2(0.5f, 0.5f);
        hotspotRect.pivot = new Vector2(0.5f, 0.5f);
        hotspotRect.sizeDelta = new Vector2(200, 340); // соотношение сторон реального арта (383x651)
        hotspotRect.anchoredPosition = new Vector2(-457, 702); // зеркально TrainingZone по X — поправить в редакторе при необходимости

        var img = hotspotObj.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;

        var btn = hotspotObj.AddComponent<Button>();
        btn.onClick.AddListener(OnDeathDungeonClicked);
    }

    private void OnDeathDungeonClicked()
    {
        // Недельная блокировка после ретрита (см. DeathDungeonManager.ApplyRetreatConsequence) — не
        // штрафуем игрока за нормальный клин clear, только за спасение бегством.
        if (DeathDungeonManager.Instance != null && DeathDungeonManager.Instance.IsRetreatLockoutActive)
        {
            var remaining = System.TimeSpan.FromTicks(DeathDungeonManager.Instance.retreatLockoutExpiryTicks - System.DateTime.UtcNow.Ticks);
            int daysLeft = Mathf.Max(1, Mathf.CeilToInt((float)remaining.TotalDays));
            ConfirmationDialog.ShowInfo(canvasRoot, $"The Death Dungeon is sealed to you for {daysLeft} more day(s) after your retreat.");
            return;
        }

        deathDungeonEntryUI?.Open(canvasRoot, owner);
    }

    // Вызывается MainMenuUI.Start() после возврата с боя-узла гаунтлета (returningFromDeathDungeon) —
    // если забег ещё не завершён, сразу показываем карту вместо голого Замка.
    public void ReopenDeathDungeonMapIfActive()
    {
        if (DeathDungeonManager.Instance != null && DeathDungeonManager.Instance.IsRunActive)
            deathDungeonMapUI?.Open(canvasRoot, owner);
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
        System.Action<RectTransform> addCostRow = null;

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
            status = "Not built";
            int costWood = building.buildCostWood;
            int costStone = building.buildCostStone;
            addCostRow = row => BuildCostIconRow(row, costWood, costStone);
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

                // level < maxLevel — тот же guard, что уже есть у ветки SquadCapacity выше. Без него для
                // любого производственного здания с maxLevel==1 (сейчас таких в Замке нет, но раньше сюда
                // по ошибке попадали TerritoryMine_* — см. фильтр в PopulateBuildings) кнопка Upgrade
                // показывалась всегда, хотя UpgradeBuilding молча отказывала — обманчивая нерабочая кнопка.
                if (level < building.maxLevel)
                {
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
                else
                {
                    status += "\nMAX";
                    addActions = actionRow => CreateActionButton(actionRow, "Collect", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 160), () =>
                    {
                        manager.CollectProduction(building);
                        CloseBuildingDetailPopup();
                        Refresh();
                    });
                }
            }
        }

        BuildBuildingDetailPopupWindow(building, status, addActions, addCostRow);
    }

    private void BuildBuildingDetailPopupWindow(BuildingData building, string status, System.Action<RectTransform> addActions, System.Action<RectTransform> addCostRow = null)
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

        // Иконки Wood/Stone стоимости постройки — узкий ряд под статусом, только когда addCostRow задан
        // (сейчас только для "Not built", см. OpenBuildingDetailPopup) — заменяет прежний плоский текст
        // "Cost: N Wood, N Stone" внутри самого status.
        if (addCostRow != null)
        {
            var costRowObj = new GameObject("CostRow", typeof(RectTransform));
            var costRowRect = (RectTransform)costRowObj.transform;
            costRowRect.SetParent(windowRect, false);
            costRowRect.anchorMin = new Vector2(0.5f, 0f);
            costRowRect.anchorMax = new Vector2(0.5f, 0f);
            costRowRect.pivot = new Vector2(0.5f, 0f);
            costRowRect.sizeDelta = new Vector2(400, 60);
            costRowRect.anchoredPosition = new Vector2(0, 420);
            addCostRow(costRowRect);
        }

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

    // [Wood-иконка] N  [Stone-иконка] N — заменяет прежний плоский текст "Cost: N Wood, N Stone" в попапе
    // "Not built" (см. OpenBuildingDetailPopup/BuildBuildingDetailPopupWindow). Использует общий примитив
    // ConfirmationDialog.CreateCurrencyIcon вместо третьей копии загрузки Resources.Load<Sprite> в этом файле.
    private void BuildCostIconRow(RectTransform row, int wood, int stone)
    {
        const float iconSize = 40f;
        const float textWidth = 70f;
        const float pairGap = 24f;
        float x = 0f;

        ConfirmationDialog.CreateCurrencyIcon(row, ConfirmationDialog.GetCurrencyIconPath(CurrencyType.Wood), new Vector2(x, 0), iconSize);
        x += iconSize + 8f;
        CreateInlineAmountText(row, x, textWidth, wood.ToString());
        x += textWidth + pairGap;

        ConfirmationDialog.CreateCurrencyIcon(row, ConfirmationDialog.GetCurrencyIconPath(CurrencyType.Stone), new Vector2(x, 0), iconSize);
        x += iconSize + 8f;
        CreateInlineAmountText(row, x, textWidth, stone.ToString());
    }

    private void CreateInlineAmountText(RectTransform parent, float x, float width, string amount)
    {
        var textObj = new GameObject("Amount", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0, 0.5f);
        textRect.anchorMax = new Vector2(0, 0.5f);
        textRect.pivot = new Vector2(0, 0.5f);
        textRect.sizeDelta = new Vector2(width, 44);
        textRect.anchoredPosition = new Vector2(x, 0);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = amount;
        text.fontSize = ConfirmationDialog.BodyFontSize;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
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
