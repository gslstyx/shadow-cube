using System.Collections.Generic;
using ShadowCube.Core;
using ShadowCube.Game;
using ShadowCube.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    /// <summary>一键生成 M1 灰盒场景（示例关卡 + 相机 + 灯光 + 主控）</summary>
    public static class M1SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/M1_Prototype.unity";
        private const string DataDir = "Assets/Data";
        private const string LevelDir = "Assets/Data/Levels";
        private const string LevelPath = "Assets/Data/Levels/Level_01.asset";

        [MenuItem("Tools/Shadow Cube/5) 生成 M1 灰盒场景")]
        public static void Build()
        {
            var level = EnsureSampleLevel();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureLight();
            ConfigureCamera(level);

            // 运行时材质资产（保证 shader 进包，真机不再 Shader.Find 失败）
            ShadowCubeMaterialBuilder.Build();
            var mats = new
            {
                platform = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_Platform.mat"),
                voxel = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_Voxel.mat"),
                grid = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_GridLine.mat"),
                hover = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_Hover.mat"),
                wallTarget = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_WallTarget.mat"),
                wallCurrent = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_WallCurrent.mat"),
                trail = AssetDatabase.LoadAssetAtPath<Material>($"{ShadowCubeMaterialBuilder.Dir}/M_DragTrail.mat"),
            };

            // 进度（存档 + 关卡目录）
            var catalog = EnsureCatalog(level);

            // 场景流程（跨场景单例）
            if (Object.FindObjectOfType<LevelFlow>() == null)
            {
                var flowGo = new GameObject("LevelFlow");
                flowGo.AddComponent<LevelFlow>().catalog = catalog;
            }

            var progressGo = new GameObject("ProgressManager");
            var progress = progressGo.AddComponent<ProgressManager>();
            progress.catalog = catalog;

            // 玩法主控
            var controllerGo = new GameObject("GameController");
            var controller = controllerGo.AddComponent<GameController>();
            controller.level = level;
            controller.progress = progress;
            controller.platformMaterial = mats.platform;
            controller.voxelMaterial = mats.voxel;
            controller.gridLineMaterial = mats.grid;
            controller.hoverMaterial = mats.hover;
            controller.wallTargetMaterial = mats.wallTarget;
            controller.wallCurrentMaterial = mats.wallCurrent;
            controller.dragTrailMaterial = mats.trail;

            // HUD + 结算
            var hudGo = new GameObject("GameHud");
            var hud = hudGo.AddComponent<GameHud>();
            hud.controller = controller;
            controller.hud = hud;

            var settlementGo = new GameObject("SettlementPanel");
            var settlement = settlementGo.AddComponent<SettlementPanel>();
            settlement.controller = controller;

            // 新手引导（仅首次触发）
            var tutorialGo = new GameObject("TutorialOverlay");
            tutorialGo.AddComponent<TutorialOverlay>().progress = progress;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowCube] M1 灰盒场景已生成：{ScenePath}（关卡 {LevelPath}，最优 {level.OptimalCount} 块）");
        }

        private static LevelData EnsureSampleLevel()
        {
            if (!AssetDatabase.IsValidFolder(DataDir))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(LevelDir))
                AssetDatabase.CreateFolder(DataDir, "Levels");

            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            if (level != null)
            {
                // 教学关用 C0-L01，避免与生成器产出的 C1-Lxx 重号
                if (string.IsNullOrEmpty(level.levelId) || level.levelId == "C1-L01")
                {
                    level.levelId = "C0-L01";     // 存档用稳定 ID
                    EditorUtility.SetDirty(level);
                }
                return level;
            }

            level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId = "C0-L01";
            level.width = 5;
            level.depth = 5;
            level.maxHeight = 3;
            level.levelName = "第一次投影";
            level.chapterId = 1;
            level.levelIndex = 1;
            level.solution = new[]
            {
                new Vector3Int(1, 0, 1),
                new Vector3Int(2, 0, 1),
                new Vector3Int(1, 0, 2),
                new Vector3Int(1, 1, 1),
                new Vector3Int(3, 0, 3)
            };

            AssetDatabase.CreateAsset(level, LevelPath);
            return level;
        }

        private static LevelCatalog EnsureCatalog(LevelData level)
        {
            string path = LevelDir + "/LevelCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            if (!catalog.levels.Contains(level))
            {
                catalog.levels.Add(level);
                EditorUtility.SetDirty(catalog);
            }
            return catalog;
        }

        private static void ConfigureLight()
        {
            var light = Object.FindObjectOfType<Light>();
            if (light == null) return;

            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void ConfigureCamera(LevelData level)
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);

            // 相机挂在云台下，云台绕 Y 轴旋转（含两面墙）→ 视角旋转时墙跟随
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.position = Vector3.zero;
            rigGo.AddComponent<CameraRig>();
            cam.transform.SetParent(rigGo.transform, true);

            float span = Mathf.Max(level.width, level.depth);
            cam.transform.position = new Vector3(span * 1.15f, level.maxHeight * 1.6f + 5f, span * 1.45f);
            cam.transform.LookAt(new Vector3(0f, level.maxHeight * 0.5f, 0f));
        }

        private static void AddToBuildSettings()
        {
            // 追加到末尾，保证首页/选关仍在最前（MenuSceneBuilder 负责最终排序）
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
