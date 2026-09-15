using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 墙面投影显示：目标投影（描边灰块）+ 当前投影（实心黑块）。
    /// isFront=true 为后墙（横向 u = x），false 为左墙（横向 u = z）。
    /// </summary>
    public class ProjectionWallView : MonoBehaviour
    {
        [Header("配置")]
        public bool isFront = true;
        public int uSize = 5;
        public int maxHeight = 4;
        public float cellSize = 1f;
        public float targetAlpha = 0.25f;

        private Transform _targetRoot;
        private Transform _currentRoot;
        private readonly System.Collections.Generic.List<GameObject> _targets = new();
        private readonly System.Collections.Generic.List<GameObject> _currents = new();
        private Material _targetMat;
        private Material _currentMat;

        public void Configure(bool front, int u, int height, float cell, bool facePositiveX)
        {
            isFront = front;
            uSize = u;
            maxHeight = height;
            cellSize = cell;

            transform.rotation = facePositiveX ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;

            _targetRoot = new GameObject("Target").transform;
            _targetRoot.SetParent(transform, false);
            _currentRoot = new GameObject("Current").transform;
            _currentRoot.SetParent(transform, false);

            _targetMat = CreateMaterial(new Color(0.35f, 0.35f, 0.35f, targetAlpha), transparent: true);
            _currentMat = CreateMaterial(new Color(0.06f, 0.06f, 0.06f, 1f), transparent: false);

            EnsurePool(_targets, _targetRoot, _targetMat, 0.92f);
            EnsurePool(_currents, _currentRoot, _currentMat, 0.86f);
        }

        public void Show(bool[,] target, bool[,] current)
        {
            Apply(_targets, target);
            Apply(_currents, current);
        }

        private void Apply(System.Collections.Generic.List<GameObject> pool, bool[,] table)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int u = i % uSize;
                int y = i / uSize;
                bool on = table != null && u < table.GetLength(0) && y < table.GetLength(1) && table[u, y];
                pool[i].SetActive(on);
            }
        }

        private void EnsurePool(System.Collections.Generic.List<GameObject> pool, Transform root, Material mat, float scale)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int u = 0; u < uSize; u++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = $"c{u}_{y}";
                    go.transform.SetParent(root, false);

                    float localX = (u + 0.5f - uSize * 0.5f) * cellSize;
                    if (!isFront) localX = -localX;          // 左墙旋转后横向取反，保证与平台方向一致
                    go.transform.localPosition = new Vector3(localX, (y + 0.5f) * cellSize, 0f);
                    go.transform.localScale = Vector3.one * cellSize * scale;

                    var renderer = go.GetComponent<Renderer>();
                    renderer.sharedMaterial = mat;
                    DestroyImmediate(go.GetComponent<Collider>());
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
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            return mat;
        }
    }
}
