namespace ShadowCube.Core
{
    /// <summary>手势规则：起手列为空 → 本次手势为生成；起手列有方块 → 本次手势为消除。</summary>
    public static class GestureRules
    {
        public enum Gesture { Add, Remove }

        public static Gesture Decide(bool startColumnHasVoxel)
            => startColumnHasVoxel ? Gesture.Remove : Gesture.Add;
    }
}
