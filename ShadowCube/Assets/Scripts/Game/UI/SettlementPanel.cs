using System;
using System.Collections;
using ShadowCube.Core;
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

        /// <summary>懒解析 LevelFlow（避免在 Awake 里抓到即将销毁的重复实例）</summary>
        private LevelFlow Flow => _flow != null ? _flow : (_flow = FindObjectOfType<LevelFlow>());
        private LevelFlow _flow;
        /// <summary>点击返回选关</summary>
        public Action BackRequested;

        [Header("星级动效")]
        [Tooltip("每颗星点亮的间隔（秒）")]
        public float starStepDelay = 0.18f;
        public float starPopDuration = 0.34f;
        public Color starOnColor = new Color(1f, 0.82f, 0.35f, 1f);
        public Color starOffColor = new Color(0.34f, 0.36f, 0.42f, 0.55f);

        public Button NextButton { get; private set; }
        public Button RetryButton { get; private set; }
        public Button BackButton { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text StarLabel { get; private set; }
        public Canvas Canvas { get; private set; }

        /// <summary>5 个星位（Image，菱形；点亮时变金色并弹出）</summary>
        public Image[] StarSlots { get; private set; }

        /// <summary>已点亮的星数（随动画递增，供测试断言"逐颗点亮"）</summary>
        public int AnimatedStars { get; private set; }

        public bool IsVisible { get; private set; }
        public int ShownStars { get; private set; }

        private Coroutine _starRoutine;

        private void Awake()
        {
            if (controller == null) controller = FindObjectOfType<GameController>();

            // 自动接入场景流程：下一关 → 进入下一关（无则回选关）；返回 → 选关
            NextRequested = () =>
            {
                if (Flow == null || controller == null) return;
                Flow.PlayNext(controller.progress, controller.level);
            };
            BackRequested = () => { if (Flow != null) Flow.GoToLevelSelect(); };

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

            TitleLabel = UiFactory.CreateText(panel, "Title", Loc.Get("settle.title"), 52, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(0f, -40f));

            BuildStarRow(panel);

            StarLabel = UiFactory.CreateText(panel, "Stars", "3 / 5", 64, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 20f), new Vector2(0f, 120f),
                UiFactory.AccentColor);

            NextButton = UiFactory.CreateButton(panel, "Next", Loc.Get("settle.next"), 38,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 190f), new Vector2(220f, 260f));

            RetryButton = UiFactory.CreateButton(panel, "Retry", Loc.Get("settle.retry"), 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 110f), new Vector2(220f, 175f));

            BackButton = UiFactory.CreateButton(panel, "Back", Loc.Get("settle.back"), 36,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 30f), new Vector2(220f, 95f));

            NextButton.onClick.AddListener(OnNextClicked);
            RetryButton.onClick.AddListener(OnRetryClicked);
            BackButton.onClick.AddListener(OnBackClicked);
        }

        /// <summary>5 个菱形星位（不依赖字体字形，M4 可替换为星星贴图）</summary>
        private void BuildStarRow(Transform panel)
        {
            var row = UiFactory.CreatePanel(panel, "StarRow", new Color(0f, 0f, 0f, 0f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-270f, -250f), new Vector2(270f, -160f));
            var rowImage = row.GetComponent<Image>();
            if (rowImage != null) rowImage.raycastTarget = false;

            StarSlots = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject($"Star{i + 1}", typeof(RectTransform), typeof(Image));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(row, false);

                float x0 = i / 5f + 0.04f;
                float x1 = (i + 1) / 5f - 0.04f;
                rect.anchorMin = new Vector2(x0, 0.12f);
                rect.anchorMax = new Vector2(x1, 0.88f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);   // 菱形

                var image = go.GetComponent<Image>();
                image.color = starOffColor;
                image.raycastTarget = false;
                StarSlots[i] = image;
            }
        }

        /// <summary>逐颗点亮：弹出 + 变金色（需求 §3.4"星级动画点亮"）</summary>
        private IEnumerator AnimateStars(int stars)
        {
            AnimatedStars = 0;

            if (StarSlots != null)
            {
                foreach (var slot in StarSlots)
                {
                    if (slot == null) continue;
                    slot.color = starOffColor;
                    slot.transform.localScale = Vector3.one * 0.6f;
                }
            }

            for (int i = 0; i < stars && StarSlots != null && i < StarSlots.Length; i++)
            {
                var slot = StarSlots[i];
                if (slot == null) continue;

                slot.color = starOnColor;
                yield return PopStar(slot);
                AnimatedStars = i + 1;
            }
        }

        private IEnumerator PopStar(Image slot)
        {
            float t = 0f;
            while (t < starPopDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / starPopDuration);
                slot.transform.localScale = Vector3.one * (0.6f + 0.4f * k + 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }
            slot.transform.localScale = Vector3.one;
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

            if (_starRoutine != null) StopCoroutine(_starRoutine);
            _starRoutine = StartCoroutine(AnimateStars(stars));
        }

        public void Hide()
        {
            IsVisible = false;

            if (_starRoutine != null) { StopCoroutine(_starRoutine); _starRoutine = null; }
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
