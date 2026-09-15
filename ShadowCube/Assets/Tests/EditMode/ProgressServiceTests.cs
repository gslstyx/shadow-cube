using System.Collections.Generic;
using NUnit.Framework;
using ShadowCube.Core;

namespace ShadowCube.Tests.EditMode
{
    public class ProgressServiceTests
    {
        private static readonly List<string> Ids = new List<string> { "L1", "L2", "L3", "L4" };

        private static ProgressService New(out SaveData data)
        {
            data = new SaveData();
            return new ProgressService(data, Ids);
        }

        [Test]
        public void 初始状态_只有首关解锁()
        {
            var p = New(out _);

            Assert.AreEqual(1, p.UnlockedCount);
            Assert.IsTrue(p.IsUnlocked("L1"));
            Assert.IsFalse(p.IsUnlocked("L2"));
            Assert.IsFalse(p.IsUnlocked("L3"));
            Assert.AreEqual(0, p.TotalStars);
            Assert.AreEqual(20, p.MaxStars);
        }

        [Test]
        public void 通关首关_解锁第二关且不影响第三关()
        {
            var p = New(out _);
            p.RecordResult("L1", 3);

            Assert.AreEqual(2, p.UnlockedCount);
            Assert.IsTrue(p.IsUnlocked("L2"));
            Assert.IsFalse(p.IsUnlocked("L3"), "严格线性：未通关 L2 不应解锁 L3");
        }

        [Test]
        public void 星级取历史最好_低分重玩不降星()
        {
            var p = New(out _);
            p.RecordResult("L1", 5);
            Assert.AreEqual(5, p.GetStars("L1"));

            p.RecordResult("L1", 2);
            Assert.AreEqual(5, p.GetStars("L1"), "低分不应覆盖高分");

            p.RecordResult("L1", 3);
            Assert.AreEqual(5, p.GetStars("L1"));
        }

        [Test]
        public void 高分重玩_正常提升星级()
        {
            var p = New(out _);
            p.RecordResult("L1", 2);
            p.RecordResult("L1", 4);
            Assert.AreEqual(4, p.GetStars("L1"));
        }

        [Test]
        public void 顺序通关_逐步解锁到最后一关()
        {
            var p = New(out _);
            for (int i = 0; i < Ids.Count; i++)
            {
                Assert.IsTrue(p.IsUnlocked(Ids[i]), $"{Ids[i]} 应已解锁");
                p.RecordResult(Ids[i], 3);
            }

            Assert.AreEqual(Ids.Count, p.UnlockedCount);
            Assert.AreEqual(Ids.Count * 3, p.TotalStars);
            Assert.AreEqual("", p.NextLevelId("L4"));
        }

        [Test]
        public void 挑战后面关卡_不重复解锁计数()
        {
            var p = New(out _);
            p.RecordResult("L1", 5);
            p.RecordResult("L2", 5);
            p.RecordResult("L1", 5);

            Assert.AreEqual(3, p.UnlockedCount);
            Assert.AreEqual(10, p.TotalStars);
        }

        [Test]
        public void 记录非目录关卡_被忽略()
        {
            var p = New(out _);
            p.RecordResult("UNKNOWN", 5);

            Assert.AreEqual(1, p.UnlockedCount);
            Assert.AreEqual(0, p.TotalStars);
        }

        [Test]
        public void 非法星级_被忽略()
        {
            var p = New(out _);
            p.RecordResult("L1", 0);
            p.RecordResult("L1", -3);

            Assert.AreEqual(1, p.UnlockedCount);
            Assert.AreEqual(0, p.TotalStars);
        }

        [Test]
        public void NextLevelId_返回下一个关卡()
        {
            var p = New(out _);
            Assert.AreEqual("L2", p.NextLevelId("L1"));
            Assert.AreEqual("L3", p.NextLevelId("L2"));
            Assert.AreEqual("", p.NextLevelId("不存在"));
        }

        [Test]
        public void 跳关记录_不会提前解锁中间关卡()
        {
            var p = New(out _);
            p.RecordResult("L3", 5);   // 理论上玩家到不了这里

            Assert.AreEqual(1, p.UnlockedCount, "L1 未通关，不应解锁任何关卡");
            Assert.AreEqual(5, p.GetStars("L3"), "但记录本身保留");
        }

        [Test]
        public void 最近游玩_优先返回未通关的已解锁关卡()
        {
            var p = New(out _);
            p.RecordResult("L1", 5);
            p.RecordResult("L2", 5);
            p.RecordResult("L1", 5);   // 最后玩 L1

            Assert.AreEqual("L1", p.LastPlayedOrFirst());
        }

        [Test]
        public void 重置_清空全部进度()
        {
            var p = New(out _);
            p.RecordResult("L1", 5);
            p.ResetAll();

            Assert.AreEqual(1, p.UnlockedCount);
            Assert.AreEqual(0, p.TotalStars);
        }
    }
}
