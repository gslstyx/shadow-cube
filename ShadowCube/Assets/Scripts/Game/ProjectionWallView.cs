using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 单面墙的投影显示：目标投影（半透明灰）+ 当前投影（实心，过关后高亮）。
    /// 显示哪张表、是否镜像由 GameController 通过 WallBindingRules 决定（随相机旋转变化）。
    /// </summary>
    public class ProjectionWallView : MonoBehaviour
    {
        [Header("配置")]
        public float cellSize = 1f;
        public float targetAlpha = 0.25f;

        private static readonly Color CurrentBaseColor = new Color(0.06f, 0.06f, 0.06f, 1f);

        private int _poolU = 5;
        private int _maxHeight = 4;
        private Transform _targetRoot;
        private Transform _currentRoot;
        private readonly List<GameObject> _targets = new();
        private readonly List<GameObject> _currents = new();
        private Material _targetMat;
        private Material _currentMat;

        public void Configure(bool facePositiveX, int poolU, int maxHeight, float cell)
        {
            _poolU = poolU;
            _maxHeight = maxHeight;
            cellSize = cell;

            transform.rotation = facePositiveX ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;

            _targetRoot = new GameObject("Target").transform;
            _targetRoot.SetParent(transform, false);
            _currentRoot = new GameObject("Current").transform;
            _currentRoot.SetParent(transform, false);

            _targetMat = CreateMaterial(new Color(0.35f, 0.35f, 0.35f, targetAlpha), transparent: true);
            _currentMat = CreateMaterial(CurrentBaseColor, transparent: false);

            BuildPool(_targets, _targetRoot, _targetMat, 0.92f);
            BuildPool(_currents, _currentRoot, _currentMat, 0.86f);
        }

        /// <summary>targetTable/currentTable 的 U 维度必须一致（同一张表的两个时刻）</summary>
        public void Show(bool[,] targetTable, bool[,] currentTable, bool flipU)
        {
            Layout(_targets, targetTable, flipU);
            Layout(_currents, currentTable, flipU);
        }

        /// <summary>过关反馈：当前投影变高亮色</summary>
        public void SetSolved(bool solved, Color highlight)
        {
            if (_currentMat == null) return;
            _currentMat.color = solved ? highlight : CurrentBaseColor;
        }

        private void Layout(List<GameObject> pool, bool[,] table, bool flipU)
        {
            if (table == null) { foreach (var go in pool) go.SetActive(false); return; }

            int uCount = table.GetLength(0);
            int hCount = table.GetLength(1);

            for (int i = 0; i < pool.Count; i++)
            {
                int u = i % _poolU;
                int y = i / _poolU;

                bool visible = u < uCount && y < hCount && table[u, y];
                pool[i].SetActive(visible);
                if (!visible) continue;

                float localX = (u + 0.5f - uCount * 0.5f) * cellSize;
                pool[i].transform.localPosition = new Vector3(flipU ? -localX : localX, (y + 0.5f) * cellSize, 0f);
            }
        }

        private void BuildPool(List<GameObject> pool, Transform root, Material mat, float scale)
        {
            for (int y = 0; y < _maxHeight; y++)
            {
                for (int u = 0; u < _poolU; u++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = $"c{u}_{y}";
                    go.transform.SetParent(root, false);
                    go.transform.localScale = Vector3.one * cellSize * scale;

                    var collider = go.GetComponent<Collider>();
                    if (collider != null)
                    {
                        collider.enabled = false;   // 立即失效，避免挡住拾取射线
                        Destroy(collider);
                    }

                    go.GetComponent<Renderer>().sharedMaterial = mat;
                    go.SetActive(false);
                    pool.Add(go);
                }
            }
        }

        private static Material CreateMaterial(Color color, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return mat;
        }
    }
}
