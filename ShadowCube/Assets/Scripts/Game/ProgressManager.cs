using System;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 进度管理：加载/保存存档，对外提供解锁与星级查询、成绩记录。
    /// 存储实现可注入（测试用临时目录或内存实现）。
    /// </summary>
    public class ProgressManager : MonoBehaviour
    {
        [Header("关卡目录")]
        public LevelCatalog catalog;

        [Header("调试")]
        [Tooltip("勾选后每次启动清空存档")]
        public bool resetOnStart;

        public ISaveStorage Storage { get; private set; }

        /// <summary>存档数据（首次访问时自动加载，避免依赖 Awake 顺序）</summary>
        public SaveData Data { get { EnsureReady(); return _data; } private set => _data = value; }

        /// <summary>进度服务（首次访问时自动加载）</summary>
        public ProgressService Progress { get { EnsureReady(); return _progress; } private set => _progress = value; }

        private SaveData _data;
        private ProgressService _progress;
        private bool _loading;

        /// <summary>存档变化回调（选关界面刷新用）</summary>
        public Action Changed;

        private bool _initialized;

        private void Awake()
        {
            if (!_initialized) Initialize(new FileSaveStorage(), catalog);
        }

        public void Initialize(ISaveStorage storage, LevelCatalog levelCatalog)
        {
            Storage = storage ?? new FileSaveStorage();
            if (levelCatalog != null) catalog = levelCatalog;

            if (resetOnStart) Storage.Delete();

            Load();
            _initialized = true;
        }

        /// <summary>懒初始化：其它组件的 Awake 可能早于本组件执行（_loading 防止 Rebuild 中递归）</summary>
        private void EnsureReady()
        {
            if ((_progress != null && _data != null) || _loading) return;

            _loading = true;
            try
            {
                if (Storage == null) Storage = new FileSaveStorage();
                Load();
            }
            finally
            {
                _loading = false;
            }
        }

        public void Load()
        {
            _data = SaveSerializer.FromJson(Storage.Load());
            Rebuild();
        }

        public void Save()
        {
            Storage.Save(SaveSerializer.ToJson(Data));
            Changed?.Invoke();
        }

        public void ResetProgress()
        {
            Progress.ResetAll();
            Save();
        }

        // ── 对外查询 ────────────────────────────────────────────
        public bool IsUnlocked(LevelData level) => Progress.IsUnlocked(level.Id);
        public int GetStars(LevelData level) => Progress.GetStars(level.Id);
        public int UnlockedCount => Progress.UnlockedCount;
        public int TotalStars => Progress.TotalStars;
        public int MaxStars => Progress.MaxStars;

        public LevelData NextLevel(LevelData level)
        {
            var id = Progress.NextLevelId(level.Id);
            return string.IsNullOrEmpty(id) ? null : FindById(id);
        }

        public LevelData LastPlayedOrFirst()
        {
            var id = Progress.LastPlayedOrFirst();
            return string.IsNullOrEmpty(id) ? null : FindById(id);
        }

        public bool HasLevel(LevelData level) => level != null && FindById(level.Id) != null;

        // ── 记录成绩 ────────────────────────────────────────────
        /// <summary>通关后记录（星级取最好）并立即落盘</summary>
        public void RecordResult(LevelData level, int stars)
        {
            if (level == null) return;

            Progress.RecordResult(level.Id, stars);
            Save();
        }

        private void Rebuild()
        {
            Progress = new ProgressService(_data, catalog != null ? catalog.OrderedIds() : null);

            if (catalog != null)
            {
                foreach (var level in catalog.levels)
                    if (level != null && string.IsNullOrEmpty(level.levelId))
                        Debug.LogWarning($"[ShadowCube] 关卡 {level.name} 未设置 levelId，正在回退用资产名（改名会导致进度失效）");
            }
        }

        private LevelData FindById(string id)
        {
            if (catalog == null || catalog.levels == null) return null;

            foreach (var level in catalog.levels)
                if (level != null && level.Id == id) return level;

            return null;
        }
    }
}
