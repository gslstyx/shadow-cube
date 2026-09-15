using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 新手引导：仅首次游玩触发，分步提示，可跳过；完成后写入存档（需求 §2.2-E、TC-UI-04/05/06）。
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        private static readonly string[] Steps =
        {
            "1/4  在平台上按住并拖动，生成方块",
            "2/4  从已有方块上起手拖动，消除方块",
            "3/4  点击 < 或 > 旋转视角，两面墙始终在你左后方",
            "4/4  让两侧投影与目标一致即可过关"
        };

        [Tooltip("留空则自动查找场景中的 ProgressManager")]
        public ProgressManager progress;

        public Text StepLabel { get; private set; }
        public Button NextButton { get; private set; }
        public Button SkipButton { get; private set; }
        public Canvas Canvas { get; private set; }

        public bool IsVisible { get; private set; }
        public int StepIndex { get; private set; }
        public int TotalSteps => Steps.Length;

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

            var panel = UiFactory.CreatePanel(dim, "Panel", UiFactory.PanelColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380f, -180f), new Vector2(380f, 180f));

            StepLabel = UiFactory.CreateText(panel, "Step", Steps[0], 38, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.35f), new Vector2(1f, 0.9f), new Vector2(30f, 0f), new Vector2(-30f, 0f));

            NextButton = UiFactory.CreateButton(panel, "Next", "Next", 36,
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(-220f, 0f), new Vector2(220f, 70f));

            SkipButton = UiFactory.CreateButton(panel, "Skip", "Skip", 30,
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(-140f, -80f), new Vector2(140f, -20f));

            NextButton.onClick.AddListener(Next);
            SkipButton.onClick.AddListener(Skip);
        }

        public bool ShouldShow()
        {
            if (progress == null) return false;
            return !progress.Data.tutorialDone;
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
        }
    }
}
