using System.Collections.Generic;
using System.Text;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// M1 灰盒：平台 + 方块生成/消除 + 两面墙实时投影 + 过关判定。
    /// 交互：按住拖拽连续生成/消除；起手列为空 → 生成手势；起手列有方块 → 消除手势；R 键重置。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("关卡")]
        public LevelData level;

        [Header("布局")]
        public float cellSize = 1f;
        public float wallGap = 1.5f;

        [Header("颜色")]
        public Color platformColor = new Color(0.18f, 0.18f, 0.2f);
        public Color gridColor = new Color(0.45f, 0.55f, 0.65f);
        public Color voxelColor = new Color(0.85f, 0.87f, 0.9f);

        private VoxelGrid _grid;
        private readonly Dictionary<Vector3Int, GameObject> _views = new();
        private Transform _voxelRoot;
        private ProjectionWallView _wallLeft;
        private ProjectionWallView _wallFront;

        private bool _dragging;
        private bool _gestureIsAdd;
        private Vector2Int _lastCell = new(-1, -1);
        private bool _solved;

        private Material _voxelMat;

        private void Awake()
        {
            if (level == null)
            {
                Debug.LogError("[ShadowCube] GameController 未指定 LevelData");
                return;
            }

            _grid = level.CreateGrid();
            _voxelMat = CreateMaterial(voxelColor, false);

            BuildPlatform();
            BuildWalls();
            RefreshProjection();
        }

        private void Update()
        {
            if (_grid == null) return;

            HandleInput();
            if (Input.GetKeyDown(KeyCode.R)) ResetLevel();
        }

        // ── 构建 ────────────────────────────────────────────────
        private void BuildPlatform()
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(transform, false);
            platform.transform.localScale = new Vector3(level.width * cellSize, 0.1f, level.depth * cellSize);
            platform.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            platform.GetComponent<Renderer>().sharedMaterial = CreateMaterial(platformColor, false);

            _voxelRoot = new GameObject("Voxels").transform;
            _voxelRoot.SetParent(transform, false);
        }

        private void BuildWalls()
        {
            float halfW = level.width * cellSize * 0.5f;
            float halfD = level.depth * cellSize * 0.5f;

            // 左墙（沿 X 方向投影，u = z）
            var left = new GameObject("Wall_Left");
            left.transform.SetParent(transform, false);
            left.transform.position = new Vector3(-halfW - wallGap, 0f, 0f);
            _wallLeft = left.AddComponent<ProjectionWallView>();
            _wallLeft.Configure(false, level.depth, level.maxHeight, cellSize, facePositiveX: true);

            // 后墙（沿 Z 方向投影，u = x）
            var front = new GameObject("Wall_Front");
            front.transform.SetParent(transform, false);
            front.transform.position = new Vector3(0f, 0f, -halfD - wallGap);
            _wallFront = front.AddComponent<ProjectionWallView>();
            _wallFront.Configure(true, level.width, level.maxHeight, cellSize, facePositiveX: false);
        }

        // ── 输入 ────────────────────────────────────────────────
        private void HandleInput()
        {
            var cam = Camera.main;
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (TryGetCell(cam, out var cell))
                {
                    _dragging = true;
                    _gestureIsAdd = _grid.GetTopHeight(cell.x, cell.y) < 0; // 起手列为空 → 生成
                    _lastCell = cell;
                    ApplyAt(cell);
                }
            }
            else if (Input.GetMouseButton(0) && _dragging)
            {
                if (TryGetCell(cam, out var cell) && cell != _lastCell)
                {
                    _lastCell = cell;
                    ApplyAt(cell);
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _lastCell = new Vector2Int(-1, -1);
            }
        }

        private bool TryGetCell(Camera cam, out Vector2Int cell)
        {
            cell = default;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float dist)) return false;

            var p = ray.GetPoint(dist);
            int x = Mathf.FloorToInt(p.x / cellSize + level.width * 0.5f);
            int z = Mathf.FloorToInt(p.z / cellSize + level.depth * 0.5f);
            if (x < 0 || x >= level.width || z < 0 || z >= level.depth) return false;

            cell = new Vector2Int(x, z);
            return true;
        }

        private void ApplyAt(Vector2Int cell)
        {
            if (_gestureIsAdd)
            {
                int y = _grid.GetNextFreeHeight(cell.x, cell.y);
                if (y < 0) return;                                  // 该列已满
                var c = new Vector3Int(cell.x, y, cell.y);
                if (!_grid.Add(c)) return;
                SpawnView(c);
            }
            else
            {
                int y = _grid.GetTopHeight(cell.x, cell.y);
                if (y < 0) return;
                var c = new Vector3Int(cell.x, y, cell.y);
                if (!_grid.Remove(c)) return;
                DestroyView(c);
            }

            RefreshProjection();
            CheckSolved();
        }

        // ── 视图 ────────────────────────────────────────────────
        private void SpawnView(Vector3Int c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Voxel_{c.x}_{c.y}_{c.z}";
            go.transform.SetParent(_voxelRoot, false);
            go.transform.localPosition = CellToWorld(c);
            go.transform.localScale = Vector3.one * cellSize * 0.92f;
            go.GetComponent<Renderer>().sharedMaterial = _voxelMat;
            Destroy(go.GetComponent<Collider>());
            _views[c] = go;
        }

        private void DestroyView(Vector3Int c)
        {
            if (_views.TryGetValue(c, out var go) && go != null) Destroy(go);
            _views.Remove(c);
        }

        private Vector3 CellToWorld(Vector3Int c)
            => new(
                (c.x + 0.5f - level.width * 0.5f) * cellSize,
                (c.y + 0.5f) * cellSize,
                (c.z + 0.5f - level.depth * 0.5f) * cellSize);

        // ── 判定 ────────────────────────────────────────────────
        private void RefreshProjection()
        {
            _wallFront?.Show(level.TargetFront, ProjectionUtil.ProjectFront(_grid));
            _wallLeft?.Show(level.TargetLeft, ProjectionUtil.ProjectLeft(_grid));
        }

        private void CheckSolved()
        {
            if (_solved) return;
            if (!LevelJudge.IsSolved(_grid, level)) return;

            _solved = true;
            int stars = LevelJudge.GetStars(_grid.Count, level.OptimalCount);
            Debug.Log($"[ShadowCube] 过关！方块 {_grid.Count} / 最优 {level.OptimalCount} → {stars} 星\n{Dump()}");
        }

        public void ResetLevel()
        {
            _grid.Clear();
            foreach (var kv in _views) if (kv.Value != null) Destroy(kv.Value);
            _views.Clear();
            _solved = false;
            RefreshProjection();
            Debug.Log("[ShadowCube] 已重置本关");
        }

        private string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("当前 后墙投影:");
            AppendTable(sb, ProjectionUtil.ProjectFront(_grid));
            sb.AppendLine("当前 左墙投影:");
            AppendTable(sb, ProjectionUtil.ProjectLeft(_grid));
            return sb.ToString();
        }

        private static void AppendTable(StringBuilder sb, bool[,] t)
        {
            for (int y = t.GetLength(1) - 1; y >= 0; y--)
            {
                sb.Append("  ");
                for (int x = 0; x < t.GetLength(0); x++) sb.Append(t[x, y] ? '■' : '·');
                sb.AppendLine();
            }
        }

        private static Material CreateMaterial(Color color, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        // ── 调试可视化 ─────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (level == null) return;

            Gizmos.color = gridColor;
            float halfW = level.width * cellSize * 0.5f;
            float halfD = level.depth * cellSize * 0.5f;

            for (int x = 0; x <= level.width; x++)
            {
                float px = x * cellSize - halfW;
                Gizmos.DrawLine(new Vector3(px, 0, -halfD), new Vector3(px, 0, halfD));
            }
            for (int z = 0; z <= level.depth; z++)
            {
                float pz = z * cellSize - halfD;
                Gizmos.DrawLine(new Vector3(-halfW, 0, pz), new Vector3(halfW, 0, pz));
            }
        }
    }
}
