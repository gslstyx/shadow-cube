using ShadowCube.Game;
using UnityEditor;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 生成/维护运行时需要的材质资产。
    /// 关键：`Shader.Find` 在打包后只能找到"被引用过"的 shader，代码里凭空 new Material(UrpLit)
    /// 在真机上会拿到 null（编辑器里却正常）。改为资产引用后，shader 必然进包。
    /// 已存在的资产**原地更新**（保留 GUID，避免场景引用失效）。
    /// </summary>
    public static class ShadowCubeMaterialBuilder
    {
        public const string Dir = "Assets/Materials";

        private static readonly (string name, Color color, bool lit, bool transparent)[] Definitions =
        {
            ("M_Platform",      new Color(0.18f, 0.18f, 0.20f),            true,  false),
            ("M_Voxel",         new Color(0.85f, 0.87f, 0.90f),            true,  false),
            ("M_GridLine",      new Color(0.45f, 0.55f, 0.65f),            false, true),
            ("M_Hover",         new Color(1f, 1f, 1f, 0.20f),              false, true),
            ("M_WallPanel",     new Color(0.20f, 0.26f, 0.36f, 0.34f),     false, true),   // 半透明发光面板（需求 §2.3）
            ("M_WallTarget",    new Color(0.90f, 0.93f, 0.98f, 0.85f),     false, true),   // 目标投影：实心亮块
            ("M_WallCurrent",   new Color(0.25f, 0.78f, 1.00f, 0.92f),     false, true),   // 玩家投影：青蓝叠加
            ("M_WallMatched",   new Color(0.28f, 0.92f, 0.55f, 1.00f),     false, true),   // 重合：变色反馈
            ("M_WallBorder",    new Color(0.45f, 0.60f, 0.75f, 0.85f),     false, true),   // 墙面边框
            ("M_DragTrail",     new Color(0.65f, 0.85f, 1f, 0.85f),        false, true),
        };

        public static void Build()
        {
            EnsureFolder();

            foreach (var def in Definitions)
                Apply(def.name, def.color, def.lit, def.transparent);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowCube] 运行时材质资产已就绪（{Dir}，共 {Definitions.Length} 个）");
        }

        /// <summary>确保材质存在并返回（重复调用安全）</summary>
        public static Material Ensure(string name, Color color, bool lit, bool transparent)
        {
            EnsureFolder();
            return Apply(name, color, lit, transparent);
        }

        private static Material Apply(string name, Color color, bool lit, bool transparent)
        {
            string path = $"{Dir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;

            if (transparent) MaterialUtils.SetTransparent(material);
            else MaterialUtils.SetOpaque(material);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets", "Materials");
        }
    }
}
