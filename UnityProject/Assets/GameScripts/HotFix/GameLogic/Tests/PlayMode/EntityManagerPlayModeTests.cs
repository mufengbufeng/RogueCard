using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Entity;
using EF.ObjectPool;
using GameLogic.Tests.PlayMode.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameLogic.Tests.PlayMode
{
    /// <summary>
    /// EntityManager 在真实 YooAsset prefab + 对象池下的 PlayMode 验证：
    /// 真实 prefab Spawn / Recycle 后命中对象池 / 跨帧批量回收。
    /// </summary>
    public sealed class EntityManagerPlayModeTests : PlayModeTestBase
    {
        private const string EntityAssetName = "GamePlay_CardItem";
        private const string TestGroup = "playmode-entity-test";

        private ObjectPoolManager _poolManager;
        private EntityManager _entityManager;

        /// <summary>
        /// 在基类初始化 ResourceManager 之后，按生产 GameEntry 的顺序组装 ObjectPoolManager + EntityManager。
        /// </summary>
        protected override UniTask OnSetUpAsync()
        {
            _poolManager = new ObjectPoolManager();
            ModuleSystem.Register<IObjectPoolManager>(_poolManager);

            _entityManager = new EntityManager();
            _entityManager.SetObjectPoolManager(_poolManager);
            _entityManager.SetResourceManager(Resource);
            _entityManager.SetEntityHelper(new DefaultEntityHelper());
            ModuleSystem.Register<IEntityManager>(_entityManager);

            // 关闭 AutoRelease 避免测试运行期间被自动回收，干扰断言。
            EntityGroupOptions options = new EntityGroupOptions
            {
                AutoRelease = false
            };
            Assert.IsTrue(_entityManager.AddEntityGroup(TestGroup, options),
                "AddEntityGroup 应当返回 true");

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Spawn 一个实体：返回非 null IEntity，其 Handle 是非 null GameObject。
        /// </summary>
        [UnityTest]
        public IEnumerator Spawn_RealPrefabProducesActiveGameObject() => UniTask.ToCoroutine(async () =>
        {
            int entityId = _entityManager.GenerateEntityId();
            IEntity entity = await _entityManager.ShowEntityAsync(entityId, EntityAssetName, TestGroup);

            Assert.IsNotNull(entity, "ShowEntityAsync 应返回非 null 实体");
            Assert.IsNotNull(entity.Handle, "实体 Handle (GameObject) 不应为 null");
            Assert.AreEqual(1, _entityManager.EntityCount, "活跃实体计数应为 1");
            Assert.IsTrue(_entityManager.HasEntity(entityId));
        });

        /// <summary>
        /// Recycle 后再次 Spawn：池命中，返回同一 IEntity 实例（其 Handle 仍是同一 GameObject）。
        /// </summary>
        [UnityTest]
        public IEnumerator Recycle_SubsequentSpawnReusesPooledEntity() => UniTask.ToCoroutine(async () =>
        {
            int firstId = _entityManager.GenerateEntityId();
            IEntity firstEntity = await _entityManager.ShowEntityAsync(firstId, EntityAssetName, TestGroup);
            GameObject firstHandle = firstEntity.Handle;

            _entityManager.HideEntity(firstId);
            Assert.AreEqual(0, _entityManager.EntityCount, "Hide 后活跃实体计数应为 0");

            int secondId = _entityManager.GenerateEntityId();
            IEntity secondEntity = await _entityManager.ShowEntityAsync(secondId, EntityAssetName, TestGroup);

            Assert.AreSame(firstEntity, secondEntity,
                "对象池应复用同一 IEntity 实例（命中池）");
            Assert.AreSame(firstHandle, secondEntity.Handle,
                "GameObject Handle 应复用，未触发新的 Instantiate");
        });

        /// <summary>
        /// 一帧内 Spawn 5 个，下一帧批量 Hide，再下一帧检查活跃计数为 0。
        /// </summary>
        [UnityTest]
        public IEnumerator BatchRecycle_AcrossFramesEntityCountReturnsToZero() => UniTask.ToCoroutine(async () =>
        {
            const int batchSize = 5;
            List<int> ids = new List<int>(batchSize);
            List<UniTask<IEntity>> spawnTasks = new List<UniTask<IEntity>>(batchSize);

            for (int i = 0; i < batchSize; i++)
            {
                int id = _entityManager.GenerateEntityId();
                ids.Add(id);
                spawnTasks.Add(_entityManager.ShowEntityAsync(id, EntityAssetName, TestGroup));
            }

            await UniTask.WhenAll(spawnTasks);
            Assert.AreEqual(batchSize, _entityManager.EntityCount,
                "5 个并发 Spawn 完成后活跃实体计数应为 5");

            // 下一帧批量回收。
            await FrameDelay(1);
            foreach (int id in ids)
            {
                _entityManager.HideEntity(id);
            }

            // 再下一帧确认活跃计数已清零。
            await FrameDelay(1);
            Assert.AreEqual(0, _entityManager.EntityCount,
                "全部 Hide 后下一帧活跃实体计数应为 0");
        });
    }
}
