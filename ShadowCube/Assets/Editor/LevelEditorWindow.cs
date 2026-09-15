using System.Collections.Generic;
using ShadowCube.Core;
using UnityEditor;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 关卡编辑器：可视化编辑体素、随机生成造型、校验并保存关卡（需求 §7-M3）。
    /// 打开：Tools → Shadow Cube → 8) 关卡编辑器
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        private LevelData _level;
        private int _layer;
        private int _seed = 1;
        private int _blockCount = 6;
        private string _message = "";

        [MenuItem("Tools/Shadow Cube/8) 关卡编辑器")]
        public static void Open()
        {
            var window = GetWindow<LevelEditorWindow>("关卡编辑器");
            window.minSize = new Vector2(420f, 560f);
            window.Show();
        }

        private HashSet<Vector3Int> Voxels()
        {
            var set = new HashSet<Vector3Int>();
            if (_level != null && _level.solution != null)
                foreach (var v in _level.solution) set.Add(v);
            return set;
        }

        private void WriteBack(HashSet<Vector3Int> set)
        {
            if (_level == null) return;

            var list = new List<Vector3Int>(set);
            list.Sort((a, b) => (a.y != b.y) ? a.y.CompareTo(b.y) : (a.x != b.x) ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            _level.solution = list.ToArray();
            EditorUtility.SetDirty(_level);
        }

        private void OnGUI()
        {
            GUILayout.Label("关卡资产", EditorStyles.boldLabel);
            _level = (LevelData)EditorGUILayout.ObjectField("LevelData", _level, typeof(LevelData), false);

            if (_level == null)
            {
                EditorGUILayout.HelpBox("先选择一个 LevelData 资产，或用下方按钮新建。", MessageType.Info);
                if (GUILayout.Button("新建关卡资产")) CreateNew();
                return;
            }

            EditorGUILayout.Space();
            GUILayout.Label("平台参数", EditorStyles.boldLabel);
            _level.width = EditorGUILayout.IntSlider("Width (X)", _level.width, 2, 8);
            _level.depth = EditorGUILayout.IntSlider("Depth (Z)", _level.depth, 2, 8);
            _level.maxHeight = EditorGUILayout.IntSlider("Max Height (Y)", _level.maxHeight, 1, 6);
            _level.levelId = EditorGUILayout.TextField("Level ID", _level.levelId);
            _level.levelName = EditorGUILayout.TextField("显示名", _level.levelName);
            _level.chapterId = EditorGUILayout.IntField("章节", _level.chapterId);
            _level.levelIndex = EditorGUILayout.IntField("关序", _level.levelIndex);

            EditorGUILayout.Space();
            GUILayout.Label($"编辑层 Y = {_layer}（共 {_level.maxHeight} 层）", EditorStyles.boldLabel);
            _layer = Mathf.Clamp(EditorGUILayout.IntSlider("层", _layer, 0, _level.maxHeight - 1), 0, _level.maxHeight - 1);

            var voxels = Voxels();
            for (int z = _level.depth - 1; z >= 0; z--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < _level.width; x++)
                {
                    var cell = new Vector3Int(x, _layer, z);
                    bool on = voxels.Contains(cell);
                    bool now = GUILayout.Toggle(on, on ? "■" : "·", GUILayout.Width(26f));
                    if (now != on)
                    {
                        if (now) voxels.Add(cell); else voxels.Remove(cell);
                        WriteBack(voxels);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            GUILayout.Label("工具", EditorStyles.boldLabel);
            _seed = EditorGUILayout.IntField("随机种子", _seed);
            _blockCount = EditorGUILayout.IntSlider("随机块数", _blockCount, 2, 20);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("随机生成")) Randomize();
            if (GUILayout.Button("清空")) { WriteBack(new HashSet<Vector3Int>()); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("校验")) Validate();
            if (GUILayout.Button("保存")) AssetDatabase.SaveAssets();
            if (GUILayout.Button("加入目录")) MenuSceneBuilder.RefreshCatalog();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);

            GUILayout.Label($"当前：{_level.solution?.Length ?? 0} 块（最优 {_level.OptimalCount}）", EditorStyles.miniLabel);
        }

        private void CreateNew()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建关卡", "C1-L", "asset", "保存到 Assets/Data/Levels");
            if (string.IsNullOrEmpty(path)) return;

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId = System.IO.Path.GetFileNameWithoutExtension(path);
            level.levelName = level.levelId;
            level.width = 5;
            level.depth = 5;
            level.maxHeight = 3;
            level.solution = new[] { new Vector3Int(1, 0, 1), new Vector3Int(2, 0, 1), new Vector3Int(1, 1, 1) };

            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            _level = level;
            _message = $"已创建：{path}";
        }

        private void Randomize()
        {
            if (_level == null) return;

            var generated = LevelGenerator.Generate(
                string.IsNullOrEmpty(_level.levelId) ? _level.name : _level.levelId,
                _level.width, _level.depth, _level.maxHeight, _blockCount, _seed);

            _level.solution = generated.solution;
            EditorUtility.SetDirty(_level);
            _message = $"已生成 {_level.solution.Length} 块造型（种子 {_seed}）";
        }

        private void Validate()
        {
            if (_level == null) { _message = "未选择关卡"; return; }

            var problems = new List<string>();
            var seen = new HashSet<Vector3Int>();

            if (_level.solution == null || _level.solution.Length == 0)
                problems.Add("标准解为空");
            else
            {
                foreach (var v in _level.solution)
                {
                    if (v.x < 0 || v.x >= _level.width || v.z < 0 || v.z >= _level.depth || v.y < 0 || v.y >= _level.maxHeight)
                        problems.Add($"体素越界：{v}");
                    if (!seen.Add(v)) problems.Add($"重复体素：{v}");
                }
            }

            var grid = _level.CreateGrid();
            foreach (var v in _level.solution) grid.Add(v);
            if (!LevelJudge.IsSolved(grid, _level)) problems.Add("标准解无法判过关（数据异常）");

            if (!HasAny(_level.TargetFront)) problems.Add("后墙目标投影为空");
            if (!HasAny(_level.TargetLeft)) problems.Add("左墙目标投影为空");

            _message = problems.Count == 0 ? "✅ 校验通过" : "⚠️ " + string.Join("；", problems);
        }

        private static bool HasAny(bool[,] table)
        {
            for (int x = 0; x < table.GetLength(0); x++)
                for (int y = 0; y < table.GetLength(1); y++)
                    if (table[x, y]) return true;
            return false;
        }
    }
}
