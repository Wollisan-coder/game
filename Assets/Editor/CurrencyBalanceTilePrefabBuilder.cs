#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Строит Resources/UI/CurrencyBalanceTile.prefab — иконка + заголовок + число, без рамки редкости и
// беджа "x-количества" (те нужны только AscensionGemTile/WondrousArmorTile, привязанным к герою).
// Пропорции подогнаны под cellSize {149,200} общей сетки ItemCollectionUI (см. сцену).
public static class CurrencyBalanceTilePrefabBuilder
{
    private const string OutputPath = "Assets/Resources/UI/CurrencyBalanceTile.prefab";

    [MenuItem("Tools/Build CurrencyBalanceTile Prefab")]
    public static void BuildPrefab()
    {
        var root = new GameObject("CurrencyBalanceTile", typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(149, 200);
        var rootBg = root.AddComponent<Image>();
        rootBg.color = new Color(1, 1, 1, 0.06f);
        var rootBtn = root.AddComponent<Button>();

        var iconObj = new GameObject("Icon", typeof(RectTransform));
        var iconRect = (RectTransform)iconObj.transform;
        iconRect.SetParent(rootRect, false);
        iconRect.anchorMin = new Vector2(0, 0.42f);
        iconRect.anchorMax = new Vector2(1, 1);
        iconRect.offsetMin = new Vector2(10, 4);
        iconRect.offsetMax = new Vector2(-10, -10);
        var icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;

        var labelObj = new GameObject("Label", typeof(RectTransform));
        var labelRect = (RectTransform)labelObj.transform;
        labelRect.SetParent(rootRect, false);
        labelRect.anchorMin = new Vector2(0, 0.26f);
        labelRect.anchorMax = new Vector2(1, 0.42f);
        labelRect.offsetMin = new Vector2(4, 0);
        labelRect.offsetMax = new Vector2(-4, 0);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = 14;
        label.color = Color.white;

        var balanceObj = new GameObject("BalanceText", typeof(RectTransform));
        var balanceRect = (RectTransform)balanceObj.transform;
        balanceRect.SetParent(rootRect, false);
        balanceRect.anchorMin = new Vector2(0, 0.02f);
        balanceRect.anchorMax = new Vector2(1, 0.26f);
        balanceRect.offsetMin = new Vector2(4, 0);
        balanceRect.offsetMax = new Vector2(-4, 0);
        var balanceText = balanceObj.AddComponent<TextMeshProUGUI>();
        balanceText.alignment = TextAlignmentOptions.Center;
        balanceText.enableAutoSizing = true;
        balanceText.fontSizeMin = 14;
        balanceText.fontSizeMax = 24;
        balanceText.fontStyle = FontStyles.Bold;
        balanceText.color = new Color(1f, 0.9f, 0.55f, 1f);

        var tileUI = root.AddComponent<CurrencyBalanceTileUI>();
        tileUI.icon = icon;
        tileUI.label = label;
        tileUI.balanceText = balanceText;
        tileUI.button = rootBtn;

        string dir = Path.GetDirectoryName(OutputPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CurrencyBalanceTile prefab saved to {OutputPath}");
    }
}
#endif
