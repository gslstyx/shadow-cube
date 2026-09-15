using System.Collections;
using NUnit.Framework;
using ShadowCube.Game;
using ShadowCube.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>HUD 与结算界面（对应 TC-UI-02/07、TC-UI-09 的自动化部分）</summary>
    public class HudAndSettlementTests
    {
        private GameController _controller;
        private GameHud _hud;
        private SettlementPanel _settlement;

        [UnitySetUp]
        public IEnumerator 加载场景()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            _hud = Object.FindObjectOfType<GameHud>();
            _settlement = Object.FindObjectOfType<SettlementPanel>();

            Assert.IsNotNull(_controller, "场景缺少 GameController");
            Assert.IsNotNull(_hud, "场景缺少 GameHud");
            Assert.IsNotNull(_settlement, "场景缺少 SettlementPanel");

            _hud.controller = _controller;
            _settlement.controller = _controller;

            _controller.ResetLevel();
            _settlement.Hide();
            _hud.Refresh();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HUD_按钮与文本齐备()
        {
            Assert.IsNotNull(_hud.BackButton);
            Assert.IsNotNull(_hud.ResetButton);
            Assert.IsNotNull(_hud.RotateLeftButton);
            Assert.IsNotNull(_hud.RotateRightButton);
            Assert.IsNotNull(_hud.LevelLabel);
            Assert.IsNotNull(_hud.StarLabel);
            Assert.IsTrue(_hud.LevelLabel.text.Contains("-"), "关卡号未显示");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 重置按钮_清空方块()
        {
            _controller.AddAt(0, 0);
            _controller.AddAt(1, 1);
            Assert.AreEqual(2, _controller.BlockCount);

            _hud.ResetButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, _controller.BlockCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 旋转按钮_改变相机朝向()
        {
            _hud.RotateRightButton.onClick.Invoke();

            float timeout = 2f;
            while (_controller.Rig.IsSnapping && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _controller.Rig.Turns, "右旋按钮未生效");

            _hud.RotateLeftButton.onClick.Invoke();
            timeout = 2f;
            while (_controller.Rig.IsSnapping && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0, _controller.Rig.Turns, "左旋按钮未生效");
        }

        [UnityTest]
        public IEnumerator 返回按钮_触发回调()
        {
            bool called = false;
            _hud.BackRequested = () => called = true;

            _hud.BackButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(called, "返回回调未触发");
        }

        [UnityTest]
        public IEnumerator 过关_结算面板自动弹出并显示星级()
        {
            Assert.IsFalse(_settlement.IsVisible, "初始不应显示结算");

            _controller.ResetLevel();
            foreach (var v in _controller.level.solution) _controller.AddAt(v.x, v.z);
            yield return null;

            Assert.IsTrue(_settlement.IsVisible, "过关后未弹出结算");
            Assert.AreEqual(5, _settlement.ShownStars, "最优解应为 5 星");
            Assert.IsTrue(_settlement.StarLabel.text.Contains("5"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator 无后续关卡时_下一关按钮隐藏()
        {
            _settlement.Show(5);
            yield return null;

            Assert.IsFalse(_settlement.HasNextLevel(), "当前只有一关，不应有下一关");
            Assert.IsFalse(_settlement.NextButton.gameObject.activeSelf, "下一关按钮应隐藏");
        }

        [UnityTest]
        public IEnumerator 重玩按钮_关闭面板并清空棋盘()
        {
            foreach (var v in _controller.level.solution) _controller.AddAt(v.x, v.z);
            Assert.IsTrue(_settlement.IsVisible);

            _settlement.RetryButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(_settlement.IsVisible, "重玩后面板应关闭");
            Assert.AreEqual(0, _controller.BlockCount, "重玩后棋盘应清空");
            Assert.IsFalse(_controller.IsSolved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 星级显示_读取存档中的最好成绩()
        {
            var progress = Object.FindObjectOfType<ProgressManager>();
            Assert.IsNotNull(progress, "场景缺少 ProgressManager");

            // 先清进度，避免同一次运行中其它用例写入的更高星级影响断言（测试间共享存档）
            progress.ResetProgress();
            progress.RecordResult(_controller.level, 4);

            _controller.hud = _hud;
            _hud.Refresh();
            yield return null;

            int best = progress.GetStars(_controller.level);
            Assert.AreEqual(4, best);
            Assert.IsTrue(_hud.StarLabel.text.Contains("4"), $"最好星级未显示：{_hud.StarLabel.text}");
        }
    }
}
