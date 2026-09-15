using NUnit.Framework;
using ShadowCube.Core;

namespace ShadowCube.Tests.EditMode
{
    public class GestureRulesTests
    {
        [Test]
        public void 起手列为空_判定为生成手势()
        {
            Assert.AreEqual(GestureRules.Gesture.Add, GestureRules.Decide(false));
        }

        [Test]
        public void 起手列有方块_判定为消除手势()
        {
            Assert.AreEqual(GestureRules.Gesture.Remove, GestureRules.Decide(true));
        }
    }
}
