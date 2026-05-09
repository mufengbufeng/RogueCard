using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Resource;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YooAsset;

namespace GameLogic.Tests.PlayMode.Framework
{
    /// <summary>
    /// PlayMode 测试基类，统一处理模块系统重置、ResourceManager 初始化（EditorSimulate）、
    /// 测试根 GameObject 生命周期等样板逻辑。
    /// 子类通过覆写 <see cref="OnSetUpAsync"/> / <see cref="OnTearDownAsync"/> 注入自己的初始化逻辑。
    /// </summary>
    public abstract class PlayModeTestBase
    {
        /// <summary>
        /// 进入 [UnitySetUp] 时 ModuleSystem 已注册的服务数量；用于跨用例隔离断言。
        /// </summary>
        protected int ModulesAtSetUpStart { get; private set; }

        /// <summary>
        /// 进入 [UnitySetUp] 时 <see cref="YooAssets.Initialized"/> 的快照；用于跨用例隔离断言。
        /// 期望值：false（上一个测试 TearDown 已经 destroy 全部 package + YooAssets.Destroy()）。
        /// </summary>
        protected bool YooAssetsInitializedAtSetUpStart { get; private set; }

        /// <summary>
        /// 测试专用根 GameObject。所有测试期临时创建的对象都应挂到这个根下，便于在 TearDown 一次性销毁。
        /// </summary>
        protected GameObject TestRoot { get; private set; }

        /// <summary>
        /// 由基类 SetUp 注册并初始化的 ResourceManager 实例。
        /// </summary>
        protected ResourceManager Resource { get; private set; }

        /// <summary>
        /// 进入 PlayMode 测试时统一执行的初始化流程。
        /// 1. 抓取入场前 ModuleSystem 注册数（用于隔离断言）。
        /// 2. 强制 ShutdownAll，避免上一个测试残留。
        /// 3. 创建测试根 GameObject（DontDestroyOnLoad）。
        /// 4. 构造 ResourceManager 并使用强制 EditorSimulate 模式的配置完成初始化。
        /// 5. 调用子类 <see cref="OnSetUpAsync"/> 钩子。
        /// </summary>
        [UnitySetUp]
        public IEnumerator UnitySetUp() => UniTask.ToCoroutine(async () =>
        {
            ModulesAtSetUpStart = ModuleSystem.RegisteredServiceCount;
            YooAssetsInitializedAtSetUpStart = YooAssets.Initialized;
            ModuleSystem.ShutdownAll();

            TestRoot = new GameObject($"PlayModeTestRoot[{GetType().Name}]");
            UnityEngine.Object.DontDestroyOnLoad(TestRoot);

            Resource = new ResourceManager();
            ModuleSystem.Register<IResourceManager>(Resource);

            ResourceModeConfig config = CreatePlayModeConfig();
            await Resource.InitializeAsync(config);

            await OnSetUpAsync();
        });

        /// <summary>
        /// 退出 PlayMode 测试时统一执行的清理流程。
        /// 顺序：
        /// 1. 子类 <see cref="OnTearDownAsync"/>。
        /// 2. 把 ResourceManager 从 ModuleSystem 移出（不触发其 Shutdown）— 因为
        ///    <see cref="ResourceManager.Shutdown"/> 内部调 <c>DestroyOperation.WaitForAsyncComplete()</c>，
        ///    YooAsset v2.3.18 的 <c>DestroyOperation</c> 没有重写 <c>InternalWaitForAsyncComplete</c>，会抛
        ///    NotImplementedException。
        /// 3. 手动 ReleaseAll 释放追踪句柄 → 反射读 <c>_packages</c> 集合 → 逐个调
        ///    <c>package.DestroyAsync()</c> 并 <c>await operation.Task</c>（异步等待，不踩同步 wait 的坑）→
        ///    <c>YooAssets.RemovePackage</c>。
        /// 4. <c>YooAssets.Destroy()</c> 反初始化全局静态状态，确保下次 SetUp 干净启动。
        /// 5. <c>ModuleSystem.ShutdownAll()</c> 关闭剩余模块。
        /// 6. 销毁 TestRoot → 推进一帧让 OnDestroy 完成。
        /// </summary>
        [UnityTearDown]
        public IEnumerator UnityTearDown() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                await OnTearDownAsync();
            }
            finally
            {
                await TeardownResourceManagerAsync();

                ModuleSystem.ShutdownAll();

                if (TestRoot != null)
                {
                    UnityEngine.Object.Destroy(TestRoot);
                    TestRoot = null;
                }

                Resource = null;
                await UniTask.Yield();
            }
        });

        /// <summary>
        /// 异步反初始化基类持有的 ResourceManager 与 YooAsset 全局状态，
        /// 绕过 <c>ResourceManager.Shutdown</c> 内部对 <c>DestroyOperation.WaitForAsyncComplete()</c> 的同步等待
        /// （YooAsset v2.3.18 未实现该路径）。
        /// </summary>
        private async UniTask TeardownResourceManagerAsync()
        {
            if (Resource == null)
            {
                return;
            }

            ModuleSystem.Unregister<IResourceManager>(shutdown: false);

            try
            {
                Resource.ReleaseAll();

                FieldInfo packagesField = typeof(ResourceManager).GetField(
                    "_packages",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (packagesField != null
                    && packagesField.GetValue(Resource) is Dictionary<string, ResourcePackage> packages
                    && packages.Count > 0)
                {
                    foreach (ResourcePackage package in packages.Values)
                    {
                        if (package == null)
                        {
                            continue;
                        }

                        DestroyOperation destroyOperation = package.DestroyAsync();
                        await destroyOperation.Task;
                        YooAssets.RemovePackage(package);
                    }

                    packages.Clear();
                }

                if (YooAssets.Initialized)
                {
                    YooAssets.Destroy();
                }
            }
            catch (Exception ex)
            {
                // 容忍 TearDown 异常以保证后续测试可以继续；记录日志便于排查。
                Debug.LogWarning($"[PlayModeTestBase] TearDown 反初始化 ResourceManager 出错：{ex.Message}");
            }
        }

        /// <summary>
        /// 子类可覆写：在基类完成 ResourceManager 初始化之后、测试方法执行之前的额外初始化（注册其他模块等）。
        /// </summary>
        protected virtual UniTask OnSetUpAsync() => UniTask.CompletedTask;

        /// <summary>
        /// 子类可覆写：在基类销毁资源之前的额外清理。基类 TearDown 会保证即便此处抛异常也能完成兜底清理。
        /// </summary>
        protected virtual UniTask OnTearDownAsync() => UniTask.CompletedTask;

        /// <summary>
        /// 在 PlayMode PlayerLoop 上推进若干帧。
        /// </summary>
        /// <param name="frames">要推进的帧数，默认 1。</param>
        protected async UniTask FrameDelay(int frames = 1)
        {
            if (frames <= 0)
            {
                return;
            }

            for (int i = 0; i < frames; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        /// <summary>
        /// 通过 ResourceManager 异步加载一个 fixture 资源。薄封装方便子类引用。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        protected UniTask<AssetHandle> LoadFixtureAsync<T>(string location) where T : UnityEngine.Object
        {
            return Resource.LoadAssetAsync<T>(location);
        }

        /// <summary>
        /// 断言 ResourceManager 内部追踪的资源句柄已全部释放。
        /// 借助反射读取私有 _trackedHandles 集合（生产 API 未暴露此能力，而本断言仅用于测试）。
        /// </summary>
        protected void AssertNoLeakedHandles()
        {
            FieldInfo handlesField = typeof(ResourceManager).GetField(
                "_trackedHandles",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(handlesField, "ResourceManager._trackedHandles 字段未找到，可能已被重构");

            HashSet<HandleBase> handles = (HashSet<HandleBase>)handlesField.GetValue(Resource);
            Assert.AreEqual(0, handles.Count, "ResourceManager 仍有未释放的资源句柄");
        }

        /// <summary>
        /// 基于生产 EFResourceModeConfig 复制一个测试副本，并强制为 EditorSimulate 模式。
        /// 这样既复用了生产 collector / DefaultPackage 配置，又屏蔽了开发者本地切换至 HostPlay 等模式带来的影响。
        /// </summary>
        private static ResourceModeConfig CreatePlayModeConfig()
        {
            ResourceModeConfig production = Resources.Load<ResourceModeConfig>(ResourceModeConfig.DefaultResourcesPath);
            if (production == null)
            {
                throw new InvalidOperationException(
                    $"未找到 Resources/{ResourceModeConfig.DefaultResourcesPath}.asset，PlayMode 测试无法启动 ResourceManager");
            }

            ResourceModeConfig clone = UnityEngine.Object.Instantiate(production);
            clone.hideFlags = HideFlags.DontSave;

            FieldInfo modeField = typeof(ResourceModeConfig).GetField(
                "_mode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (modeField == null)
            {
                throw new InvalidOperationException("ResourceModeConfig._mode 字段未找到，可能已被重构");
            }

            modeField.SetValue(clone, ResourceMode.EditorSimulate);
            return clone;
        }
    }
}
