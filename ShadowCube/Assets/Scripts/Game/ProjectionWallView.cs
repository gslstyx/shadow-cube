using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 单面投影墙（需求 §1.2 / §2.3 / §3.3）：
    /// - **半透明发光面板**：让投影在深色场景中可读（此前只有投影格块、没有墙面，视觉上等于"没有墙"）
    /// - **目标投影 = 实心亮块**
    /// - **玩家投影 = 另一种颜色叠加**（青色）
    /// - **与目标重合的格子换成"命中色"**（绿），直观反馈"对上了"
    /// - 边框线勾勒墙面范围
    /// 显示哪张表、是否镜像由 GameController 通过 WallBindingRules 决定（随相机旋转变化）。
    /// </summary>
    public class ProjectionWallView : MonoBehaviour
    {
        [Header("配置")]
        public float cellSize = 1f;
        public float cellScale = 0.94f;
        public float panelDepth = -0.03f;    // 面板相对投影格的局部 Z（负 = 更靠后）
        public float overlayDepth = 0.008f;  // 叠加层错开，避免 Z-fighting

        [Header("材质（资产引用，由 GameController 赋值）")]
        public Material panelMaterial;
        public Material targetMaterial;
        public Material currentMaterial;
        public Material matchedMaterial;
        public Material borderMaterial;

        // ── 供测试/调试查询 ───────────────────────────────────
        public bool HasPanel => _panel != null && _panel.activeSelf;
        public int ActiveTargetCount { get; private set; }
        public int ActiveCurrentCount { get; private set; }
        public int ActiveMatchedCount { get; private set; }

        /// <summary>墙面整体包围盒（含面板），用于相机视锥检测</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (_panel != null)
                {
                    var r = _panel.GetComponent<Renderer>();
                    if (r != null) return r.bounds;
                }
                return new Bounds(transform.position, Vector3.one);
            }
        }

        private int _poolU = 5;
        private int _maxHeight = 4;
        private GameObject _panel;
        private LineRenderer _border;
        private Transform _targetRoot;
        private Transform _currentRoot;
        private Transform _matchedRoot;
        private readonly List<GameObject> _targets = new();
        private readonly List<GameObject> _currents = new();
        private readonly List<GameObject> _matcheds = new();
        private Material _panelMat;
        private Material _targetMat;
        private Material _currentMat;
        private Material _matchedMat;

        private static readonly Color FallbackPanelColor = new(0.20f, 0.24f, 0.32f, 0.30f);
        private static readonly Color FallbackTargetColor = new(0.90f, 0.93f, 0.98f, 0.85f);
        private static readonly Color FallbackCurrentColor = new(0.25f, 0.78f, 1.00f, 0.92f);
        private static readonly Color FallbackMatchedColor = new(0.28f, 0.92f, 0.55f, 1.00f);

        /// <summary>
        /// Unity 的 `PrimitiveType.Quad` 网格**正面法线朝 -Z**（不是 +Z）。
        /// 墙面局部 +Z 是朝向平台/相机的法线，因此所有投影面必须绕 Y 轴转 180°，
        /// 否则会被背面剔除 —— 表现就是"墙在场景里存在、但画面里完全看不到"。
        /// </summary>
        private static readonly Quaternion QuadFacing = Quaternion.Euler(0f, 180f, 0f);

        public void Configure(bool facePositiveX, int poolU, int maxHeight, float cell)
        {
            _poolU = Mathf.Max(1, poolU);
            _maxHeight = Mathf.Max(1, maxHeight);
            cellSize = cell;

            // 以云台为父，局部 +Z 为墙面法线（朝向平台）
            transform.rotation = facePositiveX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            _panelMat = panelMaterial != null ? new Material(panelMaterial) : CreateMaterial(FallbackPanelColor, true);
            _targetMat = targetMaterial != null ? new Material(targetMaterial) : CreateMaterial(FallbackTargetColor, true);
            _currentMat = currentMaterial != null ? new Material(currentMaterial) : CreateMaterial(FallbackCurrentColor, true);
            _matchedMat = matchedMaterial != null ? new Material(matchedMaterial) : CreateMaterial(FallbackMatchedColor, true);

            // 面板（背景发光面）：尺寸在 Show() 中按当前投影表宽度调整
            _panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _panel.name = "Panel";
            _panel.transform.SetParent(transform, false);
            _panel.transform.localPosition = new Vector3(0f, 0f, panelDepth);
            _panel.transform.localRotation = QuadFacing;
            StripCollider(_panel);
            _panel.GetComponent<Renderer>().sharedMaterial = _panelMat;
            _panel.SetActive(false);

            _targetRoot = CreateRoot("Target");
            _currentRoot = CreateRoot("Current");
            _matchedRoot = CreateRoot("Matched");

            BuildPool(_targets, _targetRoot, _targetMat, 0f);
            BuildPool(_currents, _currentRoot, _currentMat, overlayDepth);
            BuildPool(_matcheds, _matchedRoot, _matchedMat, overlayDepth);

            BuildBorder();
        }

        /// <summary>targetTable / currentTable 维度必须一致；flipU = 横向镜像（依相机右方向）</summary>
        public void Show(bool[,] targetTable, bool[,] currentTable, bool flipU)
        {
            if (targetTable == null)
            {
                HideAll();
                return;
            }

            int uCount = targetTable.GetLength(0);
            int hCount = targetTable.GetLength(1);
            bool sameShape = currentTable != null
                          && currentTable.GetLength(0) == uCount
                          && currentTable.GetLength(1) == hCount;

            ResizePanel(uCount, hCount);
            ActiveTargetCount = ActiveCurrentCount = ActiveMatchedCount = 0;

            for (int i = 0; i < _targets.Count; i++)
            {
                int u = i % _poolU;
                int y = i / _poolU;
                bool inRange = u < uCount && y < hCount;

                bool t = inRange && targetTable[u, y];
                bool c = inRange && sameShape && currentTable[u, y];

                // 命中（与目标重合）优先显示"匹配色"，否则区分玩家/目标
                bool showMatched = t && c;
                bool showCurrent = c && !t;
                bool showTarget = t && !c;

                SetCell(_matcheds[i], showMatched, u, y, uCount, flipU);
                SetCell(_currents[i], showCurrent, u, y, uCount, flipU);
                SetCell(_targets[i], showTarget, u, y, uCount, flipU);

                if (showMatched) ActiveMatchedCount++;
                else if (showCurrent) ActiveCurrentCount++;
                else if (showTarget) ActiveTargetCount++;
            }
        }

        /// <summary>过关反馈：面板与命中色整体提亮</summary>
        public void SetSolved(bool solved, Color highlight)
        {
            if (_panelMat != null)
            {
                var c = panelMaterial != null ? panelMaterial.color : FallbackPanelColor;
                _panelMat.color = solved ? new Color(highlight.r, highlight.g, highlight.b, 0.28f) : c;
            }

            if (_matchedMat != null && matchedMaterial != null)
            {
                var m = matchedMaterial.color;
                _matchedMat.color = solved ? new Color(m.r, m.g, m.b, 1f) : m;
            }
        }

        // ── 内部 ───────────────────────────────────────────────
        private void HideAll()
        {
            foreach (var go in _targets) go.SetActive(false);
            foreach (var go in _currents) go.SetActive(false);
            foreach (var go in _matcheds) go.SetActive(false);
            if (_panel != null) _panel.SetActive(false);
            if (_border != null) _border.gameObject.SetActive(false);
            ActiveTargetCount = ActiveCurrentCount = ActiveMatchedCount = 0;
        }

        private void ResizePanel(int uCount, int hCount)
        {
            float width = uCount * cellSize;
            float height = hCount * cellSize;

            if (_panel != null)
            {
                _panel.SetActive(true);
                _panel.transform.localPosition = new Vector3(0f, height * 0.5f, panelDepth);
                _panel.transform.localScale = new Vector3(width, height, 1f);
            }

            if (_border != null)
            {
                _border.gameObject.SetActive(true);
                float halfW = width * 0.5f;
                _border.SetPositions(new[]
                {
                    new Vector3(-halfW, 0f, 0f),
                    new Vector3(halfW, 0f, 0f),
                    new Vector3(halfW, height, 0f),
                    new Vector3(-halfW, height, 0f),
                    new Vector3(-halfW, 0f, 0f)
                });
            }
        }

        private void SetCell(GameObject go, bool visible, int u, int y, int uCount, bool flipU)
        {
            go.SetActive(visible);
            if (!visible) return;

            float localX = (u + 0.5f - uCount * 0.5f) * cellSize;
            var pos = go.transform.localPosition;
            go.transform.localPosition = new Vector3(flipU ? -localX : localX, (y + 0.5f) * cellSize, pos.z);
        }

        private Transform CreateRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        private void BuildPool(List<GameObject> pool, Transform root, Material mat, float localZ)
        {
            for (int y = 0; y < _maxHeight; y++)
            {
                for (int u = 0; u < _poolU; u++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = $"c{u}_{y}";
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(0f, 0f, localZ);
                    go.transform.localRotation = QuadFacing;
                    go.transform.localScale = Vector3.one * cellSize * cellScale;

                    StripCollider(go);
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                    go.SetActive(false);
                    pool.Add(go);
                }
            }
        }

        private void BuildBorder()
        {
            var go = new GameObject("Border");
            go.transform.SetParent(transform, false);

            _border = go.AddComponent<LineRenderer>();
            _border.useWorldSpace = false;
            _border.loop = false;
            _border.positionCount = 5;
            _border.startWidth = _border.endWidth = 0.03f;
            _border.numCapVertices = 0;
            _border.material = borderMaterial != null
                ? new Material(borderMaterial)
                : CreateMaterial(new Color(0.45f, 0.55f, 0.65f, 0.8f), true);
            _border.gameObject.SetActive(false);
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;

            collider.enabled = false;   // 立即失效，避免挡住拾取射线
            Destroy(collider);
        }

        private static Material CreateMaterial(Color color, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (transparent) MaterialUtils.SetTransparent(mat);
            return mat;
        }
    }
}
