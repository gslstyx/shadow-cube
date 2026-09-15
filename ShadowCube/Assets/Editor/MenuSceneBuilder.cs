using System.Collections.Generic;
using ShadowCube.Core;
using ShadowCube.Game;
using ShadowCube.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    /// <summary>一键生成首页 / 选关场景，并注册全部场景到 Build Settings</summary>
    public static class MenuSceneBuilder
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string LevelSelectPath = "Assets/Scenes/LevelSelect.unity";
        private const string CatalogPath = "Assets/Data/Levels/LevelCatalog.asset";
        private const string LevelDir = "Assets/Data/Levels";

        [MenuItem("Tools/Shadow Cube/6) 生成首页与选关场景")]
        public static void Build()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[ShadowCube] 未找到 {CatalogPath}，请先执行菜单 5) 生成 M1 灰盒场景");
                return;
            }

            BuildMainMenu(catalog);
            BuildLevelSelect(catalog);
            RegisterScenes();

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowCube] 首页/选关场景已生成，共 {catalog.Count} 关");
        }

        private static void BuildMainMenu(LevelCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (Camera.main != null) UnityEngine.Object.DestroyImmediate(Camera.main.gameObject);

            var flow = CreateFlow(catalog);
            var progress = CreateProgress(catalog);

            var uiGo = new GameObject("MainMenuUI");
            var ui = uiGo.AddComponent<MainMenuUI>();
            ui.progress = progress;
            ui.flow = flow;

            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        private static void BuildLevelSelect(LevelCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (Camera.main != null) UnityEngine.Object.DestroyImmediate(Camera.main.gameObject);

            var flow = CreateFlow(catalog);
            var progress = CreateProgress(catalog);

            var uiGo = new GameObject("LevelSelectUI");
            var ui = uiGo.AddComponent<LevelSelectUI>();
            ui.progress = progress;
            ui.flow = flow;

            EditorSceneManager.SaveScene(scene, LevelSelectPath);
        }

        private static LevelFlow CreateFlow(LevelCatalog catalog)
        {
            var go = new GameObject("LevelFlow");
            var flow = go.AddComponent<LevelFlow>();
            flow.catalog = catalog;
            return flow;
        }

        private static ProgressManager CreateProgress(LevelCatalog catalog)
        {
            var go = new GameObject("ProgressManager");
            var progress = go.AddComponent<ProgressManager>();
            progress.catalog = catalog;
            return progress;
        }

        /// <summary>把关卡资产按顺序写入目录（去重），供 M3 批量生成关卡后调用</summary>
        public static void RefreshCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var assets = AssetDatabase.FindAssets("t:LevelData", new[] { LevelDir });
            var levels = new List<LevelData>();
            foreach (var guid in assets)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level != null) levels.Add(level);
            }

            levels.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            catalog.levels = levels;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowCube] 关卡目录已刷新：{levels.Count} 关");
        }

        private static void RegisterScenes()
        {
            var order = new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/LevelSelect.unity",
                "Assets/Scenes/M1_Prototype.unity"
            };

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path.Contains("Assets/Scenes/"));

            foreach (var path in order)
                scenes.Add(new EditorBuildSettingsScene(path, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
