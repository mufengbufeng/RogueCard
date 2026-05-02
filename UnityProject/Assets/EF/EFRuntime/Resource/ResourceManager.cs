using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Debugger;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using SceneHandle = YooAsset.SceneHandle;

namespace EF.Resource
{
    /// <summary>
    /// 资源管理器，封装 YooAssets 的初始化与资源加载流程。
    /// </summary>
    public sealed class ResourceManager : AEFManager, IResourceManager
    {
        #region 字段

        private readonly Dictionary<string, ResourcePackage> _packages = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<HandleBase> _trackedHandles = new();

        private ResourceModeConfig _config;
        private string _defaultPackageName;
        private bool _isInitialized;

        #endregion

        #region 属性

        /// <inheritdoc />
        public ResourceMode Mode => _config != null ? _config.Mode : ResourceMode.EditorSimulate;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public string DefaultPackageName => _defaultPackageName;

        /// <inheritdoc />
        public ResourceModeConfig Configuration => _config;

        #endregion

        #region 初始化

        /// <inheritdoc />
        public async UniTask InitializeAsync(ResourceModeConfig overrideConfig = null, IProgress<float> progress = null)
        {
            if (_isInitialized)
            {
                progress?.Report(1f);
                return;
            }
            YooAssets.Initialize();
            _config = overrideConfig ?? LoadDefaultConfig();
            if (_config == null)
            {
                throw new InvalidOperationException($"未找到资源配置文件，请确认 Resources/{ResourceModeConfig.DefaultResourcesPath}.asset 是否存在");
            }

            if (_config.Packages == null || _config.Packages.Count == 0)
            {
                throw new InvalidOperationException("资源配置未包含任何包裹，请至少配置一个包裹信息");
            }

            EnsureYooAssetsInitialized();

            _packages.Clear();
            _defaultPackageName = null;

            IReadOnlyList<ResourcePackageEntry> entries = _config.Packages;
            int total = entries.Count;
            for (int index = 0; index < total; index++)
            {
                ResourcePackageEntry entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                ResourcePackage package = YooAssets.TryGetPackage(entry.PackageName) ?? YooAssets.CreatePackage(entry.PackageName);
                if (entry.IsDefault || string.IsNullOrEmpty(_defaultPackageName))
                {
                    _defaultPackageName = package.PackageName;
                    YooAssets.SetDefaultPackage(package);
                }

                InitializeParameters parameters = CreateInitializeParameters(entry);
                parameters.BundleLoadingMaxConcurrency = _config.BundleLoadingMaxConcurrency;


                InitializationOperation operation = package.InitializeAsync(parameters);
                await operation;
                if (operation.Status != EOperationStatus.Succeed)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 初始化失败：{operation.Error}");
                }

                Log.Info($"开始初始化资源包裹 {entry.PackageName}，运行模式 {Mode}...");
                // await MonitorInitializationAsync(operation, progress, index, total);
                var requestVersionOperation = RequestPackageVersion(package);
                await requestVersionOperation;
                if (requestVersionOperation.Status != EOperationStatus.Succeed)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 请求版本失败：{requestVersionOperation.Error}");
                }

                var updateManifestOperation = UpdatePackageManifest(package, requestVersionOperation.PackageVersion);
                await updateManifestOperation;
                if (updateManifestOperation.Status != EOperationStatus.Succeed)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 更新清单失败：{updateManifestOperation.Error}");
                }

                await Download(package);

                if (_packages.TryGetValue(package.PackageName, out ResourcePackage existing))
                {
                    DestroyOperation destroyOperation = existing.DestroyAsync();
                    destroyOperation.WaitForAsyncComplete();
                    YooAssets.RemovePackage(existing);
                }

                _packages[package.PackageName] = package;


            }

            if (string.IsNullOrEmpty(_defaultPackageName))
            {
                ResourcePackageEntry fallbackEntry = _config.GetDefaultPackage();
                _defaultPackageName = fallbackEntry?.PackageName;
                if (!string.IsNullOrEmpty(_defaultPackageName) && _packages.TryGetValue(_defaultPackageName, out ResourcePackage fallbackPackage))
                {
                    YooAssets.SetDefaultPackage(fallbackPackage);
                }
            }

            _isInitialized = true;
            progress?.Report(1f);
        }

        private RequestPackageVersionOperation RequestPackageVersion(ResourcePackage package)
        {
            RequestPackageVersionOperation operation = package.RequestPackageVersionAsync();
            return operation;
        }


        private UpdatePackageManifestOperation UpdatePackageManifest(ResourcePackage package, string packageVersion)
        {
            var operation = package.UpdatePackageManifestAsync(packageVersion);
            return operation;

        }

        async UniTask Download(ResourcePackage package)
        {
            int downloadingMaxNum = 10;
            int failedTryAgain = 3;
            var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);

            //没有需要下载的资源
            if (downloader.TotalDownloadCount == 0)
            {
                return;
            }

            //需要下载的文件总数和总大小
            int totalDownloadCount = downloader.TotalDownloadCount;
            long totalDownloadBytes = downloader.TotalDownloadBytes;

            //注册回调方法
            downloader.DownloadFinishCallback = OnDownloadFinishFunction; //当下载器结束（无论成功或失败）
            downloader.DownloadErrorCallback = OnDownloadErrorFunction; //当下载器发生错误
            downloader.DownloadUpdateCallback = OnDownloadUpdateFunction; //当下载进度发生变化
            downloader.DownloadFileBeginCallback = OnDownloadFileBeginFunction; //当开始下载某个文件

            //开启下载
            downloader.BeginDownload();
            await downloader;

            //检测下载结果
            if (downloader.Status == EOperationStatus.Succeed)
            {
                //下载成功
            }
            else
            {
                Log.Error($"资源包裹 {package.PackageName} 下载失败：{downloader.Error}");
                //下载失败
            }
        }

        private void OnDownloadFileBeginFunction(DownloadFileData data)
        {
            //开始下载某个文件
            Log.Info($"资源包裹 {data.PackageName} 开始下载文件：{data.FileName}");
        }


        private void OnDownloadUpdateFunction(DownloadUpdateData data)
        {
            //下载进度变化
            Log.Info($"资源包裹 {data.PackageName} 下载进度：{data.Progress:P2}");
        }


        private void OnDownloadErrorFunction(DownloadErrorData data)
        {
            //下载器发生错误
            Log.Error($"资源包裹 {data.PackageName} 下载错误，文件名称：{data.FileName}，错误信息：{data.ErrorInfo}");
        }


        private void OnDownloadFinishFunction(DownloaderFinishData data)
        {
            //下载器结束（无论成功或失败）
            if (data.Succeed)
            {
                Log.Info($"资源包裹 {data.PackageName} 下载完成");
            }
            else
            {
                Log.Error($"资源包裹 {data.PackageName} 下载失败");
            }
        }




        #endregion

        #region 包裹管理


        /// <inheritdoc />
        public ResourcePackage GetPackage(string packageName)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("包裹名称不能为空", nameof(packageName));
            }

            if (_packages.TryGetValue(packageName, out ResourcePackage package))
            {
                return package;
            }

            throw new KeyNotFoundException($"未找到名称为 {packageName} 的资源包，请检查配置");
        }

        /// <inheritdoc />
        public ResourcePackage GetDefaultPackage()
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(_defaultPackageName))
            {
                throw new InvalidOperationException("未设置默认资源包，请在配置中勾选默认包裹");
            }

            return GetPackage(_defaultPackageName);
        }

        #endregion

        #region 资源加载

        /// <inheritdoc />
        public async UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0) where T : UnityEngine.Object
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("资源定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            AssetHandle handle = package.LoadAssetAsync<T>(location, priority);

            if (progress != null)
            {
                while (!handle.IsDone)
                {
                    progress(handle.Progress);
                    await UniTask.Yield();
                }
            }

            await handle.Task;
            HandleFailureIfNeed(handle, location, "加载资源");
            RegisterHandle(handle);
            progress?.Invoke(1f);
            return handle;
        }

        /// <inheritdoc />
        public AssetHandle LoadAssetSync<T>(string location, uint priority = 0) where T : UnityEngine.Object
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("资源定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            AssetHandle handle = package.LoadAssetSync<T>(location);
            HandleFailureIfNeed(handle, location, "同步加载资源");
            RegisterHandle(handle);
            return handle;
        }

        #endregion

        #region 场景管理

        public async UniTask<SceneHandle> LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0, Action<float> progress = null)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("场景定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            SceneHandle handle = package.LoadSceneAsync(location, sceneMode, physicsMode, suspendLoad, priority);

            if (progress != null)
            {
                while (!handle.IsDone)
                {
                    progress(handle.Progress);
                    await UniTask.Yield();
                }
            }

            await handle.Task;
            HandleFailureIfNeed(handle, location, "加载场景");
            RegisterHandle(handle);
            progress?.Invoke(1f);
            return handle;
        }

        /// <inheritdoc />
        public void UnloadScene(SceneHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            _trackedHandles.Remove(handle);
            UnloadSceneOperation operation = handle.UnloadAsync();
            operation.WaitForAsyncComplete();
        }

        #endregion

        #region 资源释放

        /// <inheritdoc />
        public void Release(HandleBase handle)
        {
            if (handle == null)
            {
                return;
            }

            _trackedHandles.Remove(handle);
            handle.Release();
        }

        /// <inheritdoc />
        public void ReleaseAll()
        {
            if (_trackedHandles.Count == 0)
            {
                return;
            }

            HandleBase[] buffer = new HandleBase[_trackedHandles.Count];
            _trackedHandles.CopyTo(buffer);
            foreach (HandleBase handle in buffer)
            {
                handle?.Release();
            }

            _trackedHandles.Clear();
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 释放所有追踪的资源并销毁包裹。
        /// </summary>
        public override void Shutdown()
        {
            ReleaseAll();

            if (_packages.Count > 0)
            {
                foreach (ResourcePackage package in _packages.Values)
                {
                    DestroyOperation destroyOperation = package.DestroyAsync();
                    destroyOperation.WaitForAsyncComplete();
                    YooAssets.RemovePackage(package);
                }

                _packages.Clear();
            }

            if (YooAssets.Initialized)
            {
                YooAssets.Destroy();
            }

            _defaultPackageName = null;
            _config = null;
            _isInitialized = false;
        }

        #endregion

        #region 私有辅助方法

        private static void EnsureYooAssetsInitialized()
        {
            if (!YooAssets.Initialized)
            {
                YooAssets.Initialize();
            }
        }

        private static ResourceModeConfig LoadDefaultConfig()
        {
            ResourceModeConfig config = Resources.Load<ResourceModeConfig>(ResourceModeConfig.DefaultResourcesPath);
            return config;
        }

        private void RegisterHandle(HandleBase handle)
        {
            if (handle != null)
            {
                _trackedHandles.Add(handle);
            }
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("资源模块尚未初始化，请先调用 InitializeAsync");
            }
        }


        private InitializeParameters CreateInitializeParameters(ResourcePackageEntry entry)
        {
            return Mode switch
            {
                ResourceMode.EditorSimulate => CreateEditorSimulateParameters(entry),
                ResourceMode.OfflinePlay => CreateOfflineParameters(),
                ResourceMode.HostPlay => CreateHostParameters(entry),
                ResourceMode.WebPlay => CreateWebParameters(entry),
                _ => CreateEditorSimulateParameters(entry)
            };
        }

#if UNITY_EDITOR
        private static InitializeParameters CreateEditorSimulateParameters(ResourcePackageEntry entry)
        {

            var buildResult = EditorSimulateModeHelper.SimulateBuild(entry.PackageName);
            var packageRoot = buildResult.PackageRootDirectory;
            var fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);

            var createParameters = new EditorSimulateModeParameters();
            createParameters.EditorFileSystemParameters = fileSystemParams;

            return createParameters;

        }
#else
        private static InitializeParameters CreateEditorSimulateParameters(ResourcePackageEntry entry)
        {
            throw new InvalidOperationException("编辑器模拟模式仅支持在 Unity 编辑器环境下运行");
        }
#endif

        private static InitializeParameters CreateOfflineParameters()
        {
            var parameters = new OfflinePlayModeParameters
            {
                BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
            };
            return parameters;
        }

        #endregion

        #region 嵌套类

        /// <summary>
        /// 远端资源地址查询服务类
        /// </summary>
        private class RemoteServices : IRemoteServices
        {
            private readonly string _defaultHostServer;
            private readonly string _fallbackHostServer;

            public RemoteServices(string defaultHostServer, string fallbackHostServer)
            {
                _defaultHostServer = defaultHostServer;
                _fallbackHostServer = fallbackHostServer;
            }
            string IRemoteServices.GetRemoteMainURL(string fileName)
            {
                return $"{_defaultHostServer}/{fileName}";
            }
            string IRemoteServices.GetRemoteFallbackURL(string fileName)
            {
                return $"{_fallbackHostServer}/{fileName}";
            }
        }

        #endregion

        #region 私有辅助方法

        private InitializeParameters CreateHostParameters(ResourcePackageEntry entry)
        {
            string defaultHostServer = entry.GetSanitizedMainServer();
            string fallbackHostServer = entry.GetSanitizedFallbackServer();
            Log.Info("资源主服务器地址：" + defaultHostServer);
            IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
            var cacheFileSystemParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);

            // 注释掉内置文件系统，支持完全从远端下载（无需 StreamingAssets）
            var buildinFileSystemParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();

            var createParameters = new HostPlayModeParameters();
            createParameters.BuildinFileSystemParameters = buildinFileSystemParams;
            createParameters.CacheFileSystemParameters = cacheFileSystemParams;

            return createParameters;
        }

        private InitializeParameters CreateWebParameters(ResourcePackageEntry entry)
        {
            string defaultHostServer = entry.GetSanitizedMainServer();
            string fallbackHostServer = entry.GetSanitizedFallbackServer();

            IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
            var webServerFileSystemParams = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            var webRemoteFileSystemParams = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServices); //支持跨域下载

            var createParameters = new WebPlayModeParameters();
            createParameters.WebServerFileSystemParameters = webServerFileSystemParams;
            createParameters.WebRemoteFileSystemParameters = webRemoteFileSystemParams;

            return createParameters;

        }

        private static void HandleFailureIfNeed(HandleBase handle, string location, string action)
        {
            if (handle == null)
            {
                throw new InvalidOperationException($"{action}失败：句柄为空，定位地址 {location}");
            }

            if (handle.Status == EOperationStatus.Failed)
            {
                string error = string.IsNullOrEmpty(handle.LastError) ? "未知错误" : handle.LastError;
                handle.Release();
                throw new InvalidOperationException($"{action}失败：{location}，错误信息：{error}");
            }
        }

        private static float CalcProgress(int index, int total, float step)
        {
            if (total <= 0)
            {
                return 1f;
            }

            float baseValue = Mathf.Clamp01((float)index / total);
            float stepValue = Mathf.Clamp01(step) / total;
            return Mathf.Clamp01(baseValue + stepValue);
        }

        #endregion
    }
}
