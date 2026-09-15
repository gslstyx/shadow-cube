using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 拖拽轨迹反馈：把本次手势经过的格子连成一条淡出尾迹。
    /// 松手后 0.35s 开始淡出，淡完自动清空。
    /// </summary>
    public class DragTrail : MonoBehaviour
    {
        [Header("外观")]
        public float width = 0.09f;
        public Color color = new Color(0.65f, 0.85f, 1f, 0.85f);
        [Header("淡出")]
        public float holdSeconds = 0.35f;
        public float fadeSeconds = 0.25f;
        public float minPointDistance = 0.15f;

        public int PointCount => _points.Count;

        private readonly List<Vector3> _points = new List<Vector3>();
        private LineRenderer _line;
        private Material _material;
        private float _lastAddTime = -999f;
        private float _alphaScale = 1f;

        public void Init()
        {
            _line = gameObject.GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 0;
            _line.widthMultiplier = width;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.View;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { color = color };
            _material.SetFloat("_Surface", 1f);
            _material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_ZWrite", 0f);
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _line.material = _material;
            _line.startColor = color;
            _line.endColor = color;
        }

        public void AddPoint(Vector3 worldPoint)
        {
            if (_points.Count > 0 && Vector3.Distance(_points[^1], worldPoint) < minPointDistance) return;

            _points.Add(worldPoint);
            _lastAddTime = Time.time;
            _alphaScale = 1f;
            Apply();
        }

        public void Clear()
        {
            _points.Clear();
            _alphaScale = 1f;
            Apply();
        }

        private void Update()
        {
            if (_points.Count == 0) return;
            if (Time.time - _lastAddTime < holdSeconds) return;

            _alphaScale -= Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            if (_alphaScale <= 0f)
            {
                Clear();
                return;
            }
            ApplyAlpha();
        }

        private void Apply()
        {
            if (_line == null) return;

            _line.positionCount = _points.Count;
            for (int i = 0; i < _points.Count; i++) _line.SetPosition(i, _points[i]);
            ApplyAlpha();
        }

        private void ApplyAlpha()
        {
            var c = color;
            c.a = color.a * Mathf.Clamp01(_alphaScale);
            _line.startColor = c;
            _line.endColor = c;
            if (_material != null) _material.color = c;
        }
    }
}
