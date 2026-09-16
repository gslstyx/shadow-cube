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
            ("M_WallPanel",     new Color(0.86f, 0.87f, 0.89f, 0.62f),     false, true),   // 墙面：灰白（全局设定）
            ("M_WallTarget",    new Color(0.17f, 0.18f, 0.20f, 0.95f),     false, true),   // 目标投影：灰黑（全局设定）
            ("M_WallCurrent",   new Color(0.25f, 0.78f, 1.00f, 0.92f),     false, true),   // 超出：蓝（全局设定）
            ("M_WallMatched",   new Color(0.28f, 0.92f, 0.55f, 1.00f),     false, true),   // 正确：绿（全局设定）
            ("M_WallBorder",    new Color(0.34f, 0.36f, 0.39f, 0.85f),     false, true),   // 墙面边框
            ("M_DragTrail",     new Color(0.65f, 0.85f, 1f, 0.85f),        false, true),
            ("M_Hand",          new Color(0.35f, 0.85f, 1f, 0.60f),        false, true),   // 提示演示的手指指示物
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
