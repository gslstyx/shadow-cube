using System.Collections;
using NUnit.Framework;
using ShadowCube.Core;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>M1 灰盒场景冒烟：可无头运行（TC-SMOKE-01/02 的自动化版本）</summary>
    public class M1PrototypeSmokeTests
    {
        [UnityTest]
        public IEnumerator 场景加载后_主控与关卡就绪()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            var controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(controller, "场景中未找到 GameController");
            Assert.IsNotNull(controller.level, "GameController 未指定 LevelData");
            Assert.IsTrue(controller.level.OptimalCount > 0, "关卡最优解为空");
            Assert.IsNotNull(Camera.main, "场景中缺少主相机");

            yield return null;
        }

        [UnityTest]
        public IEnumerator 搭建标准解后_判定过关并给出星级()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            var controller = Object.FindObjectOfType<GameController>();
            var level = controller.level;
            var grid = level.CreateGrid();

            foreach (var v in level.solution) grid.Add(v);

            Assert.IsTrue(LevelJudge.IsSolved(grid, level), "标准解未能判过关");
            Assert.AreEqual(5, LevelJudge.GetStars(grid.Count, level.OptimalCount), "标准解应为 5 星");

            yield return null;
        }
    }
}
