using UnityEditor;
using UnityEditor.SceneManagement;
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
        SetupSprite("Assets/Resources/UI/Castle/AchivIcon.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Castle/DailyQIcon.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Currency/Wood.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Currency/Stone.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Currency/Shards.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Currency/Gems.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Currency/PP.png", Vector4.zero);
        SetupSprite("Assets/Resources/UI/Currency/Energy.png", Vector4.zero);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireCastleBuildings();
        AssetDatabase.SaveAssets();

        BootstrapSceneManagers();
        WireGridManagerSpecialPrefabs();

        Debug.Log("UIAssetImportSetup: done");
    }

    // GridManager.lineClearSpecialPrefab/colorBombSpecialPrefab (см. Item.MarkAsSpecial) — в SampleScene
    // (боевая сцена), НЕ MainMenuScene. Идемпотентно — как и WireCastleBuildings, не трогает, если уже
    // назначено (не затираем возможную ручную донастройку).
    private static void WireGridManagerSpecialPrefabs()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var gridManager = Object.FindAnyObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("UIAssetImportSetup: GridManager not found in SampleScene");
            return;
        }

        // Обёрточные префабы (пользователь вручную подобрал Scale в сцене и сохранил как Prefab) —
        // а не сырые .glb напрямую: у сырых ассетов масштаб экспорта (Tripo/Meshy) в разы больше
        // размера обычной фишки, эти префабы уже несут правильный Transform.localScale.
        // LineClearSpecial.prefab — третья итерация: звезда (LineClearSpecial_Pivot, обёрнутый вокруг
        // 2Sides.glb) заменена на double-headed-arrow модель (2462 треугольника — на порядок легче
        // изначального arroow.glb на 19802 треугольника, см. LineClearSpecialFixer). Тот же принцип —
        // Item.MarkAsSpecial ставит localPosition=zero этому prefab'у как ребёнку фишки.
        var lineClear = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3inrow base/LineClearSpecial.prefab");
        var colorBomb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3inrow base/BombClearSpecial_Pivot.prefab");

        bool dirty = false;

        if (gridManager.lineClearSpecialPrefab != lineClear)
        {
            gridManager.lineClearSpecialPrefab = lineClear;
            dirty = true;
        }

        if (gridManager.colorBombSpecialPrefab != colorBomb)
        {
            gridManager.colorBombSpecialPrefab = colorBomb;
            dirty = true;
        }

        if (!dirty) return;

        EditorUtility.SetDirty(gridManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("UIAssetImportSetup: wired GridManager special prefabs in SampleScene");
    }

    // DailyQuestManager — синглтон-менеджер, живёт как GameObject в MainMenuScene (та же конвенция, что
    // и AccountManager/PlayerCurrencies/SummonService — все всегда-загруженные менеджеры лежат там, а не
    // самосоздаются лениво, и переживают переход в SampleScene только через DontDestroyOnLoad). Ставится
    // через код, а не руками в YAML сцены — надёжнее для fileID/иерархии.
    private static void BootstrapSceneManagers()
    {
        const string scenePath = "Assets/Scenes/MainMenuScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        if (GameObject.Find("DailyQuestManager") == null)
        {
            var go = new GameObject("DailyQuestManager");
            go.AddComponent<DailyQuestManager>();
            Debug.Log("UIAssetImportSetup: created DailyQuestManager in MainMenuScene");
        }

        if (GameObject.Find("AchievementManager") == null)
        {
            var go = new GameObject("AchievementManager");
            go.AddComponent<AchievementManager>();
            Debug.Log("UIAssetImportSetup: created AchievementManager in MainMenuScene");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Проставляет mapSprite/mapPosition/builtIntoBackground на BuildingData-ассетах под фон
    // BaseBackground.png — координаты изначально подобраны по пиксельным позициям на исходной картинке
    // (768x1376), пересчитаны в anchoredPosition канваса 1080x1920, ЗАТЕМ вручную подогнаны пользователем
    // в Inspector/Play Mode. ВАЖНО: SetMapSprite/SetBuiltIntoBackground ниже — только для первичной
    // прошивки (skip, если mapSprite/builtIntoBackground уже выставлены) — этот метод гоняется при каждом
    // batch-прогоне (см. Run()), и раньше он безусловно перезаписывал mapPosition/mapSpriteSize заново
    // захардкоженными значениями при каждом запуске, затирая ручную подгонку — конкретно так и случилось
    // 2026-08-05, пришлось руками восстанавливать по скриншотам Inspector.
    private static void WireCastleBuildings()
    {
        SetBuiltIntoBackground("Assets/Building_Altar.asset", new Vector2(-47.8f, 806.7f), new Vector2(260, 220));

        SetMapSprite("Assets/Building_ShardMine.asset", "Assets/Resources/UI/Castle/ShardMine.png", new Vector2(5f, -89f), new Vector2(210, 200));
        SetMapSprite("Assets/Building_WoodCamp.asset", "Assets/Resources/UI/Castle/WoodCamp.png", new Vector2(344.1f, 14.1f), new Vector2(225, 300));
        SetMapSprite("Assets/Building_StoneQuarry.asset", "Assets/Resources/UI/Castle/StoneQuarry.png", new Vector2(-300f, 14f), new Vector2(260, 170));
        SetMapSprite("Assets/Building_Barracks.asset", "Assets/Resources/UI/Castle/Barracks.png", new Vector2(296f, 386f), new Vector2(220, 175));
        SetMapSprite("Assets/Building_Forge.asset", "Assets/Resources/UI/Castle/Forge.png", new Vector2(-306f, 401f), new Vector2(190, 190));
    }

    private static void SetMapSprite(string buildingPath, string spritePath, Vector2 mapPosition, Vector2 mapSpriteSize)
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingData>(buildingPath);
        if (building == null)
        {
            Debug.LogError($"UIAssetImportSetup: building not found at {buildingPath}");
            return;
        }

        if (building.mapSprite != null) return; // уже прошито (и, возможно, вручную подогнано) — не трогаем

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

        if (building.builtIntoBackground) return; // уже прошито — не трогаем

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
