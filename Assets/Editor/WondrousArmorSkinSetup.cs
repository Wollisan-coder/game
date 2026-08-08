#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Одноразовый инструмент — создаёт WondrousArmorSkinSet.asset (если ещё нет) и добавляет оверлей-Image
// "WondrousArmorOverlay" поверх портрета героя в трёх местах: HeroMiniCard.prefab (общий для Collection и
// Squad), HeroInventoryUI (уже открытая сцена — НЕ переоткрывает её отдельной копией, чтобы не плодить
// дубликат рядом с тем, что уже открыт в Editor) и WondrousArmorTile.prefab (плитка в инвентаре
// предметов). Тот же приём наложения картинки, что уже есть у AscensionOverlay — см. WondrousArmorSkinSet.Apply.
public static class WondrousArmorSkinSetup
{
    private const string SkinSetPath = "Assets/WondrousArmorSkinSet.asset";
    private const string HeroCardPrefabPath = "Assets/HeroMiniCard.prefab";
    private const string ArmorTilePrefabPath = "Assets/Resources/UI/WondrousArmorTile.prefab";

    [MenuItem("Tools/Setup Wondrous Armor Skins")]
    public static void Setup()
    {
        var skinSet = AssetDatabase.LoadAssetAtPath<WondrousArmorSkinSet>(SkinSetPath);
        if (skinSet == null)
        {
            skinSet = ScriptableObject.CreateInstance<WondrousArmorSkinSet>();
            AssetDatabase.CreateAsset(skinSet, SkinSetPath);
            Debug.Log($"Created {SkinSetPath}");
        }

        PatchHeroMiniCardPrefab(skinSet);
        PatchHeroInventoryInOpenScene(skinSet);
        PatchWondrousArmorTilePrefab(skinSet);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Wondrous Armor skin wiring complete. Заполни WondrousArmorSkinSet.asset (heroId + skinSprite на каждого героя) в Inspector, когда появится арт.");
    }

    private static void PatchHeroMiniCardPrefab(WondrousArmorSkinSet skinSet)
    {
        var root = PrefabUtility.LoadPrefabContents(HeroCardPrefabPath);
        try
        {
            var cardUI = root.GetComponent<HeroMiniCardUI>();
            if (cardUI == null) { Debug.LogWarning($"HeroMiniCardUI not found on {HeroCardPrefabPath}"); return; }

            if (cardUI.wondrousArmorOverlay == null && cardUI.portraitImage != null)
                cardUI.wondrousArmorOverlay = CreateOverlayMatching(cardUI.portraitImage.rectTransform, "WondrousArmorOverlay");
            cardUI.wondrousArmorSkinSet = skinSet;

            PrefabUtility.SaveAsPrefabAsset(root, HeroCardPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // НЕ вызывает EditorSceneManager.OpenScene — у пользователя сцена уже открыта в Editor, повторное
    // открытие создало бы дублирующую загрузку. Ищем компонент в уже загруженной сцене напрямую.
    private static void PatchHeroInventoryInOpenScene(WondrousArmorSkinSet skinSet)
    {
        var inventoryUI = Object.FindAnyObjectByType<HeroInventoryUI>(FindObjectsInactive.Include);
        if (inventoryUI == null)
        {
            Debug.LogWarning("HeroInventoryUI не найден в открытой сцене — открой MainMenuScene и повтори.");
            return;
        }

        if (inventoryUI.wondrousArmorOverlay == null && inventoryUI.portraitImage != null)
            inventoryUI.wondrousArmorOverlay = CreateOverlayMatching(inventoryUI.portraitImage.rectTransform, "WondrousArmorOverlay");
        inventoryUI.wondrousArmorSkinSet = skinSet;

        EditorUtility.SetDirty(inventoryUI);
        EditorSceneManager.MarkSceneDirty(inventoryUI.gameObject.scene);
        Debug.Log("HeroInventoryUI пропатчен — сцена помечена изменённой, сохрани её сам (Ctrl+S).");
    }

    private static void PatchWondrousArmorTilePrefab(WondrousArmorSkinSet skinSet)
    {
        var root = PrefabUtility.LoadPrefabContents(ArmorTilePrefabPath);
        try
        {
            var tileUI = root.GetComponent<WondrousArmorTileUI>();
            if (tileUI == null) { Debug.LogWarning($"WondrousArmorTileUI not found on {ArmorTilePrefabPath}"); return; }

            tileUI.skinSet = skinSet;
            PrefabUtility.SaveAsPrefabAsset(root, ArmorTilePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Копирует anchor/pivot/anchoredPosition/sizeDelta портрета — оверлей ляжет ровно поверх него,
    // на 1 sibling выше портрета, чтобы рендерился поверх него, но под рамками/баджами/текстом.
    private static Image CreateOverlayMatching(RectTransform portraitRect, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(portraitRect.parent, false);
        rect.anchorMin = portraitRect.anchorMin;
        rect.anchorMax = portraitRect.anchorMax;
        rect.pivot = portraitRect.pivot;
        rect.anchoredPosition = portraitRect.anchoredPosition;
        rect.sizeDelta = portraitRect.sizeDelta;
        rect.SetSiblingIndex(portraitRect.GetSiblingIndex() + 1);

        var img = obj.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = false; // включится через WondrousArmorSkinSet.Apply, когда появится скин и он надет

        return img;
    }
}
#endif
