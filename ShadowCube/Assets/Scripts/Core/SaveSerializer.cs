using System;
using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>存档序列化 + 容错（损坏/旧版本/未来版本都能安全回退，不崩溃）</summary>
    public static class SaveSerializer
    {
        public const int CurrentVersion = 1;

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data ?? new SaveData(), true);

        /// <summary>任何异常情况都返回可用的空存档，保证 TC-LEVEL-08（存档损坏不崩溃）</summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new SaveData();

            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return new SaveData();

                // 未来版本存档：不做破坏性解读，回退为空存档（避免写坏新数据）
                if (data.version > CurrentVersion) return new SaveData();

                data.levels ??= new System.Collections.Generic.List<LevelProgress>();
                data.levels.RemoveAll(p => p == null || string.IsNullOrEmpty(p.levelId));
                data.levels.RemoveAll(p => p.stars < 0);
                data.lastPlayedLevelId ??= "";
                return data;
            }
            catch (Exception)
            {
                return new SaveData();
            }
        }
    }
}
