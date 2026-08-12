using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Попап подтверждения перед боевой нодой карты — стоимость энергии (иконка+число на кнопке "В бой"),
// вредные фишки поля (иконка+клик-описание) и возможный лут (иконками, не текстом).
// В отличие от ConfirmationDialog, клик вне окна (по затемнению) сам закрывает попап без действия.
// Строится на лету, как и остальные рантайм-попапы в проекте (ConfirmationDialog/ItemSacrificeUI).
public static class BattlePrepPopup
{
    public static void Show(Transform parent, MapNodeData node, int energyCost, System.Action onConfirmed)
    {
        if (parent == null || node == null) return;

        var overlay = new GameObject("BattlePrepPopup", typeof(RectTransform));
        var overlayRect = (RectTransform)overlay.transform;
        overlayRect.SetParent(parent, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        var dim = overlay.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        var dimBtn = overlay.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => Object.Destroy(overlay)); // клик вне окна — просто закрыть, без действия

        var windowObj = new GameObject("Window", typeof(RectTransform));
        var windowRect = (RectTransform)windowObj.transform;
        windowRect.SetParent(overlayRect, false);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(700, 1400);
        var windowBg = windowObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsPanel(windowBg);
        // Пустой Button-блокер поверх фона окна — чтобы клик по самому попапу не проваливался
        // на dim-кнопку позади и не закрывал его вместе с кликом "вне".
        var windowBlocker = windowObj.AddComponent<Button>();
        windowBlocker.transition = Selectable.Transition.None;

        // Горизонтальный отступ везде — ConfirmationDialog.WindowContentPaddingX (40), тот же стандарт,
        // что и у остальных рантайм-окон. Рамка окна (DialogWindowFrame) режется на 9-slice с border 70px
        // с каждой стороны — раньше у Title отступа не было вообще (sizeDelta.x=0, во всю ширину окна) —
        // при узком (700px) окне и крупном тексте заголовка текст реально залезал на орнамент рамки (баг
        // найден 2026-08-10). Заголовок также получил enableAutoSizing — при любой ширине окна/длине
        // текста больше не может физически переполниться, что бы ни было в node.nodeName.
        float pad = ConfirmationDialog.WindowContentPaddingX;

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(-pad * 2, 140);
        titleRect.anchoredPosition = new Vector2(0, -20);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        // nodeName редко заполнен вручную для рядовых боевых нод (авторить его на все 100+ нод — морока),
        // так что для них честнее показать territory+nodeIndex, чем сырой nodeId ("Beasts 0_ 5").
        title.text = !string.IsNullOrEmpty(node.nodeName) ? node.nodeName : $"{node.territory} #{node.nodeIndex}";
        title.enableAutoSizing = true;
        title.fontSizeMin = ConfirmationDialog.MinTextFontSize;
        title.fontSizeMax = 84;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        // Энергия переехала на саму кнопку "В бой" (иконка + число) — 2026-08-10, по просьбе пользователя.
        // Освободившееся место (было под отдельную строку EnergyCost) отдано под HarmfulTiles-секцию ниже.
        var contentObj = new GameObject("LootContent", typeof(RectTransform));
        var contentRect = (RectTransform)contentObj.transform;
        contentRect.SetParent(windowRect, false);
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = new Vector2(pad, 230);
        contentRect.offsetMax = new Vector2(-pad, -190);

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 20;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        BuildEnemyPassivesSection(contentRect, node, overlayRect);
        BuildHarmfulTilesSection(contentRect, node, overlayRect);
        BuildLootSection(contentRect, "Possible loot:", GetPossibleLootItems(node));
        if (node.isFarmNode)
            BuildLootSection(contentRect, "Guaranteed drop:", GetFarmPoolItems(node));

        var btnObj = new GameObject("GoButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(windowRect, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.sizeDelta = new Vector2(400, 180);
        btnRect.anchoredPosition = new Vector2(0, 40);
        var btnBg = btnObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(btnBg);
        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            Object.Destroy(overlay);
            onConfirmed?.Invoke();
        });

        var btnTextObj = new GameObject("Text", typeof(RectTransform));
        var btnTextRect = (RectTransform)btnTextObj.transform;
        btnTextRect.SetParent(btnRect, false);
        btnTextRect.anchorMin = new Vector2(0, 0.42f);
        btnTextRect.anchorMax = new Vector2(1, 1);
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "В бой";
        btnText.fontSize = 66;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = ConfirmationDialog.ButtonTextColor;

        // Стоимость энергии — иконка + число, нижняя часть кнопки, под подписью "В бой". Иконка и текст
        // оба на анкоре (0, 0.5) — том же, что жёстко задаёт сам ConfirmationDialog.CreateCurrencyIcon —
        // чтобы anchoredPosition считался в одной системе координат у обоих, без путаницы со сменой анкора
        // после создания (была реальная ошибка в первой версии этой правки — сдвигало иконку не туда).
        var costRowObj = new GameObject("EnergyCostRow", typeof(RectTransform));
        var costRowRect = (RectTransform)costRowObj.transform;
        costRowRect.SetParent(btnRect, false);
        costRowRect.anchorMin = new Vector2(0, 0);
        costRowRect.anchorMax = new Vector2(1, 0.4f);
        costRowRect.offsetMin = Vector2.zero;
        costRowRect.offsetMax = Vector2.zero;

        const float costIconSize = 60f;
        const float costTextWidth = 60f;
        const float costPairGap = 6f;
        float pairWidth = costIconSize + costPairGap + costTextWidth;
        float startX = (btnRect.sizeDelta.x - pairWidth) / 2f; // центрируем пару в ширине кнопки

        ConfirmationDialog.CreateCurrencyIcon(costRowRect, "UI/Currency/Energy", new Vector2(startX, 0), costIconSize);

        var costTextObj = new GameObject("Text", typeof(RectTransform));
        var costTextRect = (RectTransform)costTextObj.transform;
        costTextRect.SetParent(costRowRect, false);
        costTextRect.anchorMin = new Vector2(0, 0.5f);
        costTextRect.anchorMax = new Vector2(0, 0.5f);
        costTextRect.pivot = new Vector2(0, 0.5f);
        costTextRect.sizeDelta = new Vector2(costTextWidth, 40);
        costTextRect.anchoredPosition = new Vector2(startX + costIconSize + costPairGap, 0);
        var costText = costTextObj.AddComponent<TextMeshProUGUI>();
        costText.text = energyCost.ToString();
        costText.fontSize = ConfirmationDialog.MinTextFontSize;
        costText.fontStyle = FontStyles.Bold;
        costText.alignment = TextAlignmentOptions.MidlineLeft;
        costText.color = ConfirmationDialog.ButtonTextColor;
    }

    // Строка иконок пассивных умений врага (см. EnemyData.passives) — те же карточки-плашки, что и у
    // вредных фишек ниже, клик открывает попап с описанием (авторский текст + точная цифра эффекта,
    // см. EnemyPassiveUtility). Пропускает секцию целиком, если у врага нет пассивок.
    private static void BuildEnemyPassivesSection(Transform parent, MapNodeData node, Transform popupRoot)
    {
        var passives = node.enemy != null ? node.enemy.passives : null;
        if (passives == null || passives.Length == 0) return;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(parent, false);
        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredHeight = 50;
        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "Enemy passive:";
        labelText.fontSize = 40;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = Color.white;

        var rowObj = new GameObject("PassiveRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(parent, false);
        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 110;

        var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 16;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        foreach (var passive in passives)
        {
            if (passive == null) continue;
            CreateEnemyPassiveIcon(rowRect, passive, popupRoot);
        }
    }

    private static void CreateEnemyPassiveIcon(Transform parent, EnemyPassiveData passive, Transform popupRoot)
    {
        var cellObj = new GameObject(passive.name, typeof(RectTransform));
        var cellRect = (RectTransform)cellObj.transform;
        cellRect.SetParent(parent, false);
        cellRect.sizeDelta = new Vector2(110, 110);

        var bg = cellObj.AddComponent<Image>();
        bg.color = EnemyPassiveUtility.GetColor(passive.effectType);
        var btn = cellObj.AddComponent<Button>();
        btn.targetGraphic = bg;
        var passiveName = string.IsNullOrEmpty(passive.passiveName) ? passive.name : passive.passiveName;
        string fullText = EnemyPassiveUtility.GetFullDescription(passive);
        btn.onClick.AddListener(() => ConfirmationDialog.ShowInfo(popupRoot, fullText, title: passiveName));

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(cellRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = EnemyPassiveUtility.GetShortLabel(passive.effectType);
        text.fontSize = ConfirmationDialog.MinTextFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    // Строка иконок вредных фишек, которые враг может заспавнить в начале боя (см.
    // EnemyData.harmfulTileSpawns) — клик по иконке открывает попап с описанием этого типа. Пропускает
    // секцию целиком, если у врага таких правил нет (большинство рядовых врагов).
    private static void BuildHarmfulTilesSection(Transform parent, MapNodeData node, Transform popupRoot)
    {
        var rules = node.enemy != null ? node.enemy.harmfulTileSpawns : null;
        if (rules == null || rules.Length == 0) return;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(parent, false);
        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredHeight = 50;
        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "Harmful tiles:";
        labelText.fontSize = 40;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = Color.white;

        var rowObj = new GameObject("HarmfulTileRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowObj.transform;
        rowRect.SetParent(parent, false);
        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 110;

        var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 16;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        foreach (var rule in rules)
        {
            if (rule == null || rule.type == HarmfulTileType.None) continue;
            CreateHarmfulTileIcon(rowRect, rule, popupRoot);
        }
    }

    private static void CreateHarmfulTileIcon(Transform parent, HarmfulTileSpawnRule rule, Transform popupRoot)
    {
        var cellObj = new GameObject(rule.type.ToString(), typeof(RectTransform));
        var cellRect = (RectTransform)cellObj.transform;
        cellRect.SetParent(parent, false);
        cellRect.sizeDelta = new Vector2(110, 110);

        var bg = cellObj.AddComponent<Image>();
        var btn = cellObj.AddComponent<Button>();
        btn.targetGraphic = bg;
        var type = rule.type;
        int value = rule.value;
        btn.onClick.AddListener(() =>
            ConfirmationDialog.ShowInfo(popupRoot, HarmfulTileUtility.GetDescription(type, value), title: type.ToString()));

        // Реальный арт, если он найден (см. HarmfulTileUtility.GetIcon) — иначе старая цветная плашка с
        // коротким лейблом как fallback (типы, под которые своего арта ещё не завезли).
        var sprite = HarmfulTileUtility.GetIcon(type);
        if (sprite != null)
        {
            bg.sprite = sprite;
            bg.color = Color.white;
            bg.preserveAspect = true;
            return;
        }

        bg.color = HarmfulTileUtility.GetColor(type);

        var textObj = new GameObject("Text", typeof(RectTransform));
        var textRect = (RectTransform)textObj.transform;
        textRect.SetParent(cellRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = HarmfulTileUtility.GetShortLabel(type);
        text.fontSize = ConfirmationDialog.MinTextFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    private static ItemData[] GetPossibleLootItems(MapNodeData node)
    {
        if (node.enemy == null || node.enemy.loot == null || node.enemy.loot.items == null) return new ItemData[0];
        return node.enemy.loot.items.Where(e => e != null && e.item != null).Select(e => e.item).ToArray();
    }

    private static ItemData[] GetFarmPoolItems(MapNodeData node)
    {
        if (node.farmLootPool == null || node.farmLootPool.entries == null) return new ItemData[0];
        return node.farmLootPool.entries.Where(e => e != null && e.item != null).Select(e => e.item).ToArray();
    }

    // Пропускает секцию целиком (ни подписи, ни пустой сетки), если лута нет — большинство рядовых
    // боевых нод не имеют вручную авторенного loot.items.
    private static void BuildLootSection(Transform parent, string label, ItemData[] items)
    {
        if (items == null || items.Length == 0) return;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        labelObj.transform.SetParent(parent, false);
        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredHeight = 50;
        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 40;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = Color.white;

        var gridObj = new GameObject("IconGrid", typeof(RectTransform));
        var gridRect = (RectTransform)gridObj.transform;
        gridRect.SetParent(parent, false);
        int rows = Mathf.CeilToInt(items.Length / 4f);
        var gridLE = gridObj.AddComponent<LayoutElement>();
        gridLE.preferredHeight = rows * 126;

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(110, 110);
        grid.spacing = new Vector2(16, 16);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        foreach (var item in items)
            CreateItemIcon(gridRect, item);
    }

    // cellObj — прямой ребёнок GridLayoutGroup (сетка сама принудительно задаёт его размер под cellSize),
    // иконка вложена ещё на уровень глубже — поэтому ApplyRarityFrame (спавнит рамку сиблингом иконки в её
    // родителе, т.е. внутри cellObj, а не в самой сетке) сюда прекрасно ложится и не ломает GridLayoutGroup.
    private static void CreateItemIcon(Transform parent, ItemData item)
    {
        var cellObj = new GameObject(item.itemName, typeof(RectTransform));
        cellObj.transform.SetParent(parent, false);

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(cellObj.transform, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(6, 6);
        iconRect.offsetMax = new Vector2(-6, -6);
        var icon = iconObj.AddComponent<Image>();
        icon.sprite = item.icon;
        icon.preserveAspect = true;

        Image rarityFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(icon, RarityUtility.GetColor(item.rarity), ref rarityFrame);
    }
}
