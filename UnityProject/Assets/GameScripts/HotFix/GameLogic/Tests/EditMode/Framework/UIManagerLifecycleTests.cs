using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Model;
using EF.Resource;
using EF.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace GameLogic.Tests
{
    /// <summary>
    /// UGUI UIManager 生命周期测试，约束窗口创建、挂载、初始化和释放顺序。
    /// </summary>
    [TestFixture]
    public sealed class UIManagerLifecycleTests
    {
        private readonly List<string> _events = new();
        private GameObject _layerRootObject;
        private GameObject _prefab;
        private UIManager _manager;

        /// <summary>
        /// 初始化每个测试所需的层级根节点、Prefab 和 UIManager。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            Probe.Events = _events;

            _layerRootObject = new GameObject("NormalRoot", typeof(RectTransform));
            _prefab = new GameObject("WindowPrefab", typeof(RectTransform));
            _manager = new UIManager(new ThrowingResourceManager(), new ModelManager());
            _manager.RegisterLayerRoot(UILayer.Normal, _layerRootObject.transform);
            _manager.SetPrefabLoaderForTests(new InMemoryPrefabLoader(_prefab));
        }

        /// <summary>
        /// 清理测试创建的 Unity 对象，避免污染后续测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _manager?.Shutdown();
            UnityEngine.Object.DestroyImmediate(_prefab);
            UnityEngine.Object.DestroyImmediate(_layerRootObject);
            Probe.Events = null;
        }

        /// <summary>
        /// 打开窗口时必须加载 Prefab、挂到目标层级，并按规格执行 Controller/View 生命周期。
        /// </summary>
        [Test]
        public async Task OpenWindowAsync_首次打开窗口_挂载到层级并执行生命周期顺序()
        {
            UIWindowHandle handle = await _manager.OpenWindowAsync<TestView>(
                "TestWindow",
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false,
                userData: "payload");

            Assert.AreEqual("TestView", handle.WindowName);
            Assert.AreEqual(1, _manager.ActiveWindowCount);
            Assert.IsInstanceOf<TestView>(handle.View);
            Assert.IsInstanceOf<TestController>(handle.Controller);
            Assert.AreSame(_layerRootObject.transform, handle.View.transform.parent);
            Assert.AreEqual(UIWindowState.Opened, handle.State);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Controller.Initialize",
                    "Controller.Prepare:payload",
                    "View.Initialize",
                    "View.Bindings",
                    "View.Prepare:payload",
                    "View.Open:payload",
                    "View.Refresh:payload",
                    "Controller.Enter:payload"
                },
                _events);
        }

        /// <summary>
        /// 单泛型打开窗口时，UIManager 必须按 View/Controller 命名约定解析 Controller。
        /// </summary>
        [Test]
        public async Task OpenWindowAsync_单泛型接口_按命名约定创建Controller()
        {
            UIWindowHandle handle = await _manager.OpenWindowAsync<TestView>(
                "TestWindow",
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false,
                userData: "payload");

            Assert.IsInstanceOf<TestController>(handle.Controller);
        }

        /// <summary>
        /// 外部 Controller 工厂接管后，UIManager 必须通过工厂创建约定解析出的 Controller。
        /// </summary>
        [Test]
        public async Task OpenWindowAsync_设置ControllerFactory_通过工厂创建Controller()
        {
            var factory = new RecordingControllerFactory();
            _manager.SetControllerFactory(factory);

            UIWindowHandle handle = await _manager.OpenWindowAsync<TestView>(
                "TestWindow",
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false);

            Assert.AreEqual(typeof(TestController), factory.LastControllerType);
            Assert.AreSame(factory.LastController, handle.Controller);
        }

        /// <summary>
        /// Prefab 根对象没有请求的 View 组件时，UIManager 必须动态添加并继续初始化。
        /// </summary>
        [Test]
        public async Task OpenWindowAsync_Prefab缺少View组件_动态添加View()
        {
            Assert.IsNull(_prefab.GetComponent<TestView>());

            UIWindowHandle handle = await _manager.OpenWindowAsync<TestView, TestController>(
                "TestWindow",
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false);

            Assert.IsNotNull(handle.View);
            Assert.IsInstanceOf<TestView>(handle.View);
            Assert.AreEqual("TestView_Instance", handle.View.gameObject.name);
        }

        /// <summary>
        /// 缺少命名约定匹配的 Controller 时，UIManager 必须给出包含期望类型名的错误。
        /// </summary>
        [Test]
        public void OpenWindowAsync_缺少约定Controller_抛出明确异常()
        {
            var exception = Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            {
                await _manager.OpenWindowAsync<MissingControllerView>(
                    "MissingControllerWindow",
                    UILayer.Normal,
                    cacheOnClose: false,
                    allowMultiple: false);
            });

            StringAssert.Contains("MissingControllerController", exception.Message);
        }

        /// <summary>
        /// 关闭非缓存窗口时必须退出、关闭、释放并销毁实例。
        /// </summary>
        [Test]
        public async Task CloseWindowAsync_非缓存窗口_执行释放生命周期()
        {
            UIWindowHandle handle = await _manager.OpenWindowAsync<TestView, TestController>(
                "TestWindow",
                UILayer.Normal,
                cacheOnClose: false,
                allowMultiple: false);
            _events.Clear();

            await handle.CloseAsync();

            CollectionAssert.AreEqual(
                new[]
                {
                    "Controller.Exit",
                    "View.Close",
                    "View.Release",
                    "Controller.Release",
                    "Controller.Dispose"
                },
                _events);
            Assert.AreEqual(UIWindowState.None, handle.State);
            Assert.AreEqual(0, _manager.ActiveWindowCount);
        }

        /// <summary>
        /// 未注册目标层级且没有 fallback 时，打开窗口必须给出明确错误。
        /// </summary>
        [Test]
        public void OpenWindowAsync_没有层级根节点_抛出明确异常()
        {
            _manager = new UIManager(new ThrowingResourceManager(), new ModelManager());
            _manager.SetPrefabLoaderForTests(new InMemoryPrefabLoader(_prefab));

            var exception = Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            {
                await _manager.OpenWindowAsync<TestView, TestController>(
                    "TestWindow",
                    UILayer.Popup,
                    cacheOnClose: false,
                    allowMultiple: false);
            });

            StringAssert.Contains(nameof(UILayer.Popup), exception.Message);
        }

        private static class Probe
        {
            public static List<string> Events { get; set; }
        }

        private sealed class TestView : UIView
        {
            protected override void OnInitialize() => Probe.Events.Add("View.Initialize");

            protected override void OnBindings() => Probe.Events.Add("View.Bindings");

            protected override UniTask OnPrepareAsync(object userData, CancellationToken cancellationToken)
            {
                Probe.Events.Add($"View.Prepare:{userData}");
                return UniTask.CompletedTask;
            }

            protected override void OnOpen(object userData) => Probe.Events.Add($"View.Open:{userData}");

            protected override void OnRefresh(object userData) => Probe.Events.Add($"View.Refresh:{userData}");

            protected override void OnClose() => Probe.Events.Add("View.Close");

            protected override void OnRelease() => Probe.Events.Add("View.Release");
        }

        private sealed class TestController : UIController
        {
            protected override void OnInitialize() => Probe.Events.Add("Controller.Initialize");

            protected override UniTask OnPrepareAsync(object userData, CancellationToken cancellationToken)
            {
                Probe.Events.Add($"Controller.Prepare:{userData}");
                return UniTask.CompletedTask;
            }

            protected override void OnEnter(object userData) => Probe.Events.Add($"Controller.Enter:{userData}");

            protected override void OnExit() => Probe.Events.Add("Controller.Exit");

            protected override void OnRelease() => Probe.Events.Add("Controller.Release");

            protected override void OnDispose() => Probe.Events.Add("Controller.Dispose");
        }

        private sealed class MissingControllerView : UIView
        {
        }

        private sealed class RecordingControllerFactory : IUIControllerFactory
        {
            public Type LastControllerType { get; private set; }

            public UIController LastController { get; private set; }

            public UIController Create(Type controllerType)
            {
                LastControllerType = controllerType;
                LastController = new TestController();
                return LastController;
            }
        }

        private sealed class InMemoryPrefabLoader : IUIManagerPrefabLoader
        {
            private readonly GameObject _prefab;

            public InMemoryPrefabLoader(GameObject prefab)
            {
                _prefab = prefab;
            }

            public UniTask<UIPrefabLoadResult> LoadAsync(string location, CancellationToken cancellationToken)
            {
                return UniTask.FromResult(new UIPrefabLoadResult(_prefab, null));
            }

            public void Release(UIPrefabLoadResult result)
            {
            }
        }

        private sealed class ThrowingResourceManager : IResourceManager
        {
            public ResourceMode Mode => ResourceMode.EditorSimulate;

            public bool IsInitialized => true;

            public string DefaultPackageName => "DefaultPackage";

            public ResourceModeConfig Configuration => null;

            public UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, System.IProgress<float> progress = null)
            {
                return UniTask.CompletedTask;
            }

            public ResourcePackage GetPackage(string packageName)
            {
                throw new System.NotSupportedException("测试路径不需要资源包。");
            }

            public ResourcePackage GetDefaultPackage()
            {
                throw new System.NotSupportedException("测试路径不需要资源包。");
            }

            public UniTask<AssetHandle> LoadAssetAsync<T>(string location, System.Action<float> progress = null, uint priority = 0)
                where T : UnityEngine.Object
            {
                throw new System.NotSupportedException("测试路径必须使用 IUIManagerPrefabLoader。");
            }

            public AssetHandle LoadAssetSync<T>(string location, uint priority = 0)
                where T : UnityEngine.Object
            {
                throw new System.NotSupportedException("测试路径必须使用 IUIManagerPrefabLoader。");
            }

            public UniTask<SceneHandle> LoadSceneAsync(
                string location,
                LoadSceneMode sceneMode = LoadSceneMode.Single,
                LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
                bool suspendLoad = false,
                uint priority = 0,
                System.Action<float> progress = null)
            {
                throw new System.NotSupportedException("测试路径不需要场景加载。");
            }

            public void UnloadScene(SceneHandle handle)
            {
            }

            public void Release(HandleBase handle)
            {
            }

            public void ReleaseAll()
            {
            }

            public void Shutdown()
            {
            }

            public void Update(float elapseSeconds, float realElapseSeconds)
            {
            }
        }
    }
}
