using System;
using System.Collections.Generic;
using EF.Save;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class SaveManagerTests
    {
        /// <summary>
        /// 纯内存 Fake 策略，不依赖文件系统，用于隔离测试 SaveManager 逻辑。
        /// </summary>
        private class FakeSaveStrategy : ISaveStrategy
        {
            public Dictionary<string, object> Store { get; } = new();

            public bool Save<T>(string key, T data)
            {
                Store[key] = data;
                return true;
            }

            public T Load<T>(string key, T defaultValue = default)
            {
                if (Store.TryGetValue(key, out var value))
                {
                    if (value is T typed)
                    {
                        return typed;
                    }
                }

                return defaultValue;
            }

            public bool HasKey(string key)
            {
                return Store.ContainsKey(key);
            }

            public bool Delete(string key)
            {
                return Store.Remove(key);
            }

            public void DeleteAll()
            {
                Store.Clear();
            }
        }

        private SaveManager _manager;
        private FakeSaveStrategy _fakeStrategy;

        [SetUp]
        public void SetUp()
        {
            _manager = new SaveManager();
            _fakeStrategy = new FakeSaveStrategy();

            // 通过反射替换内部策略为 Fake（SaveManager 没有公开注入策略的方法）
            var strategiesField = typeof(SaveManager).GetField("_strategies",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var strategies = (Dictionary<SaveStrategyType, ISaveStrategy>)strategiesField.GetValue(_manager);
            strategies[SaveStrategyType.Json] = _fakeStrategy;
            _manager.SetSaveStrategy(SaveStrategyType.Json);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Shutdown();
        }

        #region Save / Load

        [Test]
        public void Save_WhenValid_ShouldStoreData()
        {
            _manager.Save("key1", 42);

            Assert.That(_fakeStrategy.Store.ContainsKey("key1"), Is.True);
        }

        [Test]
        public void Save_ThenLoad_ShouldReturnSameValue()
        {
            _manager.Save("key1", 42);

            int result = _manager.Load<int>("key1");

            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public void Load_WhenKeyNotExists_ShouldReturnDefault()
        {
            int result = _manager.Load<int>("nonexistent", -1);

            Assert.That(result, Is.EqualTo(-1));
        }

        [Test]
        public void Save_WithStringValue_ShouldPersist()
        {
            _manager.Save("name", "hello");

            Assert.That(_manager.Load<string>("name"), Is.EqualTo("hello"));
        }

        #endregion

        #region HasKey

        [Test]
        public void HasKey_WhenKeyExists_ShouldReturnTrue()
        {
            _manager.Save("key1", 1);

            Assert.That(_manager.HasKey("key1"), Is.True);
        }

        [Test]
        public void HasKey_WhenKeyNotExists_ShouldReturnFalse()
        {
            Assert.That(_manager.HasKey("nonexistent"), Is.False);
        }

        #endregion

        #region Delete

        [Test]
        public void Delete_WhenKeyExists_ShouldRemoveAndReturnTrue()
        {
            _manager.Save("key1", 1);

            bool result = _manager.Delete("key1");

            Assert.That(result, Is.True);
            Assert.That(_manager.HasKey("key1"), Is.False);
        }

        [Test]
        public void Delete_WhenKeyNotExists_ShouldReturnFalse()
        {
            bool result = _manager.Delete("nonexistent");

            Assert.That(result, Is.False);
        }

        #endregion

        #region DeleteAll

        [Test]
        public void DeleteAll_ShouldRemoveAllKeys()
        {
            _manager.Save("key1", 1);
            _manager.Save("key2", 2);
            _manager.Save("key3", 3);

            _manager.DeleteAll();

            Assert.That(_manager.HasKey("key1"), Is.False);
            Assert.That(_manager.HasKey("key2"), Is.False);
            Assert.That(_manager.HasKey("key3"), Is.False);
        }

        #endregion

        #region SetSaveStrategy

        [Test]
        public void SetSaveStrategy_ShouldUpdateCurrentStrategyType()
        {
            _manager.SetSaveStrategy(SaveStrategyType.Json);

            Assert.That(_manager.CurrentStrategyType, Is.EqualTo(SaveStrategyType.Json));
        }

        #endregion

        #region Shutdown

        [Test]
        public void Shutdown_ShouldNotThrow()
        {
            _manager.Save("key1", 1);

            Assert.DoesNotThrow(() => _manager.Shutdown());
        }

        #endregion
    }
}
