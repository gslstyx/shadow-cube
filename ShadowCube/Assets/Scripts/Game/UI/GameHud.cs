using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 游玩界面 HUD：关卡号 / 最好星级 / 返回 / 重置 / 左右旋转（需求 §3.3）。
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

        public Button BackButton { get; private set; }
        public Button ResetButton { get; private set; }
        public Button RotateLeftButton { get; private set; }
        public Button RotateRightButton { get; private set; }
        public Text LevelLabel { get; private set; }
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
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -140f), Vector2.zero);

            BackButton = UiFactory.CreateButton(top, "Back", "Back", 34,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(200f, -20f));

            LevelLabel = UiFactory.CreateText(top, "Level", "Level", 40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(210f, 0f), new Vector2(-210f, 0f));

            StarLabel = UiFactory.CreateText(top, "Stars", "Best: -", 30, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-24f, 0f));

            // 底部操作栏
            var bottom = UiFactory.CreatePanel(Canvas.transform, "BottomBar", UiFactory.PanelColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 150f));

            RotateLeftButton = UiFactory.CreateButton(bottom, "RotateLeft", "<", 44,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 22f), new Vector2(150f, -22f));

            ResetButton = UiFactory.CreateButton(bottom, "Reset", "Reset", 34,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-90f, 22f), new Vector2(90f, -22f));

            RotateRightButton = UiFactory.CreateButton(bottom, "RotateRight", ">", 44,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-150f, 22f), new Vector2(-24f, -22f));

            BackButton.onClick.AddListener(OnBackClicked);
            ResetButton.onClick.AddListener(OnResetClicked);
            RotateLeftButton.onClick.AddListener(() => Rotate(-1));
            RotateRightButton.onClick.AddListener(() => Rotate(1));
        }

        // ── 公开操作（按钮与测试共用）────────────────────────────
        public void OnBackClicked() => BackRequested?.Invoke();

        public void OnResetClicked() => controller?.ResetLevel();

        public void Rotate(int deltaTurns)
        {
            if (controller != null && controller.Rig != null) controller.Rig.RequestTurn(deltaTurns);
        }

        /// <summary>刷新关卡号与最好星级</summary>
        public void Refresh()
        {
            if (controller == null || controller.level == null) return;

            var level = controller.level;
            if (LevelLabel != null)
                LevelLabel.text = $"Level {level.chapterId}-{level.levelIndex}";

            int best = controller.progress != null ? controller.progress.GetStars(level) : 0;
            if (StarLabel != null)
                StarLabel.text = best > 0 ? $"Best: {best}/5" : "Best: -";
        }

        private void OnEnable() => Refresh();
    }
}
