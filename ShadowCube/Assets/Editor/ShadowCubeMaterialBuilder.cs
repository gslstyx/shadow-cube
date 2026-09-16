using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 生成运行时需要的材质资产。
    /// 关键：Shader.Find 在打包后只能找到"被引用过"的 shader，代码里凭空 new Material(UrpLit)
    /// 在真机上会拿到 null（编辑器里却正常）。改为资产引用后，shader 必然进包。
    /// </summary>
    public static class ShadowCubeMaterialBuilder
    {
        public const string Dir = "Assets/Materials";

        [MenuItem("Tools/Shadow Cube/9) 生成运行时材质资产")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            Create("M_Platform", new Color(0.18f, 0.18f, 0.20f), lit: true, transparent: false);
            Create("M_Voxel", new Color(0.85f, 0.87f, 0.90f), lit: true, transparent: false);
            Create("M_GridLine", new Color(0.45f, 0.55f, 0.65f), lit: false, transparent: true);
            Create("M_Hover", new Color(1f, 1f, 1f, 0.20f), lit: false, transparent: true);
            Create("M_WallTarget", new Color(0.35f, 0.35f, 0.35f, 0.25f), lit: false, transparent: true);
            Create("M_WallCurrent", new Color(0.06f, 0.06f, 0.06f), lit: false, transparent: false);
            Create("M_DragTrail", new Color(0.65f, 0.85f, 1f, 0.85f), lit: false, transparent: true);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowCube] 运行时材质资产生成完毕（{Dir}）");
        }

        /// <summary>确保材质存在并返回（重复调用安全）</summary>
        public static Material Ensure(string name, Color color, bool lit, bool transparent)
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string path = $"{Dir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Create(name, color, lit, transparent);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return material;
        }

        private static void Create(string name, Color color, bool lit, bool transparent)
        {
            string path = $"{Dir}/{name}.mat";
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard");

            var material = new Material(shader) { color = color };

            if (!lit)
            {
                // Unlit 无需额外配置；Transparent 需要混合设置
            }

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetOverrideEnabled("_SURFACE_TYPE_TRANSPARENT", true);
            }

            AssetDatabase.CreateAsset(material, path);
        }

        private static void SetOverrideEnabled(this Material mat, string keyword, bool value)
        {
            if (value) mat.EnableKeyword(keyword); else mat.DisableKeyword(keyword);
        }

        [MenuItem("Tools/Shadow Cube/10) 清理孤儿材质资产")]
        public static void CleanOrphans()
        {
            if (!Directory.Exists(Dir)) return;

            foreach (var file in Directory.GetFiles(Dir, "*.mat"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(file);
                if (material != null && !IsReferenced(material))
                    Debug.LogWarning($"[ShadowCube] 未被引用的材质（可手动删除）：{file}");
            }
        }

        private static bool IsReferenced(Material material)
        {
            string guid = AssetDatabase.GetAssetPath(material);
            foreach (var scene in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(scene);
                if (File.ReadAllText(path).Contains(System.IO.Path.GetFileNameWithoutExtension(guid)))
                    return true;
            }
            return false;
        }
    }
}
