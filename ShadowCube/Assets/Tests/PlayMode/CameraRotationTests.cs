using System.Collections;
using NUnit.Framework;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>相机 90° 旋转与吸附（对应 TC-CAM-01/02/05/06）</summary>
    public class CameraRotationTests
    {
        private GameController _controller;
        private CameraRig _rig;

        [UnitySetUp]
        public IEnumerator 加载场景()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            _controller = Object.FindObjectOfType<GameController>();
            _rig = Object.FindObjectOfType<CameraRig>();
            Assert.IsNotNull(_controller);
            Assert.IsNotNull(_rig, "场景中缺少 CameraRig");

            _rig.SnapToTurn(0);
            _controller.ResetLevel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 初始朝向_后墙显示XY表_左墙显示ZY表()
        {
            Assert.AreEqual(0, _rig.Turns);
            Assert.IsTrue(_controller.BackWallUsesXY, "0° 时后墙应为 XY 表");
            Assert.IsFalse(_controller.LeftWallUsesXY, "0° 时左墙应为 ZY 表");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 请求旋转90度_吸附到位且两面墙表互换()
        {
            _rig.RequestTurn(1);

            // 等待吸附完成
            float timeout = 2f;
            while (_rig.IsSnapping && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(1, _rig.Turns, "未吸附到 90°");
            Assert.IsFalse(_controller.BackWallUsesXY, "90° 后后墙应换成 ZY 表");
            Assert.IsTrue(_controller.LeftWallUsesXY, "90° 后左墙应换成 XY 表");
        }

        [UnityTest]
        public IEnumerator 拖拽结束_角度吸附到最近90度()
        {
            _rig.BeginDrag();
            _rig.Drag(100f);           // 拖一段非整 90° 的距离
            Assert.IsTrue(_rig.Angle != 0f, "拖拽未改变角度");
            _rig.EndDrag();

            float timeout = 2f;
            while (_rig.IsSnapping && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0f, Mathf.Repeat(_rig.Angle, 90f), 0.01f, "未吸附到 90° 整数倍");
        }

        [UnityTest]
        public IEnumerator 水平旋转时_云台自身不做俯仰或翻滚()
        {
            _rig.RequestTurn(1);
            yield return new WaitForSeconds(0.5f);

            var euler = _rig.transform.rotation.eulerAngles;
            Assert.AreEqual(0f, Mathf.DeltaAngle(euler.x, 0f), 0.5f, "云台俯仰角被改变");
            Assert.AreEqual(0f, Mathf.DeltaAngle(euler.z, 0f), 0.5f, "云台滚转角被改变");
        }

        /// <summary>用例之间用独立的配置实例，避免互相污染真实资产</summary>
        private CameraConfig FreshConfig()
        {
            var config = ScriptableObject.CreateInstance<CameraConfig>();
            _rig.config = config;
            return config;
        }

        [UnityTest]
        public IEnumerator 初始俯仰_等于配置默认值且在范围内()
        {
            var config = FreshConfig();
            config.defaultPitch = 38f;
            _rig.Configure(Camera.main, _rig.pivotLocal, _rig.distance);

            Assert.AreEqual(38f, _rig.Pitch, 0.01f, "初始俯仰应取配置默认值");
            Assert.GreaterOrEqual(_rig.Pitch, config.pitchMin);
            Assert.LessOrEqual(_rig.Pitch, config.pitchMax);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 拖拽上移_俯仰增大并被上限钳制()
        {
            var config = FreshConfig();
            config.pitchMin = 20f;
            config.pitchMax = 60f;
            _rig.SetPitch(40f);

            _rig.BeginDrag();
            _rig.Drag(0f, 30f);                       // 上移 30px × 0.3 = +9°
            Assert.AreEqual(49f, _rig.Pitch, 0.01f, "上移应增大俯角");

            _rig.Drag(0f, 1000f);                     // 远超上限
            Assert.AreEqual(60f, _rig.Pitch, 0.01f, "俯角应被 pitchMax 钳制");
            _rig.EndDrag();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 拖拽下移_俯仰减小并被下限钳制()
        {
            var config = FreshConfig();
            config.pitchMin = 20f;
            config.pitchMax = 60f;
            _rig.SetPitch(40f);

            _rig.BeginDrag();
            _rig.Drag(0f, -1000f);
            Assert.AreEqual(20f, _rig.Pitch, 0.01f, "俯角应被 pitchMin 钳制");
            _rig.EndDrag();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 关闭吸附_松手后保持自由角度()
        {
            var config = FreshConfig();
            config.snapEnabled = false;

            _rig.BeginDrag();
            _rig.Drag(100f);                          // -30°
            _rig.EndDrag();
            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(-30f, _rig.Angle, 0.5f, "关闭吸附后不应回弹到 90° 整数倍");
            Assert.Greater(Mathf.Abs(Mathf.Repeat(_rig.Angle, 90f)), 0.5f, "角度不应落在 90° 整数倍上");
        }

        [UnityTest]
        public IEnumerator 关闭水平旋转_拖拽不改变水平角()
        {
            var config = FreshConfig();
            config.enableYaw = false;
            config.enablePitch = true;
            float before = _rig.Angle;

            _rig.BeginDrag();
            _rig.Drag(200f, 0f);
            _rig.EndDrag();
            yield return null;

            Assert.AreEqual(before, _rig.Angle, 0.01f, "关闭水平旋转后角度不应变化");
        }

        [UnityTest]
        public IEnumerator 关闭俯仰_拖拽不改变俯角()
        {
            var config = FreshConfig();
            config.enablePitch = false;
            float before = _rig.Pitch;

            _rig.BeginDrag();
            _rig.Drag(0f, 500f);
            _rig.EndDrag();
            yield return null;

            Assert.AreEqual(before, _rig.Pitch, 0.01f, "关闭俯仰后俯角不应变化");
        }

        [UnityTest]
        public IEnumerator 相机始终看向平台中心()
        {
            var config = FreshConfig();
            config.defaultPitch = 30f;
            _rig.Configure(Camera.main, _rig.pivotLocal, _rig.distance);
            yield return null;

            var cam = Camera.main;
            var toPivot = (_rig.PivotWorld - cam.transform.position).normalized;
            Assert.Greater(Vector3.Dot(cam.transform.forward, toPivot), 0.999f, "相机没有对准环绕中心");
        }

        [UnityTest]
        public IEnumerator 俯仰两端_两面墙都仍在相机视野内()
        {
            var config = FreshConfig();

            foreach (float pitch in new[] { config.pitchMin, config.pitchMax })
            {
                _rig.SetPitch(pitch);
                yield return null;

                var planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
                foreach (var wall in new[] { _controller.WallLeft, _controller.WallFront })
                {
                    Assert.IsTrue(GeometryUtility.TestPlanesAABB(planes, wall.WorldBounds),
                        $"俯角 {pitch}° 时 {wall.name} 掉出了视野");
                }
            }
        }

        [UnityTest]
        public IEnumerator 俯仰变化_两面墙保持竖直()
        {
            var config = FreshConfig();
            _rig.SetPitch(25f);
            yield return null;

            foreach (var wall in new[] { _controller.WallLeft, _controller.WallFront })
            {
                var euler = wall.transform.rotation.eulerAngles;
                Assert.AreEqual(0f, Mathf.DeltaAngle(euler.x, 0f), 0.5f, $"{wall.name} 俯仰后不再竖直");
            }
        }

        [UnityTest]
        public IEnumerator 旋转90度后_标准解仍判过关()
        {
            _rig.SnapToTurn(1);
            yield return null;

            foreach (var v in _controller.level.solution)
                Assert.IsTrue(_controller.AddAt(v.x, v.z), $"({v.x},{v.z}) 放置失败");

            Assert.IsTrue(_controller.IsSolved, "旋转后标准解应仍判过关");
            Assert.AreEqual(5, _controller.StarRating);
        }

        [UnityTest]
        public IEnumerator 转到180度_与初始朝向表相同()
        {
            _rig.SnapToTurn(2);
            yield return null;

            Assert.AreEqual(2, _rig.Turns);
            Assert.IsTrue(_controller.BackWallUsesXY, "180° 时后墙应回到 XY 表");
            Assert.IsFalse(_controller.LeftWallUsesXY, "180° 时左墙应回到 ZY 表");
        }
    }
}
