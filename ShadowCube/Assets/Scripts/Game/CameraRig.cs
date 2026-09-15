using System;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 相机云台：绕 Y 轴水平旋转，松手吸附到最近 90°（需求 §2.2-C）。
    /// 两面墙作为子物体跟随旋转，始终位于当前视角的"左后"方向；垂直方向锁死。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("手感")]
        public float degreesPerPixel = 0.30f;
        public float snapSpeed = 12f;

        /// <summary>当前吸附到第几个 90°（0~3）</summary>
        public int Turns { get; private set; }

        /// <summary>吸附完成后回调（参数为新的 Turns）</summary>
        public Action<int> TurnsChanged;

        /// <summary>墙面挂载点：随相机一起旋转（懒创建，避免 Awake 顺序问题）</summary>
        public Transform WallsRoot => EnsureWallsRoot();

        private Transform _wallsRoot;

        public Transform EnsureWallsRoot()
        {
            if (_wallsRoot == null)
            {
                var walls = new GameObject("Walls");
                walls.transform.SetParent(transform, false);
                _wallsRoot = walls.transform;
            }
            return _wallsRoot;
        }

        public bool IsDragging => _dragging;
        public bool IsSnapping => Mathf.Abs(_angle - _targetAngle) > 0.01f;
        public float Angle => _angle;

        private float _angle;
        private float _targetAngle;
        private bool _dragging;
        private float _dragStartAngle;
        private float _dragAccum;

        private void Awake()
        {
            EnsureWallsRoot();

            transform.rotation = Quaternion.identity;
            _angle = _targetAngle = 0f;
            Turns = 0;
        }

        // ── 拖拽旋转 ────────────────────────────────────────────
        public void BeginDrag()
        {
            _dragging = true;
            _dragStartAngle = _angle;
            _dragAccum = 0f;
        }

        public void Drag(float deltaX)
        {
            if (!_dragging) return;

            _dragAccum += deltaX;
            _angle = _dragStartAngle - _dragAccum * degreesPerPixel; // 向右拖 → 视角左转
            ApplyRotation();
        }

        public void EndDrag()
        {
            if (!_dragging) return;

            _dragging = false;
            _targetAngle = Mathf.Round(_angle / 90f) * 90f;         // 吸附到最近 90°
        }

        /// <summary>按 90° 步进旋转（UI 按钮 / 测试用）</summary>
        public void RequestTurn(int deltaTurns)
        {
            float baseAngle = Mathf.Round((IsSnapping ? _targetAngle : _angle) / 90f) * 90f;
            _targetAngle = baseAngle + deltaTurns * 90f;
        }

        /// <summary>立即切到指定朝向（测试/初始化用）</summary>
        public void SnapToTurn(int turns)
        {
            Turns = ((turns % 4) + 4) % 4;
            _angle = _targetAngle = Turns * 90f;
            ApplyRotation();
            TurnsChanged?.Invoke(Turns);
        }

        private void Update()
        {
            if (_dragging || !IsSnapping) return;

            _angle = Mathf.LerpAngle(_angle, _targetAngle, Mathf.Clamp01(Time.deltaTime * snapSpeed));
            if (Mathf.Abs(Mathf.DeltaAngle(_angle, _targetAngle)) < 0.5f)
            {
                _angle = _targetAngle;
                ApplyRotation();
                UpdateTurns();
                return;
            }
            ApplyRotation();
        }

        private void ApplyRotation() => transform.rotation = Quaternion.Euler(0f, _angle, 0f);

        private void UpdateTurns()
        {
            int turns = ((Mathf.RoundToInt(_angle / 90f) % 4) + 4) % 4;
            if (turns == Turns) return;

            Turns = turns;
            TurnsChanged?.Invoke(Turns);
        }
    }
}
