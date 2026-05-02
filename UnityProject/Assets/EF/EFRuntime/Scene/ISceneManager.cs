using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace EF.Scene
{
    /// <summary>
    /// 场景管理器接口
    /// </summary>
    public interface ISceneManager
    {
        /// <summary>
        /// 当前场景信息
        /// </summary>
        SceneInfo? CurrentScene { get; }
        
        /// <summary>
        /// 场景加载完成事件
        /// </summary>
        event Action<SceneInfo> OnSceneLoaded;
        
        /// <summary>
        /// 场景卸载完成事件
        /// </summary>
        event Action<string> OnSceneUnloaded;
        
        /// <summary>
        /// 场景加载进度事件
        /// </summary>
        event Action<float> OnLoadingProgress;
        
        /// <summary>
        /// 场景错误事件
        /// </summary>
        event Action<Exception> OnSceneError;
        
        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="sceneMode">加载模式</param>
        /// <param name="physicsMode">物理模式</param>
        /// <param name="suspendLoad">是否暂停加载</param>
        /// <param name="priority">加载优先级</param>
        /// <returns>加载是否成功</returns>
        UniTask<bool> LoadSceneAsync(string sceneName, LoadSceneMode sceneMode = LoadSceneMode.Single, 
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0);
            
        /// <summary>
        /// 异步卸载当前场景
        /// </summary>
        /// <returns>卸载是否成功</returns>
        UniTask<bool> UnloadSceneAsync();

        /// <summary>
        /// 异步卸载当前场景（仅当当前场景与期望快照一致时执行）。
        /// </summary>
        /// <param name="expectedScene">期望卸载的场景快照。</param>
        /// <returns>卸载是否成功。</returns>
        UniTask<bool> UnloadSceneAsync(SceneInfo? expectedScene);
        
        /// <summary>
        /// 获取当前场景信息
        /// </summary>
        /// <returns>场景信息，如果没有则返回null</returns>
        SceneInfo? GetCurrentScene();
    }
}
