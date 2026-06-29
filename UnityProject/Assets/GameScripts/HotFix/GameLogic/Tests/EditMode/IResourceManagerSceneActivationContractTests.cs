using System;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EF.Resource;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace GameLogic.Tests
{
    /// <summary>
    /// 验证场景加载激活控制参数与 YooAsset 3.0 的 allowSceneActivation 语义一致。
    /// </summary>
    [TestFixture]
    public sealed class IResourceManagerSceneActivationContractTests
    {
        /// <summary>
        /// 默认调用 IResourceManager.LoadSceneAsync 时应立即允许场景激活。
        /// </summary>
        [Test]
        public async Task IResourceManager默认加载场景_允许立即激活()
        {
            var resourceManager = new SceneActivationSpyResourceManager();

            await ((IResourceManager)resourceManager).LoadSceneAsync("Assets/Scenes/Test.unity");

            Assert.AreEqual(true, resourceManager.LastAllowSceneActivation);
        }

        /// <summary>
        /// 显式传入 allowSceneActivation:false 时应透传延迟激活语义。
        /// </summary>
        [Test]
        public async Task IResourceManager显式延迟激活_透传False()
        {
            var resourceManager = new SceneActivationSpyResourceManager();

            await ((IResourceManager)resourceManager).LoadSceneAsync("Assets/Scenes/Test.unity", allowSceneActivation: false);

            Assert.AreEqual(false, resourceManager.LastAllowSceneActivation);
        }

        /// <summary>
        /// 公开接口不得再暴露旧的 suspendLoad 参数名。
        /// </summary>
        [Test]
        public void LoadSceneAsync公开契约_暴露AllowSceneActivation且不暴露SuspendLoad()
        {
            AssertLoadSceneAsyncParameterContract(typeof(IResourceManager));
            AssertLoadSceneAsyncParameterContract(typeof(EF.Scene.ISceneManager));
        }

        private static void AssertLoadSceneAsyncParameterContract(Type managerType)
        {
            var method = managerType.GetMethods()
                .Single(methodInfo => methodInfo.Name == nameof(IResourceManager.LoadSceneAsync));
            var parameters = method.GetParameters();
            var activationParameter = parameters.SingleOrDefault(parameter => parameter.Name == "allowSceneActivation");

            Assert.IsNotNull(activationParameter, $"{managerType.Name}.LoadSceneAsync 必须声明 allowSceneActivation 参数。");
            Assert.AreEqual(typeof(bool), activationParameter.ParameterType);
            Assert.IsTrue(activationParameter.HasDefaultValue);
            Assert.AreEqual(true, activationParameter.DefaultValue);
            Assert.IsFalse(parameters.Any(parameter => parameter.Name == "suspendLoad"),
                $"{managerType.Name}.LoadSceneAsync 不应再暴露 suspendLoad 参数。");
        }

        private sealed class SceneActivationSpyResourceManager : IResourceManager
        {
            public ResourceMode Mode => ResourceMode.EditorSimulate;

            public bool IsInitialized => true;

            public string DefaultPackageName => "DefaultPackage";

            public ResourceModeConfig Configuration => null;

            public bool? LastAllowSceneActivation { get; private set; }

            public UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null)
            {
                return UniTask.CompletedTask;
            }

            public ResourcePackage GetPackage(string packageName) => throw new NotSupportedException();

            public ResourcePackage GetDefaultPackage() => throw new NotSupportedException();

            public UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0)
                where T : UnityEngine.Object
            {
                throw new NotSupportedException();
            }

            public AssetHandle LoadAssetSync<T>(string location, uint priority = 0)
                where T : UnityEngine.Object
            {
                throw new NotSupportedException();
            }

            public UniTask<SceneHandle> LoadSceneAsync(
                string location,
                LoadSceneMode sceneMode = LoadSceneMode.Single,
                LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
                bool allowSceneActivation = true,
                uint priority = 0,
                Action<float> progress = null)
            {
                LastAllowSceneActivation = allowSceneActivation;
                progress?.Invoke(1f);
                return UniTask.FromResult<SceneHandle>(null);
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
