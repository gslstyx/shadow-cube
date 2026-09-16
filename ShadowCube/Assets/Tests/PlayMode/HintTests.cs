using System;
using System.Collections;
using NUnit.Framework;
using ShadowCube.Core;
using ShadowCube.Game;
using ShadowCube.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>
    /// 提示系统（需求 §2.2-F / §2.2-G）：免费关直接演示、付费关需看完激励视频、
    /// 演示只演"开头操作"且结束后棋盘原样、演示期间屏蔽玩法输入。
    /// </summary>
    public class HintTests
    {
        private GameController _controller;
        private HintService _hint;
        private HintDemoPlayer _demo;
        private GameHud _hud;

        /// <summary>替代广告服务：记录调用并可控返回结果</summary>
        private class FakeAdService : IAdService
        {
            public int Calls;
            public string LastPlacement;
            public RewardedResult Result = RewardedResult.Watched;

            public bool IsAvailable => true;

            public void ShowRewarded(string placement, Action<RewardedResult> onDone)
            {
                Calls++;
                LastPlacement = placement;
                onDone?.Invoke(Result);
            }
        }

        [UnitySetUp]
        public IEnumerator 加载场景()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;
            yield return null;   // 等 Awake 完成

            _controller = UnityEngine.Object.FindObjectOfType<GameController>();
            _hint = UnityEngine.Object.FindObjectOfType<HintService>();
            _demo = UnityEngine.Object.FindObjectOfType<HintDemoPlayer>();
            _hud = UnityEngine.Object.FindObjectOfType<GameHud>();

            Assert.IsNotNull(_controller);
            Assert.IsNotNull(_hint, "场景缺少 HintService");
            Assert.IsNotNull(_demo, "场景缺少 HintDemoPlayer");

            _hint.controller = _controller;
            _hint.demo = _demo;
            _hint.freeHintLevels = 5;

            // 演示节奏调快，便于测试
            _demo.startDelay = 0.02f;
            _demo.moveDuration = 0.05f;
            _demo.holdDuration = 0.02f;

            _controller.ResetLevel();
            yield return null;
        }

        private IEnumerator 等待演示结束(float timeout = 5f)
        {
            float t = 0f;
            while ((_hint.IsPlaying || _demo.IsPlaying) && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(_demo.IsPlaying, "提示演示没有在超时内结束");
        }

        [UnityTest]
        public IEnumerator HUD_有提示按钮且点击走提示服务()
        {
            Assert.IsNotNull(_hud, "场景缺少 GameHud");
            Assert.IsNotNull(_hud.HintButton, "HUD 缺少右下角提示按钮");

            var ads = new FakeAdService { Result = RewardedResult.Skipped };
            _hint.Ads = ads;
            _hint.freeHintLevels = 0;          // 强制走广告分支

            _hud.OnHintClicked();
            yield return null;

            Assert.AreEqual(1, ads.Calls, "收费关点击提示应先请求广告");
            Assert.AreEqual("hint", ads.LastPlacement);
        }

        [UnityTest]
        public IEnumerator 免费关_不看广告直接演示()
        {
            var ads = new FakeAdService();
            _hint.Ads = ads;

            Assert.IsTrue(_hint.IsFreeForCurrentLevel, "第 1 关应免费");

            _hint.RequestHint();
            yield return null;

            Assert.AreEqual(0, ads.Calls, "免费关不应请求广告");
            Assert.AreEqual(HintRequestOutcome.PlayedFree, _hint.LastOutcome);
            Assert.AreEqual(1, _hint.UsedCount);

            yield return 等待演示结束();
        }

        [UnityTest]
        public IEnumerator 收费关_看完广告才演示()
        {
            var ads = new FakeAdService { Result = RewardedResult.Skipped };
            _hint.Ads = ads;
            _hint.freeHintLevels = 0;

            _hint.RequestHint();
            yield return null;

            Assert.AreEqual(1, ads.Calls, "应收看一次激励视频");
            Assert.AreEqual(HintRequestOutcome.AdDeclined, _hint.LastOutcome, "未看完广告不应播放演示");
            Assert.IsFalse(_demo.IsPlaying);
            Assert.AreEqual(0, _hint.UsedCount);

            ads.Result = RewardedResult.Watched;
            _hint.RequestHint();
            yield return null;

            Assert.AreEqual(HintRequestOutcome.PlayedAfterAd, _hint.LastOutcome);
            Assert.AreEqual(1, _hint.UsedCount);

            yield return 等待演示结束();
        }

        [UnityTest]
        public IEnumerator 演示期间_屏蔽玩法输入()
        {
            _hint.RequestHint();
            yield return null;

            Assert.IsTrue(_controller.InputBlocked, "演示期间应屏蔽玩法输入");
            Assert.IsTrue(_demo.IsPlaying);

            yield return 等待演示结束();

            Assert.IsFalse(_controller.InputBlocked, "演示结束应恢复输入");
        }

        [UnityTest]
        public IEnumerator 演示会生成方块_结束后棋盘原样恢复()
        {
            // 玩家先摆 2 块，演示不应破坏已有进度
            Assert.IsTrue(_controller.AddAt(0, 0));
            Assert.IsTrue(_controller.AddAt(0, 1));
            int before = _controller.BlockCount;

            _hint.RequestHint();

            // 演示过程中应能看到方块被"生成"出来
            float t = 0f;
            bool grew = false;
            while (t < 2f && _demo.IsPlaying)
            {
                if (_controller.BlockCount > before) { grew = true; break; }
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(grew, "演示应在平台上连续生成方块");

            yield return 等待演示结束();

            Assert.AreEqual(before, _controller.BlockCount, "演示结束后棋盘应恢复到演示前的状态");
            Assert.IsFalse(_controller.IsSolved, "演示不应把关卡解出来");
        }

        [UnityTest]
        public IEnumerator 演示会复位到推荐视角()
        {
            _controller.Rig.SnapToTurn(2);
            yield return null;

            _hint.RequestHint();
            yield return null;

            Assert.AreEqual(0, _controller.Rig.Turns, "提示应把相机转回推荐视角");
            yield return 等待演示结束();
        }
    }
}
