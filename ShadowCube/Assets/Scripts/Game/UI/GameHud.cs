using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 游玩界面 HUD（需求 §3.3）：
    /// 左上 返回 ｜ 顶部中央 关卡号 + 系列名 + 当前方块数 ｜ 右上 重置 / 最好星级 ｜ 右下 💡 提示
    /// 全部代码生成，无需预制体；按钮回调走公开方法，便于测试与后续接本地化。
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Tooltip("留空则自动查找场景中的 GameController")]
        public GameController controller;

        /// <summary>点击返回（由上层决定跳到选关还是首页）</summary>
        public Action BackRequested;

        /// <summary>懒解析 LevelFlow（避免在 Awake 里抓到即将销毁的重复实例）</summary>
        private LevelFlow Flow => _flow != null ? _flow : (_flow = FindObjectOfType<LevelFlow>());
        private LevelFlow _flow;

        /// <summary>懒解析提示服务（同 PROB-CODE-08 的处理方式）</summary>
        private HintService Hint => _hint != null ? _hint : (_hint = FindObjectOfType<HintService>());
        private HintService _hint;

        public Button BackButton { get; private set; }
        public Button ResetButton { get; private set; }
        public Button HintButton { get; private set; }
        public Button RotateLeftButton { get; private set; }
        public Button RotateRightButton { get; private set; }
        public Text LevelLabel { get; private set; }
        public Text SeriesLabel { get; private set; }
        public Text BlockLabel { get; private set; }
        public Text StarLabel { get; private set; }
        public Canvas Canvas { get; private set; }

        private void Awake()
        {
            if (controller == null) controller = FindObjectOfType<GameController>();

            BackRequested = () => { if (Flow != null) Flow.GoToLevelSelect(); };

            Build();
            Refresh();
        }

        private void Build()
        {
            Canvas = UiFactory.CreateCanvas("HUD", 10);

            // 顶部栏
            var top = UiFactory.CreatePanel(Canvas.transform, "TopBar", UiFactory.PanelColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -150f), Vector2.zero);

            BackButton = UiFactory.CreateButton(top, "Back", "Back", 32,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(190f, -20f));

            LevelLabel = UiFactory.CreateText(top, "Level", "Level", 38, TextAnchor.LowerCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(200f, -4f), new Vector2(-200f, -16f));

            SeriesLabel = UiFactory.CreateText(top, "Series", "", 24, TextAnchor.UpperCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(200f, 18f), new Vector2(-200f, 4f));

            // 右上：重置（需求 §3.3）+ 最好星级
            ResetButton = UiFactory.CreateButton(top, "Reset", "Reset", 30,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-210f, 20f), new Vector2(-20f, -20f));

            StarLabel = UiFactory.CreateText(top, "Stars", "Best: -", 26, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-226f, 0f));

            // 底部栏
            var bottom = UiFactory.CreatePanel(Canvas.transform, "BottomBar", UiFactory.PanelColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 150f));

            RotateLeftButton = UiFactory.CreateButton(bottom, "RotateLeft", "<", 44,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 22f), new Vector2(130f, -22f));

            RotateRightButton = UiFactory.CreateButton(bottom, "RotateRight", ">", 44,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(146f, 22f), new Vector2(252f, -22f));

            BlockLabel = UiFactory.CreateText(bottom, "Blocks", "0 / 0", 32, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-140f, 0f), new Vector2(140f, 0f));

            // 右下：提示（需求 §3.3 / §2.2-F）
            HintButton = UiFactory.CreateButton(bottom, "Hint", "Hint", 32,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-210f, 22f), new Vector2(-24f, -22f));

            BackButton.onClick.AddListener(OnBackClicked);
            ResetButton.onClick.AddListener(OnResetClicked);
            HintButton.onClick.AddListener(OnHintClicked);
            RotateLeftButton.onClick.AddListener(() => Rotate(-1));
            RotateRightButton.onClick.AddListener(() => Rotate(1));
        }

        private void Update()
        {
            if (controller == null || controller.level == null || BlockLabel == null) return;

            // 当前 / 最优方块数：让玩家对星级有预期（需求 §3.3）
            string text = $"{controller.BlockCount} / {controller.level.OptimalCount}";
            if (BlockLabel.text != text) BlockLabel.text = text;
        }

        // ── 公开操作（按钮与测试共用）────────────────────────────
        public void OnBackClicked() => BackRequested?.Invoke();

        public void OnResetClicked() => controller?.ResetLevel();

        /// <summary>点击 💡 提示：免费关直接演示；非免费关先看激励视频（需求 §2.2-G）</summary>
        public void OnHintClicked() => Hint?.RequestHint();

        public void Rotate(int deltaTurns)
        {
            if (controller != null && controller.Rig != null) controller.Rig.RequestTurn(deltaTurns);
        }

        /// <summary>刷新关卡号、系列名与最好星级</summary>
        public void Refresh()
        {
            if (controller == null || controller.level == null) return;

            var level = controller.level;
            if (LevelLabel != null)
                LevelLabel.text = $"Level {level.chapterId}-{level.levelIndex}";

            if (SeriesLabel != null)
                SeriesLabel.text = string.IsNullOrEmpty(level.levelName) ? string.Empty : level.levelName;

            int best = controller.progress != null ? controller.progress.GetStars(level) : 0;
            if (StarLabel != null)
                StarLabel.text = best > 0 ? $"Best: {best}/5" : "Best: -";

            if (BlockLabel != null)
                BlockLabel.text = $"{controller.BlockCount} / {level.OptimalCount}";
        }
    }
}
