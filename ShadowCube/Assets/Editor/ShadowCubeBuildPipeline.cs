using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 傻瓜式打包管线：GUI 菜单一键打包 / 命令行一键打包。
    /// 配置来自 BuildConfig/build.json（包名等，可入库）与 BuildConfig/keystore.json（签名，不入库）。
    /// </summary>
    public static class ShadowCubeBuildPipeline
    {
        private const string MenuRoot = "Tools/Shadow Cube/";
        private const string ConfigDir = "BuildConfig";
        private const string BuildConfigFile = "build.json";
        private const string KeystoreConfigFile = "keystore.json";
        private const string FallbackScene = "Assets/Scenes/Bootstrap.unity";

        [Serializable]
        private class KeystoreConfig
        {
            public string keystoreName = "";
            public string keystorePass = "";
            public string keyaliasName = "";
            public string keyaliasPass = "";
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        // ── GUI 一键打包入口 ───────────────────────────────────────────────
        [MenuItem(MenuRoot + "0) 应用 BuildConfig 配置（包名/产品名，不构建）")]
        public static void ApplyBuildConfig()
        {
            ApplyCommonSettings(ShadowCubeBuildConfig.Load());
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowCube] 已应用配置，包名 = {PlayerSettings.applicationIdentifier}");
        }

        [MenuItem(MenuRoot + "1) 生成 IDE 工程文件（.sln / .csproj，供 VS Code 调试）")]
        public static void SyncProjectFiles()
        {
            const string vscodePath = "/Applications/Visual Studio Code.app";
            if (Directory.Exists(vscodePath))
                Unity.CodeEditor.CodeEditor.SetExternalScriptEditor(vscodePath);

            var editor = Unity.CodeEditor.CodeEditor.CurrentEditor;
            Debug.Log($"[ShadowCube] CurrentEditor = {editor?.GetType().FullName ?? "null"}");
            editor.SyncAll();
            Debug.Log("[ShadowCube] 已请求生成 IDE 工程文件");
        }

        [MenuItem(MenuRoot + "2) 一键打包 · 联调测试包 (APK / 全架构 / 可调试)")]
        public static void BuildDevelopment()
        {
            Run(Profile.Development);
        }

        [MenuItem(MenuRoot + "3) 一键打包 · 正式包 (AAB / 64位 / 签名)")]
        public static void BuildRelease()
        {
            Run(Profile.Release);
        }

        // ── 命令行入口：Unity -executeMethod ShadowCube.EditorTools.ShadowCubeBuildPipeline.BuildDevelopment ──
        private enum Profile { Development, Release }

        private static void Run(Profile profile)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var config = ShadowCubeBuildConfig.Load();

            ApplyCommonSettings(config);
            if (profile == Profile.Development) ApplyDevelopmentSettings(config);
            else ApplyReleaseSettings(config);

            string version = PlayerSettings.bundleVersion;
            int versionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode + 1);
            PlayerSettings.Android.bundleVersionCode = versionCode;
            AssetDatabase.SaveAssets();

            string outputDir = Path.Combine(ProjectRoot, "Builds", "Android");
            Directory.CreateDirectory(outputDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string fileName = profile == Profile.Development
                ? $"ShadowCube-dev-v{version}+{versionCode}_{stamp}.apk"
                : $"ShadowCube-release-v{version}+{versionCode}_{stamp}.aab";
            string outputPath = Path.Combine(outputDir, fileName);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = CollectScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = profile == Profile.Development ? BuildOptions.Development : BuildOptions.None
            });

            sw.Stop();
            var summary = report.summary;
            Debug.Log($"[ShadowCube] 构建结果: {summary.result} | 版本 {version}({versionCode}) | 耗时 {sw.Elapsed:hh\\:mm\\:ss} | 体积 {summary.totalSize / 1048576} MB | 警告 {summary.totalWarnings} | 错误 {summary.totalErrors}");
            Debug.Log($"[ShadowCube] 产物: {outputPath}");

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"[ShadowCube] 构建失败（{profile}），详见 Editor.log");

            File.WriteAllText(Path.Combine(outputDir, "last-build.txt"), outputPath);
        }

        // ── 设置 ──────────────────────────────────────────────────────────
        private static void ApplyCommonSettings(BuildConfigData config)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.companyName = config.companyName;
            PlayerSettings.productName = config.productName;
            PlayerSettings.applicationIdentifier = config.packageName;

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)config.minSdkVersion;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        private static void ApplyDevelopmentSettings(BuildConfigData config)
        {
            // 联调包：ARM64 + x86_64，覆盖真机（arm）与模拟器（x86_64）
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;

            EditorUserBuildSettings.buildAppBundle = false;   // APK，可直接安装到真机/模拟器
            EditorUserBuildSettings.allowDebugging = true;    // 允许脚本调试
            EditorUserBuildSettings.connectProfiler = true;   // 自动连 Profiler
            PlayerSettings.Android.useCustomKeystore = false; // 用 debug keystore

            SetDefine("SC_DEV", true);

            ApplyHardening(config, enable: false);   // 联调包不做剥离/混淆，保证可调试
        }

        private static void ApplyReleaseSettings(BuildConfigData config)
        {
            // 正式包：64 位架构（ARM64 + x86_64），AAB 上架 Google Play
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            ApplyKeystore(LoadKeystoreConfig());
            SetDefine("SC_DEV", false);

            ApplyHardening(config, enable: true);    // 正式包按 build.json 开启加固/混淆
        }

        /// <summary>代码加固与混淆（可开关）：引擎代码剥离 + 托管代码剥离 + Java R8</summary>
        private static void ApplyHardening(BuildConfigData config, bool enable)
        {
            var obf = config.obfuscation;
            bool active = enable && obf.enabled && obf.level >= 1;

            PlayerSettings.stripEngineCode = active;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,
                active ? ShadowCubeBuildConfig.ParseStrippingLevel(obf.managedStrippingLevel)
                       : ManagedStrippingLevel.Disabled);

            // Java 层压缩/混淆（R8）
            bool minify = active && obf.minifyJava;
            PlayerSettings.Android.minifyDebug = false;
            PlayerSettings.Android.minifyRelease = minify;
            PlayerSettings.Android.minifyWithR8 = minify;

            Debug.Log($"[ShadowCube] 加固/混淆: {(active ? $"开启(level={obf.level}, stripping={obf.managedStrippingLevel}, R8={obf.minifyJava})" : "关闭")}");
        }

        private static void ApplyKeystore(KeystoreConfig ks)
        {
            if (ks == null || string.IsNullOrEmpty(ks.keystoreName))
            {
                Debug.LogWarning("[ShadowCube] 未配置 BuildConfig/keystore.json，正式包将使用 debug keystore（不可上架）");
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks.keystoreName;
            PlayerSettings.Android.keystorePass = ks.keystorePass;
            PlayerSettings.Android.keyaliasName = ks.keyaliasName;
            PlayerSettings.Android.keyaliasPass = ks.keyaliasPass;
        }

        private static void SetDefine(string symbol, bool enable)
        {
            var target = NamedBuildTarget.Android;
            var defines = PlayerSettings.GetScriptingDefineSymbols(target) ?? string.Empty;
            var set = new System.Collections.Generic.HashSet<string>(
                defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            if (enable) set.Add(symbol); else set.Remove(symbol);

            var list = new System.Collections.Generic.List<string>(set);
            list.Sort();
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
        }

        // ── 辅助 ──────────────────────────────────────────────────────────
        private static string[] CollectScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && !string.IsNullOrEmpty(s.path)) scenes.Add(s.path);

            if (scenes.Count == 0 && File.Exists(Path.Combine(ProjectRoot, FallbackScene)))
                scenes.Add(FallbackScene);

            if (scenes.Count == 0)
                throw new Exception("[ShadowCube] 没有可用于构建的场景，请在 Build Settings 中添加场景");

            return scenes.ToArray();
        }


        private static KeystoreConfig LoadKeystoreConfig()
        {
            string path = Path.Combine(ProjectRoot, ConfigDir, KeystoreConfigFile);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<KeystoreConfig>(File.ReadAllText(path));
        }
    }
}
