using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ShadowCube.EditorTools
{
    [Serializable]
    public class ObfuscationSettings
    {
        /// <summary>总开关：false 时不做任何加固/混淆</summary>
        public bool enabled = true;
        /// <summary>0=关闭 1=基础加固(内置) 2=托管DLL名称混淆(需外部工具) 3=第三方商业混淆器</summary>
        public int level = 1;
        /// <summary>Low / Medium / High / Minimal / Disabled</summary>
        public string managedStrippingLevel = "Medium";
        /// <summary>Java 层是否启用 R8 压缩与混淆</summary>
        public bool minifyJava = true;
        /// <summary>外部混淆器命令，占位符 {DIR} = 脚本程序集目录；留空则跳过</summary>
        public string externalCommand = "";
        /// <summary>仅混淆这些程序集（逗号分隔），默认游戏代码</summary>
        public string targetAssemblies = "Assembly-CSharp.dll,Assembly-CSharp-firstpass.dll";
    }

    [Serializable]
    public class BuildConfigData
    {
        public string packageName = "com.gslstyx.shadowcube";
        public string companyName = "gslstyx";
        public string productName = "Shadow Cube";
        public int minSdkVersion = 26;
        public ObfuscationSettings obfuscation = new ObfuscationSettings();

        public bool ObfuscationActive(int minLevel)
            => obfuscation != null && obfuscation.enabled && obfuscation.level >= minLevel;
    }

    public static class ShadowCubeBuildConfig
    {
        public const string ConfigDirName = "BuildConfig";
        public const string BuildConfigFile = "build.json";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string ConfigPath => Path.Combine(ProjectRoot, ConfigDirName, BuildConfigFile);

        public static BuildConfigData Load()
        {
            if (!File.Exists(ConfigPath)) return new BuildConfigData();
            var data = JsonUtility.FromJson<BuildConfigData>(File.ReadAllText(ConfigPath));
            if (data.obfuscation == null) data.obfuscation = new ObfuscationSettings();
            return data;
        }

        public static void Save(BuildConfigData data)
            => File.WriteAllText(ConfigPath, JsonUtility.ToJson(data, true));

        /// <summary>菜单/脚本用的开关切换</summary>
        public static bool ToggleObfuscation()
        {
            var data = Load();
            data.obfuscation.enabled = !data.obfuscation.enabled;
            Save(data);
            Debug.Log($"[ShadowCube] 代码混淆开关：{(data.obfuscation.enabled ? "开启" : "关闭")}（level={data.obfuscation.level}）");
            return data.obfuscation.enabled;
        }

        public static ManagedStrippingLevel ParseStrippingLevel(string name)
        {
            switch ((name ?? string.Empty).Trim().ToLower())
            {
                case "disabled": return ManagedStrippingLevel.Disabled;
                case "low": return ManagedStrippingLevel.Low;
                case "medium": return ManagedStrippingLevel.Medium;
                case "high": return ManagedStrippingLevel.High;
                case "minimal": return ManagedStrippingLevel.Minimal;
                default: return ManagedStrippingLevel.Medium;
            }
        }
    }
}
