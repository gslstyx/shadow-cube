using System.Collections.Generic;

namespace ShadowCube.Core
{
    /// <summary>
    /// 关卡进度规则（纯逻辑）：**严格线性解锁** —— 通关第 N 关才解锁第 N+1 关；
    /// 星级取历史最好成绩，重玩低分不会降星。
    /// </summary>
    public class ProgressService
    {
        private readonly SaveData _data;
        private readonly List<string> _orderedIds;

        public ProgressService(SaveData data, IReadOnlyList<string> orderedLevelIds)
        {
            _data = data ?? new SaveData();
            _orderedIds = new List<string>(orderedLevelIds ?? new string[0]);
        }

        public SaveData Data => _data;

        /// <summary>已解锁关卡数量（至少 1，即首关）</summary>
        public int UnlockedCount
        {
            get
            {
                int unlocked = _orderedIds.Count == 0 ? 0 : 1;
                for (int i = 0; i < _orderedIds.Count - 1; i++)
                {
                    if (GetStars(_orderedIds[i]) <= 0) break;
                    unlocked++;
                }
                return unlocked;
            }
        }

        public int LevelCount => _orderedIds.Count;

        public int TotalStars
        {
            get
            {
                int sum = 0;
                foreach (var id in _orderedIds) sum += GetStars(id);
                return sum;
            }
        }

        public int MaxStars => _orderedIds.Count * 5;

        public bool IsUnlocked(string levelId)
        {
            int index = _orderedIds.IndexOf(levelId);
            return index >= 0 && index < UnlockedCount;
        }

        public int GetStars(string levelId)
        {
            var entry = Find(levelId);
            return entry?.stars ?? 0;
        }

        /// <summary>记录成绩：只在更高分时更新，自动解锁下一关</summary>
        public void RecordResult(string levelId, int stars)
        {
            if (string.IsNullOrEmpty(levelId) || stars <= 0) return;
            if (!_orderedIds.Contains(levelId)) return;   // 非本目录关卡忽略，避免脏数据

            var entry = Find(levelId);
            if (entry == null)
            {
                entry = new LevelProgress(levelId, stars);
                _data.levels.Add(entry);
            }
            else if (stars > entry.stars)
            {
                entry.stars = stars;
            }

            _data.lastPlayedLevelId = levelId;
        }

        public string NextLevelId(string levelId)
        {
            int index = _orderedIds.IndexOf(levelId);
            if (index < 0 || index + 1 >= _orderedIds.Count) return "";
            return _orderedIds[index + 1];
        }

        /// <summary>最近一次游玩的关卡（无存档时返回首关）</summary>
        public string LastPlayedOrFirst()
        {
            if (!string.IsNullOrEmpty(_data.lastPlayedLevelId) && IsUnlocked(_data.lastPlayedLevelId))
                return _data.lastPlayedLevelId;
            return _orderedIds.Count > 0 ? _orderedIds[0] : "";
        }

        public void ResetAll()
        {
            _data.levels.Clear();
            _data.lastPlayedLevelId = "";
            _data.tutorialDone = false;
        }

        private LevelProgress Find(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return null;
            foreach (var p in _data.levels)
                if (p.levelId == levelId) return p;
            return null;
        }
    }
}
