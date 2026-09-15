using System;
using System.Collections.Generic;

namespace ShadowCube.Core
{
    /// <summary>单个关卡的成绩</summary>
    [Serializable]
    public class LevelProgress
    {
        public string levelId = "";
        public int stars;

        public LevelProgress() { }

        public LevelProgress(string id, int starCount)
        {
            levelId = id;
            stars = starCount;
        }
    }

    /// <summary>本地存档数据（JSON 序列化）</summary>
    [Serializable]
    public class SaveData
    {
        public int version = SaveSerializer.CurrentVersion;
        public List<LevelProgress> levels = new List<LevelProgress>();
        public string lastPlayedLevelId = "";
        public bool tutorialDone;
    }
}
