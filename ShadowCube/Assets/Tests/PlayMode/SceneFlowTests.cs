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
    /// <summary>首页 / 选关 / 游玩 三场景流程（对应 TC-UI-01、TC-LEVEL-01/02）</summary>
    public class SceneFlowTests
    {
        private static IEnumerator LoadScene(string name)
        {
            SceneManager.LoadScene(name);
            yield return null;
            yield return null;
        }

        /// <summary>等待场景切换完成（按钮内部会自行 LoadScene）</summary>
        private static IEnumerator WaitForScene(string name, float timeout = 5f)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != name && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            yield return null;
        }

        /// <summary>等待场景重载后拿到新的 GameController（同一场景名重载场景）</summary>
        private static IEnumerator WaitForNewController(GameController old, float timeout = 5f)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var current = Object.FindObjectOfType<GameController>();
                if (current != null && current != old) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator 首页_按钮齐备且可进入选关()
        {
            yield return LoadScene(LevelFlow.SceneMainMenu);

            var ui = Object.FindObjectOfType<MainMenuUI>();
            Assert.IsNotNull(ui, "首页缺少 MainMenuUI");
            Assert.IsNotNull(ui.PlayButton);
            Assert.IsNotNull(ui.LevelSelectButton);

            ui.LevelSelectButton.onClick.Invoke();
            yield return LoadScene(LevelFlow.SceneLevelSelect);

            Assert.AreEqual(LevelFlow.SceneLevelSelect, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator 选关_仅首关可点_通关后解锁第二关()
        {
            yield return LoadScene(LevelFlow.SceneLevelSelect);

            var ui = Object.FindObjectOfType<LevelSelectUI>();
            var progress = Object.FindObjectOfType<ProgressManager>();
            Assert.IsNotNull(ui, "选关场景缺少 LevelSelectUI");
            Assert.IsNotNull(progress, "选关场景缺少 ProgressManager");

            progress.ResetProgress();
            Assert.AreEqual(0, progress.TotalStars, "重置后应为 0 星（进度未清空）");
            Assert.AreEqual(1, progress.UnlockedCount, "重置后应仅首关解锁");

            ui.Refresh();

            Assert.Greater(ui.LevelButtons.Count, 1, "关卡数量不足，无法验证解锁");
            Assert.IsTrue(ui.LevelButtons[0].interactable, "首关应可点");
            Assert.IsFalse(ui.LevelButtons[1].interactable, "第二关初始应锁定");
            StringAssert.Contains("Locked", ui.LevelLabels[1].text);

            progress.RecordResult(progress.catalog.Get(0), 3);
            ui.Refresh();

            Assert.IsTrue(ui.LevelButtons[1].interactable, "通关首关后第二关应解锁");
            StringAssert.Contains("3", ui.LevelLabels[0].text, "首关应显示星级");
        }

        [UnityTest]
        public IEnumerator 点击关卡_进入游玩场景且载入该关卡()
        {
            yield return LoadScene(LevelFlow.SceneLevelSelect);

            var ui = Object.FindObjectOfType<LevelSelectUI>();
            var progress = Object.FindObjectOfType<ProgressManager>();
            progress.ResetProgress();
            ui.Refresh();

            var target = progress.catalog.Get(0);

            Assert.IsNotNull(LevelFlow.Instance, "LevelFlow 缺失（UI 无法跳转）");
            Assert.IsTrue(ui.LevelButtons[0].interactable, "首关按钮不可点");
            Assert.IsTrue(progress.IsUnlocked(target), "首关未解锁");

            ui.LevelButtons[0].onClick.Invoke();   // 内部会自行 LoadScene

            yield return WaitForScene(LevelFlow.SceneGame, 10f);
            yield return null;

            var controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(controller,
                $"游玩场景缺少 GameController（当前场景={SceneManager.GetActiveScene().name}，场景数={SceneManager.sceneCount}）");
            Assert.AreSame(target, controller.level, "未载入选中的关卡");
        }

        [UnityTest]
        public IEnumerator 结算下一关_进入下一关()
        {
            yield return LoadScene(LevelFlow.SceneLevelSelect);

            var ui = Object.FindObjectOfType<LevelSelectUI>();
            var progress = Object.FindObjectOfType<ProgressManager>();
            progress.ResetProgress();
            progress.RecordResult(progress.catalog.Get(0), 3);
            ui.Refresh();

            ui.LevelButtons[1].onClick.Invoke();
            yield return LoadScene(LevelFlow.SceneGame);

            var controller = Object.FindObjectOfType<GameController>();
            var settlement = Object.FindObjectOfType<SettlementPanel>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(settlement);

            var level = controller.level;
            foreach (var v in level.solution) controller.AddAt(v.x, v.z);
            yield return null;

            Assert.IsTrue(settlement.IsVisible, "过关未弹结算");
            Assert.IsTrue(settlement.HasNextLevel(), "第二关之后应有下一关（若关卡数 ≥3）");

            var next = progress.NextLevel(level);
            settlement.NextButton.onClick.Invoke();  // 内部会自行 LoadScene

            yield return WaitForNewController(controller);

            var controller2 = Object.FindObjectOfType<GameController>();
            Assert.AreSame(next, controller2.level, "下一关未生效");
        }

        [UnityTest]
        public IEnumerator 切换关卡_清空状态并重建投影()
        {
            yield return LoadScene(LevelFlow.SceneGame);

            var controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(controller);

            var flow = Object.FindObjectOfType<LevelFlow>();
            Assert.IsNotNull(flow, "缺少 LevelFlow");
            Assert.GreaterOrEqual(flow.catalog.Count, 2, "需要至少 2 个关卡");

            controller.AddAt(0, 0);
            Assert.AreEqual(1, controller.BlockCount);

            var another = flow.catalog.Get(1);
            controller.LoadLevel(another);
            yield return null;

            Assert.AreSame(another, controller.level);
            Assert.AreEqual(0, controller.BlockCount, "切换关卡后应清空方块");
            Assert.IsFalse(controller.IsSolved);
            Assert.AreEqual(another.width, controller.Grid.Width, "网格尺寸未更新");
        }
    }
}
