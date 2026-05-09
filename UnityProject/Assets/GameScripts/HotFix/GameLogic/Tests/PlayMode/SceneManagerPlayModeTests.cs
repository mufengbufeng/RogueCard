using System.Collections;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Scene;
using GameLogic.Tests.PlayMode.Framework;
using NUnit.Framework;
using UnityEngine.TestTools;
using LoadSceneMode = UnityEngine.SceneManagement.LoadSceneMode;
using UnityScene = UnityEngine.SceneManagement.Scene;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace GameLogic.Tests.PlayMode
{
    /// <summary>
    /// SceneManager 在 YooAsset EditorSimulate 下的 PlayMode 验证。
    /// 使用 LoadSceneMode.Additive 避免卸载 PlayMode Test Runner 的 InitTestScene。
    /// </summary>
    public sealed class SceneManagerPlayModeTests : PlayModeTestBase
    {
        /// <summary>
        /// Game.unity 在 collector 下的 location key，AddressByFileName 规则。
        /// </summary>
        private const string SceneFixtureName = "Game";

        private SceneManager _sceneManager;

        /// <summary>
        /// 在基类完成 ResourceManager 初始化后注册并初始化 SceneManager。
        /// </summary>
        protected override UniTask OnSetUpAsync()
        {
            _sceneManager = new SceneManager(Resource);
            // 注：EF.Scene.ISceneManager 当前未继承 IEFManager（EF 既有不一致），无法直接注册接口；
            // 这里以具体类型 SceneManager 注册，仍能保证 ModuleSystem.ShutdownAll 时调到 SceneManager.Shutdown。
            ModuleSystem.Register<SceneManager>(_sceneManager);
            _sceneManager.Initialize();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步加载场景应返回成功，Unity SceneManager 可枚举到目标场景且其 isLoaded == true。
        /// </summary>
        [UnityTest]
        public IEnumerator Load_AdditiveSceneIsRegisteredInUnity() => UniTask.ToCoroutine(async () =>
        {
            bool success = await _sceneManager.LoadSceneAsync(SceneFixtureName, LoadSceneMode.Additive);

            Assert.IsTrue(success, "SceneManager.LoadSceneAsync 应返回 true");
            Assert.IsTrue(_sceneManager.CurrentScene.HasValue, "CurrentScene 应当被赋值");
            Assert.AreEqual(SceneFixtureName, _sceneManager.CurrentScene.Value.Name);

            UnityScene scene = UnitySceneManager.GetSceneByName(SceneFixtureName);
            Assert.IsTrue(scene.IsValid(), "Unity SceneManager 中应找到目标场景");
            Assert.IsTrue(scene.isLoaded, "目标场景的 isLoaded 应为 true");

            // 收尾，避免污染下一用例。
            await _sceneManager.UnloadSceneAsync();
        });

        /// <summary>
        /// 卸载应清理 CurrentScene 并使 Unity SceneManager 中的对应场景不再加载。
        /// </summary>
        [UnityTest]
        public IEnumerator Unload_ClearsCurrentSceneAndUnityState() => UniTask.ToCoroutine(async () =>
        {
            Assert.IsTrue(await _sceneManager.LoadSceneAsync(SceneFixtureName, LoadSceneMode.Additive));
            Assert.IsTrue(_sceneManager.CurrentScene.HasValue);

            bool unloaded = await _sceneManager.UnloadSceneAsync();
            Assert.IsTrue(unloaded, "UnloadSceneAsync 应返回 true");
            Assert.IsFalse(_sceneManager.CurrentScene.HasValue, "卸载后 CurrentScene 应为 null");

            UnityScene scene = UnitySceneManager.GetSceneByName(SceneFixtureName);
            Assert.IsFalse(scene.IsValid() && scene.isLoaded,
                "卸载后 Unity SceneManager 中不应仍有 isLoaded 的同名场景");
        });

        /// <summary>
        /// 连续 3 次"加载 → 卸载"不应在 Unity 侧留下 isLoaded 的残场景，且 CurrentScene 每轮都干净重置。
        /// 注：当前生产实现 SceneManager.UnloadSceneAsync 直接调 sceneHandle.UnloadAsync()
        /// 而未经过 ResourceManager.UnloadScene 路径，因此 ResourceManager._trackedHandles 内会残留
        /// 已 Unload 的 SceneHandle 引用，要等 Shutdown 时统一 ReleaseAll 清理；
        /// 这是已知的内部账本不一致问题，不在本变更修复范围内。本用例只断言 Unity 侧契约。
        /// </summary>
        [UnityTest]
        public IEnumerator Load_Unload_RepeatedThreeTimesUnityStateRemainsClean() => UniTask.ToCoroutine(async () =>
        {
            for (int round = 0; round < 3; round++)
            {
                bool loaded = await _sceneManager.LoadSceneAsync(SceneFixtureName, LoadSceneMode.Additive);
                Assert.IsTrue(loaded, $"第 {round + 1} 轮加载应当成功");

                bool unloaded = await _sceneManager.UnloadSceneAsync();
                Assert.IsTrue(unloaded, $"第 {round + 1} 轮卸载应当成功");
                Assert.IsFalse(_sceneManager.CurrentScene.HasValue,
                    $"第 {round + 1} 轮卸载后 CurrentScene 应为 null");

                UnityScene scene = UnitySceneManager.GetSceneByName(SceneFixtureName);
                Assert.IsFalse(scene.IsValid() && scene.isLoaded,
                    $"第 {round + 1} 轮卸载后 Unity SceneManager 中不应仍有 isLoaded 的同名场景");
            }
        });
    }
}
