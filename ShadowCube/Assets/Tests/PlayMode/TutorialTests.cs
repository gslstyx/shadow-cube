using System.Collections;
using NUnit.Framework;
using ShadowCube.Game;
using ShadowCube.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
            Assert.AreEqual(4, _tutorial.TotalSteps);
            StringAssert.Contains("1/4", _tutorial.StepLabel.text);
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
