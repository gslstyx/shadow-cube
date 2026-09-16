using UnityEngine;
using ShadowCube.Core;

namespace ShadowCube.Game
{
    /// <summary>
    /// 提示服务（需求 §2.2-F / §2.2-G）：
    /// - **第 1~N 关免费**（`freeHintLevels`，默认 5），不限次数
    /// - **第 N+1 关起**：看一次激励视频换一次提示
    /// - 演示内容只讲"开头操作"（旋转镜头 + 拖拽生成/消除），不给完整解
    /// - 广告 SDK 未接入前由 <see cref="NullAdService"/> 空实现放行（M5 替换实现即可）
    /// </summary>
    public class HintService : MonoBehaviour
    {
        [Tooltip("留空则自动查找场景中的 GameController")]
        public GameController controller;

        [Tooltip("演示播放器（留空则自动查找）")]
        public HintDemoPlayer demo;

        [Tooltip("第 1~N 关提示免费（需求：1~5 关免费，第 6 关起看激励视频）")]
        public int freeHintLevels = 5;

        /// <summary>广告服务（测试可注入替身）</summary>
        public IAdService Ads { get; set; } = new NullAdService();

        /// <summary>本局使用过的提示次数（统计 / HUD 展示用）</summary>
        public int UsedCount { get; private set; }

        /// <summary>最近一次请求的结果，便于 UI 反馈与测试断言</summary>
        public HintRequestOutcome LastOutcome { get; private set; } = HintRequestOutcome.None;

        public bool IsPlaying => demo != null && demo.IsPlaying;

        /// <summary>当前关卡序号（1 起）</summary>
        public int CurrentLevelIndex => controller != null && controller.level != null
            ? controller.level.levelIndex : 0;

        /// <summary>当前关卡是否免费（需求 §2.2-G）</summary>
        public bool IsFreeForCurrentLevel => CurrentLevelIndex > 0 && CurrentLevelIndex <= freeHintLevels;

        private void Awake()
        {
            if (controller == null) controller = FindObjectOfType<GameController>();
            if (demo == null) demo = FindObjectOfType<HintDemoPlayer>();
        }

        /// <summary>点击 💡 提示</summary>
        public void RequestHint()
        {
            if (controller == null || demo == null || IsPlaying)
            {
                LastOutcome = HintRequestOutcome.Busy;
                return;
            }

            if (IsFreeForCurrentLevel)
            {
                Play(HintRequestOutcome.PlayedFree);
                return;
            }

            LastOutcome = HintRequestOutcome.WaitingAd;
            Ads.ShowRewarded("hint", result =>
            {
                if (result == RewardedResult.Watched) Play(HintRequestOutcome.PlayedAfterAd);
                else
                {
                    LastOutcome = HintRequestOutcome.AdDeclined;
                    Debug.Log($"[ShadowCube][提示] 未获得奖励（{result}），不播放演示");
                }
            });
        }

        private void Play(HintRequestOutcome outcome)
        {
            UsedCount++;
            LastOutcome = outcome;
            demo.Play(controller);
        }
    }

    public enum HintRequestOutcome
    {
        None,
        /// <summary>免费关，直接播放</summary>
        PlayedFree,
        /// <summary>看完广告后播放</summary>
        PlayedAfterAd,
        /// <summary>等待广告结果</summary>
        WaitingAd,
        /// <summary>广告未看完，未播放</summary>
        AdDeclined,
        /// <summary>正在播放或缺少依赖</summary>
        Busy
    }
}
