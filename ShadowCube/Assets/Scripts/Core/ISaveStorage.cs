namespace ShadowCube.Core
{
    /// <summary>存档读写接口：便于替换实现（文件 / PlayerPrefs / 云存档）与测试注入</summary>
    public interface ISaveStorage
    {
        /// <summary>读取存档原文；不存在返回 null</summary>
        string Load();

        void Save(string json);

        /// <summary>清空存档（调试/重置进度用）</summary>
        void Delete();
    }
}
