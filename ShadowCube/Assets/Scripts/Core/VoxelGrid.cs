using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>
    /// 真 3D 体素空间：X × Z 为平台平面，Y 为高度（0 ~ MaxHeight-1）。
    /// 纯 C#，不依赖渲染，便于单元测试与跨平台复用。
    /// </summary>
    public class VoxelGrid
    {
        public int Width { get; }      // X
        public int Depth { get; }      // Z
        public int MaxHeight { get; }  // Y

        private readonly HashSet<Vector3Int> _voxels = new HashSet<Vector3Int>();

        public int Count => _voxels.Count;
        public IEnumerable<Vector3Int> Voxels => _voxels;

        public VoxelGrid(int width, int depth, int maxHeight)
        {
            Width = width;
            Depth = depth;
            MaxHeight = maxHeight;
        }

        public bool InBounds(Vector3Int c)
            => c.x >= 0 && c.x < Width && c.z >= 0 && c.z < Depth && c.y >= 0 && c.y < MaxHeight;

        public bool Contains(Vector3Int c) => _voxels.Contains(c);

        /// <summary>放置方块，越界返回 false</summary>
        public bool Add(Vector3Int c) => InBounds(c) && _voxels.Add(c);

        public bool Remove(Vector3Int c) => _voxels.Remove(c);

        public void Clear() => _voxels.Clear();

        /// <summary>该列从底向上第一个空位；列已满返回 -1</summary>
        public int GetNextFreeHeight(int x, int z)
        {
            for (int y = 0; y < MaxHeight; y++)
                if (!_voxels.Contains(new Vector3Int(x, y, z))) return y;
            return -1;
        }

        /// <summary>该列顶部方块的高度；空列返回 -1</summary>
        public int GetTopHeight(int x, int z)
        {
            for (int y = MaxHeight - 1; y >= 0; y--)
                if (_voxels.Contains(new Vector3Int(x, y, z))) return y;
            return -1;
        }
    }
}
