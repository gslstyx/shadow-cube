using NUnit.Framework;
using ShadowCube.Core;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>
    /// PlayMode 全局准备：文案已本地化（默认跟随系统语言），
    /// 为避免"同一套断言在不同系统语言下结果不同"，测试统一固定为英语。
    /// </summary>
    [SetUpFixture]
    public class PlayModeSetup
    {
        [OneTimeSetUp]
        public void 固定测试语言()
        {
            Loc.Language = Language.English;
        }
    }
}
