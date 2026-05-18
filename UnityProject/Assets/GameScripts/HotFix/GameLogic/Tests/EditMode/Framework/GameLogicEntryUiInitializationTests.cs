using EF.Model;
using EF.Resource;
using EF.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using SceneHandle = YooAsset.SceneHandle;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证热更入口能从 Entry/ReferenceCollector 初始化 UGUI 四层根节点。
    /// </summary>
    [TestFixture]
    public sealed class GameLogicEntryUiInitializationTests
    {
        private GameObject _entry;
        private GameObject _uiRoot;
        private UIManager _uiManager;

        /// <summary>
        /// 创建只含 UIRoot 的 Entry 场景契约，模拟当前入口场景。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _entry = new GameObject("Entry");
            _uiRoot = new GameObject("UIRoot", typeof(RectTransform));

            var collector = _entry.AddComponent<ReferenceCollector>();
            collector.data.Add(new ReferenceCollectorData { key = "UIRoot", gameObject = _uiRoot });
            collector.OnAfterDeserialize();

            _uiManager = new UIManager(new ThrowingResourceManager(), new ModelManager());
            GameLogicEntry.SetUIManagerForTests(_uiManager);
        }

        /// <summary>
        /// 清理测试创建的对象和静态入口字段。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            GameLogicEntry.SetUIManagerForTests(null);
            _uiManager?.Shutdown();
            Object.DestroyImmediate(_entry);
            Object.DestroyImmediate(_uiRoot);
        }

        /// <summary>
        /// Entry 只配置 UIRoot 时，入口初始化必须补齐四个 UGUI 层级并设置 fallback。
        /// </summary>
        [Test]
        public void InitializeUI_只有UIRoot引用_创建并注册四层根节点()
        {
            GameLogicEntry.InitializeUIForTests();

            AssertLayerCreated("Background");
            AssertLayerCreated("Normal");
            AssertLayerCreated("Popup");
            AssertLayerCreated("Overlay");
            Assert.IsNotNull(_uiRoot.GetComponent<Canvas>());
            Assert.IsNotNull(_uiRoot.GetComponent<GraphicRaycaster>());
        }

        private void AssertLayerCreated(string layerName)
        {
            Transform child = _uiRoot.transform.Find(layerName);
            Assert.IsNotNull(child, $"缺少 {layerName} 层级根节点");
            Assert.IsInstanceOf<RectTransform>(child);
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

            public ResourcePackage GetPackage(string packageName) => throw new System.NotSupportedException();

            public ResourcePackage GetDefaultPackage() => throw new System.NotSupportedException();

            public UniTask<AssetHandle> LoadAssetAsync<T>(string location, System.Action<float> progress = null, uint priority = 0)
                where T : Object
            {
                throw new System.NotSupportedException();
            }

            public AssetHandle LoadAssetSync<T>(string location, uint priority = 0)
                where T : Object
            {
                throw new System.NotSupportedException();
            }

            public UniTask<SceneHandle> LoadSceneAsync(
                string location,
                LoadSceneMode sceneMode = LoadSceneMode.Single,
                LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
                bool suspendLoad = false,
                uint priority = 0,
                System.Action<float> progress = null)
            {
                throw new System.NotSupportedException();
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
