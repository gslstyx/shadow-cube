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
    /// 过关表现（需求 §3.4）：星级逐颗点亮动画 + 主题基线生效 + 重置短动效。
    /// </summary>
    public class SettlementFeedbackTests
    {
        private GameController _controller;
        private SettlementPanel _settlement;

        [UnitySetUp]
        public IEnumerator 加载场景()
        {
            Loc.Language = Language.English;
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            _settlement = Object.FindObjectOfType<SettlementPanel>();
            Assert.IsNotNull(_controller);
            Assert.IsNotNull(_settlement, "场景缺少 SettlementPanel");

            _controller.ResetLevel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 结算_星级逐颗点亮()
        {
            Assert.IsNotNull(_settlement.StarSlots, "结算面板缺少星位");
            Assert.AreEqual(5, _settlement.StarSlots.Length);

            _settlement.Show(3);
            Assert.AreEqual(0, _settlement.AnimatedStars, "刚打开时还没点亮任何星");

            float t = 0f;
            while (_settlement.AnimatedStars < 3 && t < 5f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(3, _settlement.AnimatedStars, "应逐颗点亮到 3 星");
            Assert.IsTrue(_settlement.StarLabel.text.Contains("3"), $"星级文本未更新：{_settlement.StarLabel.text}");
        }

        [UnityTest]
        public IEnumerator 重置_立即清空且播放消散动画()
        {
            Assert.IsTrue(_controller.AddAt(1, 1));
            Assert.IsTrue(_controller.AddAt(2, 2));
            Assert.AreEqual(2, _controller.BlockCount);

            _controller.ResetLevel();
            Assert.AreEqual(0, _controller.BlockCount, "重置后逻辑应立即归零（动画只是表现）");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, _controller.BlockCount);
        }

        [UnityTest]
        public IEnumerator 主题_应用到背景与方块颜色()
        {
            Assert.IsNotNull(_controller.theme, "未配置视觉主题 ThemeProfile");

            var cam = Camera.main;
            Assert.IsNotNull(cam);
            Assert.AreEqual(_controller.theme.backgroundColor, cam.backgroundColor,
                "相机背景色应来自主题");

            Assert.AreEqual(_controller.theme.voxelColor, _controller.voxelColor, "方块颜色应来自主题");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 墙面色板_不随主题变化()
        {
            // 需求：墙面灰白 / 投影灰黑 / 重合绿 / 超出蓝 是"所有关卡通用"设定
            var wall = _controller.WallFront;
            Assert.IsNotNull(wall);

            float target = wall.targetMaterial != null ? wall.targetMaterial.color.grayscale : -1f;
            Assert.GreaterOrEqual(target, 0f);
            Assert.Less(target, 0.35f, "目标投影应保持灰黑色（不随主题变亮）");

            yield return null;
        }
    }
}
