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
        public IEnumerator 旋转任意角度_垂直方向始终锁定()
        {
            _rig.RequestTurn(1);
            yield return new WaitForSeconds(0.5f);

            var euler = _rig.transform.rotation.eulerAngles;
            Assert.AreEqual(0f, Mathf.DeltaAngle(euler.x, 0f), 0.5f, "俯仰角被改变");
            Assert.AreEqual(0f, Mathf.DeltaAngle(euler.z, 0f), 0.5f, "滚转角被改变");
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
