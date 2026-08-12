#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Разовый инструмент: копирует существующий 2D-арт из "Assets/3inrow base/" (там же живут 3D-модели/
// материалы гемов, но эти конкретные PNG сейчас нигде не используются — только лежат) в
// Resources/UI/HarmfulTiles/, переименовывая под HarmfulTileType, и переключает Texture Type на Sprite
// (source-файлы импортированы как Default — этого достаточно для 3D, но Resources.Load<Sprite> вернёт
// null без Sprite-суб-ассета). См. HarmfulTileUtility.GetIcon — путь оттуда должен совпадать 1:1.
// Копия, а не перемещение — исходники в "3inrow base" не трогаем на случай, если они всё же пригодятся
// для 3D-версии вредных фишек на поле позже.
public static class HarmfulTileIconImporter
{
    private const string SourceDir = "Assets/3inrow base/";
    private const string DestDir = "Assets/Resources/UI/HarmfulTiles/";

    // (HarmfulTileType-имя для файла назначения, исходный файл)
    private static readonly (string destName, string sourceFile)[] Mappings =
    {
        ("Ice", "medium_01_ice.png"),
        ("Spike", "light_02_spike.png"),
        ("Trap", "medium_03_trap_gem.png"),
        ("Anchor", "heavy_04_anchor.png"),
        ("Cursed", "heavy_01_cursed.png"),
        ("BloodMark", "light_03_blood_mark.png"),
        ("Rotten", "heavy_02_rot_gem.png"),
        ("Chaos", "heavy_03_chaos.png"),
    };

    [MenuItem("Tools/Import Harmful Tile Icons")]
    public static void Import()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        if (!AssetDatabase.IsValidFolder(DestDir.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Resources/UI", "HarmfulTiles");

        foreach (var (destName, sourceFile) in Mappings)
        {
            string sourcePath = SourceDir + sourceFile;
            string destPath = DestDir + destName + ".png";

            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                Debug.LogError($"HarmfulTileIconImporter: failed to copy {sourcePath} -> {destPath}");
                continue;
            }

            var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"HarmfulTileIconImporter: no TextureImporter at {destPath}");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("HarmfulTileIconImporter: done.");
    }
}
#endif
