#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Строит Resources/UI/WondrousArmorTile.prefab — портрет героя + бедж количества, вся плитка кликабельна
// (см. WondrousArmorTileUI.OnClicked — открывает попап с Wear/Disenchant). Раньше (до 2026-08-12) плитка
// собиралась вручную с 2 отдельными кнопками прямо на ней и НЕ переиспользовала PickerTileUtility (не
// хватало места под кнопки при её пропорциях иконка/подпись) — после переноса кнопок в попап это
// ограничение снялось, теперь тот же билдер, что у AscensionGemTile (см. AscensionGemTilePrefabBuilder).
public static class WondrousArmorTilePrefabBuilder
{
    private const string OutputPath = "Assets/Resources/UI/WondrousArmorTile.prefab";

    [MenuItem("Tools/Build WondrousArmorTile Prefab")]
    public static void BuildPrefab()
    {
        var tempParent = new GameObject("TempParent", typeof(RectTransform)).transform;

        PickerTileUtility.BuildTile(tempParent, "WondrousArmorTile", new Color(1, 1, 1, 0.06f),
            out Image bg, out Image icon, out TMP_Text label, out Button button, new Vector2(149, 200)); // cellSize общей сетки ItemCollectionUI
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = 14;

        TMP_Text quantityBadge = null;
        ItemBadgeUtility.ApplyQuantityBadge(icon.rectTransform, 2, ref quantityBadge); // quantity=2 просто чтобы бедж создался/был виден в превью префаба
        GameObject quantityBadgeRoot = quantityBadge.transform.parent.gameObject;

        GameObject tileRoot = bg.gameObject;
        tileRoot.transform.SetParent(null, false); // sizeDelta уже 149x200 — передан в BuildTile, до CardDepthUtility внутри него

        var tileUI = tileRoot.AddComponent<WondrousArmorTileUI>();
        tileUI.icon = icon;
        tileUI.label = label;
        tileUI.quantityBadgeRoot = quantityBadgeRoot;
        tileUI.quantityBadge = quantityBadge;
        tileUI.button = button;

        string dir = Path.GetDirectoryName(OutputPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        PrefabUtility.SaveAsPrefabAsset(tileRoot, OutputPath);

        Object.DestroyImmediate(tileRoot);
        Object.DestroyImmediate(tempParent.gameObject);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"WondrousArmorTile prefab saved to {OutputPath}");
    }
}
#endif
