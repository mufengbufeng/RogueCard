using System;
using System.Collections.Generic;
using EF.ObjectPool;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class ObjectPoolManagerTests
    {
        private class ItemA { }
        private class ItemB { }

        private ObjectPoolManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = new ObjectPoolManager();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Shutdown();
        }

        [Test]
        public void CreatePool_ShouldCreateAndReturnTypedPool()
        {
            var pool = _manager.CreatePool("TestA", () => new ItemA());

            Assert.That(pool, Is.Not.Null);
            Assert.That(_manager.PoolCount, Is.EqualTo(1));
        }

        [Test]
        public void GetPool_AfterCreate_ShouldReturnSameInstance()
        {
            var created = _manager.CreatePool("TestA", () => new ItemA());

            var retrieved = _manager.GetPool<ItemA>("TestA");

            Assert.That(retrieved, Is.SameAs(created));
        }

        [Test]
        public void CreatePool_WhenDuplicateKey_ShouldThrowInvalidOperationException()
        {
            _manager.CreatePool("TestA", () => new ItemA());

            Assert.Throws<InvalidOperationException>(() =>
                _manager.CreatePool("TestA", () => new ItemA()));
        }

        [Test]
        public void HasPool_WhenExists_ShouldReturnTrue()
        {
            _manager.CreatePool("TestA", () => new ItemA());

            Assert.That(_manager.HasPool<ItemA>("TestA"), Is.True);
        }

        [Test]
        public void HasPool_WhenNotExists_ShouldReturnFalse()
        {
            Assert.That(_manager.HasPool<ItemA>("NonExistent"), Is.False);
        }

        [Test]
        public void DestroyPool_ShouldRemovePool()
        {
            _manager.CreatePool("TestA", () => new ItemA());

            bool result = _manager.DestroyPool<ItemA>("TestA");

            Assert.That(result, Is.True);
            Assert.That(_manager.HasPool<ItemA>("TestA"), Is.False);
        }

        [Test]
        public void DestroyPool_WhenNotExists_ShouldReturnFalse()
        {
            bool result = _manager.DestroyPool<ItemA>("NonExistent");

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetPool_WhenExists_ShouldReturnTrue()
        {
            var created = _manager.CreatePool("TestA", () => new ItemA());

            bool found = _manager.TryGetPool<ItemA>("TestA", out var pool);

            Assert.That(found, Is.True);
            Assert.That(pool, Is.SameAs(created));
        }

        [Test]
        public void TryGetPool_WhenNotExists_ShouldReturnFalse()
        {
            bool found = _manager.TryGetPool<ItemA>("NonExistent", out var pool);

            Assert.That(found, Is.False);
            Assert.That(pool, Is.Null);
        }

        [Test]
        public void SameName_DifferentTypes_ShouldCoexist()
        {
            var poolA = _manager.CreatePool<ItemA>("Shared", () => new ItemA());
            var poolB = _manager.CreatePool<ItemB>("Shared", () => new ItemB());

            Assert.That(_manager.HasPool<ItemA>("Shared"), Is.True);
            Assert.That(_manager.HasPool<ItemB>("Shared"), Is.True);
            Assert.That(_manager.PoolCount, Is.EqualTo(2));
        }

        [Test]
        public void GetAllPools_ShouldReturnAllCreatedPools()
        {
            _manager.CreatePool("Pool1", () => new ItemA());
            _manager.CreatePool("Pool2", () => new ItemB());

            var all = _manager.GetAllPools();

            Assert.That(all.Count, Is.EqualTo(2));
        }

        [Test]
        public void Shutdown_ShouldClearAllPools()
        {
            _manager.CreatePool("TestA", () => new ItemA());
            _manager.CreatePool("TestB", () => new ItemB());

            _manager.Shutdown();

            Assert.That(_manager.PoolCount, Is.EqualTo(0));
        }
    }
}
