using UnityEditor;
using UnityEngine;

// Разово пересобирает LineClearSpecial.prefab — пользователь случайно завернул старый тяжёлый
// arroow.glb (19802 треугольника) вместо лёгкого "double headed arrow 3d model.glb" (2462 треугольника,
// специально переделан под мобильный бюджет). Переносим уже подобранные Scale/Position на правильный
// исходник, а не просим переделывать вручную в Editor.
public static class LineClearSpecialFixer
{
    public static void Run()
    {
        const string glbPath = "Assets/3inrow base/double headed arrow 3d model.glb";
        const string prefabPath = "Assets/3inrow base/LineClearSpecial.prefab";

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (source == null)
        {
            Debug.LogError($"LineClearSpecialFixer: source not found at {glbPath}");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        instance.transform.localPosition = new Vector3(-40.535f, -0f, -1.997f);
        instance.transform.localRotation = Quaternion.identity;

        AssetDatabase.DeleteAsset(prefabPath); // старая версия (обёртка вокруг arroow.glb) — заменяем целиком
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        AssetDatabase.SaveAssets();
        Debug.Log($"LineClearSpecialFixer: {prefabPath} now wraps {glbPath}");
    }
}
