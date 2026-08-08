#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Строит Resources/UI/WondrousArmorTile.prefab — портрет героя + бедж количества + кнопки Wear/Disenchant
// прямо на плитке. НЕ использует PickerTileUtility (как AscensionGemTilePrefabBuilder) — той не хватает
// места под 2 доп. кнопки при её фиксированных пропорциях иконка/подпись. Пропорции подогнаны под
// cellSize {149,200} общей сетки ItemCollectionUI (см. сцену) — GridLayoutGroup форсирует под неё
// sizeDelta любого прямого потомка, так что сам префаб размером с этот cellSize.
public static class WondrousArmorTilePrefabBuilder
{
    private const string OutputPath = "Assets/Resources/UI/WondrousArmorTile.prefab";

    [MenuItem("Tools/Build WondrousArmorTile Prefab")]
    public static void BuildPrefab()
    {
        var root = new GameObject("WondrousArmorTile", typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(149, 200);
        var rootBg = root.AddComponent<Image>();
        rootBg.color = new Color(1, 1, 1, 0.06f);

        // Обёртка с RectMask2D — preserveAspect только МАСШТАБИРУЕТ картинку под рамку, но не обрезает её,
        // если пропорции спрайта сильно отличаются от рамки (портретное фан-арт изображение и т.п.) —
        // без маски лишнее просто рисуется поверх соседних плиток в сетке.
        var iconMaskObj = new GameObject("IconMask", typeof(RectTransform));
        var iconMaskRect = (RectTransform)iconMaskObj.transform;
        iconMaskRect.SetParent(rootRect, false);
        iconMaskRect.anchorMin = new Vector2(0, 0.50f);
        iconMaskRect.anchorMax = new Vector2(1, 1);
        iconMaskRect.offsetMin = new Vector2(6, 0);
        iconMaskRect.offsetMax = new Vector2(-6, -6);
        iconMaskObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f); // RectMask2D нужен Graphic на этом же объекте, чтобы маскировать детей
        iconMaskObj.AddComponent<RectMask2D>();

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(iconMaskRect, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(rootRect, false);
        labelRect.anchorMin = new Vector2(0, 0.36f);
        labelRect.anchorMax = new Vector2(1, 0.50f);
        labelRect.offsetMin = new Vector2(4, 0);
        labelRect.offsetMax = new Vector2(-4, 0);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = 14;

        var wearObj = new GameObject("WearButton", typeof(RectTransform));
        var wearRect = (RectTransform)wearObj.transform;
        wearRect.SetParent(rootRect, false);
        wearRect.anchorMin = new Vector2(0, 0.19f);
        wearRect.anchorMax = new Vector2(1, 0.36f);
        wearRect.offsetMin = new Vector2(4, 0);
        wearRect.offsetMax = new Vector2(-4, 0);
        var wearImg = wearObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(wearImg);
        var wearBtn = wearObj.AddComponent<Button>();

        var wearTextObj = new GameObject("Text", typeof(RectTransform));
        var wearTextRect = (RectTransform)wearTextObj.transform;
        wearTextRect.SetParent(wearRect, false);
        wearTextRect.anchorMin = Vector2.zero;
        wearTextRect.anchorMax = Vector2.one;
        wearTextRect.offsetMin = Vector2.zero;
        wearTextRect.offsetMax = Vector2.zero;
        var wearText = wearTextObj.AddComponent<TextMeshProUGUI>();
        wearText.text = "Wear";
        wearText.fontSize = 13;
        wearText.alignment = TextAlignmentOptions.Center;
        wearText.color = ConfirmationDialog.ButtonTextColor;

        var disObj = new GameObject("DisenchantButton", typeof(RectTransform));
        var disRect = (RectTransform)disObj.transform;
        disRect.SetParent(rootRect, false);
        disRect.anchorMin = new Vector2(0, 0.02f);
        disRect.anchorMax = new Vector2(1, 0.19f);
        disRect.offsetMin = new Vector2(4, 0);
        disRect.offsetMax = new Vector2(-4, 0);
        var disImg = disObj.AddComponent<Image>();
        ConfirmationDialog.StyleAsButton(disImg);
        var disBtn = disObj.AddComponent<Button>();

        var disTextObj = new GameObject("Text", typeof(RectTransform));
        var disTextRect = (RectTransform)disTextObj.transform;
        disTextRect.SetParent(disRect, false);
        disTextRect.anchorMin = Vector2.zero;
        disTextRect.anchorMax = Vector2.one;
        disTextRect.offsetMin = Vector2.zero;
        disTextRect.offsetMax = Vector2.zero;
        var disText = disTextObj.AddComponent<TextMeshProUGUI>();
        disText.text = "Disenchant (+2)";
        disText.fontSize = 11;
        disText.alignment = TextAlignmentOptions.Center;
        disText.color = ConfirmationDialog.ButtonTextColor;

        // Бедж количества — тот же приём, что у ItemBadgeUtility.ApplyQuantityBadge (создаём вручную,
        // чтобы получить прямые ссылки на root/text для WondrousArmorTileUI).
        var badgeObj = new GameObject("QuantityBadge", typeof(RectTransform));
        var badgeRect = (RectTransform)badgeObj.transform;
        badgeRect.SetParent(rootRect, false);
        badgeRect.anchorMin = new Vector2(0, 1);
        badgeRect.anchorMax = new Vector2(0, 1);
        badgeRect.pivot = new Vector2(0, 1);
        badgeRect.sizeDelta = new Vector2(32, 18);
        badgeRect.anchoredPosition = new Vector2(2, -2);
        var badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0f, 0f, 0f, 0.65f);

        var badgeTextObj = new GameObject("Text", typeof(RectTransform));
        var badgeTextRect = (RectTransform)badgeTextObj.transform;
        badgeTextRect.SetParent(badgeRect, false);
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.offsetMin = Vector2.zero;
        badgeTextRect.offsetMax = Vector2.zero;
        var badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        badgeText.fontSize = 13;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = Color.white;

        var tileUI = root.AddComponent<WondrousArmorTileUI>();
        tileUI.icon = icon;
        tileUI.label = label;
        tileUI.quantityBadgeRoot = badgeObj;
        tileUI.quantityBadge = badgeText;
        tileUI.wearButton = wearBtn;
        tileUI.wearButtonText = wearText;
        tileUI.disenchantButton = disBtn;

        string dir = Path.GetDirectoryName(OutputPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"WondrousArmorTile prefab saved to {OutputPath}");
    }
}
#endif
