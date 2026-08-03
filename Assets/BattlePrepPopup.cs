using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Попап подтверждения перед боевой нодой карты — стоимость энергии + возможный лут (иконками, не текстом).
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

        var titleObj = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleObj.transform;
        titleRect.SetParent(windowRect, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 140);
        titleRect.anchoredPosition = new Vector2(0, -20);
        var title = titleObj.AddComponent<TextMeshProUGUI>();
        // nodeName редко заполнен вручную для рядовых боевых нод (авторить его на все 100+ нод — морока),
        // так что для них честнее показать territory+nodeIndex, чем сырой nodeId ("Beasts 0_ 5").
        title.text = !string.IsNullOrEmpty(node.nodeName) ? node.nodeName : $"{node.territory} #{node.nodeIndex}";
        title.fontSize = 84;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var energyObj = new GameObject("EnergyCost", typeof(RectTransform));
        var energyRect = (RectTransform)energyObj.transform;
        energyRect.SetParent(windowRect, false);
        energyRect.anchorMin = new Vector2(0, 1);
        energyRect.anchorMax = new Vector2(1, 1);
        energyRect.pivot = new Vector2(0.5f, 1);
        energyRect.sizeDelta = new Vector2(0, 90);
        energyRect.anchoredPosition = new Vector2(0, -180);
        var energyText = energyObj.AddComponent<TextMeshProUGUI>();
        energyText.text = $"Energy cost: {energyCost}";
        energyText.fontSize = 66;
        energyText.alignment = TextAlignmentOptions.Center;
        energyText.color = new Color(0.6f, 0.85f, 1f);

        var contentObj = new GameObject("LootContent", typeof(RectTransform));
        var contentRect = (RectTransform)contentObj.transform;
        contentRect.SetParent(windowRect, false);
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = new Vector2(30, 230);
        contentRect.offsetMax = new Vector2(-30, -290);

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 20;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        BuildLootSection(contentRect, "Possible loot:", GetPossibleLootItems(node));
        if (node.isFarmNode)
            BuildLootSection(contentRect, "Guaranteed drop:", GetFarmPoolItems(node));

        var btnObj = new GameObject("GoButton", typeof(RectTransform));
        var btnRect = (RectTransform)btnObj.transform;
        btnRect.SetParent(windowRect, false);
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.sizeDelta = new Vector2(380, 160);
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
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "В бой";
        btnText.fontSize = 78;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = ConfirmationDialog.ButtonTextColor;
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

    // Не использует ItemBadgeUtility.ApplyRarityFrame — та рамка спавнится сиблингом иконки в её
    // родителе, что ломает GridLayoutGroup (родитель здесь — сама сетка, лишний сиблинг = лишняя ячейка).
    // Вместо этого сама ячейка сетки — это цветной фон редкости, а иконка предмета лежит внутри неё с отступом.
    private static void CreateItemIcon(Transform parent, ItemData item)
    {
        var cellObj = new GameObject(item.itemName, typeof(RectTransform));
        cellObj.transform.SetParent(parent, false);
        var frameBg = cellObj.AddComponent<Image>();
        frameBg.color = RarityUtility.GetColor(item.rarity);

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
    }
}
