using System;
using System.IO;
using System.Reflection;
using ShadowCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 无头渲染一张游玩画面到 PNG（用于复盘/回归留档，不需要真机也不需要点 Play）。
    /// 用法：
    ///   Unity -batchmode -projectPath ShadowCube -executeMethod ShadowCube.EditorTools.ShadowCubeScreenshot.Capture -logFile /tmp/shot.log
    /// 产物：docs/images/prototype-m1-walls.png
    /// </summary>
    public static class ShadowCubeScreenshot
    {
        private const string ScenePath = "Assets/Scenes/M1_Prototype.unity";
        private const string OutputRelative = "docs/images/prototype-m1-walls.png";
        private const int Width = 1280;
        private const int Height = 720;

        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[ShadowCube][截图] 已打开场景 {scene.name}");

            var controller = UnityEngine.Object.FindObjectOfType<GameController>();
            if (controller == null)
            {
                Debug.LogError("[ShadowCube][截图] 场景缺少 GameController");
                return;
            }

            // 编辑模式下手动触发运行时构建（Awake 里才会建平台/两面墙/投影）
            InvokeAwake(controller);

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[ShadowCube][截图] 场景缺少主相机");
                return;
            }

            var image = Render(cam);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", OutputRelative));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[ShadowCube][截图] 已保存 {path}（{Width}x{Height}）");
        }

        /// <summary>诊断：墙面/投影块的实际渲染状态（朝向、包围盒、材质、可见性）</summary>
        public static void Diagnose()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindObjectOfType<GameController>();
            InvokeAwake(controller);

            var cam = Camera.main;
            Debug.Log($"[诊断] 相机 pos={cam.transform.position} fwd={cam.transform.forward} fov={cam.fieldOfView} ortho={cam.orthographic}");

            foreach (var wall in new[] { controller.WallLeft, controller.WallFront })
            {
                Debug.Log($"[诊断] {wall.name} 世界旋转={wall.transform.rotation.eulerAngles} forward={wall.transform.forward} " +
                          $"localScale={wall.transform.localScale} 目标块={wall.ActiveTargetCount} 玩家块={wall.ActiveCurrentCount} 命中块={wall.ActiveMatchedCount}");

                foreach (Transform child in wall.transform)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    var mat = renderer.sharedMaterial;
                    Debug.Log($"[诊断]   ├ {child.name}: active={child.gameObject.activeSelf} " +
                              $"shader={(mat != null ? mat.shader.name : "null")} color={(mat != null ? mat.color.ToString() : "-")} " +
                              $"queue={(mat != null ? mat.renderQueue : -1)} zwrite={(mat != null ? mat.GetFloat("_ZWrite") : -1)} " +
                              $"cull={(mat != null && mat.HasProperty("_Cull") ? mat.GetFloat("_Cull") : -999)}");
                }

                var firstTarget = wall.GetComponentsInChildren<Renderer>(false);
                foreach (var r in firstTarget)
                {
                    if (r.transform.parent == null) continue;
                    if (!r.gameObject.activeSelf) continue;

                    var toCam = (cam.transform.position - r.bounds.center).normalized;
                    Debug.Log($"[诊断]   ├ {r.transform.parent.name}/{r.name} 世界位置={r.transform.position} " +
                              $"缩放={r.transform.lossyScale} 包围盒={r.bounds.size} " +
                              $"quadForward·toCam={Vector3.Dot(r.transform.forward, toCam):F2} " +
                              $"在视锥={GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), r.bounds)}");
                }
            }

            // 决定性验证：渲染一帧后，把目标块/面板的世界坐标投到屏幕，直接采样那个像素的颜色
            // 注意 WorldToScreenPoint 用的是 Screen 分辨率，诊断时按 Screen 尺寸渲染以保证 1:1 映射
            int w = Mathf.Max(64, Screen.width), h = Mathf.Max(64, Screen.height);
            Debug.Log($"[诊断] Screen={Screen.width}x{Screen.height}");
            var image = Render(cam, w, h);   // 只做像素采样，不落盘（避免临时图混进仓库）

            foreach (var wall in new[] { controller.WallLeft, controller.WallFront })
            {
                Sample(image, cam, $"{wall.name} 面板中心", wall.transform.TransformPoint(0f, 1.5f, 0f));

                foreach (Transform child in wall.transform)
                foreach (Transform quad in child)
                {
                    if (!quad.gameObject.activeSelf) continue;
                    Sample(image, cam, $"{wall.name}/{child.name}/{quad.name}", quad.position);
                    break;
                }
            }

            Sample(image, cam, "平台中心(对照)", new Vector3(0f, 0f, 0f));
            Sample(image, cam, "空背景(对照)", new Vector3(0f, 6f, 0f));

            int panelLogged = 0;
            foreach (var wall in new[] { controller.WallLeft, controller.WallFront })
            {
                var panelFinder = wall.transform.Find("Panel");
                var panelRenderer = panelFinder != null ? panelFinder.GetComponent<Renderer>() : null;
                if (panelRenderer == null) continue;

                panelLogged++;
                Debug.Log($"[诊断] {wall.name}/Panel renderer: enabled={panelRenderer.enabled} " +
                          $"isVisible={panelRenderer.isVisible} forceOff={panelRenderer.forceRenderingOff} " +
                          $"activeInHierarchy={panelRenderer.gameObject.activeInHierarchy}");
            }

            if (panelLogged == 0)
                Debug.Log("[诊断] 未找到墙面面板 Renderer（Panel 缺失）");
        }

        private static void SaveImage(Texture2D image, string relativePath)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[诊断] 已保存 {path}");
        }

        private static void Sample(Texture2D image, Camera cam, string label, Vector3 world)
        {
            // 用视口坐标（0~1），与 Screen 分辨率解耦
            var vp = cam.WorldToViewportPoint(world);
            int px = Mathf.Clamp(Mathf.RoundToInt(vp.x * image.width), 0, image.width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(vp.y * image.height), 0, image.height - 1);
            var color = image.GetPixel(px, py);
            Debug.Log($"[诊断] 像素采样 {label} 世界={world} 像素=({px},{py},z={vp.z:F1}) → RGBA({color.r:F3},{color.g:F3},{color.b:F3},{color.a:F3})");
        }

        private static void InvokeAwake(GameController controller)
        {
            var method = typeof(GameController).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogError("[ShadowCube][截图] 找不到 GameController.Awake");
                return;
            }
            method.Invoke(controller, null);
        }

        private static Texture2D Render(Camera cam) => Render(cam, Width, Height);

        private static Texture2D Render(Camera cam, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };

            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShadowCube][截图] SRP 渲染请求失败，回退 Camera.Render：{e.Message}");
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            rt.Release();

            return tex;
        }
    }
}
