using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Разово обесцвечивает Base Color текстуру спец-фишек (LineClearSpecial_Pivot/BombClearSpecial_Pivot,
// см. GridManager.lineClearSpecialPrefab/colorBombSpecialPrefab) — исходные модели ярко-красные, цвет
// запечён прямо в текстуру (не в _BaseColor свойство материала), это мешает потом тонировать их под
// цвет матча. Вызывается через -executeMethod из командной строки.
//
// TexturesReadable у gltFast-импортёра — приватное поле ImportSettings.texturesReadable, доступно
// только через SerializedObject (см. GLTFast.Editor.ImportSettingsEditor, pathPrefix "importSettings.").
// Читаем пиксели чисто на CPU (Texture2D.GetPixels после реимпорта с readable=true), без Graphics.Blit —
// GPU-путь ненадёжен в -batchmode -nographics.
public static class SpecialTileTextureDesaturator
{
    private static readonly (string glbPath, string prefabPath, string label)[] Targets =
    {
        ("Assets/3inrow base/2Sides.glb", "Assets/3inrow base/LineClearSpecial_Pivot.prefab", "LineClearSpecial"),
        ("Assets/3inrow base/Bomb.glb", "Assets/3inrow base/BombClearSpecial_Pivot.prefab", "BombClearSpecial"),
    };

    public static void Run()
    {
        foreach (var target in Targets)
            Process(target.glbPath, target.prefabPath, target.label);

        AssetDatabase.SaveAssets();
        Debug.Log("SpecialTileTextureDesaturator: done");
    }

    private static void Process(string glbPath, string prefabPath, string label)
    {
        if (!SetTexturesReadable(glbPath, true))
        {
            Debug.LogError($"SpecialTileTextureDesaturator: couldn't set TexturesReadable on {glbPath}");
            return;
        }

        try
        {
            // Источник — ВСЕГДА сам .glb, а не то, что сейчас висит на префабе: после первого успешного
            // прогона Renderer уже смотрит на наш же {label}_Gray.mat (не readable, это не .glb sub-asset),
            // так что повторный прогон с чтением "из текущего материала префаба" читал бы уже готовый
            // серый результат прошлого раза (и падал — та PNG не readable) вместо оригинальной текстуры.
            var glbMaterial = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<Material>().FirstOrDefault();
            if (glbMaterial == null)
            {
                Debug.LogError($"SpecialTileTextureDesaturator: no Material sub-asset found in {glbPath}");
                return;
            }

            string baseColorProp = FindBaseColorTextureProperty(glbMaterial);
            if (baseColorProp == null)
            {
                Debug.LogError($"SpecialTileTextureDesaturator: no base-color texture property on material for {glbPath}");
                return;
            }

            var sourceTex = glbMaterial.GetTexture(baseColorProp) as Texture2D;
            if (sourceTex == null)
            {
                Debug.LogError($"SpecialTileTextureDesaturator: base-color texture is null for {glbPath}");
                return;
            }

            var grayTex = CreateDesaturatedTexture(sourceTex, label);
            if (grayTex == null) return;

            // Дублируем материал, а не правим исходный in-place — исходный это sub-asset самого .glb,
            // регенерируется при реимпорте, редактировать его напрямую ненадёжно.
            var newMaterial = new Material(glbMaterial);
            newMaterial.SetTexture(baseColorProp, grayTex);

            string materialPath = $"Assets/3inrow base/{label}_Gray.mat";
            AssetDatabase.DeleteAsset(materialPath); // на случай повторного прогона
            AssetDatabase.CreateAsset(newMaterial, materialPath);

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"SpecialTileTextureDesaturator: prefab not found at {prefabPath}");
                return;
            }

            try
            {
                var renderer = prefabRoot.GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                {
                    Debug.LogError($"SpecialTileTextureDesaturator: no Renderer found in {prefabPath}");
                    return;
                }

                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            Debug.Log($"SpecialTileTextureDesaturator: {label} -> {materialPath} ({grayTex.name})");
        }
        finally
        {
            SetTexturesReadable(glbPath, false); // возвращаем как было — не хотим раздувать рантайм-память финальной сборки
        }
    }

    private static bool SetTexturesReadable(string glbPath, bool readable)
    {
        var importer = AssetImporter.GetAtPath(glbPath);
        if (importer == null) return false;

        var so = new SerializedObject(importer);
        var prop = so.FindProperty("importSettings.texturesReadable");
        if (prop == null) return false;

        prop.boolValue = readable;
        so.ApplyModifiedProperties();
        AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static string FindBaseColorTextureProperty(Material material)
    {
        var shader = material.shader;
        int count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;

            string lower = shader.GetPropertyName(i).ToLowerInvariant();
            if (lower.Contains("basecolor") || lower.Contains("base_color"))
                return shader.GetPropertyName(i);
        }
        return null;
    }

    // Средневзвешенная яркость (стандартные ITU-R BT.601 веса) на каждый пиксель — цвет пропадает,
    // грани/блики/орнамент (которые и есть яркостные вариации в текстуре) остаются читаемыми.
    private static Texture2D CreateDesaturatedTexture(Texture2D sourceTex, string label)
    {
        Color[] pixels;
        try
        {
            pixels = sourceTex.GetPixels();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SpecialTileTextureDesaturator: GetPixels failed for {sourceTex.name}: {e.Message}");
            return null;
        }

        // Гамма-кривая, не линейный "пол" — первая версия (Lerp(MinBrightness, 1, luminance)) поднимала
        // ВСЕ тёмные пиксели одинаково, включая настоящий чёрный (стрелки-указатели направления, часть
        // дизайна текстуры — см. чат про "чёрная стрелка — часть самой модели") — из-за подъёма стрелки
        // тоже становились видимо серыми и красились тонированием вместе с кристаллом. gray = luminance^γ
        // (γ<1) даёт 0→0 (истинный чёрный остаётся чёрным без изменений — 0 в любой положительной
        // степени это 0), но резко поднимает промежуточные тона (тени кристалла), без линии/порога.
        const float Gamma = 0.4f;
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float luminance = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float gray = Mathf.Pow(luminance, Gamma);
            pixels[i] = new Color(gray, gray, gray, c.a);
        }

        var grayTexAsset = new Texture2D(sourceTex.width, sourceTex.height, TextureFormat.RGBA32, false);
        grayTexAsset.SetPixels(pixels);
        grayTexAsset.Apply();

        byte[] png = grayTexAsset.EncodeToPNG();
        string outPath = $"Assets/3inrow base/{label}_basecolor_gray.png";
        File.WriteAllBytes(outPath, png);
        AssetDatabase.ImportAsset(outPath);

        if (AssetImporter.GetAtPath(outPath) is TextureImporter texImporter)
        {
            texImporter.sRGBTexture = true;
            texImporter.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }
}
