using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

public static class ShadowCubeBuild
{
    private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
    private const string OutputDir = "Builds/Android";

    public static void SmokeBuildAndroid()
    {
        EnsureBootstrapScene();

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = true;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputDir + "/shadowcube.aab",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        var summary = report.summary;
        UnityEngine.Debug.Log($"[ShadowCube] 构建结果: {summary.result}, 耗时 {summary.totalTime}, 体积 {summary.totalSize}, 警告 {summary.totalWarnings}, 错误 {summary.totalErrors}");

        if (summary.result != BuildResult.Succeeded)
            throw new Exception("[ShadowCube] Android 构建失败，详见日志");
    }

    private static void EnsureBootstrapScene()
    {
        if (System.IO.File.Exists(ScenePath)) return;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("[ShadowCube] 已创建引导场景: " + ScenePath);
    }
}
