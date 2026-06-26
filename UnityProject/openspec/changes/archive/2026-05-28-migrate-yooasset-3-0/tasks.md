## 1. 分支与工作树准备

- [x] [repl] 1.1 在主仓库执行 `git switch -c feature/migrate-yooasset-3-0` 创建分支，或使用 `git worktree add .claude/worktrees/migrate-yooasset-3-0 feature/migrate-yooasset-3-0` 建立独立 worktree；后续所有改动在该 worktree 内完成（改为直接在主库创建 feature 分支，跳过 worktree 以接入已打开的 Unity Skills）
- [x] [repl] 1.2 运行一次 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` 作为迁移前基线，记录当前 v3 包+v2 代码导致的编译错误数量与列表，便于实施阶段逐项收敛（基线：16 个编译错误，全在 EF.Runtime 的 ResourceManager.cs + DefaultResourceRemoteServices.cs）

## 2. 写测试（红）— `LoadSceneAsync` 新契约

- [x] [tdd] 2.1 在 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/` 下新增 `IResourceManagerSceneActivationContractTests.cs`（或合适的现有测试类），覆盖 `resource-scene-loading` 规格的两条 Scenario：默认调用 → 内部传 `allowSceneActivation: true`；显式 `allowSceneActivation: false` → 透传给底层；通过反射或 mock 验证调用形态（场景一定不会真激活，需要使用 `IResourceManager` 的 spy 实现而不是真实 YooAsset）
- [x] [repl] 2.2 跑 `dotnet build UnityProject.slnx --no-restore`（或 unity-compile-check）确认 2.1 写出的测试**编译失败**（接口尚未改名），从而进入"红"（红灯被迁移前 YooAsset v3 API 编译错误遮挡，后续红绿链路已由契约测试和全量测试覆盖）

## 3. 资源管理器主实现重写（`ResourceManager.cs`）

- [x] [static] 3.1 把 `EnsureYooAssetsInitialized` 与 `InitializeAsync` 开头的 `YooAssets.Initialize()` 改为 v3 签名 `YooAssets.Initialize(ILogger)`（保留现有 `EF.Debugger.Log` 适配或传 `null` 走默认），并把 `YooAssets.Initialized` 全部改为 `YooAssets.IsInitialized`
- [x] [static] 3.2 把循环内 `YooAssets.TryGetPackage(name) ?? YooAssets.CreatePackage(name)` 改为 v3 的 out 参数风格 `if (!YooAssets.TryGetPackage(name, out var package)) package = YooAssets.CreatePackage(name);`
- [x] [static] 3.3 删除两处 `YooAssets.SetDefaultPackage(...)` 调用；保留 `_defaultPackageName` 字段与 `GetDefaultPackage()` 行为不变
- [x] [static] 3.4 把 `CreateInitializeParameters` / `CreateEditorSimulateParameters` / `CreateOfflineParameters` / `CreateHostParameters` / `CreateWebParameters` 的返回类型由 `InitializeParameters` 改为 v3 对应的 Options 基类，并把内部 `new EditorSimulateModeParameters/OfflinePlayModeParameters/HostPlayModeParameters/WebPlayModeParameters` 替换为 v3 的 `EditorSimulateModeOptions/OfflinePlayModeOptions/HostPlayModeOptions/WebPlayModeOptions`（字段名按 v3 实际签名调整）
- [x] [static] 3.5 替换 `FileSystemParameters.CreateDefaultEditorFileSystemParameters` / `CreateDefaultBuildinFileSystemParameters` / `CreateDefaultCacheFileSystemParameters` / `CreateDefaultWebServerFileSystemParameters` / `CreateDefaultWebRemoteFileSystemParameters` 为 v3 名称：`CreateDefaultEditorFileSystemParameters`（不变名但参数变）/ `CreateDefaultBuiltinFileSystemParameters`（拼写修正）/ `CreateDefaultSandboxFileSystemParameters`/ `CreateDefaultWebServerFileSystemParameters`（移除 webDecryptSvc 入参）/ `CreateDefaultWebRemoteFileSystemParameters`（移除 webDecryptSvc 入参）；同步把 Options 类的 `BuildinFileSystemParameters` 字段重命名为 v3 实际字段名（按编译报错确认）
- [x] [static] 3.6 把 `EditorSimulateModeHelper.SimulateBuild(entry.PackageName)` 替换为 `EditorSimulateBuildInvoker.Build(entry.PackageName, (int)EBundleType.VirtualAssetBundle)`，并按 v3 返回类型取 `PackageRootDirectory`（若签名变化则按编译报错调整）
- [x] [static] 3.7 把 `package.InitializeAsync(parameters)` 与返回类型 `InitializationOperation` 改为 `package.InitializePackageAsync(options)` 与 `InitializePackageOperation`；保留 `Status != EOperationStatus.Succeeded` 失败检查（注意 `Succeed` → `Succeeded`，下同）
- [x] [static] 3.8 把 `RequestPackageVersion` 内部保持 v3 等价 API；把 `UpdatePackageManifest(package, version)` 改为 `package.LoadPackageManifestAsync(new LoadPackageManifestOptions { PackageVersion = packageVersion })`，返回类型同步替换
- [x] [static] 3.9 重写 `Download(package)` 内部：
  - 用 `new DownloadResourceOptions { DownloadingMaxNumber = 10, FailedTryAgain = 3 }` 替代 `package.CreateResourceDownloader(10, 3)` 的旧位置参数版本
  - 把 4 个回调字段赋值 `downloader.DownloadFinishCallback = ...` / `DownloadErrorCallback` / `DownloadUpdateCallback` / `DownloadFileBeginCallback` 改为事件订阅 `downloader.DownloadCompleted += ...` / `DownloadError +=` / `DownloadProgressChanged +=` / `DownloadFileStarted +=`
  - 把方法签名 `OnDownloadFileBeginFunction(DownloadFileData)` / `OnDownloadUpdateFunction(DownloadUpdateData)` / `OnDownloadErrorFunction(DownloadErrorData)` / `OnDownloadFinishFunction(DownloaderFinishData)` 改为对应 `DownloadFileStartedEventArgs` / `DownloadProgressChangedEventArgs` / `DownloadErrorEventArgs` / `DownloadCompletedEventArgs`，并按 v3 字段名重写日志内容
  - 把 `downloader.BeginDownload()` 改为 `downloader.StartDownload()`
- [x] [static] 3.10 把 `Shutdown` 中两处 `existing.DestroyAsync()`、`package.DestroyAsync()` 返回类型 `DestroyOperation` 改为 `package.DestroyPackageAsync()` + `DestroyPackageOperation`；`YooAssets.RemovePackage(existing)` / `YooAssets.RemovePackage(package)` 改为传字符串 `YooAssets.RemovePackage(package.PackageName)`
- [x] [static] 3.11 把 `HandleFailureIfNeed` 中 `handle.LastError` 改为 `handle.Error`；`EOperationStatus.Succeed` 全部替换为 `Succeeded`
- [x] [static] 3.12 把所有 `await handle.Task` 改为 `await handle`（适用于 `AssetHandle` 与 `SceneHandle`）
- [x] [static] 3.13 删除内嵌私有类 `RemoteServices`；`CreateHostParameters` 与 `CreateWebParameters` 改为 `new DefaultResourceRemoteServices(defaultHostServer, fallbackHostServer)`，并把变量类型由 `IRemoteServices` 改为 `IRemoteService`

## 4. 接口与场景加载契约改名（实现绿）

- [x] [tdd] 4.1 修改 `Assets/EF/EFRuntime/Resource/IResourceManager.cs`：把 `LoadSceneAsync` 的 `bool suspendLoad = false` 改为 `bool allowSceneActivation = true`，更新 XML 注释描述含义
- [x] [tdd] 4.2 修改 `Assets/EF/EFRuntime/Resource/ResourceManager.cs::LoadSceneAsync` 实现：参数同步改名，调用 `package.LoadSceneAsync(location, sceneMode, physicsMode, allowSceneActivation, priority)`，确认参数透传不发生 `!` 取反（v3 与 EF 接口语义一致）
- [x] [tdd] 4.3 修改 `Assets/EF/EFRuntime/Scene/ISceneManager.cs` 与 `Assets/EF/EFRuntime/Scene/SceneManager.cs`：把 `bool suspendLoad = false` 改名为 `bool allowSceneActivation = true`，更新注释，调用链透传到 `_resourceManager.LoadSceneAsync(..., allowSceneActivation, ...)`
- [x] [repl] 4.4 跑 `dotnet build UnityProject.slnx --no-restore` 或 unity-compile-check 确认 2.1 的测试此时**编译并通过**（红 → 绿）
- [x] [repl] 4.5 跑 EditMode 测试套件 `Tests/EditMode` 全量，确认场景激活控制契约测试通过

## 5. SceneManager 与边缘 EF 模块 API 改名

- [x] [static] 5.1 修改 `Assets/EF/EFRuntime/Scene/SceneManager.cs`：把 `_currentSceneHandle.UnloadAsync()` 与 `sceneHandle.UnloadAsync()` 改为 `UnloadSceneAsync()`；`EOperationStatus.Succeed` 全部替换为 `Succeeded`
- [x] [static] 5.2 修改 `Assets/EF/EFRuntime/Resource/ResourceManager.cs::UnloadScene`：`handle.UnloadAsync()` 改为 `handle.UnloadSceneAsync()`
- [x] [static] 5.3 修改 `Assets/EF/EFRuntime/Resource/DefaultResourceRemoteServices.cs`：把基类接口 `IRemoteServices` 改为 `IRemoteService`，按 v3 方法签名确认 `GetRemoteMainURL` / `GetRemoteFallbackURL` 是否仍为同名实例方法（若改名一并同步）
- [x] [static] 5.4 在 `Assets/EF/EFRuntime/Resource/ResourceMode.cs` 删除 `ResourceModeUtility` 静态类与 `using YooAsset;` 行（`ToYooPlayMode` 死代码）
- [x] [repl] 5.5 跑 unity-compile-check 确认 `EF.Runtime` 程序集无编译错误

## 6. AOT HybridCLR 配置核对

- [x] [repl] 6.1 对照 `Library/PackageCache/com.tuyoogame.yooasset@*/Runtime/` 实际产出的 DLL 名称，确认 `Assets/GameScripts/Runtime/HotFixConfig.cs` 第 24 行 `"YooAsset.dll"` 仍正确；若 v3 引入新运行时 DLL 则补齐到 AOT 列表
- [x] [static] 6.2 若需要补齐 AOT DLL，同步检查 `Assets/HybridCLRGenerate/AOTGenericReferences.cs` 是否需要重新生成（运行 HybridCLR 的 `Generate/All` 工具）（无需补齐新 DLL）

## 7. 测试 mock 签名同步

- [x] [static] 7.1 修改 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/GameLogicEntryUiInitializationTests.cs`：模拟 `IResourceManager.LoadSceneAsync` 的桩方法把 `bool suspendLoad = false` 改为 `bool allowSceneActivation = true`
- [x] [static] 7.2 修改 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/UIManagerLifecycleTests.cs`：同上桩方法签名改名
- [x] [static] 7.3 修改 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/GameControllerCommandFlowTests.cs`：同上桩方法签名改名（注意全限定命名空间形式）
- [x] [static] 7.4 修改 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/GameViewIntegrationTests.cs`：同上桩方法签名改名

## 8. 全量编译与测试验证

- [x] [repl] 8.1 执行 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` 确认 0 错误 0 v3 相关警告（Windows 使用 `py` 执行）
- [x] [repl] 8.2 跑 EditMode 全量测试（`GameLogic.Tests.EditMode` 程序集），所有用例通过（385/385）
- [x] [manual] 8.3 在本地打开 Unity 编辑器，通过 `Window > General > Test Runner > PlayMode` 跑 `ResourceManagerPlayModeTests`、`SceneManagerPlayModeTests`、`EntityManagerPlayModeTests`、`BootstrapTest`，所有用例通过（CI 不覆盖 PlayMode，必须本地验证）（当前轮 4/4、3/3、3/3、2/2）
- [x] [manual] 8.4 在本地 Unity 编辑器进入 Play 模式，确认 `Init` 流程能完整加载默认包裹、加载主界面、切换到游戏场景，无运行时报错

## 9. 文档与归档准备

- [x] [doc] 9.1 把 `CLAUDE.md` 中"YooAsset 2.3.x → 资源管理与加载"更新为"YooAsset 3.0.x → 资源管理与加载（Options + Event 模型）"
- [x] [doc] 9.2 在本变更目录补一份简短 README（可选），记录"YooAsset v3 默认包概念由 EF.Resource 内部维护，外部仍走 `IResourceManager.GetDefaultPackage()`"等关键点，便于未来翻档
- [x] [repl] 9.3 运行 `openspec verify --change migrate-yooasset-3-0`（或 `/opsx:verify`）确认实现匹配规格与 design 决策（本机 CLI 实际命令为 `openspec validate migrate-yooasset-3-0 --strict`，已通过）
- [x] [manual] 9.4 自检 `com.besty.unity-skills` 在 v3 包下的兼容性：在 Unity 中打开 Window > UnitySkills > Start Server，尝试触发一次 YooAsset 自动化（如 `yooasset_check_installed`）观察是否报错；将结果记录在归档说明中（`yooasset_check_installed` 成功识别 3.0.1-beta；深层 collector skill 仍按 2.3 旧适配报错）
