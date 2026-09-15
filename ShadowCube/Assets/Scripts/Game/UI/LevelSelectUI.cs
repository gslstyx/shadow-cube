using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 选关界面：按目录顺序展示关卡，锁定关卡显示锁图标且不可点，已通关显示星级（需求 §3.2）。
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        [Header("布局")]
        public int columns = 4;
        public float cellSize = 200f;
        public float spacing = 24f;

        public ProgressManager progress;
        public LevelFlow flow;

        /// <summary>
        /// 懒解析 LevelFlow：Awake 期间可能抓到"即将因重复而被销毁"的实例，
        /// 因此每次使用时若已失效（Unity fake-null）则重新查找。
        /// </summary>
        private LevelFlow Flow => flow != null ? flow : (flow = FindObjectOfType<LevelFlow>());

        public Text TitleLabel { get; private set; }
        public Text StarsLabel { get; private set; }
        public Button BackButton { get; private set; }

        public List<Button> LevelButtons { get; } = new List<Button>();
        public List<Text> LevelLabels { get; } = new List<Text>();

        private Canvas _canvas;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<ProgressManager>();
            if (flow == null) flow = FindObjectOfType<LevelFlow>();

            Build();
            Refresh();
        }

        private void Build()
        {
            _canvas = UiFactory.CreateCanvas("LevelSelect", 10);

            var background = UiFactory.CreatePanel(_canvas.transform, "Background", new Color(0.05f, 0.06f, 0.08f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.SetAsFirstSibling();

            TitleLabel = UiFactory.CreateText(_canvas.transform, "Title", "SELECT LEVEL", 56, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.82f), new Vector2(1f, 0.95f), Vector2.zero, Vector2.zero, UiFactory.AccentColor);

            StarsLabel = UiFactory.CreateText(_canvas.transform, "Stars", "", 32, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.76f), new Vector2(1f, 0.82f), Vector2.zero, Vector2.zero);

            BackButton = UiFactory.CreateButton(_canvas.transform, "Back", "Back", 34,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(200f, 100f));
            BackButton.onClick.AddListener(() => { if (Flow != null) Flow.GoToMainMenu(); });

            BuildLevelButtons();
        }

        private void BuildLevelButtons()
        {
            if (progress == null || progress.catalog == null) return;

            var grid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(_canvas.transform, false);
            grid.anchorMin = new Vector2(0.5f, 0.5f);
            grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.offsetMin = new Vector2(-700f, -700f);
            grid.offsetMax = new Vector2(700f, 700f);

            for (int i = 0; i < progress.catalog.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;

                float x = (col - (columns - 1) * 0.5f) * (cellSize + spacing);
                float y = -row * (cellSize + spacing);

                var button = UiFactory.CreateButton(grid, $"Level_{i}", "", 34,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x - cellSize * 0.5f, y - cellSize * 0.5f),
                    new Vector2(x + cellSize * 0.5f, y + cellSize * 0.5f));

                int index = i;
                button.onClick.AddListener(() => OnLevelClicked(index));

                var label = button.GetComponentInChildren<Text>();
                LevelButtons.Add(button);
                LevelLabels.Add(label);
            }
        }

        public void Refresh()
        {
            if (progress == null || progress.catalog == null) return;

            if (StarsLabel != null)
                StarsLabel.text = $"{progress.TotalStars} / {progress.MaxStars} stars";

            for (int i = 0; i < LevelButtons.Count; i++)
            {
                var level = progress.catalog.Get(i);
                var button = LevelButtons[i];
                var label = LevelLabels[i];
                if (level == null || button == null || label == null) continue;

                bool unlocked = progress.IsUnlocked(level);
                int stars = progress.GetStars(level);

                button.interactable = unlocked;
                label.text = unlocked
                    ? (stars > 0 ? $"{i + 1}\n{stars}/5" : $"{i + 1}")
                    : "Locked";
                label.color = unlocked ? UiFactory.TextColor : new Color(0.55f, 0.55f, 0.58f, 1f);
            }
        }

        public void OnLevelClicked(int index)
        {
            if (progress == null || progress.catalog == null || Flow == null) return;

            var level = progress.catalog.Get(index);
            if (level == null || !progress.IsUnlocked(level)) return;

            Flow.PlayLevel(level);
        }
    }
}
