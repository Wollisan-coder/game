using System.Linq;
using UnityEditor;
using UnityEngine;

// Разовая диагностика импортированной 3D-модели — полигоны/вершины, размер текстур, warnings/errors
// импорта. Вызывается через -executeMethod с аргументом через переменную (см. Run(string)).
public static class ModelInspectorCheck
{
    public static void RunArrow() => Run("Assets/3inrow base/arroow.glb");
    public static void RunDoubleHeadedArrow() => Run("Assets/3inrow base/double headed arrow 3d model.glb");

    public static void Run(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        var meshes = allAssets.OfType<Mesh>().ToArray();
        var materials = allAssets.OfType<Material>().ToArray();
        var textures = allAssets.OfType<Texture2D>().ToArray();

        Debug.Log($"MODEL_CHECK: path={path}");
        Debug.Log($"MODEL_CHECK: meshCount={meshes.Length}");
        foreach (var mesh in meshes)
            Debug.Log($"MODEL_CHECK: mesh='{mesh.name}' vertices={mesh.vertexCount} triangles={mesh.triangles.Length / 3}");

        Debug.Log($"MODEL_CHECK: materialCount={materials.Length}");
        foreach (var mat in materials)
            Debug.Log($"MODEL_CHECK: material='{mat.name}' shader={mat.shader.name}");

        Debug.Log($"MODEL_CHECK: textureCount={textures.Length}");
        foreach (var tex in textures)
            Debug.Log($"MODEL_CHECK: texture='{tex.name}' size={tex.width}x{tex.height}");

        var importer = AssetImporter.GetAtPath(path);
        Debug.Log($"MODEL_CHECK: importerType={importer?.GetType().Name}");
    }
}
