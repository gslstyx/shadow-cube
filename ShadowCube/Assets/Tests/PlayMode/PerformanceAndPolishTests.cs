using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>M4 表现打磨与性能：网格线可见、方块对象池复用、批量操作耗时（TC-PERF-*）</summary>
    public class PerformanceAndPolishTests
    {
        private GameController _controller;

        [UnitySetUp]
        public IEnumerator 加载场景()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(_controller);
            _controller.ResetLevel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 平台网格线_运行时可见()
        {
            Assert.IsNotNull(_controller.GridLines, "未生成平台网格线");
            Assert.Greater(_controller.GridLines.positionCount, 0);

            int expected = (_controller.level.width + 1 + _controller.level.depth + 1) * 2;
            Assert.AreEqual(expected, _controller.GridLines.positionCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 方块回收进对象池_不重复创建()
        {
            var level = _controller.level;

            // 放置一批方块
            for (int x = 0; x < level.width; x++)
                for (int z = 0; z < level.depth; z++)
                    _controller.AddAt(x, z);

            int placed = _controller.BlockCount;
            Assert.Greater(placed, 0);

            // 全部移除（带消散动画，需等动画结束才回收）
            for (int x = 0; x < level.width; x++)
                for (int z = 0; z < level.depth; z++)
                    _controller.RemoveAt(x, z);

            Assert.AreEqual(0, _controller.BlockCount);
            yield return new WaitForSeconds(_controller.despawnDuration + 0.2f);

            Assert.Greater(_controller.PooledVoxelCount, 0, "方块未回收进对象池");

            // 再次放置应复用池中对象，而不是再新建一堆
            int pooled = _controller.PooledVoxelCount;
            _controller.AddAt(0, 0);
            Assert.Less(_controller.PooledVoxelCount, pooled, "复用池后空闲数应减少");
        }

        [UnityTest]
        public IEnumerator 批量增删_判定链路耗时可控()
        {
            var level = _controller.level;
            var sw = new Stopwatch();

            sw.Start();
            for (int i = 0; i < 150; i++)
            {
                int x = i % level.width;
                int z = (i / level.width) % level.depth;

                if (_controller.HasVoxelInColumn(x, z)) _controller.RemoveAt(x, z);
                else _controller.AddAt(x, z);
            }
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 2000,
                $"150 次增删 + 判定 + 投影刷新耗时 {sw.ElapsedMilliseconds}ms，超过阈值");

            UnityEngine.Debug.Log($"[Perf] 150 次增删耗时 {sw.ElapsedMilliseconds}ms");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 反复重置_不泄漏方块对象()
        {
            for (int round = 0; round < 5; round++)
            {
                for (int i = 0; i < 8; i++)
                    _controller.AddAt(i % _controller.level.width, (i * 2) % _controller.level.depth);
                _controller.ResetLevel();
            }

            yield return new WaitForSeconds(_controller.despawnDuration + 0.2f);

            Assert.AreEqual(0, _controller.BlockCount);
            Assert.Greater(_controller.PooledVoxelCount, 0, "重置后方块应回收到池中");
        }
    }
}
