## Context

`Packages/manifest.json` 已将 `com.tuyoogame.yooasset` 锁定到 `3.0.1-beta`，但 `Assets/EF/EFRuntime/Resource/` 下的实现仍全量调用 YooAsset 2.3 API；`ProjectSettings` 中 `YOOASSET_LEGACY_API` 兼容宏未开启。即便开启该宏，`YooAssets.SetDefaultPackage` 等被 v3 彻底移除的接口仍无兼容层，必须手动迁移。

工程内 YooAsset 调用面集中在四块：

| 区域 | 文件 | 调用密度 |
| ---- | ---- | -------- |
| 资源管理器主实现 | `Assets/EF/EFRuntime/Resource/ResourceManager.cs` | 高（初始化、下载、加载、释放、销毁全链路） |
| 资源管理器接口 | `Assets/EF/EFRuntime/Resource/IResourceManager.cs` | 单点（`LoadSceneAsync` 参数） |
| 远端服务实现 | `Assets/EF/EFRuntime/Resource/DefaultResourceRemoteServices.cs` + `ResourceManager.RemoteServices` 内嵌类 | 双实现，接口名变化 |
| 其它 EF 管理器 | `Scene/SceneManager.cs`、`Sound/SoundManager.cs`、`Entity/EntityManager.cs` | 仅引用 `AssetHandle` / `SceneHandle` / `EOperationStatus.Succeed` |

外部调用方仅依赖 `IResourceManager` 抽象；全工程检索 `LoadSceneAsync` 未发现任何调用点显式传入 `suspendLoad: true`，参数改名+默认值反转的真实影响为零。

`com.besty.unity-skills` 第三方包内嵌的 YooAsset 自动化仍基于 v2 API，与运行时无关，本次不修复，仅在风险段落标注。

## Goals / Non-Goals

**Goals:**

- 让 Unity 工程在不开启 `YOOASSET_LEGACY_API` 宏的前提下编译通过，并通过现有 EditMode + PlayMode 测试。
- 一次性把 `EF.Resource` 模块迁移到 YooAsset 3.0 的 Options + Event 编程模型，消除所有 `[Obsolete]` 警告与 v2 残留写法。
- `IResourceManager` 公共契约的破坏性变更（`LoadSceneAsync` 参数改名+语义反转）写入 `resource-scene-loading` 最小规格，留下可被未来修订追踪的锚点。
- 删除 `ResourceManager` 内嵌的 `RemoteServices` 类，统一走 `DefaultResourceRemoteServices`，消除重复代码。
- 删除 `ResourceMode.ToYooPlayMode` 死代码（v3 不再有 `EPlayMode` 枚举）。

**Non-Goals:**

- 不补齐 `EF.Resource` 模块的完整规格化（除 `LoadSceneAsync` 之外的接口仍维持未 spec 化状态，留待后续单独 OpenSpec 变更）。
- 不修复 `com.besty.unity-skills` 第三方包对 YooAsset 2.3 API 的依赖（不影响游戏运行时）。
- 不引入加密资源解密链（项目暂未使用 `IDecryptionServices`；v3 拆分出的 `IBundleOffsetDecryptor` / `IBundleStreamDecryptor` / `IBundleMemoryDecryptor` 接口不在本次范围）。
- 不调整 `ResourceModeConfig.asset` 的字段结构或包裹配置（YooAsset 升级是纯代码侧迁移）。
- 不调整 HybridCLR AOT 列表中的 `YooAsset.dll`（除非 v3 引入新运行时 DLL，则补齐）。

## Decisions

### 决策一：一次性彻底改 v3，不启用兼容宏

- **选择**：直接重写所有 YooAsset 调用点为 v3 API；不在 `ScriptingDefineSymbols` 中加入 `YOOASSET_LEGACY_API`。
- **替代方案**：开启兼容宏作为过渡，让旧代码先编译，再逐个替换 `[Obsolete]` 调用。
- **理由**：
  - 调用面集中（仅 4 个 EF 文件 + 5 个测试文件），单 PR 可控。
  - `YooAssets.SetDefaultPackage` 等核心接口在 v3 无兼容层，开宏并不能让所有问题消失，反而引入"半新半旧"中间态。
  - 跟兼容宏过渡相比，一次到位避免长期残留 `[Obsolete]` 警告海与心智负担。

### 决策二：`LoadSceneAsync` 参数跟随 v3 改名 + 默认值反转

- **选择**：`IResourceManager.LoadSceneAsync` 与 `ISceneManager.LoadSceneAsync` 的布尔参数由 `bool suspendLoad = false` 改为 `bool allowSceneActivation = true`，语义反转。
- **替代方案**：保留 `suspendLoad` 参数名，内部转换 `allowSceneActivation: !suspendLoad`。
- **理由**：
  - 全工程检索结果显示，所有调用方都使用该参数的默认值，没有任何调用点显式传 `suspendLoad: true`。"零真实迁移成本"使得"保留旧名给调用方"这个补偿动机不成立。
  - 跟 v3 心智模型对齐，避免长期维护"参数名说反话"的认知陷阱。
  - 把这条破坏性变更显式收入 `resource-scene-loading` 规格，给未来的维护者留下可追踪记录。

### 决策三：默认包概念移入 `ResourceManager` 内部

- **选择**：删除 `YooAssets.SetDefaultPackage(package)` 调用；`ResourceManager` 内部 `_defaultPackageName` 字段继续承担"默认包"语义；公共入口 `IResourceManager.GetDefaultPackage()` 行为保持不变。
- **替代方案**：在 v3 中重新实现一个"默认包"机制（例如自定义静态扩展）。
- **理由**：
  - 外部调用方从未直接依赖 `YooAssets.SetDefaultPackage`，统一走 `_resourceManager.GetDefaultPackage()`。
  - `ResourceManager._defaultPackageName` 字段已存在，迁移成本=删两行代码。
  - 避免重新发明 v3 已主动废弃的全局状态概念。

### 决策四：删除 `ResourceManager` 内嵌的 `RemoteServices`，统一走 `DefaultResourceRemoteServices`

- **选择**：移除 `ResourceManager` 内嵌的私有 `RemoteServices` 类，`CreateHostParameters` / `CreateWebParameters` 改用顶层 `DefaultResourceRemoteServices`。
- **替代方案**：保留两份实现仅做接口名替换。
- **理由**：
  - 内嵌类与 `DefaultResourceRemoteServices` 功能等价但实现重复，本次迁移本就要改两处，顺手清理。
  - `IRemoteServices` → `IRemoteService` 改名只在统一实现处改一次。

### 决策五：删除 `ResourceMode.ToYooPlayMode` 死代码

- **选择**：移除 `ResourceModeUtility.ToYooPlayMode` 扩展方法与 `using YooAsset;` 引用。
- **替代方案**：保留为空实现或抛异常占位。
- **理由**：v3 已彻底移除 `EPlayMode` 枚举；`ResourceManager.CreateInitializeParameters` 已通过 switch + `ResourceMode` 直接构造对应 Options 类型，扩展方法处于无引用状态。

### 决策六：Downloader 回调按 v3 事件模型重写

- **选择**：4 个回调字段赋值（`DownloadFinishCallback = ...`）改为标准 .NET 事件订阅（`DownloadCompleted += ...`）；`BeginDownload()` → `StartDownload()`；4 个 `DownloadXxxData` 数据类换成对应 `DownloadXxxEventArgs`。
- **理由**：v3 把 Downloader API 整体切换到标准事件模型；保留旧的字段赋值方式不可行（编译期就会 `[Obsolete]` 警告）。

### 决策七：FileSystem 工厂方法适配 v3 命名与签名

- **选择**：
  - `CreateDefaultBuildinFileSystemParameters` → `CreateDefaultBuiltinFileSystemParameters`（拼写修正）。
  - `CreateDefaultCacheFileSystemParameters` → `CreateDefaultSandboxFileSystemParameters`。
  - `CreateDefaultWebServerFileSystemParameters` / `CreateDefaultWebRemoteFileSystemParameters` 重新核对参数列表（v3 移除了 `webDecryptSvc` 入参）。
  - 字符串硬编码的内部文件系统类名（若有）从 `DefaultCacheFileSystem` 等改为 `SandboxFileSystem` 等。
- **理由**：v3 的 `FileSystemParameters` 工厂方法既改名又精简参数，必须逐个替换；命名拼写修正（`Buildin`→`Builtin`）是不可逆的破坏性变更。

### 决策八：测试 mock 同步签名变更

- **选择**：所有实现 `IResourceManager` 的测试桩（`GameLogicEntryUiInitializationTests` / `UIManagerLifecycleTests` / `GameControllerCommandFlowTests` / `GameViewIntegrationTests` 等）的 `LoadSceneAsync` 参数同步改名 `suspendLoad` → `allowSceneActivation` 并反转默认值。
- **替代方案**：在 mock 里手写 `suspendLoad` 转 `allowSceneActivation` 适配层。
- **理由**：mock 直接 `throw NotSupportedException`，没有真实逻辑，签名跟接口一致即可，没必要保留旧名。

## Risks / Trade-offs

- **[风险] Unity 编辑器侧仍引用 v2 API 的第三方包** → 缓解：`com.besty.unity-skills` 不影响运行时；`unity-compile-check.py` 与 `dotnet build` 仍能验证 C# 编译；若该包对 v3 报错则改用 Unity Skills `POST /skill/compile_check` 或编辑器手动验证作为兜底。
- **[风险] `EditorSimulateBuildInvoker.Build` 第二参数类型不确定** → 缓解：迁移指南建议传 `(int)EBundleType.VirtualAssetBundle`；若 v3 签名不同，由实施阶段在 `ResourceManager.CreateEditorSimulateParameters` 调试时按编译报错调整。
- **[风险] HybridCLR AOT 列表里 `YooAsset.dll` 名称可能变化** → 缓解：实施阶段对照 `Library/PackageCache/com.tuyoogame.yooasset@*/` 实际 dll 名称同步 `HotFixConfig.cs`。
- **[风险] `ResourceModeConfig.asset` 的序列化字段（包裹列表）可能被 v3 影响** → 缓解：本次不改 `ResourceModeConfig` / `ResourcePackageEntry` 字段；仅在 `ResourceManager` 里把这些配置喂给 v3 的 Options 类。
- **[风险] PlayMode 测试需要真实初始化包裹** → 缓解：迁移完成后必须在本地 Unity 编辑器跑 `ResourceManagerPlayModeTests` 与 `SceneManagerPlayModeTests`；EditMode 测试在 CI 上覆盖。
- **[Trade-off] 把 `LoadSceneAsync` 参数改名为 `allowSceneActivation`** → 与 v3 心智一致但破坏二进制兼容（重编译热更新 DLL 后旧 HybridCLR DLL 不再兼容）；考虑到这是一次彻底升级，可接受。

## Migration Plan

1. 在主分支创建独立分支 `feature/migrate-yooasset-3-0` 与 worktree `.claude/worktrees/migrate-yooasset-3-0/`。
2. 按 `tasks.md` 顺序：先迁移 `ResourceManager.cs`（最大改动面），再处理接口 + 远端服务 + Mode 简化，最后扫边 EF Scene/Sound/Entity 与测试 mock。
3. 每个文件改完执行 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`（或 `dotnet build UnityProject.slnx --no-restore`）确认无编译错误后再继续。
4. 所有代码改完跑完整 EditMode 测试套件；本地 Unity 编辑器跑 PlayMode 测试。
5. 更新 `CLAUDE.md` 中的"YooAsset 2.3.x"为"YooAsset 3.0.x"。
6. 提交并在主工作区合并。归档变更后通过 `mempalace-session-sync` 把"v3 默认包概念移入 ResourceManager 内部"等架构决策落入 MemPalace。

**Rollback**：若发现 v3 包关键缺陷无法在本次范围内修复，回滚步骤为：
1. `git revert` 本系列提交。
2. 把 `Packages/manifest.json` 中 `com.tuyoogame.yooasset` 还原为 `2.3.x` 旧版本。
3. 删除已生成的 `openspec/specs/resource-scene-loading/` 目录或将其同步保留为"未来 v3 迁移再启用"。

## Open Questions

- v3 包是否仍把 AOT 程序集名为 `YooAsset.dll`？若发生改名需同步 `Assets/GameScripts/Runtime/HotFixConfig.cs` 第 24 行。
- v3 `EditorSimulateBuildInvoker.Build` 的第二参数确切类型与值（`int` vs `EBundleType`），需在实施阶段以编译报错确认。
- `com.besty.unity-skills` 是否计划发布兼容 YooAsset 3.0 的版本？本次不阻塞，但需要在本变更归档后跟踪上游。
