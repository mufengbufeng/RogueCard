using System;
using Cysharp.Threading.Tasks;
using EF.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace EF.Resource
{
    /// <summary>
    /// 资源管理器对外暴露的能力定义。
    /// </summary>
    public interface IResourceManager : IEFManager
    {
        /// <summary>
        /// 当前运行模式。
        /// </summary>
        ResourceMode Mode { get; }

        /// <summary>
        /// 是否已经完成初始化。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 默认资源包名称。
        /// </summary>
        string DefaultPackageName { get; }

        /// <summary>
        /// 当前使用的配置资产。
        /// </summary>
        ResourceModeConfig Configuration { get; }

        /// <summary>
        /// 初始化资源模块。
        /// </summary>
        /// <param name="overrideConfig">手动指定的配置，传入 null 时将按默认路径加载。</param>
        /// <param name="progress">初始化进度回调。</param>
        UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null);

        /// <summary>
        /// 获取指定名称的资源包。
        /// </summary>
        ResourcePackage GetPackage(string packageName);

        /// <summary>
        /// 获取默认资源包。
        /// </summary>
        ResourcePackage GetDefaultPackage();

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="progress">进度回调。</param>
        /// <param name="priority">加载优先级。</param>
        UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0) where T : UnityEngine.Object;

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源定位地址。</param>
        /// <param name="priority">加载优先级。</param>
        AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        UniTask<SceneHandle> LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0, Action<float> progress = null);

        /// <summary>
        /// 卸载场景。
        /// </summary>
        void UnloadScene(SceneHandle handle);

        /// <summary>
        /// 释放句柄引用。
        /// </summary>
        void Release(HandleBase handle);

        /// <summary>
        /// 释放所有由资源管理器追踪的句柄。
        /// </summary>
        void ReleaseAll();
    }
}
