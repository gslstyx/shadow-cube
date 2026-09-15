using System.Collections;
using System.IO;
using NUnit.Framework;
using ShadowCube.Core;
using ShadowCube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowCube.Tests.PlayMode
{
    /// <summary>存档落盘与读回（真实文件 IO，使用临时目录避免污染玩家存档）</summary>
    public class ProgressManagerTests
    {
        private string _dir;
        private ProgressManager _manager;
        private LevelCatalog _catalog;

        private LevelData MakeLevel(string id)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId = id;
            level.levelName = id;
            level.width = 3;
            level.depth = 3;
            level.maxHeight = 2;
            level.solution = new[] { new Vector3Int(0, 0, 0) };
            return level;
        }

        [UnitySetUp]
        public IEnumerator 准备临时存档目录与目录资产()
        {
            _dir = Path.Combine(Application.temporaryCachePath, "sc-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);

            _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            _catalog.levels.Add(MakeLevel("L1"));
            _catalog.levels.Add(MakeLevel("L2"));
            _catalog.levels.Add(MakeLevel("L3"));

            var go = new GameObject("ProgressManager");
            _manager = go.AddComponent<ProgressManager>();
            _manager.Initialize(new FileSaveStorage(_dir), _catalog);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator 清理临时文件()
        {
            if (_manager != null) Object.Destroy(_manager.gameObject);
            yield return null;

            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, true); } catch { /* 忽略清理失败 */ }
            }
        }

        [UnityTest]
        public IEnumerator 初始状态_仅首关解锁()
        {
            Assert.AreEqual(1, _manager.UnlockedCount);
            Assert.IsTrue(_manager.IsUnlocked(_catalog.levels[0]));
            Assert.IsFalse(_manager.IsUnlocked(_catalog.levels[1]));
            Assert.AreEqual(0, _manager.TotalStars);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 通关_星级写入磁盘且能读回()
        {
            _manager.RecordResult(_catalog.levels[0], 4);

            string path = Path.Combine(_dir, FileSaveStorage.DefaultFileName);
            Assert.IsTrue(File.Exists(path), "存档文件未生成");
            StringAssert.Contains("L1", File.ReadAllText(path));

            // 用同一目录重建管理器，验证持久化
            var again = new GameObject("ProgressManager2").AddComponent<ProgressManager>();
            again.Initialize(new FileSaveStorage(_dir), _catalog);

            Assert.AreEqual(4, again.GetStars(_catalog.levels[0]));
            Assert.AreEqual(2, again.UnlockedCount, "通关后应解锁第二关");
            Assert.IsTrue(again.IsUnlocked(_catalog.levels[1]));

            Object.Destroy(again.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 低分重玩_不覆盖磁盘上的高分()
        {
            _manager.RecordResult(_catalog.levels[0], 5);
            _manager.RecordResult(_catalog.levels[0], 1);

            var again = new GameObject("ProgressManager3").AddComponent<ProgressManager>();
            again.Initialize(new FileSaveStorage(_dir), _catalog);

            Assert.AreEqual(5, again.GetStars(_catalog.levels[0]));
            Object.Destroy(again.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 存档损坏_回退为空进度且不崩溃()
        {
            File.WriteAllText(Path.Combine(_dir, FileSaveStorage.DefaultFileName), "{{{ 坏掉的存档");

            var again = new GameObject("ProgressManager4").AddComponent<ProgressManager>();
            Assert.DoesNotThrow(() => again.Initialize(new FileSaveStorage(_dir), _catalog));

            Assert.AreEqual(1, again.UnlockedCount, "损坏存档应回退到初始状态");
            Assert.AreEqual(0, again.TotalStars);

            Object.Destroy(again.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 下一关与最近游玩_查询正确()
        {
            _manager.RecordResult(_catalog.levels[0], 3);

            Assert.AreSame(_catalog.levels[1], _manager.NextLevel(_catalog.levels[0]));
            Assert.AreSame(_catalog.levels[0], _manager.LastPlayedOrFirst());
            Assert.IsNull(_manager.NextLevel(_catalog.levels[2]), "最后一关无下一关");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 重置进度_清空并落盘()
        {
            _manager.RecordResult(_catalog.levels[0], 5);
            _manager.ResetProgress();

            Assert.AreEqual(1, _manager.UnlockedCount);
            Assert.AreEqual(0, _manager.TotalStars);

            var again = new GameObject("ProgressManager5").AddComponent<ProgressManager>();
            again.Initialize(new FileSaveStorage(_dir), _catalog);
            Assert.AreEqual(0, again.GetStars(_catalog.levels[0]));
            Object.Destroy(again.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 通关关卡后_结算记录进存档()
        {
            SceneManager.LoadScene("M1_Prototype");
            yield return null;

            var controller = Object.FindObjectOfType<GameController>();
            Assert.IsNotNull(controller, "场景缺少 GameController");

            // 用场景里的真实关卡重建目录，确保 levelId 在目录内
            var catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            catalog.levels.Add(controller.level);
            _manager.Initialize(new FileSaveStorage(_dir), catalog);
            controller.progress = _manager;

            foreach (var v in controller.level.solution)
                Assert.IsTrue(controller.AddAt(v.x, v.z));

            Assert.IsTrue(controller.IsSolved, "标准解未判过关");
            Assert.Greater(_manager.GetStars(controller.level), 0, "过关后未写入进度");
            yield return null;
        }
    }
}
