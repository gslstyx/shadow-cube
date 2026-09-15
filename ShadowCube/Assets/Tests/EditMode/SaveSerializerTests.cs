using NUnit.Framework;
using ShadowCube.Core;

namespace ShadowCube.Tests.EditMode
{
    public class SaveSerializerTests
    {
        [Test]
        public void 序列化往返_数据一致()
        {
            var data = new SaveData { lastPlayedLevelId = "L2", tutorialDone = true };
            data.levels.Add(new LevelProgress("L1", 5));
            data.levels.Add(new LevelProgress("L2", 3));

            var restored = SaveSerializer.FromJson(SaveSerializer.ToJson(data));

            Assert.AreEqual(1, restored.version);
            Assert.AreEqual("L2", restored.lastPlayedLevelId);
            Assert.IsTrue(restored.tutorialDone);
            Assert.AreEqual(2, restored.levels.Count);
            Assert.AreEqual(5, new ProgressService(restored, new[] { "L1", "L2" }).GetStars("L1"));
        }

        [Test]
        public void 空字符串_返回空存档()
        {
            var data = SaveSerializer.FromJson("");
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.levels.Count);
        }

        [Test]
        public void null_返回空存档()
        {
            Assert.IsNotNull(SaveSerializer.FromJson(null));
        }

        [Test]
        public void 损坏的JSON_安全回退不抛异常()
        {
            SaveData data = null;
            Assert.DoesNotThrow(() => data = SaveSerializer.FromJson("{ 这不是合法 json "));
            Assert.IsNotNull(data);
            Assert.AreEqual(0, data.levels.Count);
        }

        [Test]
        public void 未来版本存档_回退为空存档()
        {
            var json = "{\"version\":999,\"levels\":[{\"levelId\":\"L1\",\"stars\":5}]}";
            var data = SaveSerializer.FromJson(json);

            Assert.AreEqual(0, data.levels.Count, "未来版本不应被破坏性解读");
        }

        [Test]
        public void 脏数据_被清理()
        {
            var json = "{\"version\":1,\"levels\":[{\"levelId\":\"\",\"stars\":5},{\"levelId\":\"L1\",\"stars\":-2},{\"levelId\":\"L2\",\"stars\":4}]}";
            var data = SaveSerializer.FromJson(json);

            Assert.AreEqual(1, data.levels.Count);
            Assert.AreEqual("L2", data.levels[0].levelId);
        }
    }
}
