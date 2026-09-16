using ShadowCube.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowCube.Game
{
    /// <summary>
    /// 场景流程：首页 → 选关 → 游玩。跨场景单例（DontDestroyOnLoad）。
    /// 关卡数据通过 PendingLevel 传递给游玩场景的 GameController。
    /// </summary>
    public class LevelFlow : MonoBehaviour
    {
        public const string SceneMainMenu = "MainMenu";
        public const string SceneLevelSelect = "LevelSelect";
        public const string SceneGame = "M1_Prototype";

        public static LevelFlow Instance { get; private set; }

        /// <summary>待载入的关卡（进入游玩场景后由 GameController 取走）</summary>
        public static LevelData PendingLevel { get; set; }

        [Tooltip("关卡目录（解锁顺序 = 数组顺序）")]
        public LevelCatalog catalog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public string CurrentScene => SceneManager.GetActiveScene().name;

        public void GoToMainMenu() => SceneManager.LoadScene(SceneMainMenu);

        public void GoToLevelSelect() => SceneManager.LoadScene(SceneLevelSelect);

        /// <summary>进入指定关卡</summary>
        public void PlayLevel(LevelData level)
        {
            if (level == null)
            {
                Debug.LogWarning("[ShadowCube] PlayLevel：目标关卡为空");
                return;
            }

            Debug.Log($"[ShadowCube] PlayLevel：{level.Id} → {SceneGame}");
            PendingLevel = level;
            SceneManager.LoadScene(SceneGame);
        }

        /// <summary>进入存档中的最近关卡（无存档则首关）</summary>
        public void PlayLastOrFirst(ProgressManager progress)
        {
            var target = progress != null ? progress.LastPlayedOrFirst() : null;
            Debug.Log($"[ShadowCube] PlayLastOrFirst：目标 = {(target != null ? target.Id : "null")}（progress={(progress != null ? "有" : "无")}）");
            if (target == null && catalog != null && catalog.Count > 0) target = catalog.Get(0);
            PlayLevel(target);
        }

        /// <summary>进入下一关；没有下一关则回选关</summary>
        public void PlayNext(ProgressManager progress, LevelData current)
        {
            var next = progress != null ? progress.NextLevel(current) : null;
            if (next != null) PlayLevel(next);
            else GoToLevelSelect();
        }

        public void ReloadCurrent() => SceneManager.LoadScene(CurrentScene);
    }
}
