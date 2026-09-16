using System.Collections;
using ShadowCube.Core;
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
        /// <summary>引导文案走本地化 key（需求 §0.2），不硬编码</summary>
        private static readonly string[] StepKeys =
        {
            "tutorial.step1", "tutorial.step2", "tutorial.step3", "tutorial.step4", "tutorial.step5"
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
        public int TotalSteps => StepKeys.Length;

        private Image[] _highlightEdges;
        private Image _hand;
        private Coroutine _handRoutine;
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

            BuildHand(dim);

            var panel = UiFactory.CreatePanel(dim, "Panel", UiFactory.PanelColor,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-380f, 300f), new Vector2(380f, 640f));

            StepLabel = UiFactory.CreateText(panel, "Step", Loc.Get(StepKeys[0]), 34, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.35f), new Vector2(1f, 0.9f), new Vector2(30f, 0f), new Vector2(-30f, 0f));

            NextButton = UiFactory.CreateButton(panel, "Next", Loc.Get("tutorial.next"), 34,
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(-220f, 0f), new Vector2(220f, 70f));

            SkipButton = UiFactory.CreateButton(panel, "Skip", Loc.Get("tutorial.skip"), 28,
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(-140f, -80f), new Vector2(140f, -20f));

            NextButton.onClick.AddListener(Next);
            SkipButton.onClick.AddListener(Skip);
        }

        /// <summary>手指指示物（需求 §2.2-E「手指动画」）：沿当前步骤对应的路径来回滑动</summary>
        private void BuildHand(Transform parent)
        {
            var go = new GameObject("Hand", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(46f, 46f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.85f);
            image.raycastTarget = false;

            _hand = image;
            go.SetActive(false);
        }

        /// <summary>每步的手指路径（参考分辨率 1080×1920，坐标以屏幕中心为原点）</summary>
        private static (Vector2 from, Vector2 to, bool visible) HandPath(int step) => step switch
        {
            0 => (new Vector2(-220f, -80f), new Vector2(220f, -80f), true),    // 拖拽生成
            1 => (new Vector2(220f, -80f), new Vector2(-220f, -80f), true),    // 拖拽消除（反向）
            2 => (new Vector2(-110f, -820f), new Vector2(110f, -820f), true),  // 旋转按钮区域
            _ => (Vector2.zero, Vector2.zero, false)
        };

        private void RestartHand(int step)
        {
            if (_handRoutine != null) { StopCoroutine(_handRoutine); _handRoutine = null; }
            if (_hand == null) return;

            var path = HandPath(step);
            if (!path.visible) { _hand.gameObject.SetActive(false); return; }

            _hand.gameObject.SetActive(true);
            _handRoutine = StartCoroutine(HandAnimation(path.from, path.to));
        }

        private IEnumerator HandAnimation(Vector2 from, Vector2 to)
        {
            var rect = _hand.rectTransform;

            while (true)
            {
                rect.anchoredPosition = from;
                SetHandAlpha(0f);
                yield return new WaitForSeconds(0.35f);

                SetHandAlpha(0.85f);
                float t = 0f;
                while (t < 1.1f)
                {
                    t += Time.deltaTime;
                    rect.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(t / 1.1f));
                    yield return null;
                }

                float u = 0f;
                while (u < 0.25f)
                {
                    u += Time.deltaTime;
                    SetHandAlpha(0.85f * (1f - Mathf.Clamp01(u / 0.25f)));
                    yield return null;
                }
            }
        }

        private void SetHandAlpha(float alpha)
        {
            if (_hand == null) return;
            var c = _hand.color;
            _hand.color = new Color(c.r, c.g, c.b, alpha);
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

            if (_handRoutine != null) { StopCoroutine(_handRoutine); _handRoutine = null; }
            if (_hand != null) _hand.gameObject.SetActive(false);

            if (Canvas != null) Canvas.gameObject.SetActive(false);
        }

        public void Next()
        {
            StepIndex++;
            if (StepIndex >= StepKeys.Length) { Finish(); return; }
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
            if (StepLabel != null) StepLabel.text = Loc.Get(StepKeys[StepIndex]);
            if (NextButton != null)
            {
                var label = NextButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = Loc.Get(StepIndex == StepKeys.Length - 1 ? "tutorial.gotIt" : "tutorial.next");
            }

            ApplyHighlight(StepIndex);
            RestartHand(StepIndex);
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
