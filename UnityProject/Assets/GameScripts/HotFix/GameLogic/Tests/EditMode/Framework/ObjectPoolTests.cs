using System;
using System.Collections.Generic;
using EF.ObjectPool;
using NUnit.Framework;

namespace GameLogic.Tests.EditMode.Framework
{
    [TestFixture]
    public class ObjectPoolTests
    {
        private class PoolItem
        {
            public int Id { get; set; }
        }

        private ObjectPool<PoolItem> _pool;
        private List<PoolItem> _spawnedItems;
        private List<PoolItem> _recycledItems;
        private List<PoolItem> _destroyedItems;

        [SetUp]
        public void SetUp()
        {
            _spawnedItems = new List<PoolItem>();
            _recycledItems = new List<PoolItem>();
            _destroyedItems = new List<PoolItem>();

            _pool = new ObjectPool<PoolItem>(
                "TestPool",
                () => new PoolItem { Id = _spawnedItems.Count },
                new ObjectPoolOptions(),
                item => _spawnedItems.Add(item),
                item => _recycledItems.Add(item),
                item => _destroyedItems.Add(item));
        }

        #region Spawn

        [Test]
        public void Spawn_WhenPoolEmpty_ShouldCreateViaFactory()
        {
            var item = _pool.Spawn();

            Assert.That(item, Is.Not.Null);
            Assert.That(_pool.TotalCount, Is.EqualTo(1));
            Assert.That(_pool.SpawnedCount, Is.EqualTo(1));
        }

        [Test]
        public void Spawn_ShouldInvokeOnSpawnCallback()
        {
            _pool.Spawn();

            Assert.That(_spawnedItems.Count, Is.EqualTo(1));
        }

        [Test]
        public void Spawn_WhenAvailableObject_ShouldReuse()
        {
            var item1 = _pool.Spawn();
            _pool.Recycle(item1);

            var item2 = _pool.Spawn();

            Assert.That(item2, Is.SameAs(item1));
            Assert.That(_pool.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void Spawn_WithCustomFactory_ShouldUseProvidedFactory()
        {
            var item = _pool.Spawn(() => new PoolItem { Id = 999 });

            Assert.That(item.Id, Is.EqualTo(999));
        }

        [Test]
        public void Spawn_WithNullFactory_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _pool.Spawn(null as Func<PoolItem>));
        }

        [Test]
        public void Spawn_WhenNoDefaultFactory_ShouldThrowInvalidOperationException()
        {
            var pool = new ObjectPool<PoolItem>("NoFactory", null as Func<PoolItem>);
            Assert.Throws<InvalidOperationException>(() => pool.Spawn());
        }

        #endregion

        #region Recycle / TryRecycle

        [Test]
        public void Recycle_WhenValidItem_ShouldReturnToAvailable()
        {
            var item = _pool.Spawn();

            _pool.Recycle(item);

            Assert.That(_pool.AvailableCount, Is.EqualTo(1));
            Assert.That(_pool.SpawnedCount, Is.EqualTo(0));
        }

        [Test]
        public void Recycle_ShouldInvokeOnRecycleCallback()
        {
            var item = _pool.Spawn();

            _pool.Recycle(item);

            Assert.That(_recycledItems.Count, Is.EqualTo(1));
            Assert.That(_recycledItems[0], Is.SameAs(item));
        }

        [Test]
        public void Recycle_WhenItemNotInPool_ShouldThrowInvalidOperationException()
        {
            var externalItem = new PoolItem();

            Assert.Throws<InvalidOperationException>(() => _pool.Recycle(externalItem));
        }

        [Test]
        public void Recycle_WhenAlreadyRecycled_ShouldThrowInvalidOperationException()
        {
            var item = _pool.Spawn();
            _pool.Recycle(item);

            Assert.Throws<InvalidOperationException>(() => _pool.Recycle(item));
        }

        [Test]
        public void TryRecycle_WhenValidItem_ShouldReturnTrue()
        {
            var item = _pool.Spawn();

            bool result = _pool.TryRecycle(item);

            Assert.That(result, Is.True);
        }

        [Test]
        public void TryRecycle_WhenItemNotInPool_ShouldReturnFalse()
        {
            bool result = _pool.TryRecycle(new PoolItem());

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryRecycle_WhenAlreadyRecycled_ShouldReturnFalse()
        {
            var item = _pool.Spawn();
            _pool.TryRecycle(item);

            bool result = _pool.TryRecycle(item);

            Assert.That(result, Is.False);
        }

        #endregion

        #region Prewarm

        [Test]
        public void Prewarm_ShouldCreateObjectsInAvailableQueue()
        {
            _pool.Prewarm(5);

            Assert.That(_pool.TotalCount, Is.EqualTo(5));
            Assert.That(_pool.AvailableCount, Is.EqualTo(5));
        }

        [Test]
        public void Prewarm_WithZeroOrNegative_ShouldDoNothing()
        {
            _pool.Prewarm(0);
            _pool.Prewarm(-1);

            Assert.That(_pool.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public void Prewarm_WhenNoFactory_ShouldThrowInvalidOperationException()
        {
            var pool = new ObjectPool<PoolItem>("NoFactory", null as Func<PoolItem>);

            Assert.Throws<InvalidOperationException>(() => pool.Prewarm(3));
        }

        #endregion

        #region AutoRelease

        [Test]
        public void Update_WhenObjectExpired_ShouldReleaseIt()
        {
            var options = new ObjectPoolOptions
            {
                AutoRelease = true,
                ExpireTime = 5f,
                AutoReleaseInterval = 1f
            };
            var pool = new ObjectPool<PoolItem>("AutoRelease", () => new PoolItem(), options, onDestroy: item => _destroyedItems.Add(item));

            var item = pool.Spawn();
            pool.Recycle(item);

            // 推进时间超过过期时间 + 检测间隔
            pool.Update(7f, 7f);

            Assert.That(_destroyedItems, Does.Contain(item));
            Assert.That(pool.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public void Update_WhenObjectNotExpired_ShouldNotRelease()
        {
            var options = new ObjectPoolOptions
            {
                AutoRelease = true,
                ExpireTime = 60f,
                AutoReleaseInterval = 1f
            };
            var pool = new ObjectPool<PoolItem>("NoExpire", () => new PoolItem(), options);

            var item = pool.Spawn();
            pool.Recycle(item);

            pool.Update(3f, 3f);

            Assert.That(pool.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_WhenAutoReleaseDisabled_ShouldNotRelease()
        {
            var options = new ObjectPoolOptions { AutoRelease = false, ExpireTime = 1f };
            var pool = new ObjectPool<PoolItem>("NoAuto", () => new PoolItem(), options);

            var item = pool.Spawn();
            pool.Recycle(item);

            pool.Update(100f, 100f);

            Assert.That(pool.TotalCount, Is.EqualTo(1));
        }

        #endregion

        #region SetLocked

        [Test]
        public void SetLocked_WhenLocked_ShouldPreventAutoRelease()
        {
            var destroyed = new List<PoolItem>();
            var options = new ObjectPoolOptions
            {
                AutoRelease = true,
                ExpireTime = 5f,
                AutoReleaseInterval = 1f
            };
            var pool = new ObjectPool<PoolItem>(
                "Locked", () => new PoolItem(), options,
                onDestroy: item => destroyed.Add(item));

            var item = pool.Spawn();
            pool.Recycle(item);
            pool.SetLocked(item, true);

            pool.Update(7f, 7f);

            Assert.That(destroyed.Count, Is.EqualTo(0));
            Assert.That(pool.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void SetLocked_WhenItemNotInPool_ShouldThrowInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _pool.SetLocked(new PoolItem(), true));
        }

        #endregion

        #region Contains

        [Test]
        public void Contains_WhenItemInPool_ShouldReturnTrue()
        {
            var item = _pool.Spawn();

            Assert.That(_pool.Contains(item), Is.True);
        }

        [Test]
        public void Contains_WhenItemNotInPool_ShouldReturnFalse()
        {
            Assert.That(_pool.Contains(new PoolItem()), Is.False);
        }

        [Test]
        public void Contains_WhenNull_ShouldReturnFalse()
        {
            Assert.That(_pool.Contains(null), Is.False);
        }

        #endregion

        #region Register

        [Test]
        public void Register_WhenValidItem_ShouldAddToPool()
        {
            var item = new PoolItem();

            _pool.Register(item);

            Assert.That(_pool.TotalCount, Is.EqualTo(1));
            Assert.That(_pool.AvailableCount, Is.EqualTo(1));
        }

        [Test]
        public void Register_WithSpawnedTrue_ShouldMarkAsInUse()
        {
            var item = new PoolItem();

            _pool.Register(item, spawned: true);

            Assert.That(_pool.SpawnedCount, Is.EqualTo(1));
            Assert.That(_spawnedItems, Does.Contain(item));
        }

        #endregion

        #region Clear / Shutdown

        [Test]
        public void Clear_ShouldReleaseAllObjects()
        {
            _pool.Spawn();
            _pool.Spawn();
            _pool.Spawn();

            _pool.Clear();

            Assert.That(_pool.TotalCount, Is.EqualTo(0));
            Assert.That(_destroyedItems.Count, Is.EqualTo(3));
        }

        [Test]
        public void Shutdown_ShouldClearPool()
        {
            _pool.Spawn();

            _pool.Shutdown();

            Assert.That(_pool.TotalCount, Is.EqualTo(0));
        }

        #endregion

        #region ReleaseAll

        [Test]
        public void ReleaseAll_ShouldReleaseOnlyAvailableObjects()
        {
            var item1 = _pool.Spawn();
            var item2 = _pool.Spawn();
            _pool.Recycle(item1);

            _pool.ReleaseAll();

            // item1 已回收（可用），应被释放；item2 仍在使用中，不应被释放
            Assert.That(_pool.TotalCount, Is.EqualTo(1));
            Assert.That(_pool.Contains(item2), Is.True);
        }

        #endregion

        #region Properties

        [Test]
        public void Name_ShouldReturnConfiguredName()
        {
            Assert.That(_pool.Name, Is.EqualTo("TestPool"));
        }

        [Test]
        public void ObjectType_ShouldReturnPoolType()
        {
            Assert.That(_pool.ObjectType, Is.EqualTo(typeof(PoolItem)));
        }

        [Test]
        public void AllowMultiSpawn_ShouldDefaultFalse()
        {
            Assert.That(_pool.AllowMultiSpawn, Is.False);
        }

        [Test]
        public void PoolName_WhenEmpty_ShouldThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new ObjectPool<PoolItem>("", () => new PoolItem()));
        }

        #endregion
    }
}
