using System.Collections.Generic;
using ShadowCube.Core;
using ShadowCube.Game;
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

            var controllerGo = new GameObject("GameController");
            var controller = controllerGo.AddComponent<GameController>();
            controller.level = level;

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
                if (string.IsNullOrEmpty(level.levelId))
                {
                    level.levelId = "C1-L01";     // 存档用稳定 ID
                    EditorUtility.SetDirty(level);
                }
                return level;
            }

            level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId = "C1-L01";
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
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
