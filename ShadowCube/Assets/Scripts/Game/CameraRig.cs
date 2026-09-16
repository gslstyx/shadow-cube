using System;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 相机云台（需求 §2.2-C）：
    /// - **水平旋转**（绕 Y 轴）+ **垂直俯仰**（绕相机本地 X 轴），全部参数来自 <see cref="CameraConfig"/>
    /// - 水平：松手吸附到最近 `snapAngle`（可用 `snapEnabled` 关闭做自由视角对比）
    /// - 垂直：限制在 [`pitchMin`, `pitchMax`]，避免 90° 顶视导致投影读不出
    /// - 两面墙挂在云台下（`WallsRoot`），**只跟随水平旋转**，始终位于当前视角"左后"方向
    /// - 相机始终看向 `pivotLocal`（平台中心），因此俯仰只是绕着平台升降，不会跑偏
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Tooltip("手感配置（Assets/Data/CameraConfig.asset）")]
        public CameraConfig config;

        [Header("视角（由场景生成器按关卡设置）")]
        [Tooltip("环绕中心（平台中心上方）")]
        public Vector3 pivotLocal = new Vector3(0f, 1.5f, 0f);
        [Tooltip("相机到环绕中心的距离")]
        public float distance = 12.4f;
        [Tooltip("相机所在的水平方位角（0 = 平台正后方）")]
        public float azimuth = 38.4f;

        /// <summary>当前档位（默认 1 档 = 90°）</summary>
        public int Turns { get; private set; }

        public float Angle => _angle;
        public float Pitch => _pitch;
        public float PitchTarget => _targetPitch;
        public bool IsDragging => _dragging;
        public bool IsSnapping => Mathf.Abs(Mathf.DeltaAngle(_angle, _targetAngle)) > 0.01f
                               || Mathf.Abs(_pitch - _targetPitch) > 0.01f;

        /// <summary>吸附完成后回调（参数为新的 Turns）</summary>
        public Action<int> TurnsChanged;

        /// <summary>墙面挂载点：随相机水平旋转（懒创建，避免 Awake 顺序问题）</summary>
        public Transform WallsRoot => EnsureWallsRoot();

        /// <summary>两个方向都关掉时，拖拽不应该抢占玩法输入</summary>
        public bool RotationEnabled => config == null || config.enableYaw || config.enablePitch;

        public Vector3 PivotWorld => transform.TransformPoint(pivotLocal);

        private Transform _wallsRoot;
        private Camera _camera;
        private float _angle, _targetAngle;
        private float _pitch, _targetPitch;
        private float _yawVelocity, _pitchVelocity;
        private bool _dragging;
        private float _dragStartAngle, _dragStartPitch, _dragAccumX, _dragAccumY;

        private float SnapStep => config != null ? Mathf.Max(1f, config.snapAngle) : 90f;
        private float Sensitivity => config != null ? config.rotateSensitivity : 0.3f;
        private float Damping => config != null ? Mathf.Max(0.01f, config.damping) : 0.15f;
        private bool YawEnabled => config == null || config.enableYaw;
        private bool PitchEnabled => config == null || config.enablePitch;
        private bool SnapEnabled => config == null || config.snapEnabled;

        private float ClampPitch(float pitch)
        {
            if (config != null) return config.ClampPitch(pitch);
            return Mathf.Clamp(pitch, 15f, 75f);
        }

        private void Awake()
        {
            EnsureWallsRoot();

            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            if (_camera == null) _camera = Camera.main;

            transform.rotation = Quaternion.identity;
            _angle = _targetAngle = 0f;
            _pitch = _targetPitch = ClampPitch(config != null ? config.defaultPitch : 42f);
            Turns = 0;
            ApplyTransform();
        }

        /// <summary>按关卡配置视角（场景生成器 / 切关时调用）</summary>
        public void Configure(Camera cam, Vector3 pivot, float dist, float? pitch = null, float? azimuthOverride = null)
        {
            if (cam != null) _camera = cam;
            pivotLocal = pivot;
            distance = dist;
            if (azimuthOverride.HasValue) azimuth = azimuthOverride.Value;

            float startPitch = pitch ?? (config != null ? config.defaultPitch : _pitch);
            _pitch = _targetPitch = ClampPitch(startPitch);
            ApplyTransform();
        }

        /// <summary>立即切到指定俯仰（提示演示 / 测试用）</summary>
        public void SetPitch(float pitch)
        {
            _pitch = _targetPitch = ClampPitch(pitch);
            ApplyTransform();
        }

        /// <summary>把视角复位到配置里的默认姿态（提示演示用）</summary>
        public void SnapToDefaultView()
        {
            SnapToTurn(0);
            SetPitch(config != null ? config.defaultPitch : 42f);
        }

        // ── 拖拽旋转 ────────────────────────────────────────────
        public void BeginDrag()
        {
            _dragging = true;
            _dragStartAngle = _targetAngle;    // 从目标角继续，避免回弹途中抖动
            _dragStartPitch = _targetPitch;
            _dragAccumX = 0f;
            _dragAccumY = 0f;
        }

        public void Drag(float deltaX, float deltaY = 0f)
        {
            if (!_dragging) return;

            if (YawEnabled)
            {
                _dragAccumX += deltaX;
                _angle = _dragStartAngle - _dragAccumX * Sensitivity;   // 向右拖 → 视角左转
                _targetAngle = _angle;                                  // 拖拽中跟手，松手再吸附
            }

            if (PitchEnabled)
            {
                _dragAccumY += deltaY;
                _pitch = _targetPitch = ClampPitch(_dragStartPitch + _dragAccumY * Sensitivity);
            }

            ApplyTransform();
        }

        public void EndDrag()
        {
            if (!_dragging) return;

            _dragging = false;
            _targetAngle = SnapEnabled ? Mathf.Round(_angle / SnapStep) * SnapStep : _angle;
            _targetPitch = ClampPitch(_pitch);
        }

        /// <summary>按档位步进旋转（UI 按钮 / 测试用）</summary>
        public void RequestTurn(int deltaTurns)
        {
            float baseAngle = Mathf.Round((IsSnapping ? _targetAngle : _angle) / SnapStep) * SnapStep;
            _targetAngle = baseAngle + deltaTurns * SnapStep;
        }

        /// <summary>立即切到指定档位（测试 / 初始化用）</summary>
        public void SnapToTurn(int turns)
        {
            turns = ((turns % 4) + 4) % 4;
            Turns = turns;
            _angle = _targetAngle = turns * SnapStep;
            _yawVelocity = 0f;
            ApplyTransform();
            TurnsChanged?.Invoke(Turns);
        }

        private void Update()
        {
            if (_dragging || !IsSnapping) return;

            float dt = Time.deltaTime;
            _angle = Mathf.SmoothDampAngle(_angle, _targetAngle, ref _yawVelocity, Damping, Mathf.Infinity, dt);
            _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVelocity, Damping, Mathf.Infinity, dt);

            bool yawDone = Mathf.Abs(Mathf.DeltaAngle(_angle, _targetAngle)) < 0.05f;
            bool pitchDone = Mathf.Abs(_pitch - _targetPitch) < 0.05f;

            if (yawDone && pitchDone)
            {
                _angle = _targetAngle;
                _pitch = _targetPitch;
                ApplyTransform();
                UpdateTurns();
                return;
            }

            ApplyTransform();
        }

        private void UpdateTurns()
        {
            int turns = ((Mathf.RoundToInt(_angle / SnapStep) % 4) + 4) % 4;
            if (turns == Turns) return;

            Turns = turns;
            TurnsChanged?.Invoke(Turns);
        }

        /// <summary>由 (方位角, 俯仰, 距离) 计算相机在云台下的局部位置与朝向</summary>
        private void ApplyTransform()
        {
            transform.rotation = Quaternion.Euler(0f, _angle, 0f);

            if (_camera == null) return;

            var cam = _camera.transform;
            if (cam.parent != transform) cam.SetParent(transform, false);
            if (cam.localScale != Vector3.one) cam.localScale = Vector3.one;

            float p = _pitch * Mathf.Deg2Rad;
            float a = azimuth * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(a) * Mathf.Cos(p));

            var camLocal = pivotLocal + dir * distance;
            cam.localPosition = camLocal;
            cam.localRotation = Quaternion.LookRotation(pivotLocal - camLocal, Vector3.up);
        }

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
    }
}
