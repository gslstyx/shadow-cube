using NUnit.Framework;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Tests.EditMode
{
    public class WallBindingRulesTests
    {
        // 默认视角下相机右方向 ≈ (-0.78, 0, 0.62)
        private static readonly Vector3 CameraRight = new Vector3(-0.78f, 0f, 0.62f);

        [Test]
        public void 法线沿Z的墙_使用XY表()
        {
            var b = WallBindingRules.Resolve(new Vector3(0f, 0f, 1f), CameraRight);
            Assert.IsTrue(b.UseXY);
        }

        [Test]
        public void 法线沿X的墙_使用ZY表()
        {
            var b = WallBindingRules.Resolve(new Vector3(1f, 0f, 0f), CameraRight);
            Assert.IsFalse(b.UseXY);
        }

        [Test]
        public void XY表_U轴与相机右方向相反时镜像()
        {
            // 相机右 = -X（相机在 +Z 看向 -Z 时）→ x 增大在屏幕左侧 → 需要镜像
            var b = WallBindingRules.Resolve(new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f));
            Assert.IsTrue(b.UseXY);
            Assert.IsTrue(b.FlipU);
        }

        [Test]
        public void XY表_U轴与相机右方向相同时不镜像()
        {
            var b = WallBindingRules.Resolve(new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f));
            Assert.IsTrue(b.UseXY);
            Assert.IsFalse(b.FlipU);
        }

        [Test]
        public void ZY表_按Z轴与相机右方向决定镜像()
        {
            var noFlip = WallBindingRules.Resolve(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f));
            Assert.IsFalse(noFlip.UseXY);
            Assert.IsFalse(noFlip.FlipU);

            var flip = WallBindingRules.Resolve(new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, -1f));
            Assert.IsTrue(flip.FlipU);
        }

        [Test]
        public void 旋转180度_仍使用XY表但镜像相反()
        {
            var at0 = WallBindingRules.Resolve(new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f));
            var at180 = WallBindingRules.Resolve(new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f));

            Assert.AreEqual(at0.UseXY, at180.UseXY, "180° 应仍是 XY 表");
            Assert.AreNotEqual(at0.FlipU, at180.FlipU, "从对侧看墙，x 方向相反 → 镜像应相反");
        }

        [Test]
        public void 旋转360度_绑定与初始完全一致()
        {
            var camRight0 = new Vector3(-0.78f, 0f, 0.62f);
            var camRight360 = Quaternion.Euler(0f, 360f, 0f) * camRight0;

            var at0 = WallBindingRules.Resolve(Vector3.forward, camRight0);
            var at360 = WallBindingRules.Resolve(Quaternion.Euler(0f, 360f, 0f) * Vector3.forward, camRight360);

            Assert.AreEqual(at0.UseXY, at360.UseXY);
            Assert.AreEqual(at0.FlipU, at360.FlipU);
        }

        [Test]
        public void 旋转90度_两面墙的表互换()
        {
            var turn0Front = WallBindingRules.Resolve(Quaternion.Euler(0f, 0f, 0f) * Vector3.forward, CameraRight);
            var turn0Left = WallBindingRules.Resolve(Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, CameraRight);

            var turn90Front = WallBindingRules.Resolve(Quaternion.Euler(0f, 90f, 0f) * Vector3.forward, CameraRight);
            var turn90Left = WallBindingRules.Resolve(Quaternion.Euler(0f, 180f, 0f) * Vector3.forward, CameraRight);

            Assert.IsTrue(turn0Front.UseXY, "0° 时后墙应为 XY 表");
            Assert.IsFalse(turn0Left.UseXY, "0° 时左墙应为 ZY 表");
            Assert.IsFalse(turn90Front.UseXY, "90° 后原后墙应换成 ZY 表");
            Assert.IsTrue(turn90Left.UseXY, "90° 后原左墙应换成 XY 表");
        }
    }
}
