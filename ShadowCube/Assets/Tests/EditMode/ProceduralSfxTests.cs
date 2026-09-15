using NUnit.Framework;
using ShadowCube.Core;
using ShadowCube.Game;
using UnityEngine;

namespace ShadowCube.Tests.EditMode
{
    public class ProceduralSfxTests
    {
        [Test]
        public void 生成音_波形非空且长度合理()
        {
            var clip = ProceduralSfxPlayer.CreateTone("t", 440f, 880f, 0.08f, 0.5f);

            Assert.IsNotNull(clip);
            Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(44100, clip.frequency);
            Assert.Greater(clip.samples, 0);
            Assert.AreEqual(Mathf.RoundToInt(44100 * 0.08f), clip.samples);

            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            Assert.Greater(peak, 0.05f, "波形不应为静音");
            Assert.LessOrEqual(peak, 1f, "波形不应削波");
        }

        [Test]
        public void 过关音_为三段琶音()
        {
            var clip = ProceduralSfxPlayer.CreateSolvedClip();

            Assert.IsNotNull(clip);
            Assert.AreEqual(Mathf.RoundToInt(44100 * 0.11f) * 3, clip.samples);
        }

        [Test]
        public void 空实现_调用不抛异常()
        {
            ISfxPlayer sfx = new NullSfxPlayer();
            Assert.DoesNotThrow(() => { sfx.PlayAdd(); sfx.PlayRemove(); sfx.PlaySolved(); });
        }
    }
}
