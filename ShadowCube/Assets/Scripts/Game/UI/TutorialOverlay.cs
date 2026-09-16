using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 新手引导（需求 §2.2-E）：**仅第 1 关、仅首次游玩**触发，分步讲解 + 高亮目标区域，可跳过。
    /// 关键约束：遮罩**不拦截输入**（`raycastTarget = false`），玩家可以"边教边做"——
    /// 引导期间仍能拖拽生成方块、点击旋转按钮（这是之前的缺陷：全屏遮罩把玩法输入全挡了）。
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        private static readonly string[] Steps =
        {
            "1/5  在平台上按住并拖动，连续生成方块",
            "2/5  从已有方块上起手拖动，消除方块",
            "3/5  点 < 或 > 旋转视角，两面墙始终在你左后方",
            "4/5  两侧墙上的黑色剪影是目标：让投影与它重合（重合的格子会变绿）",
            "5/5  两面墙全部对上即可过关，方块越少星级越高"
        };

        /// <summary>每一步高亮的目标区域（归一化锚点：中心 x, 中心 y, 宽, 高）</summary>
        private static readonly Vector4[] HighlightRects =
        {
            new(0.5f, 0.50f, 0.72f, 0.30f),   // 平台
            new(0.5f, 0.50f, 0.72f, 0.30f),   // 平台
            new(0.5f, 0.06f, 0.34f, 0.09f),   // 旋转按钮
            new(0.5f, 0.66f, 0.86f, 0.40f),   // 两面墙
            new(0.5f, 0.92f, 0.42f, 0.08f)    // 顶部关卡/星级
        };

        [Tooltip("留空则自动查找场景中的 ProgressManager")]
        public ProgressManager progress;

        [Tooltip("留空则自动查找场景中的 GameController（用于限定仅第 1 关触发）")]
        public GameController controller;

        [Tooltip("引导步的脉冲速度")]
        public float pulseSpeed = 3f;

        public Text StepLabel { get; private set; }
        public Button NextButton { get; private set; }
        public Button SkipButton { get; private set; }
        public Canvas Canvas { get; private set; }
        public RectTransform Highlight { get; private set; }

        public bool IsVisible { get; private set; }
        public int StepIndex { get; private set; }
        public int TotalSteps => Steps.Length;

        private Image[] _highlightEdges;
        private float _pulse;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<ProgressManager>();
            Build();
            Hide();
        }

        private void Start()
        {
            if (ShouldShow()) Show();
        }

        private void Build()
        {
            Canvas = UiFactory.CreateCanvas("Tutorial", 30);

            var dim = UiFactory.CreatePanel(Canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.6f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // 关键：遮罩不吃射线，玩法与 HUD 按钮在引导期间照常可用（需求 §2.2-E 分步教学）
            var dimImage = dim.GetComponent<Image>();
            if (dimImage != null) dimImage.raycastTarget = false;

            // 高亮框（四条边，中间镂空）
            var highlight = UiFactory.CreatePanel(dim, "Highlight", new Color(0f, 0f, 0f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-100f, -100f), new Vector2(100f, 100f));
            Highlight = highlight;
            var highlightImage = highlight.GetComponent<Image>();
            if (highlightImage != null) highlightImage.raycastTarget = false;   // 镂空框同样不吃射线

            _highlightEdges = new[]
            {
                CreateEdge(highlight, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 6f), Vector2.zero),
                CreateEdge(highlight, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 6f)),
                CreateEdge(highlight, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f)),
                CreateEdge(highlight, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-6f, 0f), Vector2.zero)
            };

            var panel = UiFactory.CreatePanel(dim, "Panel", UiFactory.PanelColor,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-380f, 300f), new Vector2(380f, 640f));

            StepLabel = UiFactory.CreateText(panel, "Step", Steps[0], 34, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.35f), new Vector2(1f, 0.9f), new Vector2(30f, 0f), new Vector2(-30f, 0f));

            NextButton = UiFactory.CreateButton(panel, "Next", "Next", 34,
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(-220f, 0f), new Vector2(220f, 70f));

            SkipButton = UiFactory.CreateButton(panel, "Skip", "Skip", 28,
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(-140f, -80f), new Vector2(140f, -20f));

            NextButton.onClick.AddListener(Next);
            SkipButton.onClick.AddListener(Skip);
        }

        private static Image CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = UiFactory.CreatePanel(parent, name, new Color(0.35f, 0.85f, 0.60f, 0.9f),
                anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>是否应当弹出引导：仅第 1 关 + 首次游玩（需求 §2.2-E）</summary>
        public bool ShouldShow()
        {
            if (progress == null || progress.Data.tutorialDone) return false;

            if (controller == null) controller = FindObjectOfType<GameController>();
            if (controller == null || controller.level == null) return false;

            return controller.level.chapterId == 1 && controller.level.levelIndex == 1;
        }

        public void Show()
        {
            StepIndex = 0;
            IsVisible = true;
            UpdateContent();
            if (Canvas != null) Canvas.gameObject.SetActive(true);
        }

        public void Hide()
        {
            IsVisible = false;
            if (Canvas != null) Canvas.gameObject.SetActive(false);
        }

        public void Next()
        {
            StepIndex++;
            if (StepIndex >= Steps.Length) { Finish(); return; }
            UpdateContent();
        }

        public void Skip() => Finish();

        private void Finish()
        {
            Hide();
            if (progress != null)
            {
                progress.Data.tutorialDone = true;
                progress.Save();
            }
        }

        private void UpdateContent()
        {
            if (StepLabel != null) StepLabel.text = Steps[StepIndex];
            if (NextButton != null)
            {
                var label = NextButton.GetComponentInChildren<Text>();
                if (label != null) label.text = StepIndex == Steps.Length - 1 ? "Got it" : "Next";
            }

            ApplyHighlight(StepIndex);
        }

        /// <summary>把高亮框移到当前步骤对应的区域（按 1080×1920 参考分辨率定位）</summary>
        private void ApplyHighlight(int step)
        {
            if (Highlight == null || HighlightRects.Length == 0) return;

            var rect = HighlightRects[Mathf.Clamp(step, 0, HighlightRects.Length - 1)];
            Highlight.anchorMin = new Vector2(0.5f, 0.5f);
            Highlight.anchorMax = new Vector2(0.5f, 0.5f);

            const float refW = 1080f, refH = 1920f;
            Highlight.sizeDelta = new Vector2(rect.z * refW, rect.w * refH);
            Highlight.anchoredPosition = new Vector2(0f, (rect.y - 0.5f) * refH);
        }

        private void Update()
        {
            if (!IsVisible || _highlightEdges == null) return;

            // 高亮框呼吸，提示玩家注意目标区域
            _pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed));
            foreach (var edge in _highlightEdges)
            {
                if (edge == null) continue;
                var c = edge.color;
                edge.color = new Color(c.r, c.g, c.b, _pulse);
            }
        }
    }
}
