using UnityEditor;
using UnityEngine;

public static class WebGLBuildScript
{
    // Вызывается из командной строки: -batchmode -quit -executeMethod WebGLBuildScript.Build
    // Собирает WebGL-билд для плейтеста (не для релиза — без доп. настроек компрессии/качества).
    public static void Build()
    {
        string outputPath = "WebGLBuild";

        var options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/MainMenuScene.unity",
                "Assets/Scenes/SampleScene.unity",
            },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"WebGL build result: {report.summary.result}, total errors: {report.summary.totalErrors}, output: {outputPath}");
    }
}
