using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowCube.Game
{
    /// <summary>
    /// URP 材质透明/不透明切换的统一封装。
    /// 注意：只改 `_SrcBlend` 等数值而**不设关键字**时，URP 可能仍按不透明渲染（alpha 被忽略），
    /// 半透明效果会静默失效；这里把关键字、混合、队列一次配齐。
    /// </summary>
    public static class MaterialUtils
    {
        public static void SetTransparent(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat("_Surface", 1f);          // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);            // 0 = Alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            // 双面渲染：半透明面片（墙面投影、悬停高亮、拖拽尾迹）朝向万一时也不会被背面剔除
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        public static void SetOpaque(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f);

            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");

            mat.renderQueue = (int)RenderQueue.Geometry;
        }
    }
}
