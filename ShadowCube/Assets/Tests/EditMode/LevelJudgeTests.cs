using NUnit.Framework;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Tests.EditMode
{
    public class LevelJudgeTests
    {
        private static LevelData MakeLevel(params Vector3Int[] solution)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.width = 3;
            level.depth = 3;
            level.maxHeight = 3;
            level.solution = solution;
            return level;
        }

        private static VoxelGrid Build(LevelData level, params Vector3Int[] voxels)
        {
            var grid = level.CreateGrid();
            foreach (var v in voxels) grid.Add(v);
            return grid;
        }

        [Test]
        public void 标准解_判定过关()
        {
            var level = MakeLevel(new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 1));
            Assert.IsTrue(LevelJudge.IsSolved(Build(level, new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 1)), level));
        }

        [Test]
        public void 允许被完全遮挡的额外方块_仍过关()
        {
            // (1,0,0) 的 front(x=1,y=0) 与 left(z=0,y=0) 均已被占用 → 投影不变
            var level = MakeLevel(new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 1));
            var grid = Build(level,
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 0, 1),
                new Vector3Int(1, 0, 0));

            Assert.AreEqual(3, grid.Count);
            Assert.IsTrue(LevelJudge.IsSolved(grid, level));
        }

        [Test]
        public void 单面不符_不过关()
        {
            var level = MakeLevel(new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 1));
            var grid = Build(level, new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 2));
            Assert.IsFalse(LevelJudge.IsSolved(grid, level));
        }

        [Test]
        public void 空盘_不过关()
        {
            var level = MakeLevel(new Vector3Int(0, 0, 0));
            Assert.IsFalse(LevelJudge.IsSolved(level.CreateGrid(), level));
        }

        [Test]
        public void 相机旋转90度_两墙互换仍过关()
        {
            // 标准解沿 Z 排开；玩家搭成沿 X 排开（相当于旋转 90°）
            var level = MakeLevel(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1));
            var grid = Build(level, new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0));
            Assert.IsTrue(LevelJudge.IsSolved(grid, level));
        }

        [Test]
        public void 星级_边界值正确()
        {
            Assert.AreEqual(5, LevelJudge.GetStars(10, 10));
            Assert.AreEqual(4, LevelJudge.GetStars(11, 10));
            Assert.AreEqual(4, LevelJudge.GetStars(12, 10));
            Assert.AreEqual(3, LevelJudge.GetStars(13, 10));
            Assert.AreEqual(3, LevelJudge.GetStars(14, 10));
            Assert.AreEqual(2, LevelJudge.GetStars(15, 10));
            Assert.AreEqual(2, LevelJudge.GetStars(16, 10));
            Assert.AreEqual(1, LevelJudge.GetStars(17, 10));
            Assert.AreEqual(0, LevelJudge.GetStars(0, 10));
        }
    }
}
