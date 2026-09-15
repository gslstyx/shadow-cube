using System.Collections;
using NUnit.Framework;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>玩法链路自动化：通过 GameController 公开 API 驱动生成/消除/重置与过关判定</summary>
    public class GameplayFlowTests
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
        public IEnumerator 生成方块_计数与投影同步()
        {
            Assert.IsTrue(_controller.AddAt(0, 0), "生成失败");
            Assert.AreEqual(1, _controller.BlockCount);
            Assert.IsTrue(_controller.HasVoxelInColumn(0, 0));

            yield return null;
            Assert.IsFalse(_controller.IsSolved, "只放一块不应过关");
        }

        [UnityTest]
        public IEnumerator 同列连续生成_自动叠放受高度上限约束()
        {
            var level = _controller.level;
            for (int i = 0; i < level.maxHeight; i++)
                Assert.IsTrue(_controller.AddAt(2, 2), $"第 {i} 层应可放置");

            Assert.IsFalse(_controller.AddAt(2, 2), "超出 maxHeight 应放置失败");
            Assert.AreEqual(level.maxHeight, _controller.BlockCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator 消除_移除该列顶部方块()
        {
            _controller.AddAt(1, 1);
            _controller.AddAt(1, 1);
            Assert.AreEqual(2, _controller.BlockCount);

            Assert.IsTrue(_controller.RemoveAt(1, 1));
            Assert.AreEqual(1, _controller.BlockCount);

            Assert.IsTrue(_controller.RemoveAt(1, 1));
            Assert.IsFalse(_controller.RemoveAt(1, 1), "空列无法再消除");

            yield return null;
        }

        [UnityTest]
        public IEnumerator 搭建标准解_过关且为五星()
        {
            var level = _controller.level;
            foreach (var v in level.solution)
                Assert.IsTrue(_controller.AddAt(v.x, v.z), $"({v.x},{v.z}) 放置失败");

            Assert.IsTrue(_controller.IsSolved, "标准解应判过关");
            Assert.AreEqual(level.OptimalCount, _controller.BlockCount);
            Assert.AreEqual(5, _controller.StarRating);

            yield return null;
        }

        [UnityTest]
        public IEnumerator 重置_清空方块与过关状态()
        {
            foreach (var v in _controller.level.solution) _controller.AddAt(v.x, v.z);
            Assert.IsTrue(_controller.IsSolved);

            _controller.ResetLevel();
            yield return null;

            Assert.AreEqual(0, _controller.BlockCount);
            Assert.IsFalse(_controller.IsSolved);
            Assert.AreEqual(0, _controller.StarRating);
        }
    }
}
