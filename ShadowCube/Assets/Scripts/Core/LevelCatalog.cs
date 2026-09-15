using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>
    /// 关卡目录：集中维护关卡顺序（解锁顺序 = 数组顺序）。
    /// 后续章节扩展包只需追加目录资产，逻辑层无需改动（需求 §3.2 / §4.2）。
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Cube/Level Catalog", fileName = "LevelCatalog")]
    public class LevelCatalog : ScriptableObject
    {
        public List<LevelData> levels = new List<LevelData>();

        public int Count => levels?.Count ?? 0;

        public LevelData Get(int index)
            => levels != null && index >= 0 && index < levels.Count ? levels[index] : null;

        public List<string> OrderedIds()
        {
            var ids = new List<string>();
            if (levels == null) return ids;
            foreach (var level in levels)
                if (level != null) ids.Add(level.Id);
            return ids;
        }

        public int IndexOf(LevelData level)
            => level == null || levels == null ? -1 : levels.IndexOf(level);
    }
}
