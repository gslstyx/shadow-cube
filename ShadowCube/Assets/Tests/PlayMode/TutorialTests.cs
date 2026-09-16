using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShadowCube.Core;
using ShadowCube.Game;
using ShadowCube.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>新手引导：仅首次触发、分步推进、跳过/完成写入存档（TC-UI-04/05/06）</summary>
    public class TutorialTests
    {
        private GameController _controller;
        private TutorialOverlay _tutorial;
        private ProgressManager _progress;

        [UnitySetUp]
        public IEnumerator 加载场景并重置进度()
        {
            Loc.Language = Language.English;   // 文案已本地化；测试固定用英语断言
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            _progress = Object.FindObjectOfType<ProgressManager>();
            _tutorial = Object.FindObjectOfType<TutorialOverlay>();

            Assert.IsNotNull(_controller);
            Assert.IsNotNull(_progress, "场景缺少 ProgressManager");
            Assert.IsNotNull(_tutorial, "场景缺少 TutorialOverlay");

            _tutorial.progress = _progress;
            _progress.ResetProgress();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 首次游玩_自动显示引导并从第一步开始()
        {
            Assert.IsFalse(_progress.Data.tutorialDone, "重置后应显示引导");
            Assert.IsTrue(_tutorial.ShouldShow());

            _tutorial.Show();
            yield return null;

            Assert.IsTrue(_tutorial.IsVisible);
            Assert.AreEqual(0, _tutorial.StepIndex);
            Assert.AreEqual(5, _tutorial.TotalSteps, "引导应覆盖：生成 / 消除 / 旋转 / 观察投影 / 过关");
            StringAssert.Contains("1/5", _tutorial.StepLabel.text);
        }

        /// <summary>
        /// 曾经的缺陷：引导遮罩铺满全屏且 raycastTarget = true，引导期间玩家
        /// **无法拖拽生成方块、也点不到旋转按钮**，与"分步教学、边教边做"冲突。
        /// </summary>
        [UnityTest]
        public IEnumerator 引导遮罩_不拦截玩法与HUD输入()
        {
            _tutorial.Show();
            yield return null;

            var dim = _tutorial.Canvas.transform.Find("Dim");
            Assert.IsNotNull(dim, "引导缺少遮罩层");
            var dimImage = dim.GetComponent<Image>();
            Assert.IsFalse(dimImage.raycastTarget, "引导遮罩不能吃射线，否则玩家无法操作玩法");

            // 在玩法区域做一次 UI 射线检测：不应命中引导层
            var raycaster = _tutorial.Canvas.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(raycaster);
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.62f)
            };
            var results = new List<RaycastResult>();
            raycaster.Raycast(eventData, results);
            Assert.IsEmpty(results, "引导遮罩挡住了屏幕中央（平台区域）的输入");

            // 引导与提示演示不同：不应屏蔽玩法输入
            Assert.IsFalse(_controller.InputBlocked, "引导期间仍应允许玩家操作");
        }

        [UnityTest]
        public IEnumerator 仅在关卡1_1_触发()
        {
            _tutorial.controller = _controller;
            _tutorial.Show();
            _tutorial.Skip();
            _progress.ResetProgress();
            yield return null;

            Assert.IsTrue(_tutorial.ShouldShow(), "第 1 关第 1 小关应触发引导");

            // 换成第 1 关第 2 小关：不再触发
            var other = ScriptableObject.CreateInstance<LevelData>();
            other.width = other.depth = 4;
            other.maxHeight = 2;
            other.chapterId = 1;
            other.levelIndex = 2;
            other.solution = new[] { new Vector3Int(1, 0, 1), new Vector3Int(2, 0, 1) };
            _controller.LoadLevel(other);
            yield return null;

            Assert.IsFalse(_tutorial.ShouldShow(), "非第 1 关不应触发引导");
        }

        [UnityTest]
        public IEnumerator 每一步都有高亮目标()
        {
            _tutorial.Show();
            yield return null;

            Assert.IsNotNull(_tutorial.Highlight, "引导缺少高亮框");

            var sizes = new List<Vector2>();
            for (int i = 0; i < _tutorial.TotalSteps; i++)
            {
                sizes.Add(_tutorial.Highlight.sizeDelta);
                _tutorial.Next();
                yield return null;
            }

            Assert.Greater(sizes.Count, 1);
            bool changed = false;
            for (int i = 1; i < sizes.Count; i++)
                if (sizes[i] != sizes[0]) changed = true;

            Assert.IsTrue(changed, "高亮区域应随步骤变化（指向当前要教的控件）");
        }

        [UnityTest]
        public IEnumerator 逐步推进_到最后一步完成并写入存档()
        {
            _tutorial.Show();
            yield return null;

            for (int i = 1; i < _tutorial.TotalSteps; i++)
            {
                _tutorial.Next();
                Assert.AreEqual(i, _tutorial.StepIndex);
                yield return null;
            }

            Assert.IsTrue(_tutorial.IsVisible, "最后一步仍应显示");
            _tutorial.Next();   // 完成

            Assert.IsFalse(_tutorial.IsVisible, "完成后应关闭");
            Assert.IsTrue(_progress.Data.tutorialDone, "完成后应写入存档");
        }

        [UnityTest]
        public IEnumerator 跳过_立即关闭并标记完成()
        {
            _tutorial.Show();
            yield return null;

            _tutorial.Skip();

            Assert.IsFalse(_tutorial.IsVisible);
            Assert.IsTrue(_progress.Data.tutorialDone);

            // 重新加载场景后不应再显示
            SceneManager.LoadScene("M1_Prototype");
            yield return null;
            yield return null;

            var again = Object.FindObjectOfType<TutorialOverlay>();
            var progress = Object.FindObjectOfType<ProgressManager>();
            again.progress = progress;

            Assert.IsFalse(again.ShouldShow(), "已完成引导后不应再显示");
            Assert.IsFalse(again.IsVisible);
        }

        [UnityTest]
        public IEnumerator 重置进度后_引导再次显示()
        {
            _tutorial.Show();
            _tutorial.Skip();
            Assert.IsTrue(_progress.Data.tutorialDone);

            _progress.ResetProgress();
            yield return null;

            Assert.IsFalse(_progress.Data.tutorialDone, "重置后应清除引导标记");
            Assert.IsTrue(_tutorial.ShouldShow(), "重置后应重新显示引导");
        }
    }
}
