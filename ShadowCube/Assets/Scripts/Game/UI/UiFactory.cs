using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowCube.Game.UI
{
    /// <summary>
    /// 代码生成 UGUI 的小工具：不依赖美术资源与预制体，便于 M2 快速搭界面。
    /// 文本统一使用引擎内置字体 + 英文占位（正式本地化与中文字体在 M2/M4 接 Localization 时替换）。
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.10f, 0.11f, 0.14f, 0.88f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.30f, 1f);
        public static readonly Color AccentColor = new Color(0.35f, 0.85f, 0.60f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.94f, 0.96f, 1f);

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color ?? TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreatePanel(parent, name, ButtonColor, anchorMin, anchorMax, offsetMin, offsetMax);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();

            var text = CreateText(rect, "Label", label, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.raycastTarget = false;

            return button;
        }
    }
}
