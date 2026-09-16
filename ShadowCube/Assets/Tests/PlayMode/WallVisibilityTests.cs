using System.Collections;
using NUnit.Framework;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>
    /// 两面投影墙的可见性回归（需求 §1.2 / §2.3 / §3.3）。
    /// 背景：曾经只有"投影格块"而没有墙面本身，且目标色/玩家色在深色背景上不可见，
    /// 导致 Game 视图里**看不到墙**。本测试锁定：面板存在 + 目标亮块存在 + 在相机视锥内。
    /// </summary>
    public class WallVisibilityTests
    {
        private GameController _controller;

        [UnitySetUp]
        public IEnumerator 加载场景()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(_controller, "场景缺少 GameController");
            _controller.ResetLevel();
            yield return null;
        }

        private static Plane[] FrustumOf(Camera cam) => GeometryUtility.CalculateFrustumPlanes(cam);

        [UnityTest]
        public IEnumerator 两面墙_存在且各自有可见面板()
        {
            Assert.IsNotNull(_controller.WallLeft, "缺少左墙");
            Assert.IsNotNull(_controller.WallFront, "缺少后墙");

            foreach (var wall in new[] { _controller.WallLeft, _controller.WallFront })
            {
                Assert.IsTrue(wall.HasPanel, $"{wall.name} 没有可见面板（需求要求半透明发光面）");

                var renderer = wall.GetComponentInChildren<Renderer>(false);
                Assert.IsNotNull(renderer, $"{wall.name} 面板缺少 Renderer");
                Assert.Less(renderer.sharedMaterial.color.a, 1f, $"{wall.name} 面板应为半透明");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator 目标投影_有亮块且两面墙都在相机视锥内()
        {
            Assert.Greater(_controller.WallLeft.ActiveTargetCount, 0, "左墙没有显示目标投影块");
            Assert.Greater(_controller.WallFront.ActiveTargetCount, 0, "后墙没有显示目标投影块");

            var cam = Camera.main;
            Assert.IsNotNull(cam);
            var planes = FrustumOf(cam);

            foreach (var wall in new[] { _controller.WallLeft, _controller.WallFront })
            {
                Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, wall.WorldBounds),
                    $"{wall.name} 不在相机视锥内（玩家看不到这面墙）");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator 未放置方块时_只显示目标投影()
        {
            Assert.Greater(_controller.WallFront.ActiveTargetCount, 0);
            Assert.AreEqual(0, _controller.WallFront.ActiveCurrentCount, "空盘不应有玩家投影");
            Assert.AreEqual(0, _controller.WallFront.ActiveMatchedCount, "空盘不应有重合块");

            yield return null;
        }

        [UnityTest]
        public IEnumerator 搭出标准解_投影与目标重合_显示命中色()
        {
            foreach (var v in _controller.level.solution)
                Assert.IsTrue(_controller.AddAt(v.x, v.z));

            yield return null;

            int matched = _controller.WallFront.ActiveMatchedCount + _controller.WallLeft.ActiveMatchedCount;
            Assert.Greater(matched, 0, "标准解应与目标完全重合，但没有任何命中块");

            Assert.AreEqual(0, _controller.WallFront.ActiveCurrentCount,
                "标准解下不应还有'未命中'的玩家投影块");
            Assert.AreEqual(0, _controller.WallFront.ActiveTargetCount,
                "标准解下目标块应全部变成命中色");
        }

        [UnityTest]
        public IEnumerator 玩家投影未命中_显示玩家色而非目标色()
        {
            _controller.AddAt(0, 0);   // 该格通常不在目标投影内
            yield return null;

            int current = _controller.WallFront.ActiveCurrentCount + _controller.WallLeft.ActiveCurrentCount;
            Assert.Greater(current, 0, "玩家放的方块应显示为玩家色投影");

            yield return null;
        }

        /// <summary>
        /// 像素级回归：此前的测试只断言"对象激活 + 在视锥内"，结果漏掉了
        /// "Unity Quad 网格正面朝 -Z，投影面被背面剔除 → 画面里完全看不到墙"这个真实缺陷。
        /// 本用例真正渲染一帧，检查墙面/投影块所在像素是否被画出来。
        /// </summary>
        [UnityTest]
        public IEnumerator 墙面与投影块_必须真的被渲染出来()
        {
            var cam = Camera.main;
            const int w = 640, h = 480;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);

            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            catch (System.Exception)
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            var background = cam.backgroundColor;

            foreach (var wall in new[] { _controller.WallLeft, _controller.WallFront })
            {
                var panel = wall.transform.TransformPoint(0f, 1.5f, 0f);
                var panelPixel = Sample(tex, cam, panel);
                Assert.Greater(Diff(panelPixel, background), 0.015f,
                    $"{wall.name} 的面板没有出现在画面里（像素 {panelPixel} ≈ 背景色）" +
                    "—— 检查 Quad 朝向：Unity 的 Quad 正面朝 -Z，需要绕 Y 转 180°");

                var quad = FirstActiveTargetQuad(wall);
                Assert.IsNotNull(quad, $"{wall.name} 没有激活的目标投影块");
                var quadPixel = Sample(tex, cam, quad.transform.position);
                Assert.Greater(Diff(quadPixel, background), 0.05f,
                    $"{wall.name} 的目标投影块没有出现在画面里（像素 {quadPixel} ≈ 背景色）");
            }

            rt.Release();
            yield return null;
        }

        private static Transform FirstActiveTargetQuad(ProjectionWallView wall)
        {
            foreach (Transform child in wall.transform)
            {
                if (child.name != "Target") continue;
                foreach (Transform quad in child)
                    if (quad.gameObject.activeSelf) return quad;
            }
            return null;
        }

        private static Color Sample(Texture2D image, Camera cam, Vector3 world)
        {
            var viewport = cam.WorldToViewportPoint(world);
            Assert.Greater(viewport.z, 0f, "采样点位于相机后方");
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * image.width), 0, image.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * image.height), 0, image.height - 1);
            return image.GetPixel(x, y);
        }

        private static float Diff(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [UnityTest]
        public IEnumerator 旋转90度_两面墙仍在视锥内且投影正常()
        {
            _controller.Rig.SnapToTurn(1);
            yield return null;

            var planes = FrustumOf(Camera.main);
            foreach (var wall in new[] { _controller.WallLeft, _controller.WallFront })
            {
                Assert.IsTrue(wall.HasPanel, $"{wall.name} 旋转后面板丢失");
                Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, wall.WorldBounds),
                    $"{wall.name} 旋转后不在视锥内");
                Assert.Greater(wall.ActiveTargetCount, 0, $"{wall.name} 旋转后目标投影消失");
            }
        }

        [UnityTest]
        public IEnumerator 切换关卡_墙面尺寸随关卡重建()
        {
            var flow = Object.FindObjectOfType<LevelFlow>();
            Assert.IsNotNull(flow);
            Assert.GreaterOrEqual(flow.catalog.Count, 2);

            var another = flow.catalog.Get(1);
            _controller.LoadLevel(another);
            yield return null;

            Assert.IsTrue(_controller.WallLeft.HasPanel);
            Assert.IsTrue(_controller.WallFront.HasPanel);
            Assert.Greater(_controller.WallFront.ActiveTargetCount, 0);

            var planes = FrustumOf(Camera.main);
            Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, _controller.WallFront.WorldBounds));
            yield return null;
        }
    }
}
