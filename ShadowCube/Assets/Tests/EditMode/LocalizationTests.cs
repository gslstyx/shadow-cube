using NUnit.Framework;
using ShadowCube.Core;

namespace ShadowCube.Tests.EditMode
{
    /// <summary>本地化：文案走 key，中英可切换，缺失返回 key（§0.2）</summary>
    public class LocalizationTests
    {
        private Language _original;

        [SetUp]
        public void 保存语言() => _original = Loc.Language;

        [TearDown]
        public void 还原语言() => Loc.Language = _original;

        [Test]
        public void 英文_取到英文文案()
        {
            Loc.Language = Language.English;
            Assert.AreEqual("Back", Loc.Get("hud.back"));
            Assert.AreEqual("LEVEL CLEARED", Loc.Get("settle.title"));
        }

        [Test]
        public void 中文_取到中文文案()
        {
            Loc.Language = Language.Chinese;
            Assert.AreEqual("返回", Loc.Get("hud.back"));
            Assert.AreEqual("过关", Loc.Get("settle.title"));
        }

        [Test]
        public void 带参数_格式化正确()
        {
            Loc.Language = Language.English;
            Assert.AreEqual("Best: 4/5", Loc.Get("hud.best", 4));

            Loc.Language = Language.Chinese;
            Assert.AreEqual("最好：4/5", Loc.Get("hud.best", 4));
        }

        [Test]
        public void 缺失key_返回key本身()
        {
            Loc.Language = Language.English;
            Assert.AreEqual("not.exist", Loc.Get("not.exist"));
        }

        [Test]
        public void 引导五步_中英都有文案()
        {
            var keys = new[] { "tutorial.step1", "tutorial.step2", "tutorial.step3", "tutorial.step4", "tutorial.step5" };

            Loc.Language = Language.English;
            foreach (var key in keys)
                Assert.AreNotEqual(key, Loc.Get(key), $"{key} 缺少英文文案");

            Loc.Language = Language.Chinese;
            foreach (var key in keys)
                Assert.AreNotEqual(key, Loc.Get(key), $"{key} 缺少中文文案");
        }
    }
}
