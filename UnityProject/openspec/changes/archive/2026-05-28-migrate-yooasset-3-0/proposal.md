## Why

`Packages/manifest.json` 已经把 `com.tuyoogame.yooasset` 升到 `3.0.1-beta`，但项目代码仍然全量使用 YooAsset 2.3 API（`YooAssets.SetDefaultPackage`、`InitializeParameters`、`DownloadFinishCallback`、`FileSystemParameters.CreateDefaultBuildinFileSystemParameters` 等）。`YOOASSET_LEGACY_API` 兼容宏未启用，工程当前大概率无法编译；即使开宏，`YooAssets.SetDefaultPackage` 等 API 在 v3 已被彻底移除、无兼容层，仍然必须手动迁移。

## What Changes

- **BREAKING**：`IResourceManager.LoadSceneAsync` 把布尔参数 `suspendLoad`（默认 `false`）改为 `allowSceneActivation`（默认 `true`），语义反转（与 v3 `LoadSceneAsync` 对齐，无调用方显式传 `suspendLoad: true`，零真实影响）。
- 一次性把 `EF.Resource` 模块从 YooAsset 2.3 API 改写为 YooAsset 3.0 API，不开启 `YOOASSET_LEGACY_API` 兼容宏。
- 包初始化流程改用 v3 的 `InitializePackageOptions` / `LoadPackageManifestOptions` / `DownloadResourceOptions` 等 Options 类型；移除 `YooAssets.SetDefaultPackage` 调用（v3 已删除），默认包仅由 `ResourceManager` 内部 `_defaultPackageName` 维护，外部入口保持 `GetDefaultPackage()` 不变。
- `IRemoteServices` 改名为 `IRemoteService`，删除 `ResourceManager` 内嵌的 `RemoteServices` 类，统一使用 `DefaultResourceRemoteServices`。
- `FileSystemParameters` 工厂方法改为 v3 名称（`Buildin`→`Builtin`、`Cache`→`Sandbox`），重新构造 Web/Host/Offline/EditorSimulate 模式的文件系统参数。
- Downloader 4 个回调（`DownloadFinishCallback` 等字段赋值）改为 v3 事件订阅（`DownloadCompleted` 等事件 `+=`），`BeginDownload()` 改为 `StartDownload()`，4 个 `DownloadXxxData` 类替换为对应 `DownloadXxxEventArgs`。
- 删除 `ResourceMode.cs` 中的 `ToYooPlayMode` 死代码（v3 已无 `EPlayMode` 枚举）。
- `SceneHandle.UnloadAsync()` 改为 `UnloadSceneAsync()`；`EOperationStatus.Succeed` 改为 `Succeeded`；`YooAssets.Initialized` 改为 `IsInitialized`；`YooAssets.RemovePackage(pkg)` 改为传字符串 `RemovePackage(name)`；`handle.LastError` 改为 `Error`；`await handle.Task` 改为 `await handle`；`EditorSimulateModeHelper.SimulateBuild(name)` 改为 `EditorSimulateBuildInvoker.Build(name, (int)EBundleType.VirtualAssetBundle)`。
- 同步更新 EditMode/PlayMode 测试中模拟 `IResourceManager` 的 mock 签名（`suspendLoad` → `allowSceneActivation`）。
- 把 `CLAUDE.md` 里"YooAsset 2.3.x"的版本表述更新为"YooAsset 3.0.x"。

## Capabilities

### New Capabilities

- `resource-scene-loading`：把本次唯一对外可见的破坏性契约变更（`IResourceManager.LoadSceneAsync` 由 `suspendLoad`/默认 `false` 改为 `allowSceneActivation`/默认 `true`，语义反转）固化为一份最小规格，记录场景激活控制的公开行为。

### Modified Capabilities

无（`EF.Resource` 模块的其余实现重写均属内部实现细节：默认包概念由 `ResourceManager._defaultPackageName` 维护，对外接口 `GetDefaultPackage()` 行为不变；初始化、下载、释放等流程的 API 改名均不改变 `IResourceManager` 公共契约）。

## Impact

- **受影响代码**（`Assets/EF/EFRuntime/Resource/`）
  - `ResourceManager.cs`：主战场，初始化、下载、场景加载、释放、销毁全部重写
  - `IResourceManager.cs`：`LoadSceneAsync` 参数改名+反转
  - `DefaultResourceRemoteServices.cs`：`IRemoteServices` → `IRemoteService`
  - `ResourceMode.cs`：删除 `ToYooPlayMode` 死代码
- **受影响代码**（其他 EF 模块）
  - `Assets/EF/EFRuntime/Scene/SceneManager.cs`：`UnloadAsync` → `UnloadSceneAsync`、`EOperationStatus.Succeed` → `Succeeded`
  - `Assets/EF/EFRuntime/Scene/ISceneManager.cs`：`suspendLoad` → `allowSceneActivation`（含默认值反转）
  - `Assets/EF/EFRuntime/Sound/SoundManager.cs` / `Entity/EntityManager.cs`：仅引用 `AssetHandle`，无 API 影响
- **受影响测试**
  - `Tests/EditMode/Framework/GameLogicEntryUiInitializationTests.cs`、`UIManagerLifecycleTests.cs`：mock 签名同步
  - `Tests/EditMode/Game/UI/GameControllerCommandFlowTests.cs`、`GameViewIntegrationTests.cs`：mock 签名同步
  - `Tests/PlayMode/ResourceManagerPlayModeTests.cs`、`SceneManagerPlayModeTests.cs`：跑全套 PlayMode 验证迁移正确
- **受影响依赖**
  - `Packages/manifest.json`：包版本已经是 `3.0.1-beta`，本次不再变动
  - `Assets/GameScripts/Runtime/HotFixConfig.cs`：AOT 列表中的 `YooAsset.dll` 名称不变；如果 v3 引入新的运行时 DLL 需要补齐（验证项）
- **文档**
  - `CLAUDE.md` 版本号
- **风险**
  - `com.besty.unity-skills` 第三方包内的 YooAsset 适配仍基于 v2 API（仅影响通过 Unity Skills 触发 YooAsset 构建/校验的自动化流程，不影响游戏运行时）；本次变更不修复，但需在文档中标注。
- **回滚策略**
  - 单分支 `feature/migrate-yooasset-3-0` + 独立 worktree 提交；若必须回滚，可同时把 `manifest.json` 还原到 2.3.x 并 `git revert` 本系列提交。
