using System.Collections;
using NUnit.Framework;
using ShadowCube.Core;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>音效接口注入、过关结算动效、拖拽轨迹的运行时行为</summary>
    public class FeedbackAndSfxTests
    {
        private class CountingSfxPlayer : ISfxPlayer
        {
            public int Adds, Removes, Solved;
            public void PlayAdd() => Adds++;
            public void PlayRemove() => Removes++;
            public void PlaySolved() => Solved++;
        }

        private GameController _controller;
        private CountingSfxPlayer _sfx;

        [UnitySetUp]
        public IEnumerator 加载场景并注入音效替身()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(_controller);

            _sfx = new CountingSfxPlayer();
            _controller.Sfx = _sfx;
            _controller.ResetLevel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 生成与消除_分别触发对应音效()
        {
            _controller.AddAt(0, 0);
            _controller.AddAt(0, 0);
            _controller.RemoveAt(0, 0);

            Assert.AreEqual(2, _sfx.Adds, "生成音次数不符");
            Assert.AreEqual(1, _sfx.Removes, "消除音次数不符");
            Assert.AreEqual(0, _sfx.Solved);

            yield return null;
        }

        [UnityTest]
        public IEnumerator 过关_播放过关音并触发结算动效且只触发一次()
        {
            foreach (var v in _controller.level.solution) _controller.AddAt(v.x, v.z);

            Assert.AreEqual(1, _sfx.Solved, "过关音应播放一次");
            Assert.IsTrue(_controller.SolvedFeedbackPlayed, "结算动效未触发");

            // 已过关后再改动不应重复触发
            _controller.AddAt(0, 3);
            Assert.AreEqual(1, _sfx.Solved, "过关音不应重复播放");

            yield return null;
        }

        [UnityTest]
        public IEnumerator 重置_清除结算状态()
        {
            foreach (var v in _controller.level.solution) _controller.AddAt(v.x, v.z);
            Assert.IsTrue(_controller.SolvedFeedbackPlayed);

            _controller.ResetLevel();
            yield return null;

            Assert.IsFalse(_controller.SolvedFeedbackPlayed);
            Assert.IsFalse(_controller.IsSolved);
        }

        [UnityTest]
        public IEnumerator 拖拽轨迹_记录路径点并在空闲后自动淡出清空()
        {
            var trail = _controller.Trail;
            Assert.IsNotNull(trail, "未创建轨迹组件");

            trail.Clear();
            trail.AddPoint(new Vector3(0f, 0.06f, 0f));
            trail.AddPoint(new Vector3(1f, 0.06f, 0f));
            trail.AddPoint(new Vector3(2f, 0.06f, 0f));
            Assert.AreEqual(3, trail.PointCount);

            // 同一格重复添加应被去重
            trail.AddPoint(new Vector3(2f, 0.06f, 0f));
            Assert.AreEqual(3, trail.PointCount, "过近的点应被忽略");

            // 等待 hold + fade 后自动清空
            yield return new WaitForSeconds(trail.holdSeconds + trail.fadeSeconds + 0.2f);
            Assert.AreEqual(0, trail.PointCount, "轨迹应自动淡出清空");
        }

        [UnityTest]
        public IEnumerator 静音开关_可设置()
        {
            _controller.MuteAudio = true;
            Assert.IsTrue(_controller.MuteAudio);
            _controller.MuteAudio = false;
            Assert.IsFalse(_controller.MuteAudio);

            yield return null;
        }
    }
}
