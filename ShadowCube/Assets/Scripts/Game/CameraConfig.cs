using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 相机手感配置（需求 §2.2-C）：Inspector / 资产可调，方便反复调手感。
    /// 资产：`Assets/Data/CameraConfig.asset`（由场景生成器创建，不存在时不覆盖已有数值）。
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Cube/Camera Config", fileName = "CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [Header("旋转开关")]
        [Tooltip("水平旋转（绕 Y 轴）")]
        public bool enableYaw = true;
        [Tooltip("垂直旋转（俯仰，绕相机本地 X 轴）")]
        public bool enablePitch = true;

        [Header("吸附")]
        [Tooltip("松手是否吸附到最近的 snapAngle（关闭可做自由视角对比测试）")]
        public bool snapEnabled = true;
        [Tooltip("水平吸附角度（度）")]
        public float snapAngle = 90f;

        [Header("俯仰范围")]
        [Tooltip("最小俯角（0 = 水平视角）")]
        public float pitchMin = 15f;
        [Tooltip("最大俯角（90 = 正顶视，建议不超过 75，否则投影读不出）")]
        public float pitchMax = 75f;

        [Header("手感")]
        [Tooltip("拖动灵敏度（度 / 像素）")]
        public float rotateSensitivity = 0.3f;
        [Tooltip("回弹/跟随阻尼（SmoothDamp 平滑时间，越小越跟手）")]
        public float damping = 0.15f;

        [Header("默认视角（进入关卡的初始视角）")]
        public float defaultPitch = 42f;
        public float defaultAzimuth = 38.4f;
        [Tooltip("相机距离 = max(关卡宽,关卡深) × 该系数")]
        public float distancePerSpan = 2.5f;

        public float ClampPitch(float pitch) => Mathf.Clamp(pitch, pitchMin, pitchMax);
    }
}
