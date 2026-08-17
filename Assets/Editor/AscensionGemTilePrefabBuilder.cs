#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Строит Resources/UI/AscensionGemTile.prefab тем же кодом (PickerTileUtility/ItemBadgeUtility), что
// раньше собирал эту плитку целиком в рантайме DeathDungeonEntryUI.BuildAscensionGemTile — теперь это
// сохранённый .prefab-ассет, редактируемый через Inspector (AscensionGemTileUI). Перезапустить можно в
// любой момент через Tools/Build AscensionGemTile Prefab, если понадобится поменять структуру плитки.
public static class AscensionGemTilePrefabBuilder
{
    private const string OutputPath = "Assets/Resources/UI/AscensionGemTile.prefab";

    [MenuItem("Tools/Build AscensionGemTile Prefab")]
    public static void BuildPrefab()
    {
        var tempParent = new GameObject("TempParent", typeof(RectTransform)).transform;

        PickerTileUtility.BuildTile(tempParent, "AscensionGemTile", new Color(1, 1, 1, 0.06f),
            out Image bg, out Image icon, out TMP_Text label, out Button button, new Vector2(150, 200));
        label.fontSize = 14; // = размер, который раньше ставился вручную в DeathDungeonEntryUI.BuildAscensionGemTile

        Image rarityFrame = null;
        ItemBadgeUtility.ApplyRarityFrame(icon, Color.white, ref rarityFrame);

        TMP_Text quantityBadge = null;
        ItemBadgeUtility.ApplyQuantityBadge(icon.rectTransform, 2, ref quantityBadge); // quantity=2 просто чтобы бедж создался/был виден в превью префаба
        GameObject quantityBadgeRoot = quantityBadge.transform.parent.gameObject;

        GameObject tileRoot = bg.gameObject;
        tileRoot.transform.SetParent(null, false); // sizeDelta уже 150x200 — передан в BuildTile, до CardDepthUtility внутри него

        var tileUI = tileRoot.AddComponent<AscensionGemTileUI>();
        tileUI.icon = icon;
        tileUI.rarityFrame = rarityFrame;
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
        Debug.Log($"AscensionGemTile prefab saved to {OutputPath}");
    }
}
#endif
