#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Разовый инструмент: MutationDungeonManager (как и DeathDungeonManager/HeroCollectionManager и т.п.)
// должен существовать как GameObject прямо в сцене (DontDestroyOnLoad подхватывает его при первом Awake),
// а не создаваться кодом на лету — то же самое место, что и у DeathDungeonManager в MainMenuScene.unity.
// Идемпотентно — повторный запуск ничего не ломает, если объект уже есть.
public static class MutationDungeonManagerSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/MainMenuScene.unity";

    [MenuItem("Tools/Add MutationDungeonManager To Scene")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        if (Object.FindFirstObjectByType<MutationDungeonManager>() != null)
        {
            Debug.Log("MutationDungeonManagerSceneInstaller: already present, nothing to do.");
            return;
        }

        var go = new GameObject("MutationDungeonManager");
        go.AddComponent<MutationDungeonManager>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MutationDungeonManagerSceneInstaller: added MutationDungeonManager and saved the scene.");
    }
}
#endif
