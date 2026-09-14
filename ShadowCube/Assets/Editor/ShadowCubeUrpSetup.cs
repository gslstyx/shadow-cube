using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ShadowCubeUrpSetup
{
    public static void Configure()
    {
        if (GraphicsSettings.defaultRenderPipeline != null)
        {
            Debug.Log("[ShadowCube] 已存在渲染管线资产，跳过 URP 配置");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, "Assets/Settings/UniversalRendererData.asset");

        var urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(urpAsset, "Assets/Settings/UniversalRP-Asset.asset");

        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;

        AssetDatabase.SaveAssets();
        Debug.Log("[ShadowCube] URP 配置完成: " + AssetDatabase.GetAssetPath(urpAsset));
    }
}
