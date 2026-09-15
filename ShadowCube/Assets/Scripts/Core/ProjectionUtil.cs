using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>
    /// 布尔投影：体素集合 → 平面占用表（非像素级，按格逻辑比对）。
    /// Front = 沿 Z 方向投影到后墙（平面 X-Y）；Left = 沿 X 方向投影到左墙（平面 Z-Y）。
    /// </summary>
    public static class ProjectionUtil
    {
        public static bool[,] Project(VoxelGrid grid, bool front)
        {
            int u = front ? grid.Width : grid.Depth;
            var table = new bool[u, grid.MaxHeight];

            foreach (var v in grid.Voxels)
            {
                int uu = front ? v.x : v.z;
                table[uu, v.y] = true;
            }
            return table;
        }

        public static bool[,] ProjectFront(VoxelGrid grid) => Project(grid, true);
        public static bool[,] ProjectLeft(VoxelGrid grid) => Project(grid, false);

        /// <summary>写入已有缓冲区（复用数组，避免每次操作都分配，M4 性能）</summary>
        public static void ProjectInto(VoxelGrid grid, bool[,] buffer, bool front)
        {
            if (grid == null || buffer == null) return;

            System.Array.Clear(buffer, 0, buffer.Length);
            foreach (var v in grid.Voxels)
            {
                int uu = front ? v.x : v.z;
                if (uu >= 0 && uu < buffer.GetLength(0) && v.y >= 0 && v.y < buffer.GetLength(1))
                    buffer[uu, v.y] = true;
            }
        }

        /// <summary>供关卡数据使用：直接由体素集合计算</summary>
        public static bool[,] FromVoxels(IEnumerable<Vector3Int> voxels, int uSize, int maxHeight, bool front)
        {
            var table = new bool[uSize, maxHeight];
            foreach (var v in voxels)
            {
                int uu = front ? v.x : v.z;
                table[uu, v.y] = true;
            }
            return table;
        }

        public static bool Same(bool[,] a, bool[,] b)
        {
            if (a == null || b == null) return false;
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;

            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }

        /// <summary>两墙投影集合匹配：允许左右墙互换（相机旋转 90° 后成立）</summary>
        public static bool WallsMatch(bool[,] frontA, bool[,] leftA, bool[,] frontB, bool[,] leftB)
            => (Same(frontA, frontB) && Same(leftA, leftB))
            || (Same(frontA, leftB) && Same(leftA, frontB));
    }
}
