using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Debugger;
using EF.Resource;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace EF.Scene
{
    /// <summary>
    /// 场景管理器，提供基础的场景加载功能
    /// </summary>
    public sealed class SceneManager : AEFManager, ISceneManager
    {
        #region 字段

        private IResourceManager _resourceManager;
        private SceneHandle _currentSceneHandle;
        private SceneInfo? _currentScene;
        private readonly SemaphoreSlim _sceneOperationLock = new(1, 1);

        #endregion

        #region 属性

        /// <inheritdoc />
        public SceneInfo? CurrentScene => _currentScene;

        /// <summary>
        /// 资源管理器
        /// </summary>
        public IResourceManager ResourceManager => _resourceManager;

        #endregion

        #region 事件

        /// <inheritdoc />
        public event Action<SceneInfo> OnSceneLoaded;

        /// <inheritdoc />
        public event Action<string> OnSceneUnloaded;

        /// <inheritdoc />
        public event Action<float> OnLoadingProgress;

        /// <inheritdoc />
        public event Action<Exception> OnSceneError;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="resourceManager">资源管理器</param>
        public SceneManager(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化场景管理器
        /// </summary>
        public void Initialize()
        {
            Log.Info("[SceneManager] 场景管理器初始化完成");
        }

        /// <summary>
        /// 实现IEFManager.Shutdown，关闭管理器并释放资源
        /// </summary>
        public override void Shutdown()
        {
            // 清理资源
            if (_currentSceneHandle != null)
            {
                try
                {
                    _currentSceneHandle.UnloadAsync();
                }
                catch (Exception ex)
                {
                    Log.Error($"[SceneManager] 销毁时场景卸载失败：{ex.Message}");
                }
            }

            // 清理事件
            OnSceneLoaded = null;
            OnSceneUnloaded = null;
            OnLoadingProgress = null;
            OnSceneError = null;

            Log.Info("[SceneManager] 场景管理器已关闭");
        }

        #endregion

        #region ISceneManager 实现

        /// <inheritdoc />
        public async UniTask<bool> LoadSceneAsync(string sceneName, LoadSceneMode sceneMode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                var error = new ArgumentException("场景名称不能为空");
                OnSceneError?.Invoke(error);
                return false;
            }

            await _sceneOperationLock.WaitAsync();
            try
            {
                Log.Info($"[SceneManager] 开始加载场景：{sceneName}");

                // 创建场景信息
                var sceneInfo = new SceneInfo(sceneName, sceneName, sceneMode, physicsMode)
                {
                    LoadStartTime = DateTime.Now
                };

                // 异步加载场景 - 这里会等待UniTask完成并获得SceneHandle
                var sceneHandle = await _resourceManager.LoadSceneAsync(sceneName, sceneMode, physicsMode, suspendLoad, priority, (progress) =>
                {
                    OnLoadingProgress?.Invoke(progress);
                });

                // 检查加载结果
                if (sceneHandle.Status == EOperationStatus.Succeed)
                {
                    // 卸载之前的场景
                    if (_currentSceneHandle != null && sceneMode == LoadSceneMode.Single)
                    {
                        SceneHandle previousHandle = _currentSceneHandle;
                        string previousSceneName = _currentScene?.Name ?? "Unknown";
                        await UnloadSceneHandleInternal(previousHandle, previousSceneName);
                    }

                    // 设置新场景
                    _currentSceneHandle = sceneHandle;
                    sceneInfo.LoadEndTime = DateTime.Now;
                    _currentScene = sceneInfo;

                    OnSceneLoaded?.Invoke(sceneInfo);
                    Log.Info($"[SceneManager] 场景 '{sceneName}' 加载成功，耗时：{sceneInfo.LoadDuration.TotalSeconds:F2}秒");

                    return true;
                }
                else
                {
                    var error = new Exception($"场景 '{sceneName}' 加载失败：{sceneHandle.LastError}");
                    OnSceneError?.Invoke(error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SceneManager] 场景加载异常：{ex.Message}");
                OnSceneError?.Invoke(ex);
                return false;
            }
            finally
            {
                _sceneOperationLock.Release();
            }
        }

        /// <inheritdoc />
        public async UniTask<bool> UnloadSceneAsync()
        {
            return await UnloadSceneAsync(null);
        }

        /// <inheritdoc />
        public async UniTask<bool> UnloadSceneAsync(SceneInfo? expectedScene)
        {
            await _sceneOperationLock.WaitAsync();
            try
            {
                SceneHandle targetHandle = _currentSceneHandle;
                if (targetHandle == null || !_currentScene.HasValue)
                {
                    Log.Warning("[SceneManager] 没有场景需要卸载");
                    return false;
                }

                SceneInfo currentScene = _currentScene.Value;
                if (expectedScene.HasValue && !IsSameSceneSnapshot(currentScene, expectedScene.Value))
                {
                    Log.Info(
                        $"[SceneManager] 跳过场景卸载，当前场景已变化。期望:{expectedScene.Value.Name}@{expectedScene.Value.LoadStartTime:O}，当前:{currentScene.Name}@{currentScene.LoadStartTime:O}");
                    return false;
                }

                string targetSceneName = currentScene.Name;
                return await UnloadSceneHandleInternal(targetHandle, targetSceneName);
            }
            finally
            {
                _sceneOperationLock.Release();
            }
        }

        /// <inheritdoc />
        public SceneInfo? GetCurrentScene()
        {
            return _currentScene;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 卸载指定场景句柄的内部实现。
        /// </summary>
        private async UniTask<bool> UnloadSceneHandleInternal(SceneHandle sceneHandle, string sceneName)
        {
            if (sceneHandle == null)
                return false;

            try
            {
                Log.Info($"[SceneManager] 开始卸载场景：{sceneName}");

                var unloadOperation = sceneHandle.UnloadAsync();

                while (!unloadOperation.IsDone)
                {
                    await UniTask.Yield();
                }

                if (unloadOperation.Status == EOperationStatus.Succeed)
                {
                    OnSceneUnloaded?.Invoke(sceneName);

                    // 仅在当前跟踪句柄仍是本次目标时才清空状态，避免并发切场景时误清理新场景记录。
                    if (ReferenceEquals(_currentSceneHandle, sceneHandle))
                    {
                        _currentSceneHandle = null;
                        _currentScene = null;
                    }

                    Log.Info($"[SceneManager] 场景 '{sceneName}' 卸载成功");
                    return true;
                }
                else
                {
                    var error = new Exception($"场景 '{sceneName}' 卸载失败：{unloadOperation.Error}");
                    OnSceneError?.Invoke(error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SceneManager] 场景卸载异常：{ex.Message}");
                OnSceneError?.Invoke(ex);
                return false;
            }
        }

        /// <summary>
        /// 判断当前场景是否与期望快照一致。
        /// </summary>
        private static bool IsSameSceneSnapshot(SceneInfo currentScene, SceneInfo expectedScene)
        {
            return string.Equals(currentScene.Name, expectedScene.Name, StringComparison.Ordinal) &&
                   currentScene.LoadStartTime == expectedScene.LoadStartTime;
        }

        #endregion
    }
}
