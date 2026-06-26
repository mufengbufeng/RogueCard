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

            EnsureYooAssetsInitialized();

            _config = overrideConfig ?? LoadDefaultConfig();
            if (_config == null)
            {
                throw new InvalidOperationException($"未找到资源配置文件，请确认 Resources/{ResourceModeConfig.DefaultResourcesPath}.asset 是否存在");
            }

            if (_config.Packages == null || _config.Packages.Count == 0)
            {
                throw new InvalidOperationException("资源配置未包含任何包裹，请至少配置一个包裹信息");
            }

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

                if (!YooAssets.TryGetPackage(entry.PackageName, out ResourcePackage package))
                {
                    package = YooAssets.CreatePackage(entry.PackageName);
                }

                if (entry.IsDefault || string.IsNullOrEmpty(_defaultPackageName))
                {
                    _defaultPackageName = package.PackageName;
                }

                Log.Info($"开始初始化资源包裹 {entry.PackageName}，运行模式 {Mode}...");

                InitializePackageOptions options = CreateInitializeParameters(entry);
                options.BundleLoadingMaxConcurrency = _config.BundleLoadingMaxConcurrency;

                InitializePackageOperation operation = package.InitializePackageAsync(options);
                await operation;
                if (operation.Status != EOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 初始化失败：{operation.Error}");
                }

                RequestPackageVersionOperation requestVersionOperation = RequestPackageVersion(package);
                await requestVersionOperation;
                if (requestVersionOperation.Status != EOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 请求版本失败：{requestVersionOperation.Error}");
                }

                LoadPackageManifestOperation loadManifestOperation = LoadPackageManifest(package, requestVersionOperation.PackageVersion);
                await loadManifestOperation;
                if (loadManifestOperation.Status != EOperationStatus.Succeeded)
                {
                    throw new InvalidOperationException($"资源包裹 {entry.PackageName} 更新清单失败：{loadManifestOperation.Error}");
                }

                await Download(package);

                if (_packages.TryGetValue(package.PackageName, out ResourcePackage existing) && !ReferenceEquals(existing, package))
                {
                    DestroyPackageOperation destroyOperation = existing.DestroyPackageAsync();
                    await destroyOperation;
                    if (destroyOperation.Status == EOperationStatus.Succeeded)
                    {
                        YooAssets.RemovePackage(existing.PackageName);
                    }
                }

                _packages[package.PackageName] = package;
                progress?.Report(CalcProgress(index + 1, total, 0f));
            }

            if (string.IsNullOrEmpty(_defaultPackageName))
            {
                ResourcePackageEntry fallbackEntry = _config.GetDefaultPackage();
                _defaultPackageName = fallbackEntry?.PackageName;
            }

            _isInitialized = true;
            progress?.Report(1f);
        }

        private static RequestPackageVersionOperation RequestPackageVersion(ResourcePackage package)
        {
            return package.RequestPackageVersionAsync();
        }

        private static LoadPackageManifestOperation LoadPackageManifest(ResourcePackage package, string packageVersion)
        {
            var options = new LoadPackageManifestOptions(packageVersion, 60);
            return package.LoadPackageManifestAsync(options);
        }

        private static async UniTask Download(ResourcePackage package)
        {
            var options = new ResourceDownloaderOptions(10, 3);
            ResourceDownloaderOperation downloader = package.CreateResourceDownloader(options);

            if (downloader.TotalDownloadCount == 0)
            {
                return;
            }

            downloader.DownloadCompleted += OnDownloadCompleted;
            downloader.DownloadError += OnDownloadError;
            downloader.DownloadProgressChanged += OnDownloadProgressChanged;
            downloader.DownloadFileStarted += OnDownloadFileStarted;

            downloader.StartDownload();
            await downloader;

            if (downloader.Status != EOperationStatus.Succeeded)
            {
                Log.Error($"资源包裹 {package.PackageName} 下载失败：{downloader.Error}");
            }
        }

        private static void OnDownloadFileStarted(DownloadFileStartedEventArgs data)
        {
            Log.Info($"资源包裹 {data.PackageName} 开始下载文件：{data.FileName}，大小：{data.FileSize} 字节");
        }

        private static void OnDownloadProgressChanged(DownloadProgressChangedEventArgs data)
        {
            Log.Info(
                $"资源包裹 {data.PackageName} 下载进度：{data.Progress:P2}，文件 {data.CurrentDownloadCount}/{data.TotalDownloadCount}，字节 {data.CurrentDownloadBytes}/{data.TotalDownloadBytes}");
        }

        private static void OnDownloadError(DownloadErrorEventArgs data)
        {
            Log.Error($"资源包裹 {data.PackageName} 下载错误，文件名称：{data.FileName}，错误信息：{data.ErrorInfo}");
        }

        private static void OnDownloadCompleted(DownloadCompletedEventArgs data)
        {
            if (data.Succeeded)
            {
                Log.Info($"资源包裹 {data.PackageName} 下载完成");
            }
            else
            {
                Log.Error($"资源包裹 {data.PackageName} 下载失败：{data.Error}");
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
        public async UniTask<AssetHandle> LoadAssetAsync<T>(string location, Action<float> progress = null, uint priority = 0)
            where T : UnityEngine.Object
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

            await handle;
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

        /// <inheritdoc />
        public async UniTask<SceneHandle> LoadSceneAsync(
            string location,
            LoadSceneMode sceneMode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            bool allowSceneActivation = true,
            uint priority = 0,
            Action<float> progress = null)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("场景定位地址不能为空", nameof(location));
            }

            ResourcePackage package = GetDefaultPackage();
            SceneHandle handle = package.LoadSceneAsync(location, sceneMode, physicsMode, allowSceneActivation, priority);

            if (progress != null)
            {
                while (!handle.IsDone)
                {
                    progress(handle.Progress);
                    await UniTask.Yield();
                }
            }

            await handle;
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
            UnloadSceneOperation operation = handle.UnloadSceneAsync();
            operation.Completed += completedOperation =>
            {
                if (completedOperation.Status == EOperationStatus.Failed)
                {
                    Log.Error($"卸载场景失败：{completedOperation.Error}");
                }
            };
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
                _packages.Clear();
            }

            if (YooAssets.IsInitialized)
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
            if (!YooAssets.IsInitialized)
            {
                YooAssets.Initialize(null);
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

        private InitializePackageOptions CreateInitializeParameters(ResourcePackageEntry entry)
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
        private static InitializePackageOptions CreateEditorSimulateParameters(ResourcePackageEntry entry)
        {
            PackageBuildResult buildResult = EditorSimulateBuildInvoker.Build(entry.PackageName, (int)EBundleType.VirtualAssetBundle);
            string packageRoot = buildResult.PackageRootDirectory;
            FileSystemParameters fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);

            return new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = fileSystemParams
            };
        }
#else
        private static InitializePackageOptions CreateEditorSimulateParameters(ResourcePackageEntry entry)
        {
            throw new InvalidOperationException("编辑器模拟模式仅支持在 Unity 编辑器环境下运行");
        }
#endif

        private static InitializePackageOptions CreateOfflineParameters()
        {
            return new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
            };
        }

        private InitializePackageOptions CreateHostParameters(ResourcePackageEntry entry)
        {
            string defaultHostServer = entry.GetSanitizedMainServer();
            string fallbackHostServer = entry.GetSanitizedFallbackServer();
            Log.Info("资源主服务器地址：" + defaultHostServer);

            IRemoteService remoteService = new DefaultResourceRemoteServices(defaultHostServer, fallbackHostServer);
            FileSystemParameters cacheFileSystemParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
            FileSystemParameters builtinFileSystemParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();

            return new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = builtinFileSystemParams,
                CacheFileSystemParameters = cacheFileSystemParams
            };
        }

        private InitializePackageOptions CreateWebParameters(ResourcePackageEntry entry)
        {
            string defaultHostServer = entry.GetSanitizedMainServer();
            string fallbackHostServer = entry.GetSanitizedFallbackServer();

            IRemoteService remoteService = new DefaultResourceRemoteServices(defaultHostServer, fallbackHostServer);
            FileSystemParameters webServerFileSystemParams =
                FileSystemParameters.CreateDefaultWebServerFileSystemParameters(entry.DisableUnityWebCache);
            FileSystemParameters webRemoteFileSystemParams =
                FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteService, entry.DisableUnityWebCache);

            return new WebPlayModeOptions
            {
                WebServerFileSystemParameters = webServerFileSystemParams,
                WebRemoteFileSystemParameters = webRemoteFileSystemParams
            };
        }

        private static void HandleFailureIfNeed(HandleBase handle, string location, string action)
        {
            if (handle == null)
            {
                throw new InvalidOperationException($"{action}失败：句柄为空，定位地址 {location}");
            }

            if (handle.Status == EOperationStatus.Failed)
            {
                string error = string.IsNullOrEmpty(handle.Error) ? "未知错误" : handle.Error;
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
