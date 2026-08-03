using UnityEditor;
using UnityEngine;

// Разово настраивает импорт спрайтов рамок попапа (Assets/Resources/UI/*) под 9-slice — вызывается
// через -executeMethod из командной строки, не через интерфейс редактора.
public static class UIAssetImportSetup
{
    public static void Run()
    {
        SetupSprite("Assets/Resources/UI/DialogWindowFrame.png", new Vector4(70, 70, 70, 70));
        SetupSprite("Assets/Resources/UI/DialogHeaderFrame.png", new Vector4(35, 35, 35, 35));
        AssetDatabase.SaveAssets();
        Debug.Log("UIAssetImportSetup: done");
    }

    private static void SetupSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"UIAssetImportSetup: importer not found for {path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;

        var settings = importer.GetDefaultPlatformTextureSettings();
        settings.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(settings);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}
