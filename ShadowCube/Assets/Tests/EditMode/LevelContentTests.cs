using System.Collections.Generic;
using NUnit.Framework;
using ShadowCube.Core;
using UnityEditor;
using UnityEngine;

namespace ShadowCube.Tests.EditMode
{
    /// <summary>关卡内容校验：所有 LevelData 资产的合法性（M3 量产关卡后的数据防线）</summary>
    public class LevelContentTests
    {
        private static LevelData[] AllLevels()
        {
            var guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Data/Levels" });
            var levels = new List<LevelData>();
            foreach (var guid in guids)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level != null) levels.Add(level);
            }
            return levels.ToArray();
        }

        [Test]
        public void 关卡资产_至少存在一个()
        {
            Assert.Greater(AllLevels().Length, 0, "Assets/Data/Levels 下没有关卡资产");
        }

        [Test]
        public void 每关_有唯一ID且不为空()
        {
            var ids = new HashSet<string>();
            foreach (var level in AllLevels())
            {
                Assert.IsFalse(string.IsNullOrEmpty(level.Id), $"{level.name} 缺少 levelId");
                Assert.IsTrue(ids.Add(level.Id), $"关卡 ID 重复：{level.Id}");
            }
        }

        [Test]
        public void 每关_标准解非空且全部在边界内()
        {
            foreach (var level in AllLevels())
            {
                Assert.IsNotNull(level.solution, $"{level.Id} 没有标准解");
                Assert.Greater(level.solution.Length, 0, $"{level.Id} 标准解为空");

                var seen = new HashSet<Vector3Int>();
                foreach (var v in level.solution)
                {
                    Assert.IsTrue(v.x >= 0 && v.x < level.width, $"{level.Id} 体素 x 越界：{v}");
                    Assert.IsTrue(v.z >= 0 && v.z < level.depth, $"{level.Id} 体素 z 越界：{v}");
                    Assert.IsTrue(v.y >= 0 && v.y < level.maxHeight, $"{level.Id} 体素 y 越界：{v}");
                    Assert.IsTrue(seen.Add(v), $"{level.Id} 标准解存在重复体素：{v}");
                }
            }
        }

        [Test]
        public void 每关_目标投影非空且最优数与标准解一致()
        {
            foreach (var level in AllLevels())
            {
                var front = level.TargetFront;
                var left = level.TargetLeft;

                Assert.AreEqual(level.width, front.GetLength(0), $"{level.Id} 后墙投影宽度不符");
                Assert.AreEqual(level.depth, left.GetLength(0), $"{level.Id} 左墙投影宽度不符");
                Assert.IsTrue(HasAny(front), $"{level.Id} 后墙投影为空");
                Assert.IsTrue(HasAny(left), $"{level.Id} 左墙投影为空");
                Assert.AreEqual(level.solution.Length, level.OptimalCount, $"{level.Id} optimalCount 与标准解不一致");
            }
        }

        [Test]
        public void 每关_标准解能判过关()
        {
            foreach (var level in AllLevels())
            {
                var grid = level.CreateGrid();
                foreach (var v in level.solution)
                    Assert.IsTrue(grid.Add(v), $"{level.Id} 标准解体素越界：{v}");

                Assert.IsTrue(LevelJudge.IsSolved(grid, level), $"{level.Id} 标准解未能判过关");
                Assert.AreEqual(5, LevelJudge.GetStars(grid.Count, level.OptimalCount), $"{level.Id} 标准解应为 5 星");
            }
        }

        /// <summary>需求 §4.3：前 10 关按 1 → 3 层渐进引入</summary>
        [Test]
        public void 第一章_层数按1到3渐进()
        {
            var map = new Dictionary<string, LevelData>();
            foreach (var level in AllLevels()) map[level.Id] = level;

            string[][] expected =
            {
                new[] { "C1-L01", "C1-L02" },                                   // 1 层（入门）
                new[] { "C1-L03", "C1-L04", "C1-L05", "C1-L06" },               // 2 层
                new[] { "C1-L07", "C1-L08", "C1-L09", "C1-L10" }                // 3 层
            };

            for (int tier = 0; tier < expected.Length; tier++)
            {
                foreach (var id in expected[tier])
                {
                    if (!map.TryGetValue(id, out var level)) continue;
                    Assert.AreEqual(tier + 1, level.maxHeight, $"{id} 的 maxHeight 应为 {tier + 1}（1→3 层渐进）");
                }
            }
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
