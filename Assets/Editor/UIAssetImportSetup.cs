using UnityEditor;
using UnityEngine;

// Разово настраивает импорт спрайтов рамок попапа (Assets/Resources/UI/*) под 9-slice — вызывается
// через -executeMethod из командной строки, не через интерфейс редактора.
public static class UIAssetImportSetup
{
    public static void Run()
    {
        AssetDatabase.Refresh(); // подхватить файлы, созданные вне редактора (скриптом), до первого SetupSprite

        SetupSprite("Assets/Resources/UI/DialogWindowFrame.png", new Vector4(70, 70, 70, 70));
        // Асимметрично: замерил по альфа-каналу — орнамент (вырез угла + "жемчужина") тянется по
        // бокам ~35px, а сверху/снизу это просто тонкая двойная линия без выступов, ей хватает ~10px.
        // border — (left, bottom, right, top).
        SetupSprite("Assets/Resources/UI/DialogHeaderFrame.png", new Vector4(35, 10, 35, 10));
        // Плоская прямоугольная рамка без вырезанных углов (1438x736, полностью непрозрачная) — под
        // текстовые описания. Тонкий орнаментальный бордюр по всем 4 краям, оценка на глаз ~55px.
        SetupSprite("Assets/Resources/UI/DialogDescriptionPanel.png", new Vector4(55, 55, 55, 55));

        // Castle 2D-база (Option B) — обычные спрайты, без 9-slice (border 0), рисуются как есть с preserveAspect.
        SetupSprite("Assets/Resources/UI/Castle/BaseBackground.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/Forge.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/ShardMine.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/WoodCamp.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/StoneQuarry.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/Barracks.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/Training zone.png", Vector4.zero);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireCastleBuildings();
        AssetDatabase.SaveAssets();
        Debug.Log("UIAssetImportSetup: done");
    }

    // Проставляет mapSprite/mapPosition/builtIntoBackground на BuildingData-ассетах под новый фон
    // BaseBackground.png — координаты подобраны по пиксельным позициям на исходной картинке (768x1376),
    // пересчитаны в anchoredPosition канваса 1080x1920 (baseBackground растянут preserveAspect=false,
    // так что масштаб по X и Y считается отдельно: x1.40625 / x1.395349).
    private static void WireCastleBuildings()
    {
        SetBuiltIntoBackground("Assets/Building_Altar.asset", new Vector2(-47.8f, 806.7f), new Vector2(260, 220));

        SetMapSprite("Assets/Building_ShardMine.asset", "Assets/Resources/UI/Castle/ShardMine.png", new Vector2(-47.8f, 569.3f), new Vector2(210, 200));
        SetMapSprite("Assets/Building_WoodCamp.asset", "Assets/Resources/UI/Castle/WoodCamp.png", new Vector2(-399.4f, 653.0f), new Vector2(250, 145));
        SetMapSprite("Assets/Building_StoneQuarry.asset", "Assets/Resources/UI/Castle/StoneQuarry.png", new Vector2(374.1f, 653.0f), new Vector2(260, 170));
        SetMapSprite("Assets/Building_Barracks.asset", "Assets/Resources/UI/Castle/Barracks.png", new Vector2(-413.4f, 387.9f), new Vector2(220, 175));
        SetMapSprite("Assets/Building_Forge.asset", "Assets/Resources/UI/Castle/Forge.png", new Vector2(388.1f, 387.9f), new Vector2(190, 190));
    }

    private static void SetMapSprite(string buildingPath, string spritePath, Vector2 mapPosition, Vector2 mapSpriteSize)
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingData>(buildingPath);
        if (building == null)
        {
            Debug.LogError($"UIAssetImportSetup: building not found at {buildingPath}");
            return;
        }

        building.mapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        building.mapPosition = mapPosition;
        building.mapSpriteSize = mapSpriteSize;
        EditorUtility.SetDirty(building);
    }

    private static void SetBuiltIntoBackground(string buildingPath, Vector2 mapPosition, Vector2 hitboxSize)
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingData>(buildingPath);
        if (building == null)
        {
            Debug.LogError($"UIAssetImportSetup: building not found at {buildingPath}");
            return;
        }

        building.builtIntoBackground = true;
        building.mapPosition = mapPosition;
        building.mapSpriteSize = hitboxSize;
        EditorUtility.SetDirty(building);
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
