using System;
using System.Collections.Generic;
using ShadowCube.Core;
using UnityEditor;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 关卡生成器：用固定种子生成**连通**体素造型作为标准解，目标投影由 LevelData 自动推导。
    /// 保证：不越界、不重复、块数可控，避免生成无解或退化关卡。
    /// </summary>
    public static class LevelGenerator
    {
        public static LevelData Generate(string id, int width, int depth, int maxHeight, int blockCount, int seed)
        {
            var random = new System.Random(seed);
            blockCount = Mathf.Clamp(blockCount, 2, width * depth * maxHeight);

            var shape = new HashSet<Vector3Int>();
            var current = new Vector3Int(random.Next(width), 0, random.Next(depth));
            shape.Add(current);

            var frontier = new List<Vector3Int>(Neighbors(current, width, depth, maxHeight));
            while (shape.Count < blockCount && frontier.Count > 0)
            {
                int index = random.Next(frontier.Count);
                var next = frontier[index];
                frontier.RemoveAt(index);

                if (!shape.Add(next)) continue;
                current = next;

                foreach (var n in Neighbors(next, width, depth, maxHeight))
                    if (!shape.Contains(n) && !frontier.Contains(n)) frontier.Add(n);
            }

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId = id;
            level.levelName = id;
            level.width = width;
            level.depth = depth;
            level.maxHeight = maxHeight;

            var voxels = new List<Vector3Int>(shape);
            voxels.Sort((a, b) => (a.y != b.y) ? a.y.CompareTo(b.y) : (a.x != b.x) ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            level.solution = voxels.ToArray();

            return level;
        }

        private static IEnumerable<Vector3Int> Neighbors(Vector3Int c, int width, int depth, int maxHeight)
        {
            var candidates = new[]
            {
                c + Vector3Int.right, c + Vector3Int.left,
                c + new Vector3Int(0, 0, 1), c + new Vector3Int(0, 0, -1),
                c + Vector3Int.up
            };

            foreach (var n in candidates)
                if (n.x >= 0 && n.x < width && n.z >= 0 && n.z < depth && n.y >= 0 && n.y < maxHeight)
                    yield return n;
        }

        /// <summary>生成一批关卡资产（id 形如 C{chapter}-L{index:D2}）</summary>
        [MenuItem("Tools/Shadow Cube/7) 生成关卡（前 2 章 × 10 关）")]
        public static void GenerateTwoChapters() => GenerateTwoChapters(false);

        /// <summary>按最新曲线重建关卡（覆盖已有资产）：修改层数/难度曲线后用它重新量产</summary>
        [MenuItem("Tools/Shadow Cube/7b) 重建关卡（按 1→3 层渐进曲线，覆盖）")]
        public static void RegenerateTwoChapters() => GenerateTwoChapters(true);

        public static void GenerateTwoChapters(bool overwrite)
        {
            const string dir = "Assets/Data/Levels";
            int seed = 20260916;
            int created = 0;

            for (int chapter = 1; chapter <= 2; chapter++)
            {
                for (int i = 1; i <= 10; i++)
                {
                    string id = $"C{chapter}-L{i:D2}";
                    string path = $"{dir}/{id}.asset";

                    bool exists = AssetDatabase.LoadAssetAtPath<LevelData>(path) != null;
                    if (exists && !overwrite) continue;

                    // 难度递增：块数 4 → 13，尺寸 4x4 → 6x6
                    int step = (chapter - 1) * 10 + (i - 1);
                    int size = 4 + step / 7;
                    int blocks = Mathf.Clamp(4 + step / 2, 4, 13);

                    // 层数曲线（需求 §4.3）：第 1 章 1 → 3 层渐进
                    //   1~2 关：1 层（入门）｜ 3~6 关：2 层 ｜ 7~10 关：3 层
                    //   第 2 章：3 层起步，后段放开到 4 层
                    int height = step switch
                    {
                        < 2 => 1,
                        < 6 => 2,
                        < 10 => 3,
                        _ => 3 + (step - 10) / 7
                    };

                    if (exists) AssetDatabase.DeleteAsset(path);

                    var level = Generate(id, size, size, Mathf.Clamp(height, 1, 4), blocks, seed + step);
                    AssetDatabase.CreateAsset(level, path);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            MenuSceneBuilder.RefreshCatalog();
            Debug.Log($"[ShadowCube] 关卡生成完成：{created} 关（覆盖模式={overwrite}，共 2 章，1→3 层渐进）");
        }
    }
}
