using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Resource;
using EF.UI;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace GameLogic.Tests
{
    /// <summary>
    /// Navigator 字符串重载的类型反射缓存行为测试。
    /// 端到端打开/资源加载行为属于 PlayMode 范畴，此处仅覆盖在不触发资源加载之前可断言的查找路径。
    /// </summary>
    [TestFixture]
    public class NavigatorTypeResolutionTests
    {
        private static VisualElement BuildRoot()
        {
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "screen-layer" });
            root.Add(new VisualElement { name = "popup-layer" });
            root.Add(new VisualElement { name = "system-layer" });
            return root;
        }

        [Test]
        public void OpenAsync字符串_类型不存在时抛KeyNotFoundException()
        {
            var navigator = new Navigator(new Shell(BuildRoot()), new ThrowingResourceManager());
            // 已加载程序集中不存在名为 "TotallyNonExistentSpecialView" 的 Screen 类型
            Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await navigator.OpenAsync("TotallyNonExistentSpecialView", null));
        }

        [Test]
        public void OpenAsync字符串_空字符串抛ArgumentException()
        {
            var navigator = new Navigator(new Shell(BuildRoot()), new ThrowingResourceManager());
            Assert.ThrowsAsync<ArgumentException>(async () => await navigator.OpenAsync("", null));
            Assert.ThrowsAsync<ArgumentException>(async () => await navigator.OpenAsync("   ", null));
        }

        [Test]
        public void Navigator构造_resources为null时抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new Navigator(new Shell(BuildRoot()), null));
        }

        [Test]
        public void Navigator构造_shell为null时抛异常()
        {
            Assert.Throws<ArgumentNullException>(() => new Navigator(null, new ThrowingResourceManager()));
        }

        [Test]
        public void Close_无弹窗时静默返回()
        {
            var navigator = new Navigator(new Shell(BuildRoot()), new ThrowingResourceManager());
            Assert.DoesNotThrow(() => navigator.Close());
            Assert.DoesNotThrow(() => navigator.CloseAll());
        }

        // ── 测试辅助：永不真正加载资源的 IResourceManager 实现 ──

        /// <summary>
        /// 仅用于不期望触发资源加载的早期失败路径测试；任何 Load* 调用都会触发断言失败。
        /// </summary>
        private sealed class ThrowingResourceManager : IResourceManager
        {
            public ResourceMode Mode => ResourceMode.EditorSimulate;
            public bool IsInitialized => true;
            public string DefaultPackageName => "DefaultPackage";
            public ResourceModeConfig Configuration => null;

            public UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null)
                => throw new NotSupportedException("Test stub: InitializeAsync 不应被调用");

            public ResourcePackage GetPackage(string packageName)
                => throw new NotSupportedException("Test stub: GetPackage 不应被调用");

            public ResourcePackage GetDefaultPackage()
                => throw new NotSupportedException("Test stub: GetDefaultPackage 不应被调用");

            public UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0) where T : UnityEngine.Object
                => throw new NotSupportedException("Test stub: LoadAssetAsync 不应被调用——此测试期望在到达资源加载前失败");

            public AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : UnityEngine.Object
                => throw new NotSupportedException("Test stub: LoadAssetSync 不应被调用");

            public UniTask<SceneHandle> LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0, Action<float> progress = null)
                => throw new NotSupportedException("Test stub: LoadSceneAsync 不应被调用");

            public void UnloadScene(SceneHandle handle) { }
            public void Release(HandleBase handle) { }
            public void ReleaseAll() { }

            public void Update(float elapseSeconds, float realElapseSeconds) { }
            public void Shutdown() { }
        }
    }
}
