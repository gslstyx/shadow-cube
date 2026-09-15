namespace ShadowCube.Core
{
    /// <summary>
    /// 音效播放接口：逻辑层只依赖接口，M4 可整体替换为正式音频资源实现。
    /// </summary>
    public interface ISfxPlayer
    {
        void PlayAdd();
        void PlayRemove();
        void PlaySolved();
    }

    /// <summary>空实现：用于无声环境或测试</summary>
    public sealed class NullSfxPlayer : ISfxPlayer
    {
        public void PlayAdd() { }
        public void PlayRemove() { }
        public void PlaySolved() { }
    }
}
