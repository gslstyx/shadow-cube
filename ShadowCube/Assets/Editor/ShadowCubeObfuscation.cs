using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ShadowCube.EditorTools
{
    /// <summary>
    /// 代码混淆 / 加固。
    /// - level 0：关闭（联调包默认）
    /// - level 1：内置加固（IL2CPP + 引擎代码剥离 + 托管代码剥离 + Java R8）
    /// - level 2/3：在上述基础上调用外部混淆器处理托管 DLL（需配置 externalCommand）
    /// 开关位置：BuildConfig/build.json → obfuscation.enabled
    /// </summary>
    public class ShadowCubeObfuscation : IPostBuildPlayerScriptDLLs, IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        private const string MenuRoot = "Tools/Shadow Cube/";

        [MenuItem(MenuRoot + "3) 切换代码混淆开关 (build.json → obfuscation.enabled)")]
        public static void Toggle()
        {
            ShadowCubeBuildConfig.ToggleObfuscation();
        }

        /// <summary>脚本 DLL 编译完成、IL2CPP 之前调用 —— 名称混淆的最佳时机</summary>
        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.Android) return;

            var config = ShadowCubeBuildConfig.Load();
            if (!config.ObfuscationActive(2)) return;

            string dir = FindScriptAssembliesDir(report);
            if (string.IsNullOrEmpty(dir))
            {
                Debug.LogWarning("[ShadowCube] 未找到脚本程序集目录，跳过名称混淆");
                return;
            }

            string command = config.obfuscation.externalCommand;
            if (string.IsNullOrEmpty(command))
            {
                Debug.LogWarning($"[ShadowCube] level={config.obfuscation.level} 需要外部混淆器，但未配置 obfuscation.externalCommand；本次仅应用内置加固。程序集目录：{dir}");
                return;
            }

            RunExternalObfuscator(command, dir);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.Android) return;

            var obf = ShadowCubeBuildConfig.Load().obfuscation;
            Debug.Log(obf.enabled
                ? $"[ShadowCube] 混淆/加固：开启（level={obf.level}，stripping={obf.managedStrippingLevel}，R8={obf.minifyJava}）"
                : "[ShadowCube] 混淆/加固：关闭");
        }

        // ── 内部实现 ──────────────────────────────────────────────────────
        private static string FindScriptAssembliesDir(BuildReport report)
        {
            var files = report.GetFiles();
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (string.IsNullOrEmpty(file.path)) continue;
                    if (file.path.EndsWith("Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase))
                        return Path.GetDirectoryName(file.path);
                }
            }

            string projectRoot = ShadowCubeBuildConfig.ProjectRoot;
            string[] candidates =
            {
                Path.Combine(projectRoot, "Library", "PlayerScriptAssemblies"),
                Path.Combine(projectRoot, "Temp", "StagingArea", "Data", "Managed")
            };
            foreach (var c in candidates)
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "Assembly-CSharp.dll"))) return c;

            return null;
        }

        private static void RunExternalObfuscator(string command, string assembliesDir)
        {
            string expanded = command.Replace("{DIR}", assembliesDir);
            Debug.Log($"[ShadowCube] 运行外部混淆器：{expanded}");

            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{expanded.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = assembliesDir
            };

            using (var process = Process.Start(psi))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(stdout)) Debug.Log($"[ShadowCube][混淆器] {stdout.Trim()}");
                if (process.ExitCode != 0)
                    throw new Exception($"[ShadowCube] 混淆器执行失败（exit {process.ExitCode}）：{stderr}");

                Debug.Log("[ShadowCube] 外部混淆器执行完成");
            }
        }
    }
}
