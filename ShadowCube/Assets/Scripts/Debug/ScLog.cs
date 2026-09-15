using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ShadowCube.Diagnostics
{
    /// <summary>
    /// 调试日志封装：仅在定义 SC_DEV 时编译（联调包自动带，正式包自动剔除，零运行时成本）。
    /// 用法：ScLog.Info($"体素数={count}");
    /// </summary>
    public static class ScLog
    {
        [Conditional("SC_DEV")]
        public static void Info(string message) => Debug.Log($"[SC] {message}");

        [Conditional("SC_DEV")]
        public static void Warn(string message) => Debug.LogWarning($"[SC] {message}");

        [Conditional("SC_DEV")]
        public static void Error(string message) => Debug.LogError($"[SC] {message}");

        /// <summary>打印二维占用表（体素投影 / 网格），■=占用 ·=空</summary>
        [Conditional("SC_DEV")]
        public static void DumpGrid(bool[,] grid, string title = "grid")
        {
            if (grid == null) { Debug.Log($"[SC] {title}: (null)"); return; }

            var sb = new StringBuilder();
            sb.AppendLine($"[SC] {title} ({grid.GetLength(0)}x{grid.GetLength(1)}):");
            for (int y = grid.GetLength(1) - 1; y >= 0; y--)
            {
                sb.Append("  ");
                for (int x = 0; x < grid.GetLength(0); x++)
                    sb.Append(grid[x, y] ? '■' : '·');
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }
    }
}
