using System.Collections.Generic;
using UnityEngine;

namespace ShadowCube.Core
{
    public enum Language
    {
        /// <summary>跟随系统语言</summary>
        System,
        English,
        Chinese
    }

    /// <summary>
    /// 极简本地化（需求 §0.2 中英双语）：UI 一律通过 `Loc.Get(key)` 取文案，禁止硬编码。
    /// 当前是内存表（零依赖、可在测试里强制语言）；M2/M4 接入 Unity Localization 时，
    /// 只需替换 `Lookup` 实现为 `LocalizationSettings.StringDatabase`，调用方无需改动。
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, (string en, string zh)> Table = new()
        {
            ["hud.back"] = ("Back", "返回"),
            ["hud.reset"] = ("Reset", "重置"),
            ["hud.hint"] = ("Hint", "提示"),
            ["hud.best"] = ("Best: {0}/5", "最好：{0}/5"),
            ["hud.bestNone"] = ("Best: -", "最好：-"),
            ["hud.level"] = ("Level {0}-{1}", "第 {0} 章 {1} 关"),
            ["hud.blocks"] = ("{0} / {1}", "{0} / {1}"),

            ["settle.title"] = ("LEVEL CLEARED", "过关"),
            ["settle.next"] = ("Next Level", "下一关"),
            ["settle.retry"] = ("Retry", "重玩"),
            ["settle.back"] = ("Back to Levels", "返回关卡"),

            ["select.locked"] = ("Locked", "未解锁"),
            ["select.stars"] = ("{0}/5", "{0}/5"),

            ["tutorial.step1"] = ("1/5  Hold and drag on the platform to build blocks",
                                  "1/5  在平台上按住并拖动，连续生成方块"),
            ["tutorial.step2"] = ("2/5  Drag from an existing block to remove it",
                                  "2/5  从已有方块上起手拖动，消除方块"),
            ["tutorial.step3"] = ("3/5  Tap < or > to rotate; both walls stay left-behind",
                                  "3/5  点 < 或 > 旋转视角，两面墙始终在你左后方"),
            ["tutorial.step4"] = ("4/5  Match both silhouettes: matched cells turn green",
                                  "4/5  两侧墙上的黑色剪影是目标：让投影与它重合（重合的格子会变绿）"),
            ["tutorial.step5"] = ("5/5  Clear both walls; fewer blocks = higher stars",
                                  "5/5  两面墙全部对上即可过关，方块越少星级越高"),

            ["tutorial.next"] = ("Next", "下一步"),
            ["tutorial.gotIt"] = ("Got it", "知道了"),
            ["tutorial.skip"] = ("Skip", "跳过")
        };

        /// <summary>当前语言（默认跟随系统；测试里可强制英语以保证断言稳定）</summary>
        public static Language Language { get; set; } = Language.System;

        /// <summary>取文案；缺失时返回 key 本身，便于快速发现漏翻</summary>
        public static string Get(string key, params object[] args)
        {
            if (!Table.TryGetValue(key, out var pair)) return key;

            string text = Resolve() == Language.Chinese ? pair.zh : pair.en;
            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }

        public static Language Resolve()
        {
            if (Language != Language.System) return Language;

            var system = Application.systemLanguage;
            return system == SystemLanguage.Chinese
                || system == SystemLanguage.ChineseSimplified
                || system == SystemLanguage.ChineseTraditional
                    ? Language.Chinese : Language.English;
        }
    }
}
