using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>
    /// 墙面与投影表的绑定规则：相机旋转后，每面墙应显示"垂直于其法线"的那个投影表，
    /// 并按摄像机右方向决定是否横向镜像，保证玩家看到的方向直观且两面墙始终自洽。
    /// 布尔投影沿轴正负方向结果相同，因此只有两张表：X-Y（沿 Z 投影）与 Z-Y（沿 X 投影）。
    /// </summary>
    public static class WallBindingRules
    {
        public readonly struct Binding
        {
            /// <summary>true = 使用 X-Y 投影表（墙法线沿 ±Z），false = 使用 Z-Y 表（法线沿 ±X）</summary>
            public readonly bool UseXY;
            /// <summary>true = 横向镜像显示</summary>
            public readonly bool FlipU;

            public Binding(bool useXY, bool flipU)
            {
                UseXY = useXY;
                FlipU = flipU;
            }
        }

        public static Binding Resolve(Vector3 wallNormal, Vector3 cameraRight)
        {
            bool normalAlongX = Mathf.Abs(wallNormal.x) > Mathf.Abs(wallNormal.z);
            bool useXY = !normalAlongX;

            // X-Y 表的 U 轴 = 世界 +X；Z-Y 表的 U 轴 = 世界 +Z
            Vector3 uAxis = useXY ? Vector3.right : Vector3.forward;
            bool flip = Vector3.Dot(uAxis, cameraRight) < 0f;

            return new Binding(useXY, flip);
        }
    }
}
