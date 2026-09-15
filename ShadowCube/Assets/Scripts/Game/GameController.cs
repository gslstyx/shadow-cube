using System.Collections.Generic;
using System.Text;
using ShadowCube.Core;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// M1 灰盒：平台 + 方块生成/消除 + 两面墙实时投影 + 过关判定。
    /// 交互：按住拖拽连续生成/消除；起手列为空 → 生成手势，起手列有方块 → 消除手势；R 键或 ResetLevel() 重置。
    /// 调试：Console 打印投影占用表，Scene 视图显示网格 Gizmo。
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
        public Color solvedHighlight = new Color(0.25f, 0.9f, 0.5f);

        [Header("动效")]
        public float spawnDuration = 0.14f;
        public float despawnDuration = 0.12f;

        // ── 对外只读状态（供 UI / 测试使用）──────────────
        public VoxelGrid Grid => _grid;
        public bool IsSolved => _solved;
        public int BlockCount => _grid?.Count ?? 0;
        public int StarRating => _solved && level != null
            ? LevelJudge.GetStars(_grid.Count, level.OptimalCount) : 0;
        public bool SolvedFeedbackPlayed { get; private set; }
        public DragTrail Trail { get; private set; }
        [SerializeField] private bool muteAudio;
        public bool MuteAudio
        {
            get => muteAudio;
            set
            {
                muteAudio = value;
                if (_sfx is ProceduralSfxPlayer p) p.muted = value;
            }
        }

        /// <summary>音效播放器（测试可注入替身）</summary>
        public ISfxPlayer Sfx
        {
            get => _sfx;
            set => _sfx = value ?? new NullSfxPlayer();
        }

        private ISfxPlayer _sfx = new NullSfxPlayer();

        private VoxelGrid _grid;
        private readonly Dictionary<Vector3Int, VoxelView> _views = new();
        private Transform _voxelRoot;
        private GameObject _hover;
        private Material _hoverMat;
        private ProjectionWallView _wallLeft;
        private ProjectionWallView _wallFront;
        private Material _voxelMat;

        private bool _dragging;
        private GestureRules.Gesture _gesture = GestureRules.Gesture.Add;
        private Vector2Int _lastCell = new(-1, -1);
        private bool _solved;

        private void Awake()
        {
            if (level == null)
            {
                Debug.LogError("[ShadowCube] GameController 未指定 LevelData");
                return;
            }

            _grid = level.CreateGrid();
            _voxelMat = CreateMaterial(voxelColor);

            if (_sfx is NullSfxPlayer)
            {
                var procedural = gameObject.GetComponent<ProceduralSfxPlayer>();
                if (procedural == null) procedural = gameObject.AddComponent<ProceduralSfxPlayer>();
                procedural.muted = muteAudio;
                _sfx = procedural;
            }

            BuildPlatform();
            BuildWalls();
            BuildHover();
            BuildTrail();
            RefreshProjection();
        }

        private void Update()
        {
            if (_grid == null) return;

            HandlePointer();
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
            platform.GetComponent<Renderer>().sharedMaterial = CreateMaterial(platformColor);

            _voxelRoot = new GameObject("Voxels").transform;
            _voxelRoot.SetParent(transform, false);
        }

        private void BuildWalls()
        {
            float halfW = level.width * cellSize * 0.5f;
            float halfD = level.depth * cellSize * 0.5f;

            _wallLeft = CreateWall("Wall_Left", new Vector3(-halfW - wallGap, 0f, 0f),
                isFront: false, uSize: level.depth, facePositiveX: true);

            _wallFront = CreateWall("Wall_Front", new Vector3(0f, 0f, -halfD - wallGap),
                isFront: true, uSize: level.width, facePositiveX: false);
        }

        private ProjectionWallView CreateWall(string name, Vector3 position, bool isFront, int uSize, bool facePositiveX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var wall = go.AddComponent<ProjectionWallView>();
            wall.Configure(isFront, uSize, level.maxHeight, cellSize, facePositiveX);
            return wall;
        }

        private void BuildHover()
        {
            _hover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _hover.name = "HoverIndicator";
            _hover.transform.SetParent(transform, false);
            _hover.transform.localScale = Vector3.one * cellSize * 0.98f;
            Destroy(_hover.GetComponent<Collider>());
            _hoverMat = CreateMaterial(new Color(1f, 1f, 1f, 0.18f), transparent: true);
            _hover.GetComponent<Renderer>().sharedMaterial = _hoverMat;
            _hover.SetActive(false);
        }

        private void BuildTrail()
        {
            var go = new GameObject("DragTrail");
            go.transform.SetParent(transform, false);
            Trail = go.AddComponent<DragTrail>();
            Trail.Init();
        }

        // ── 输入 ────────────────────────────────────────────────
        private void HandlePointer()
        {
            var cam = Camera.main;
            if (cam == null) return;

            bool pressed = Input.GetMouseButton(0);
            if (TryPickCell(cam, out var cell, out bool hitVoxel))
            {
                UpdateHover(cell, hitVoxel);

                if (Input.GetMouseButtonDown(0))
                {
                    _dragging = true;
                    _gesture = GestureRules.Decide(hitVoxel || HasVoxelInColumn(cell.x, cell.y));
                    _lastCell = cell;
                    Trail?.Clear();
                    AddTrailPoint(cell);
                    Apply(cell);
                }
                else if (pressed && _dragging && cell != _lastCell)
                {
                    _lastCell = cell;
                    AddTrailPoint(cell);
                    Apply(cell);
                }
            }
            else if (!pressed && _hover != null)
            {
                _hover.SetActive(false);
            }

            if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _lastCell = new Vector2Int(-1, -1);
            }
        }

        private void UpdateHover(Vector2Int cell, bool hitVoxel)
        {
            if (_hover == null) return;

            int y = hitVoxel ? _grid.GetTopHeight(cell.x, cell.y) : _grid.GetNextFreeHeight(cell.x, cell.y);
            if (y < 0) { _hover.SetActive(false); return; }

            _hover.SetActive(true);
            _hover.transform.localPosition = CellToWorld(new Vector3Int(cell.x, y, cell.y));
            _hoverMat.color = _dragging
                ? new Color(1f, 1f, 1f, 0.10f)
                : new Color(1f, 1f, 1f, 0.22f);
        }

        /// <summary>射线拾取：优先命中方块（斜视角下点在堆叠顶部也能取对列），否则命中平台</summary>
        private bool TryPickCell(Camera cam, out Vector2Int cell, out bool hitVoxel)
        {
            cell = default;
            hitVoxel = false;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);

            Vector3 point;
            if (Physics.Raycast(ray, out var hit, 200f))
            {
                point = hit.point;
                hitVoxel = hit.collider != null && hit.collider.GetComponent<VoxelView>() != null;
            }
            else if (plane.Raycast(ray, out float dist))
            {
                point = ray.GetPoint(dist);
            }
            else
            {
                return false;
            }

            int x = Mathf.FloorToInt(point.x / cellSize + level.width * 0.5f);
            int z = Mathf.FloorToInt(point.z / cellSize + level.depth * 0.5f);
            if (x < 0 || x >= level.width || z < 0 || z >= level.depth) return false;

            cell = new Vector2Int(x, z);
            return true;
        }

        private void AddTrailPoint(Vector2Int cell)
        {
            var p = new Vector3(
                (cell.x + 0.5f - level.width * 0.5f) * cellSize,
                0.06f,
                (cell.y + 0.5f - level.depth * 0.5f) * cellSize);
            Trail?.AddPoint(p);
        }

        private void Apply(Vector2Int cell)
        {
            if (_gesture == GestureRules.Gesture.Add) AddAt(cell.x, cell.y);
            else RemoveAt(cell.x, cell.y);
        }

        // ── 对外可调用的操作（UI / 自动化测试）──────────────────
        public bool HasVoxelInColumn(int x, int z) => _grid != null && _grid.GetTopHeight(x, z) >= 0;

        public bool AddAt(int x, int z)
        {
            if (_grid == null) return false;

            int y = _grid.GetNextFreeHeight(x, z);
            if (y < 0) return false;

            var c = new Vector3Int(x, y, z);
            if (!_grid.Add(c)) return false;

            SpawnView(c);
            RefreshProjection();
            _sfx.PlayAdd();
            CheckSolved();
            return true;
        }

        public bool RemoveAt(int x, int z)
        {
            if (_grid == null) return false;

            int y = _grid.GetTopHeight(x, z);
            if (y < 0) return false;

            var c = new Vector3Int(x, y, z);
            if (!_grid.Remove(c)) return false;

            DespawnView(c);
            RefreshProjection();
            _sfx.PlayRemove();
            CheckSolved();
            return true;
        }

        public void ResetLevel()
        {
            if (_grid == null) return;

            _grid.Clear();
            foreach (var kv in _views)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _views.Clear();

            _solved = false;
            SolvedFeedbackPlayed = false;
            _wallFront?.SetSolved(false, solvedHighlight);
            _wallLeft?.SetSolved(false, solvedHighlight);
            Trail?.Clear();
            RefreshProjection();
            Debug.Log("[ShadowCube] 已重置本关");
        }

        // ── 视图 ────────────────────────────────────────────────
        private void SpawnView(Vector3Int c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Voxel_{c.x}_{c.y}_{c.z}";
            go.transform.SetParent(_voxelRoot, false);
            go.transform.localPosition = CellToWorld(c);
            go.GetComponent<Renderer>().sharedMaterial = _voxelMat;

            var view = go.AddComponent<VoxelView>();
            var scale = Vector3.one * cellSize * 0.92f;
            view.Init(scale, spawnDuration);
            _views[c] = view;
        }

        private void DespawnView(Vector3Int c)
        {
            if (!_views.TryGetValue(c, out var view) || view == null)
            {
                _views.Remove(c);
                return;
            }

            _views.Remove(c);
            view.PlayDespawn(despawnDuration, () => Destroy(view.gameObject));
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
            if (_solved || !LevelJudge.IsSolved(_grid, level)) return;

            _solved = true;
            _wallFront?.SetSolved(true, solvedHighlight);
            _wallLeft?.SetSolved(true, solvedHighlight);

            _sfx.PlaySolved();
            PlaySolvedFeedback();

            Debug.Log($"[ShadowCube] 过关！方块 {_grid.Count} / 最优 {level.OptimalCount} → {StarRating} 星\n{Dump()}");
        }

        /// <summary>过关结算动效：方块按 (x+z) 错峰脉冲，形成波浪扫过</summary>
        private void PlaySolvedFeedback()
        {
            SolvedFeedbackPlayed = true;

            foreach (var kv in _views)
            {
                if (kv.Value == null) continue;
                float delay = (kv.Key.x + kv.Key.z) * 0.05f;
                kv.Value.PlayPulse(delay);
            }
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

        private static Material CreateMaterial(Color color, bool transparent = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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
