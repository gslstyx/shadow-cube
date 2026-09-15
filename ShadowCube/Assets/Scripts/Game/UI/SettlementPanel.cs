using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 过关结算：星级点亮 + 下一关 / 重玩 / 返回（需求 §3.4）。
    /// 下一关按钮在无后续关卡时自动隐藏。
    /// </summary>
    public class SettlementPanel : MonoBehaviour
    {
        [Tooltip("留空则自动查找场景中的 GameController")]
        public GameController controller;

        /// <summary>点击下一关（由上层切换关卡/场景）</summary>
        public Action NextRequested;
        /// <summary>点击返回选关</summary>
        public Action BackRequested;

        public Button NextButton { get; private set; }
        public Button RetryButton { get; private set; }
        public Button BackButton { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text StarLabel { get; private set; }
        public Canvas Canvas { get; private set; }

        public bool IsVisible { get; private set; }
        public int ShownStars { get; private set; }

        private void Awake()
        {
            if (controller == null) controller = FindObjectOfType<GameController>();

            Build();
            Hide();
        }

        private void OnEnable()
        {
            if (controller == null) return;

            controller.Solved -= OnLevelSolved;
            controller.Solved += OnLevelSolved;
        }

        private void OnDisable()
        {
            if (controller != null) controller.Solved -= OnLevelSolved;
        }

        private void Build()
        {
            Canvas = UiFactory.CreateCanvas("Settlement", 20);

            var dim = UiFactory.CreatePanel(Canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.55f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var panel = UiFactory.CreatePanel(dim, "Panel", UiFactory.PanelColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f, -320f), new Vector2(360f, 320f));

            TitleLabel = UiFactory.CreateText(panel, "Title", "LEVEL CLEARED", 52, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(0f, -40f));

            StarLabel = UiFactory.CreateText(panel, "Stars", "3 / 5", 64, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 20f), new Vector2(0f, 120f),
                UiFactory.AccentColor);

            NextButton = UiFactory.CreateButton(panel, "Next", "Next Level", 38,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 190f), new Vector2(220f, 260f));

            RetryButton = UiFactory.CreateButton(panel, "Retry", "Retry", 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 110f), new Vector2(220f, 175f));

            BackButton = UiFactory.CreateButton(panel, "Back", "Back to Levels", 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 30f), new Vector2(220f, 95f));

            NextButton.onClick.AddListener(OnNextClicked);
            RetryButton.onClick.AddListener(OnRetryClicked);
            BackButton.onClick.AddListener(OnBackClicked);
        }

        // ── 事件 ────────────────────────────────────────────────
        private void OnLevelSolved(int stars) => Show(stars);

        public void Show(int stars)
        {
            ShownStars = stars;
            IsVisible = true;

            if (StarLabel != null) StarLabel.text = $"{stars} / 5";
            if (NextButton != null) NextButton.gameObject.SetActive(HasNextLevel());

            if (Canvas != null) Canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            IsVisible = false;
            if (Canvas != null) Canvas.gameObject.SetActive(false);
        }

        public bool HasNextLevel()
        {
            if (controller == null || controller.progress == null) return false;
            return controller.progress.NextLevel(controller.level) != null;
        }

        // ── 按钮 ────────────────────────────────────────────────
        public void OnNextClicked() => NextRequested?.Invoke();

        public void OnRetryClicked()
        {
            Hide();
            controller?.ResetLevel();
            controller?.HudRefresh();
        }

        public void OnBackClicked() => BackRequested?.Invoke();
    }
}
