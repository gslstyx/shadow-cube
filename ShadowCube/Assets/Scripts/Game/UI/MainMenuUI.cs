using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>首页：进入游戏（最近关卡） + 选择关卡（需求 §3.1）</summary>
    public class MainMenuUI : MonoBehaviour
    {
        public Button PlayButton { get; private set; }
        public Button LevelSelectButton { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text ProgressLabel { get; private set; }

        public ProgressManager progress;
        public LevelFlow flow;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<ProgressManager>();
            if (flow == null) flow = FindObjectOfType<LevelFlow>();

            Build();
            Refresh();
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("MainMenu", 10);

            var background = UiFactory.CreatePanel(canvas.transform, "Background", new Color(0.05f, 0.06f, 0.08f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.SetAsFirstSibling();

            TitleLabel = UiFactory.CreateText(canvas.transform, "Title", "SHADOW CUBE", 86, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.6f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero, UiFactory.AccentColor);

            ProgressLabel = UiFactory.CreateText(canvas.transform, "Progress", "0 / 0 stars", 36, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero);

            PlayButton = UiFactory.CreateButton(canvas.transform, "Play", "PLAY", 48,
                new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(-260f, -40f), new Vector2(260f, 80f));

            LevelSelectButton = UiFactory.CreateButton(canvas.transform, "Levels", "SELECT LEVEL", 40,
                new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(-260f, -40f), new Vector2(260f, 80f));

            PlayButton.onClick.AddListener(OnPlayClicked);
            LevelSelectButton.onClick.AddListener(OnLevelSelectClicked);
        }

        public void Refresh()
        {
            if (ProgressLabel != null && progress != null)
                ProgressLabel.text = $"{progress.TotalStars} / {progress.MaxStars} stars";
        }

        public void OnPlayClicked()
        {
            Debug.Log("[ShadowCube] OnPlayClicked");
            if (flow == null) { Debug.LogWarning("[ShadowCube] OnPlayClicked：flow 为空"); return; }
            flow.PlayLastOrFirst(progress);
        }

        public void OnLevelSelectClicked()
        {
            Debug.Log("[ShadowCube] OnLevelSelectClicked");
            if (flow == null) { Debug.LogWarning("[ShadowCube] OnLevelSelectClicked：flow 为空"); return; }
            flow.GoToLevelSelect();
        }
    }
}
