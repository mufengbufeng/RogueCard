using System.Collections;
using Cysharp.Threading.Tasks;
using EF.Resource;
using GameLogic.Tests.PlayMode.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YooAsset;

namespace GameLogic.Tests.PlayMode
{
    /// <summary>
    /// ResourceManager 在 YooAsset EditorSimulate 模式下的 PlayMode 验证：
    /// 初始化、异步加载、句柄释放、重复初始化幂等、并发加载。
    /// </summary>
    public sealed class ResourceManagerPlayModeTests : PlayModeTestBase
    {
        /// <summary>
        /// 复用项目内稳定的 GameObject prefab；YooAsset Collector 使用 AddressByFileName 规则，
        /// 因此 location 仅为文件名。
        /// </summary>
        private const string PrefabFixtureLocation = "GamePlay_CardItem";

        /// <summary>
        /// 基类完成 InitializeAsync 后即应进入 ready 状态，且模式 / 默认包名符合预期。
        /// </summary>
        [UnityTest]
        public IEnumerator Init_FirstInitializeReachesReadyState() => UniTask.ToCoroutine(async () =>
        {
            Assert.IsTrue(Resource.IsInitialized, "ResourceManager 应已完成初始化");
            Assert.AreEqual(ResourceMode.EditorSimulate, Resource.Mode);
            Assert.AreEqual("DefaultPackage", Resource.DefaultPackageName);
            Assert.IsNotNull(Resource.GetDefaultPackage(), "默认包应可取出");

            await UniTask.CompletedTask;
        });

        /// <summary>
        /// 异步加载已知 prefab：返回非空句柄、AssetObject 可作为 Instantiate 的源。
        /// </summary>
        [UnityTest]
        public IEnumerator Load_AsyncLoadGameObjectPrefabSucceeds() => UniTask.ToCoroutine(async () =>
        {
            AssetHandle handle = await Resource.LoadAssetAsync<GameObject>(PrefabFixtureLocation);

            Assert.IsNotNull(handle, "AssetHandle 不应为 null");
            Assert.AreEqual(EOperationStatus.Succeed, handle.Status,
                $"加载操作未成功：{handle.LastError}");
            Assert.IsNotNull(handle.AssetObject, "AssetHandle.AssetObject 不应为 null");

            GameObject prefab = handle.AssetObject as GameObject;
            Assert.IsNotNull(prefab, "AssetObject 应为 GameObject 类型");

            GameObject instance = Object.Instantiate(prefab, TestRoot.transform);
            Assert.IsNotNull(instance, "Instantiate 应返回有效实例");
            Assert.AreSame(TestRoot.transform, instance.transform.parent);
        });

        /// <summary>
        /// 加载后 Release 单个句柄：ResourceManager 内部追踪集合应不再包含该句柄。
        /// </summary>
        [UnityTest]
        public IEnumerator Release_HandleIsRemovedFromTrackingAfterRelease() => UniTask.ToCoroutine(async () =>
        {
            AssetHandle handle = await Resource.LoadAssetAsync<GameObject>(PrefabFixtureLocation);
            Assert.AreEqual(EOperationStatus.Succeed, handle.Status);

            Resource.Release(handle);
            AssertNoLeakedHandles();
        });

        /// <summary>
        /// 已就绪状态下再次调用 InitializeAsync 应当幂等：状态保持不变，进度回调收到 1f，不抛异常。
        /// 注：使用同步 <see cref="CapturingProgress"/> 而非 <c>System.Progress&lt;float&gt;</c>，避免后者通过
        /// SynchronizationContext 异步派发回调导致断言早于回调执行。
        /// </summary>
        [UnityTest]
        public IEnumerator Init_RepeatedInitializeIsIdempotent() => UniTask.ToCoroutine(async () =>
        {
            Assert.IsTrue(Resource.IsInitialized, "前置条件：基类 SetUp 已完成首次初始化");

            CapturingProgress progressReporter = new CapturingProgress();

            await Resource.InitializeAsync(progress: progressReporter);

            Assert.IsTrue(Resource.IsInitialized, "重复初始化后状态仍为 ready");
            Assert.AreEqual(1f, progressReporter.LastReported, "进度回调应当收到 1f 表示已就绪");
            Assert.AreEqual(ResourceMode.EditorSimulate, Resource.Mode);
        });

        /// <summary>
        /// 同步捕获 IProgress&lt;float&gt; 实现，避免 System.Progress&lt;T&gt; 通过 SynchronizationContext 异步派发的时序问题。
        /// </summary>
        private sealed class CapturingProgress : System.IProgress<float>
        {
            /// <summary>
            /// 最近一次 Report 收到的进度值，未触发时为 NaN。
            /// </summary>
            public float LastReported { get; private set; } = float.NaN;

            /// <summary>
            /// 立即同步记录进度值。
            /// </summary>
            public void Report(float value) => LastReported = value;
        }

        /// <summary>
        /// 同一 location 并发加载两次：两个 Task 都应成功完成；释放任一句柄不会让另一个失效（YooAsset 内部按句柄计数）。
        /// </summary>
        [UnityTest]
        public IEnumerator Load_ConcurrentLoadsForSameLocationBothSucceed() => UniTask.ToCoroutine(async () =>
        {
            UniTask<AssetHandle> taskA = Resource.LoadAssetAsync<GameObject>(PrefabFixtureLocation);
            UniTask<AssetHandle> taskB = Resource.LoadAssetAsync<GameObject>(PrefabFixtureLocation);

            (AssetHandle handleA, AssetHandle handleB) = await UniTask.WhenAll(taskA, taskB);

            Assert.AreEqual(EOperationStatus.Succeed, handleA.Status, "并发请求 A 应当成功");
            Assert.AreEqual(EOperationStatus.Succeed, handleB.Status, "并发请求 B 应当成功");
            Assert.IsNotNull(handleA.AssetObject);
            Assert.IsNotNull(handleB.AssetObject);

            // 释放其一不应影响另一个继续可用。
            Resource.Release(handleA);
            Assert.AreEqual(EOperationStatus.Succeed, handleB.Status,
                "释放 A 后 B 仍应保持 Succeed 状态");
            Assert.IsNotNull(handleB.AssetObject, "释放 A 后 B 仍应能访问 AssetObject");

            Resource.Release(handleB);
            AssertNoLeakedHandles();
        });
    }
}
