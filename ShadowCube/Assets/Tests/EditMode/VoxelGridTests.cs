using NUnit.Framework;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Tests.EditMode
{
    public class VoxelGridTests
    {
        [Test]
        public void Add_添加成功且去重()
        {
            var grid = new VoxelGrid(5, 5, 4);
            Assert.IsTrue(grid.Add(new Vector3Int(1, 0, 1)));
            Assert.IsFalse(grid.Add(new Vector3Int(1, 0, 1)));  // 重复
            Assert.AreEqual(1, grid.Count);
        }

        [Test]
        public void Add_越界返回false()
        {
            var grid = new VoxelGrid(3, 3, 2);
            Assert.IsFalse(grid.Add(new Vector3Int(3, 0, 0)));   // x 越界
            Assert.IsFalse(grid.Add(new Vector3Int(0, 2, 0)));   // y 越界（maxHeight=2 → 0,1）
            Assert.IsFalse(grid.Add(new Vector3Int(-1, 0, 0)));  // 负数
            Assert.AreEqual(0, grid.Count);
        }

        [Test]
        public void Remove_移除已有方块()
        {
            var grid = new VoxelGrid(3, 3, 3);
            grid.Add(new Vector3Int(0, 0, 0));
            Assert.IsTrue(grid.Remove(new Vector3Int(0, 0, 0)));
            Assert.AreEqual(0, grid.Count);
        }

        [Test]
        public void 叠放_取该列第一个空位()
        {
            var grid = new VoxelGrid(3, 3, 3);
            grid.Add(new Vector3Int(1, 0, 1));
            Assert.AreEqual(1, grid.GetNextFreeHeight(1, 1));
            grid.Add(new Vector3Int(1, 1, 1));
            grid.Add(new Vector3Int(1, 2, 1));
            Assert.AreEqual(-1, grid.GetNextFreeHeight(1, 1)); // 列满
            Assert.AreEqual(2, grid.GetTopHeight(1, 1));
        }

        [Test]
        public void Clear_清空全部()
        {
            var grid = new VoxelGrid(3, 3, 3);
            grid.Add(new Vector3Int(0, 0, 0));
            grid.Add(new Vector3Int(1, 0, 1));
            grid.Clear();
            Assert.AreEqual(0, grid.Count);
        }
    }
}
