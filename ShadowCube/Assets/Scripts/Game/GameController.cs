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

        [Tooltip("留空则自动查找场景中的 ProgressManager（找不到则不存档）")]
        public ProgressManager progress;

        [Tooltip("HUD（可选，用于按钮触发后刷新显示）")]
        public UI.GameHud hud;

        /// <summary>过关事件（参数为星级），结算面板等订阅</summary>
        public event System.Action<int> Solved;

        [Header("布局")]
        public float cellSize = 1f;
        public float wallGap = 1.5f;

        [Header("颜色")]
        public Color platformColor = new Color(0.18f, 0.18f, 0.2f);
        public Color gridColor = new Color(0.45f, 0.55f, 0.65f);
        public Color voxelColor = new Color(0.85f, 0.87f, 0.9f);
        public Color solvedHighlight = new Color(0.25f, 0.9f, 0.5f);

        [Header("材质（资产引用，由场景生成器赋值；保证 shader 进包）")]
        public Material platformMaterial;
        public Material voxelMaterial;
        public Material gridLineMaterial;
        public Material hoverMaterial;
        public Material wallPanelMaterial;
        public Material wallTargetMaterial;
        public Material wallCurrentMaterial;
        public Material wallMatchedMaterial;
        public Material wallBorderMaterial;
        public Material dragTrailMaterial;

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
        public CameraRig Rig { get; private set; }

        /// <summary>平台网格线（运行时可见）</summary>
        public LineRenderer GridLines { get; private set; }

        /// <summary>左墙（投影到 Z-Y 面）</summary>
        public ProjectionWallView WallLeft => _wallLeft;
        /// <summary>后墙（投影到 X-Y 面）</summary>
        public ProjectionWallView WallFront => _wallFront;

        private GameObject _platformGo;

        /// <summary>方块对象池中的空闲数量（M4 性能：避免频繁创建/销毁）</summary>
        public int PooledVoxelCount => _voxelPool.Count;

        /// <summary>后墙当前是否显示 X-Y 投影表（随相机旋转变化，供测试/调试）</summary>
        public bool BackWallUsesXY { get; private set; }
        /// <summary>左墙当前是否显示 X-Y 投影表</summary>
        public bool LeftWallUsesXY { get; private set; }
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
        private readonly Stack<GameObject> _voxelPool = new Stack<GameObject>();
        private bool[,] _xyBuffer;
        private bool[,] _zyBuffer;
        private Transform _voxelRoot;
        private GameObject _hover;
        private Material _hoverMat;
        private bool _rotating;
        private float _lastPointerX;
        private ProjectionWallView _wallLeft;
        private ProjectionWallView _wallFront;
        private Material _voxelMat;

        private bool _dragging;
        private GestureRules.Gesture _gesture = GestureRules.Gesture.Add;
        private Vector2Int _lastCell = new(-1, -1);
        private bool _solved;

        private void Awake()
        {
            // 场景切换时由 LevelFlow 指定要玩的关卡
            if (LevelFlow.PendingLevel != null)
            {
                level = LevelFlow.PendingLevel;
                LevelFlow.PendingLevel = null;
            }

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

            if (progress == null) progress = FindObjectOfType<ProgressManager>();

            if (_voxelRoot == null)
            {
                _voxelRoot = new GameObject("Voxels").transform;
                _voxelRoot.SetParent(transform, false);
            }

            EnsureRig();
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
        /// <summary>找到（或就地创建）相机云台，使两面墙随视角旋转</summary>
        private void EnsureRig()
        {
            Rig = FindObjectOfType<CameraRig>();
            if (Rig == null)
            {
                var go = new GameObject("CameraRig");
                Rig = go.AddComponent<CameraRig>();

                if (Camera.main != null)
                    Camera.main.transform.SetParent(Rig.transform, true);
            }

            Rig.TurnsChanged += _ => RefreshProjection();
        }

        private void BuildPlatform()
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(transform, false);
            platform.transform.localScale = new Vector3(level.width * cellSize, 0.1f, level.depth * cellSize);
            platform.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            platform.GetComponent<Renderer>().sharedMaterial = Mat(platformMaterial, platformColor, transparent: false);

            _platformGo = platform.gameObject;
            BuildPlatformGrid();
        }

        /// <summary>平台网格线（运行时可见，M4 表现打磨）</summary>
        private void BuildPlatformGrid()
        {
            float halfW = level.width * cellSize * 0.5f;
            float halfD = level.depth * cellSize * 0.5f;

            var points = new List<Vector3>();
            for (int x = 0; x <= level.width; x++)
            {
                float px = x * cellSize - halfW;
                points.Add(new Vector3(px, 0.02f, -halfD));
                points.Add(new Vector3(px, 0.02f, halfD));
            }
            for (int z = 0; z <= level.depth; z++)
            {
                float pz = z * cellSize - halfD;
                points.Add(new Vector3(-halfW, 0.02f, pz));
                points.Add(new Vector3(halfW, 0.02f, pz));
            }

            var go = new GameObject("PlatformGrid");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.startWidth = line.endWidth = 0.02f;
            line.numCapVertices = 0;

            var mat = Mat(gridLineMaterial, gridColor, transparent: true);
            line.material = mat;

            GridLines = line;
        }

        private void BuildWalls()
        {
            float halfW = level.width * cellSize * 0.5f;
            float halfD = level.depth * cellSize * 0.5f;

            // 墙面挂在云台下，随视角一起旋转，始终位于"左后"方向
            var parent = Rig != null ? Rig.WallsRoot : transform;

            int poolU = Mathf.Max(level.width, level.depth);

            _wallLeft = CreateWall(parent, "Wall_Left", new Vector3(-halfW - wallGap, 0f, 0f),
                poolU: poolU, facePositiveX: true);

            _wallFront = CreateWall(parent, "Wall_Front", new Vector3(0f, 0f, -halfD - wallGap),
                poolU: poolU, facePositiveX: false);
        }

        private ProjectionWallView CreateWall(Transform parent, string name, Vector3 localPosition, int poolU, bool facePositiveX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var wall = go.AddComponent<ProjectionWallView>();
            wall.panelMaterial = wallPanelMaterial;
            wall.targetMaterial = wallTargetMaterial;
            wall.currentMaterial = wallCurrentMaterial;
            wall.matchedMaterial = wallMatchedMaterial;
            wall.borderMaterial = wallBorderMaterial;
            wall.Configure(facePositiveX, poolU, level.maxHeight, cellSize);
            return wall;
        }

        private void BuildHover()
        {
            _hover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _hover.name = "HoverIndicator";
            _hover.transform.SetParent(transform, false);
            _hover.transform.localScale = Vector3.one * cellSize * 0.98f;
            // 悬停材质做实例拷贝（透明度会动态变化，不能改共享资产）
            _hoverMat = new Material(Mat(hoverMaterial, new Color(1f, 1f, 1f, 0.2f), transparent: true));
            _hover.GetComponent<Renderer>().sharedMaterial = _hoverMat;
            var hoverCollider = _hover.GetComponent<Collider>();
            if (hoverCollider != null)
            {
                hoverCollider.enabled = false;   // 立即失效，避免挡住拾取射线
                Destroy(hoverCollider);
            }
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

            // 旋转分支：按在平台/方块之外 → 拖动旋转视角
            if (Input.GetMouseButtonDown(0) && !TryPickCell(cam, out _, out _))
            {
                _rotating = true;
                _lastPointerX = Input.mousePosition.x;
                Rig?.BeginDrag();
            }
            else if (_rotating && pressed)
            {
                float dx = Input.mousePosition.x - _lastPointerX;
                _lastPointerX = Input.mousePosition.x;
                Rig?.Drag(dx);
            }

            if (Input.GetMouseButtonUp(0) && _rotating)
            {
                _rotating = false;
                Rig?.EndDrag();
            }

            if (_rotating) { if (_hover != null) _hover.SetActive(false); return; }

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

        /// <summary>刷新 HUD 显示（关卡号 / 最好星级）</summary>
        public void HudRefresh() => hud?.Refresh();

        /// <summary>切换到另一关：清空视图并按新关卡尺寸重建墙体与投影</summary>
        public void LoadLevel(LevelData newLevel)
        {
            if (newLevel == null) return;

            level = newLevel;

            foreach (var kv in _views)
                if (kv.Value != null) RecycleVoxel(kv.Value.gameObject);
            _views.Clear();

            if (_wallFront != null) Destroy(_wallFront.gameObject);
            if (_wallLeft != null) Destroy(_wallLeft.gameObject);

            // 平台与网格线随关卡尺寸重建
            if (_platformGo != null) Destroy(_platformGo);
            if (GridLines != null) Destroy(GridLines.gameObject);
            BuildPlatform();

            Trail?.Clear();
            _grid = level.CreateGrid();
            _solved = false;
            SolvedFeedbackPlayed = false;

            BuildWalls();
            RefreshProjection();
            HudRefresh();
            Debug.Log($"[ShadowCube] 已加载关卡：{level.Id}（{level.width}x{level.depth}x{level.maxHeight}，最优 {level.OptimalCount}）");
        }

        public void ResetLevel()
        {
            if (_grid == null) return;

            _grid.Clear();
            foreach (var kv in _views)
                if (kv.Value != null) RecycleVoxel(kv.Value.gameObject);
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
            var go = AcquireVoxel();
            go.name = $"Voxel_{c.x}_{c.y}_{c.z}";
            go.transform.SetParent(_voxelRoot, false);
            go.transform.localPosition = CellToWorld(c);
            go.transform.localRotation = Quaternion.identity;
            go.GetComponent<Renderer>().sharedMaterial = Mat(voxelMaterial, voxelColor, transparent: false);

            var view = go.GetComponent<VoxelView>();
            if (view == null) view = go.AddComponent<VoxelView>();
            view.Init(Vector3.one * cellSize * 0.92f, spawnDuration);

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
            var go = view.gameObject;
            view.PlayDespawn(despawnDuration, () => RecycleVoxel(go));
        }

        /// <summary>从池中取一个方块（或新建）</summary>
        private GameObject AcquireVoxel()
        {
            while (_voxelPool.Count > 0)
            {
                var pooled = _voxelPool.Pop();
                if (pooled == null) continue;      // fake-null 保护
                pooled.SetActive(true);
                return pooled;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.GetComponent<Renderer>().sharedMaterial = _voxelMat;
            return go;
        }

        private void RecycleVoxel(GameObject go)
        {
            if (go == null) return;

            go.SetActive(false);
            go.transform.SetParent(transform, false);
            _voxelPool.Push(go);
        }

        private Vector3 CellToWorld(Vector3Int c)
            => new(
                (c.x + 0.5f - level.width * 0.5f) * cellSize,
                (c.y + 0.5f) * cellSize,
                (c.z + 0.5f - level.depth * 0.5f) * cellSize);

        // ── 判定 ────────────────────────────────────────────────
        private void RefreshProjection()
        {
            if (_grid == null) return;

            EnsureBuffers();
            ProjectionUtil.ProjectInto(_grid, _xyBuffer, true);    // U = x
            ProjectionUtil.ProjectInto(_grid, _zyBuffer, false);   // U = z

            var cameraRight = Camera.main != null ? Camera.main.transform.right : Vector3.right;

            BackWallUsesXY = BindWall(_wallFront, level.TargetFront, level.TargetLeft, _xyBuffer, _zyBuffer, cameraRight);
            LeftWallUsesXY = BindWall(_wallLeft, level.TargetFront, level.TargetLeft, _xyBuffer, _zyBuffer, cameraRight);
        }

        /// <summary>投影表缓冲区：尺寸变化时重建，平时复用（避免每次操作分配新数组）</summary>
        private void EnsureBuffers()
        {
            if (_xyBuffer == null || _xyBuffer.GetLength(0) != level.width || _xyBuffer.GetLength(1) != level.maxHeight)
                _xyBuffer = new bool[level.width, level.maxHeight];

            if (_zyBuffer == null || _zyBuffer.GetLength(0) != level.depth || _zyBuffer.GetLength(1) != level.maxHeight)
                _zyBuffer = new bool[level.depth, level.maxHeight];
        }

        /// <summary>
        /// 按墙的世界法线选择投影表（旋转 90° 后两面墙会自动互换），
        /// 并按摄像机右方向决定镜像，保证玩家看到的方向直观。
        /// </summary>
        private static bool BindWall(ProjectionWallView wall, bool[,] targetXY, bool[,] targetZY,
            bool[,] currentXY, bool[,] currentZY, Vector3 cameraRight)
        {
            if (wall == null) return false;

            var binding = WallBindingRules.Resolve(wall.transform.forward, cameraRight);
            var target = binding.UseXY ? targetXY : targetZY;
            var current = binding.UseXY ? currentXY : currentZY;

            wall.Show(target, current, binding.FlipU);
            return binding.UseXY;
        }

        private void CheckSolved()
        {
            if (_solved || !LevelJudge.IsSolved(_grid, level)) return;

            _solved = true;
            _wallFront?.SetSolved(true, solvedHighlight);
            _wallLeft?.SetSolved(true, solvedHighlight);

            _sfx.PlaySolved();
            PlaySolvedFeedback();

            progress?.RecordResult(level, StarRating);
            HudRefresh();
            Solved?.Invoke(StarRating);

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

        /// <summary>优先用资产材质（shader 保证进包），未配置时回退运行时创建（真机可能失败）</summary>
        private Material Mat(Material asset, Color color, bool transparent)
        {
            if (asset != null) return asset;

            Debug.LogWarning("[ShadowCube] 未指定材质资产，使用运行时回退（真机可能拿不到 shader，请执行菜单 9 生成材质资产）");
            return CreateMaterial(color, transparent);
        }

        private static Material CreateMaterial(Color color, bool transparent = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (transparent) MaterialUtils.SetTransparent(mat);
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
