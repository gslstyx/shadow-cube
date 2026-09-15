namespace ShadowCube.Core
{
    /// <summary>过关判定与星级评价（纯逻辑，可单元测试）</summary>
    public static class LevelJudge
    {
        /// <summary>
        /// 投影重合即过关：允许存在被完全遮挡的额外方块；
        /// 两墙顺序可互换（相机旋转 90° 后左右墙互换）。
        /// </summary>
        public static bool IsSolved(VoxelGrid grid, LevelData level)
        {
            if (grid == null || level == null) return false;

            return ProjectionUtil.WallsMatch(
                ProjectionUtil.ProjectFront(grid),
                ProjectionUtil.ProjectLeft(grid),
                level.TargetFront,
                level.TargetLeft);
        }

        /// <summary>星级：以最优解方块数为基准（5★=最优，4★≤+2，3★≤+4，2★≤+6，否则 1★）</summary>
        public static int GetStars(int usedCount, int optimalCount)
        {
            if (usedCount <= 0) return 0;

            int diff = usedCount - optimalCount;
            if (diff <= 0) return 5;
            if (diff <= 2) return 4;
            if (diff <= 4) return 3;
            if (diff <= 6) return 2;
            return 1;
        }
    }
}
