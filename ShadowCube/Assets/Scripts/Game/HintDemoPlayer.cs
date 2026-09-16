using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Game
{
    /// <summary>
    /// 提示演示（需求 §2.2-F）：只演示"开头操作"，不给完整解 ——
    /// 1. 相机自动回到推荐视角
    /// 2. 手指动画在平台上拖一行 → 连续生成方块
    /// 3. 再拖一次 → 连续消除
    /// 4. 演示结束即停在引导姿态，控制权交还玩家
    /// 演示期间屏蔽玩法输入；结束后棋盘**恢复到演示前**（只回收自己生成的方块）。
    /// </summary>
    public class HintDemoPlayer : MonoBehaviour
    {
        [Header("演示素材")]
        [Tooltip("手指指示物材质（留空用白色半透明）")]
        public Material handMaterial;

        [Header("节奏")]
        public float startDelay = 0.4f;
        public float moveDuration = 0.32f;
        public float holdDuration = 0.5f;

        public bool IsPlaying { get; private set; }

        private GameObject _hand;
        private Coroutine _routine;
        private readonly List<Vector2Int> _added = new();
        private GameController _controller;

        public void Play(GameController controller)
        {
            if (IsPlaying || controller == null) return;
            _controller = controller;
            _routine = StartCoroutine(Run(controller));
        }

        /// <summary>中断演示并恢复现场（测试 / 关卡切换用）</summary>
        public void Stop()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            Cleanup();
        }

        private IEnumerator Run(GameController controller)
        {
            IsPlaying = true;
            controller.InputBlocked = true;

            // ① 相机自动旋转到推荐视角
            controller.Rig?.SnapToDefaultView();
            yield return new WaitForSeconds(startDelay);

            var cells = PickDemoCells(controller);
            EnsureHand(controller);
            _added.Clear();

            // ② 演示拖拽生成
            for (int i = 0; i < cells.Count; i++)
            {
                yield return MoveHand(controller, cells[i]);
                if (controller.AddAt(cells[i].x, cells[i].y)) _added.Add(cells[i]);
            }
            yield return new WaitForSeconds(holdDuration);

            // ③ 再拖一次：消除
            for (int i = 0; i < cells.Count; i++)
            {
                yield return MoveHand(controller, cells[i]);
                if (_added.Contains(cells[i])) controller.RemoveAt(cells[i].x, cells[i].y);
            }
            yield return new WaitForSeconds(holdDuration);

            Cleanup();
        }

        private void Cleanup()
        {
            // 兜底：任何中途退出都保证演示方块被回收，不污染玩家的解
            if (_controller != null)
            {
                foreach (var cell in _added)
                    _controller.RemoveAt(cell.x, cell.y);
                _added.Clear();
                _controller.InputBlocked = false;
            }

            if (_hand != null) _hand.SetActive(false);
            IsPlaying = false;
        }

        /// <summary>取 2 格演示路径：优先用标准解的前两格，保证落在平台内</summary>
        private static List<Vector2Int> PickDemoCells(GameController controller)
        {
            var result = new List<Vector2Int>();
            var level = controller.level;

            if (level != null && level.solution != null && level.solution.Length >= 2)
            {
                var a = new Vector2Int(level.solution[0].x, level.solution[0].z);
                var b = new Vector2Int(level.solution[1].x, level.solution[1].z);
                if (a != b) { result.Add(a); result.Add(b); }
            }

            if (result.Count < 2)
            {
                result.Clear();
                result.Add(new Vector2Int(0, 0));
                result.Add(new Vector2Int(Mathf.Min(1, (level?.width ?? 2) - 1), 0));
            }

            return result;
        }

        private IEnumerator MoveHand(GameController controller, Vector2Int cell)
        {
            if (_hand == null) yield break;

            var from = _hand.transform.position;
            var to = controller.CellCenter(cell.x, 0, cell.y) + Vector3.up * 0.7f;

            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                _hand.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / moveDuration));
                yield return null;
            }

            _hand.transform.position = to;
        }

        private void EnsureHand(GameController controller)
        {
            if (_hand != null) { _hand.SetActive(true); return; }

            _hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _hand.name = "HintHand";
            _hand.transform.SetParent(controller.transform, false);
            _hand.transform.localScale = Vector3.one * 0.55f;

            var collider = _hand.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }

            var material = handMaterial != null
                ? new Material(handMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.35f, 0.85f, 1f, 0.6f) };
            MaterialUtils.SetTransparent(material);
            _hand.GetComponent<Renderer>().sharedMaterial = material;

            var cells = PickDemoCells(controller);
            _hand.transform.position = controller.CellCenter(cells[0].x, 0, cells[0].y) + Vector3.up * 0.7f;
        }
    }
}
