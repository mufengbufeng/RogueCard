## 1. 骨架与冒烟

- [x] 1.1 创建目录 `Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/Framework/`，并新增 `GameLogic.Tests.PlayMode.asmdef`：`includePlatforms=[]`（**不能设成 `["Editor"]`，否则会被识别为 EditMode 测试**）、`defineConstraints=["UNITY_INCLUDE_TESTS"]`（保证不进 Player）、`overrideReferences=true`、`autoReferenced=false`、`precompiledReferences=["nunit.framework.dll"]`，并通过 GUID 引用 `GameLogic / GameProto / EF.Runtime / UniTask / YooAsset / UnityEngine.TestRunner / UnityEditor.TestRunner`（直接复用 `EditMode/GameLogic.Tests.EditMode.asmdef` 中已验证的 7 个 GUID）。
- [x] 1.2 实现 `Tests/PlayMode/Framework/PlayModeTestBase.cs`：`[UnitySetUp]` 调 `ModuleSystem.ShutdownAll()`、构造测试用 root GameObject、基于生产 `EFResourceModeConfig.asset` 克隆并强制 EditorSimulate 模式后 `await InitializeAsync` ResourceManager；`[UnityTearDown]` 调 `ModuleSystem.ShutdownAll()`（链路触发 `ResourceManager.Shutdown()` 内已自带的 `YooAssets.Destroy()`）、destroy root、`UniTask.Yield()` 推进一帧。
- [x] 1.3 在 `PlayModeTestBase` 中实现 `FrameDelay(int frames)`、`LoadFixtureAsync<T>(string location)`、`AssertNoLeakedHandles()`（反射读 `_trackedHandles`）三个 protected 工具方法。
- [x] 1.4 新增 `Tests/PlayMode/BootstrapTest.cs`：包含 `Bootstrap_ModuleSystemStartsClean`、`Bootstrap_TwoConsecutiveTestsHaveNoStateLeak` 两个 `[UnityTest]`，验证基类隔离机制。
- [x] 1.5 **调研结论（含两轮运行期纠错）**：`ResourceManager.Shutdown()` 第 411 行调 `package.DestroyAsync().WaitForAsyncComplete()`，但 YooAsset v2.3.18 的 `DestroyOperation` 没有重写 `InternalWaitForAsyncComplete`，调用即抛 `NotImplementedException`（首轮失败暴露）。**首次修正**改为跳过 destroy、依赖 `ResourcePackage.InitializeAsync` 重建：再次失败，因为 `CheckInitializeParameters`（line 154）在重建路径前先检查 `_isInitialize` 直接抛 `ResourcePackage is initialized yet.`（次轮失败暴露）。**最终修正**：`PlayModeTestBase.TeardownResourceManagerAsync()` 反射读 `ResourceManager._packages` → 对每个 package **异步** `await package.DestroyAsync().Task`（`AsyncOperationBase.Task` 是合法 awaitable，绕开同步 wait 的坑） → `YooAssets.RemovePackage` → `YooAssets.Destroy()` 完整反初始化。**生产 API 仍零改动**。
- [~] 1.6 编译已通过：`unity-compile-check` 经 Unity Skills (port 8091) `debug_force_recompile` + `debug_get_errors` 返回 0 错误；`test_list testMode=PlayMode filter=BootstrapTest` 列出 2 个 `Runnable` 测试，证明 asmdef 被识别为 PlayMode 测试 assembly。**待用户在 `Window > General > Test Runner > PlayMode` 标签手动 Run 一次 BootstrapTest 以确认运行时表现**（Unity Skills 1.8.2 的 `test_run` for PlayMode 未回流结果，怀疑是版本限制；不阻塞后续 fixture 落地）。

## 2. ResourceManager PlayMode 套件

- [x] 2.1 新增 `Tests/PlayMode/ResourceManagerPlayModeTests.cs`，覆盖契约 5 个 Scenario：`Init_FirstInitializeReachesReadyState` / `Load_AsyncLoadGameObjectPrefabSucceeds` / `Release_HandleIsRemovedFromTrackingAfterRelease` / `Init_RepeatedInitializeIsIdempotent` / `Load_ConcurrentLoadsForSameLocationBothSucceed`。
- [x] 2.2 prefab fixture 复用 `Assets/AssetRaw/Prefabs/GamePlay/GamePlay_CardItem.prefab`（YooAsset location: `GamePlay_CardItem`，AddressByFileName 规则）；零新增资源。
- [~] 2.3 编译验证经 Unity Skills 通过（0 错误），`test_list` 列出 5 个 ResourceManager 测试 + 2 个 Bootstrap 测试均为 Runnable。**Unity Test Runner > PlayMode 实际运行结果留待全套 fixture 完成后统一在 T9.2 跑。**

## 3. SceneManager PlayMode 套件

- [x] 3.1 fixture 选用 `Assets/AssetRaw/Scene/Game.unity`（YooAsset location: `Game`），无需新建。
- [x] 3.2 新增 `Tests/PlayMode/SceneManagerPlayModeTests.cs`，3 个 Scenario：`Load_AdditiveSceneIsRegisteredInUnity` / `Unload_ClearsCurrentSceneAndUnityState` / `Load_Unload_RepeatedThreeTimesUnityStateRemainsClean`。**生产代码发现**：`EF.Scene.ISceneManager` 未继承 `IEFManager`、且 `SceneManager.UnloadSceneAsync` 直接调 `sceneHandle.UnloadAsync()` 跳过 `ResourceManager.UnloadScene` 导致内部 `_trackedHandles` 残留，已在 spec.md 备注，不在本变更修复。
- [~] 3.3 编译验证经 Unity Skills 通过；`test_list` 列出 10 个 PlayMode 测试均 Runnable。实际运行结果合并 T9.2 跑。

## 4. EntityManager PlayMode 套件

- [x] 4.1 新增 `Tests/PlayMode/EntityManagerPlayModeTests.cs`，3 个 Scenario：`Spawn_RealPrefabProducesActiveGameObject` / `Recycle_SubsequentSpawnReusesPooledEntity` / `BatchRecycle_AcrossFramesEntityCountReturnsToZero`；按生产 GameEntry 的顺序组装 ObjectPoolManager + EntityManager + DefaultEntityHelper。
- [x] 4.2 prefab fixture 复用 `Assets/AssetRaw/Prefabs/GamePlay/GamePlay_CardItem.prefab`（YooAsset location: `GamePlay_CardItem`）。
- [~] 4.3 编译验证经 Unity Skills 通过；`test_list` 列出 13 个 PlayMode 测试均 Runnable。实际运行结果合并 T9.2 跑。

## 5. UIManager 测试 — **已剥离**

- [x] 5.1 实施期发现 UI 系统已迁移到 UI Toolkit (`Navigator` + `Shell` + `Screen<TViewModel>` + `ReactiveProperty`)，原 spec 基于过时的 `UIManager / UIView / UIController / OpenWindowAsync / 4 层` 设计完全错位；剥离为后续单独变更 `add-uitoolkit-playmode-tests`，本变更不再覆盖。proposal.md / design.md / spec.md 已同步删除对应 Requirement。

## 6. SoundManager 测试 — **已剥离**

- [x] 6.1 项目内当前不存在任何 `.wav/.mp3/.ogg` 资源，写全 `[Ignore]` fixture 占位收益极低，剥离待资源补齐后另开变更覆盖。proposal.md / design.md / spec.md 已同步删除对应 Requirement。

## 7. UniTask + Timer PlayMode 套件

- [x] 7.1 新增 `Tests/PlayMode/UniTaskFrameDrivenTests.cs`，3 个 Scenario：`UniTaskYield_AdvancesAtLeastOneFrame` / `UniTaskDelay_ConsumesRealTime` / `TimerManager_ScheduleOnceFiresWithinExpectedWindow`。Timer 测试自己每帧调 `ModuleSystem.Update(...)` 模拟生产 GameEntry MonoBehaviour 的更新链路。
- [~] 7.2 编译验证经 Unity Skills 通过；`test_list` 列出 16 个 PlayMode 测试均 Runnable。实际运行结果合并 T9.2 跑。

## 8. 文档同步

- [x] 8.1 新建 `Assets/GameScripts/HotFix/GameLogic/Tests/README.md`，覆盖 EditMode + PlayMode 两层结构、asmdef 约束、`PlayModeTestBase` 用法、fixture 资源（`GamePlay_CardItem` / `Game.unity`）、Unity Test Runner 与 Unity Skills 触发方式、生产代码已知不一致备注、UI/Sound 剥离说明。
- [x] 8.2 更新 `CLAUDE.md` "构建与测试" 段落：分明 EditMode/PlayMode 两层；新增第 4 条"PlayMode 测试触发（仅本地）"小节；明确不在 CI 范围。

## 9. 全套验证

- [x] 9.1 编译验证：经 Unity Skills `debug_force_recompile` + `debug_get_errors` 反复跑通；`test_list testMode=PlayMode` 列出全部 16 个测试均 Runnable。
- [x] 9.2 用户在 Unity Test Runner > PlayMode 实际跑通 17 个测试（含本轮新增 `Bootstrap_TwoConsecutiveTestsHaveNoStateLeak` 的 `YooAssetsInitializedAtSetUpStart` 断言），全绿；中间经历两轮失败修复（YooAsset DestroyOperation 同步 wait NotImplementedException → 异步 await `package.DestroyAsync().Task`；`System.Progress<float>` 异步派发导致 progress 断言 0f → 改用同步 `CapturingProgress`）。
- [x] 9.3 生产代码差异核查：`git diff` 显示 `Assets/EF/EFRuntime`、`Assets/GameScripts/HotFix/GameLogic/!(Tests)`、`Assets/GameScripts/Runtime` 全部无改动；CLAUDE.md 修改属于本变更 T8.2；`UnityProject.slnx` 改动是 Unity 自动同步新 PlayMode 项目；3 个 `.uxml` (CardItem/GameView/Root) 改动是 Unity UI Builder 自动重写（GUID 引用替换 + editor-extension-mode），与本变更无关。
- [x] 9.4 `/opsx:verify add-playmode-tests` 已跑：7/7 Requirement 覆盖、21/21 Scenario 对应到测试代码或结构性约束；唯一 WARNING（spec 第二轮加强后 BootstrapTest 未跟上 YooAssets.Initialized==false 断言）已在本轮回填修复并重编译验证。
