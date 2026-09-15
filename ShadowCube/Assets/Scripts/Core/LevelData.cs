using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>
    /// 关卡数据（数据驱动）。目标投影由标准答案自动推导，避免美术手画两份。
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Cube/Level Data", fileName = "LevelData")]
    public class LevelData : ScriptableObject
    {
        [Header("平台")]
        public int width = 5;
        public int depth = 5;
        public int maxHeight = 4;

        [Header("标准答案（体素坐标 x,y,z）")]
        public Vector3Int[] solution = new Vector3Int[0];

        [Header("信息")]
        [Tooltip("存档用的稳定唯一 ID（改动会导致该关进度失效，上架后不要改）")]
        public string levelId = "";
        public string levelName = "";
        public int chapterId = 1;
        public int levelIndex = 1;

        /// <summary>稳定 ID：未填写时回退为资产名</summary>
        public string Id => string.IsNullOrEmpty(levelId) ? name : levelId;

        /// <summary>最优解方块数（星级基准）</summary>
        public int OptimalCount => solution != null ? solution.Length : 0;

        private bool[,] _targetFront;
        private bool[,] _targetLeft;

        public bool[,] TargetFront =>
            _targetFront ??= ProjectionUtil.FromVoxels(solution, width, maxHeight, true);

        public bool[,] TargetLeft =>
            _targetLeft ??= ProjectionUtil.FromVoxels(solution, depth, maxHeight, false);

#if UNITY_EDITOR
        private void OnValidate()
        {
            _targetFront = null;
            _targetLeft = null;
        }
#endif

        public VoxelGrid CreateGrid() => new VoxelGrid(width, depth, maxHeight);
    }
}
