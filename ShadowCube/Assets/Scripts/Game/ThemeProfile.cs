using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 整体视觉基线（背景 / 平台 / 网格线 / 方块 / 氛围）。
    ///
    /// ⚠️ **不含墙面色板**：墙面灰白、目标投影灰黑、重合绿、超出蓝是"所有关卡通用"设定，
    /// 由 `Assets/Materials/M_Wall*.mat` 固定，不随主题变化（避免玩家在不同系列间认知错位）。
    ///
    /// 两套预设：`Assets/Data/Theme_Light`（参考图浅色基线，默认）、`Assets/Data/Theme_Dark`（深色异次元）。
    /// 切换只需在 GameController 的 `theme` 字段换引用。
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Cube/Theme Profile", fileName = "ThemeProfile")]
    public class ThemeProfile : ScriptableObject
    {
        public string themeName = "Light";

        [Header("基础配色")]
        [Tooltip("相机背景色")]
        public Color backgroundColor = new Color(0.94f, 0.95f, 0.96f, 1f);
        [Tooltip("平台底色")]
        public Color platformColor = new Color(0.88f, 0.89f, 0.91f, 1f);
        [Tooltip("平台网格线")]
        public Color gridColor = new Color(0.55f, 0.57f, 0.60f, 1f);
        [Tooltip("方块（参考图里方块是深色实体）")]
        public Color voxelColor = new Color(0.20f, 0.21f, 0.23f, 1f);

        [Header("氛围：雾")]
        public bool enableFog = true;
        public Color fogColor = new Color(0.94f, 0.95f, 0.96f, 1f);
        public float fogStart = 16f;
        public float fogEnd = 52f;

        [Header("氛围：星尘粒子")]
        public bool enableStarDust = true;
        public Color starColor = new Color(0.72f, 0.76f, 0.82f, 0.55f);
        [Tooltip("粒子数量（低端机建议 60~120）")]
        public int starCount = 140;
        [Tooltip("星尘分布半径")]
        public float starRadius = 26f;
    }
}
