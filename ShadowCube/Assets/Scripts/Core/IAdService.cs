using System;
using UnityEngine;

namespace ShadowCube.Core
{
    /// <summary>激励视频观看结果</summary>
    public enum RewardedResult
    {
        /// <summary>完整观看（应发放奖励）</summary>
        Watched,
        /// <summary>中途关闭（不发放奖励）</summary>
        Skipped,
        /// <summary>无可用广告（网络/填充失败）</summary>
        Unavailable
    }

    /// <summary>
    /// 激励视频广告服务（需求 §2.2-G / §6.1）。
    /// M5 才接 AdMob，之前统一用 <see cref="NullAdService"/> 空实现，
    /// 逻辑层按"免费 / 需广告"走，接 SDK 时只替换实现。
    /// </summary>
    public interface IAdService
    {
        /// <summary>当前是否有可用广告（可提前决定是否展示"看广告得提示"入口）</summary>
        bool IsAvailable { get; }

        /// <summary>播放一次激励视频（回调可能在下一帧或几十秒后触发）</summary>
        void ShowRewarded(string placement, Action<RewardedResult> onDone);
    }

    /// <summary>
    /// 空实现：不接入任何 SDK。
    /// 为了让开发/测试期间提示功能可用，直接视为"已完整观看"。
    /// </summary>
    public class NullAdService : IAdService
    {
        public bool IsAvailable => false;

        public void ShowRewarded(string placement, Action<RewardedResult> onDone)
        {
            Debug.Log($"[ShadowCube][广告] 空实现（TODO M5: AdMob 激励视频，placement={placement}）→ 视为已观看");
            onDone?.Invoke(RewardedResult.Watched);
        }
    }
}
