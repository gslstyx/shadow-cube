using NUnit.Framework;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Tests.EditMode
{
    public class ProjectionUtilTests
    {
        [Test]
        public void 单块_两墙投影各占一格()
        {
            var grid = new VoxelGrid(4, 4, 3);
            grid.Add(new Vector3Int(2, 1, 3));

            var front = ProjectionUtil.ProjectFront(grid);
            var left = ProjectionUtil.ProjectLeft(grid);

            Assert.IsTrue(front[2, 1]);
            Assert.IsTrue(left[3, 1]);
            Assert.AreEqual(1, CountTrue(front));
            Assert.AreEqual(1, CountTrue(left));
        }

        [Test]
        public void 同列叠放_投影不增高只增宽()
        {
            var grid = new VoxelGrid(4, 4, 4);
            grid.Add(new Vector3Int(1, 0, 1));
            grid.Add(new Vector3Int(1, 1, 1));
            grid.Add(new Vector3Int(1, 2, 1));

            var front = ProjectionUtil.ProjectFront(grid);
            Assert.IsTrue(front[1, 0] && front[1, 1] && front[1, 2]);
            Assert.AreEqual(3, CountTrue(front));
        }

        [Test]
        public void 被完全遮挡的额外方块_不改变投影()
        {
            var grid = new VoxelGrid(4, 4, 4);
            grid.Add(new Vector3Int(1, 0, 1));
            grid.Add(new Vector3Int(1, 0, 2));  // 沿 X 方向排开
            var before = ProjectionUtil.ProjectFront(grid);

            // 在 (1,0,1) 与 (1,0,2) 之间的 (1,0,?) 无额外格；改为沿 Z 方向再加一个被遮挡项
            grid.Add(new Vector3Int(1, 0, 1));
            var after = ProjectionUtil.ProjectFront(grid);

            Assert.IsTrue(ProjectionUtil.Same(before, after));
        }

        [Test]
        public void Same_尺寸不同直接不等()
        {
            Assert.IsFalse(ProjectionUtil.Same(new bool[3, 3], new bool[4, 3]));
            Assert.IsTrue(ProjectionUtil.Same(new bool[3, 3], new bool[3, 3]));
        }

        [Test]
        public void WallsMatch_左右墙互换仍匹配()
        {
            var a = new bool[2, 2]; a[0, 0] = true;
            var b = new bool[2, 2]; b[1, 1] = true;

            Assert.IsTrue(ProjectionUtil.WallsMatch(a, b, a, b));
            Assert.IsTrue(ProjectionUtil.WallsMatch(a, b, b, a)); // 互换
            Assert.IsFalse(ProjectionUtil.WallsMatch(a, a, a, b));
        }

        private static int CountTrue(bool[,] t)
        {
            int n = 0;
            for (int x = 0; x < t.GetLength(0); x++)
                for (int y = 0; y < t.GetLength(1); y++)
                    if (t[x, y]) n++;
            return n;
        }
    }
}
