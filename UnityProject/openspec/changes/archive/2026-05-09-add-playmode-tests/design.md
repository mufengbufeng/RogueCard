## Context

RogueCard 现有自动化测试只有 `GameLogic.Tests.EditMode`（13 个 fixture，全 Editor、纯逻辑、mock 优先），从未在 PlayMode 下验证过 EF 框架的运行时基础设施。`ResourceManager`（YooAsset 2.3.18）/ `SceneManager` / `UIManager` / `EntityManager` / `SoundManager` / UniTask + Timer 的真实异步行为目前只有"进游戏手测"一条验证路径，回归发现极慢，多个模块出 bug 时还互相掩盖。

技术栈现状关键点：
- `ModuleSystem`（静态服务定位器）已提供 `ShutdownAll()`，能在测试间彻底重置注册表，无需额外测试钩子。
- `ResourceManager.InitializeAsync` 直接调 `YooAssets.Initialize()`，并通过 `Resources.Load<ResourceModeConfig>("EF/ResourceModeConfig")` 读取默认配置。YooAsset 是全局静态状态，跨用例需要谨慎销毁。
- 现有 `ResourceModeConfig` 默认包名是 `DefaultPackage`，`Mode` 默认 `EditorSimulate`，与 `Assets/AssetRaw/` 下的资源管线一致。
- HotFix 程序集 `GameLogic` 是热更新代码，但在 Editor 下没有 HybridCLR DLL 加载步骤，编辑器直接 AOT 引用，PlayMode 测试可以直接 `[Reference]` `GameLogic` asmdef。
- Packages 已经包含 `com.unity.ext.nunit 2.0.5` 和 `UniTask`，无新依赖。

## Goals / Non-Goals

**Goals:**
- 在 Editor 内可重复运行的 PlayMode 测试套件，覆盖 EF 框架运行时基础设施 + UI/Entity 集成的核心可观察行为。
- 测试套件每次运行从干净状态开始（ModuleSystem 清空、YooAsset 反初始化、`DontDestroyOnLoad` 残留清理）。
- 单一通用基类 `PlayModeTestBase` 屏蔽样板代码；新增模块测试只需继承并实现具体场景。
- YooAsset 仅使用 `EditorSimulate` 模式，零额外构建步骤；本地 Unity Test Runner > PlayMode 一键跑。
- 复用 `Assets/AssetRaw/` 下既有资源做 fixture，不污染主资源目录。

**Non-Goals:**
- 不覆盖 HybridCLR DLL 加载链路（Editor 下走 AOT 直连）。
- 不覆盖 Procedure/InitProcedure → 主菜单 → 关卡 的完整端到端流程（依赖热更元数据状态，过于脆弱，留给后续 e2e 变更）。
- 不在 batchmode/CI 跑（CI 入口单独立项）。
- 不覆盖 `OfflinePlay / HostPlay / WebPlay` YooAsset 模式。
- 不为测试新建专用 prefab/asset，避免污染资源管线。
- **不覆盖 UI Toolkit Navigator/Screen 系统**：实施期发现 UI 已彻底从旧 `UIManager + UIView/UIController` 迁移到 `Navigator (NavigateToAsync / PushPopupAsync / PopPopup)` + `Shell` (`ScreenLayer / PopupLayer / SystemLayer`) + `Screen<TViewModel>` + `ReactiveProperty`；测试这套需要在 assembly 内定义最小 `TestScreen + TestViewModel + .uxml` fixture，工作量与领域复杂度都和"运行时基础设施"差一个量级，剥离为后续 `add-uitoolkit-playmode-tests`。
- **不覆盖 SoundManager**：项目内目前无任何 `.wav/.mp3/.ogg` 资源，全 `[Ignore]` 占位无价值，待资源补齐后再开变更。

## Decisions

### 决策 1：测试 asmdef 放在 `Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/`，与 EditMode 平级

**为什么**：
- 与既有 `Tests/EditMode/` 对称、最易发现，符合 CLAUDE.md "约定" 一致。
- 物理上属于 HotFix 区，但 asmdef `includePlatforms = ["Editor"]` + `defineConstraints = ["UNITY_INCLUDE_TESTS"]`，永远不会进 Player 包，也不会被 HybridCLR 当成热更新代码处理。
- 引用 `GameLogic / GameProto / EF.Runtime / EGF / UniTask / YooAsset / nunit.framework`，能在 Editor 下完整组装一个真实运行时。

**对比的替代方案**：
- 放 `Assets/Tests/PlayMode/`（Unity 默认推荐）：物理与 EditMode 不对称，跨目录维护成本上升 → 弃。
- 放 `Assets/GameScripts/Runtime/Tests/`：Runtime 是 AOT 区且语义是"和 Player 一起发布"，引用热更新代码不合理 → 弃。

### 决策 2：测试隔离用现有 `ModuleSystem.ShutdownAll()`，**不新增**测试钩子

**为什么**：
- `ModuleSystem.ShutdownAll()` 已经"清空 _services / _aliases / _scopes / _updateOrder + 倒序调 Shutdown"，行为正合测试重置需求。
- `ResourceManager.Shutdown()`（待 design.md 阶段去 Resource 模块代码确认）会反初始化 YooAsset（destroy package + clear handles）；如果不完整，再补，**只在确认必要时**才加测试钩子，不为"或许有用"提前埋坑。
- 减少生产代码侵入面，proposal 里"按需补 `DestroyYooAssetForTests()`"的话留作 design.md 调研结论：**不预先增加**，只在 T1 冒烟测试发现状态残留时才加。

**对比的替代方案**：
- 新增 `ModuleSystem.ResetForTests()`（仅 `UNITY_INCLUDE_TESTS` 编译）：与 `ShutdownAll` 行为重叠，无新价值 → 弃。
- 反射清空内部字典：脆弱、与重构强耦合 → 弃。

### 决策 3：`PlayModeTestBase` 用 `[UnitySetUp]` + `IEnumerator` + `UniTask.ToCoroutine()`

**为什么**：
- Unity Test Framework 的 `[UnitySetUp] IEnumerator SetUp()` 是 PlayMode 下唯一支持跨帧初始化的入口；NUnit `[SetUp]` 不能 `yield return`。
- `UniTask.ToCoroutine()` 将 `async UniTask` 平滑转成 `IEnumerator`，避免在每个测试里手写状态机。
- `[UnityTest] public IEnumerator XxxTest() => UniTask.ToCoroutine(async () => { ... })` 成为统一测试形态，可读性最高。

**`PlayModeTestBase` 责任清单**（基类一次性处理）：
1. `[UnitySetUp]` 阶段：
   - `ModuleSystem.ShutdownAll()` 清空残余。
   - 创建一个测试专用 `GameObject root`（带 `DontDestroyOnLoad`），所有测试 SpawnObject 都挂到它下面，便于 TearDown 一次摧毁。
   - 注册并 `await InitializeAsync` 一个 `ResourceManager`（用代码内构造的最小 `ResourceModeConfig`，单包 `DefaultPackage` + `EditorSimulate`，不依赖 Resources 资产，避免污染默认配置加载路径）。
   - 注册其他被测模块（`UIManager / EntityManager / SoundManager / TimerManager`）。
2. `[UnityTearDown]` 阶段：
   - 调 `ModuleSystem.ShutdownAll()` 让各模块自洁。
   - 摧毁 `root` GameObject（连同所有 child、`DontDestroyOnLoad` 残留）。
   - 调 `YooAssets.Destroy()`（如果该 API 存在；不存在则单独 destroy each Package）以彻底反初始化 YooAsset。
   - 强制一帧 `yield return null` 让 `OnDestroy` 完成，保证下一个测试干净启动。
3. 工具方法：
   - `protected UniTask FrameDelay(int frames = 1)`：跨帧推进辅助。
   - `protected UniTask<T> LoadFixtureAsync<T>(string location)`：薄封装 `ResourceManager.LoadAssetAsync<T>`，统一句柄登记。
   - `protected void AssertNoLeakedHandles()`：断言 `_trackedHandles.Count == 0`，捕获句柄泄漏。

### 决策 4：测试 fixture 资源策略 — 复用 `Assets/AssetRaw/Prefabs/GamePlay/GamePlay_CardItem.prefab`（YooAsset location = `GamePlay_CardItem`，AddressByFileName 规则）

**为什么**：
- 该 prefab 已稳定在主资源管线中，被 EditorSimulate 模拟构建覆盖。
- 若引入"测试专用 prefab"会污染主资源管线，后续美术 / 策划误删时无人察觉。
- ResourceManager / EntityManager 的 PlayMode 测试需要"存在的 GameObject prefab"，`GamePlay_CardItem.prefab` 完美匹配。
- 注意：`Assets/AssetRaw/UI/Main/MainView` 在 UI Toolkit 迁移后已变为 `VisualTreeAsset (.uxml)` 而非 GameObject prefab；UIManager 测试若需 prefab 也使用 `GamePlay_CardItem`，需要 VisualTreeAsset 时再单独取 location `MainView`。
- 测试用例只验证 **生命周期与异步契约**，不依赖 prefab 业务字段，对资产稳定性要求低。

**对比的替代方案**：
- 新增 `Assets/AssetRaw/Tests/PlayModeFixture.prefab` 等专用资源：增加维护面，且后续要写到 YooAsset 收集器规则，得不偿失 → 弃。

### 决策 5：覆盖范围只到模块"可观察契约"，不深挖内部实现

**为什么**：
- PlayMode 测试比 EditMode 慢（每个 fixture 至少几秒），写细到内部实现会让套件慢且脆。
- 框架内部细节由 EditMode 覆盖（已存在），PlayMode 专注 "异步、资源、生命周期、帧驱动" 这些 EditMode 测不到的东西。
- 每个模块给 3-6 个核心场景即可，未来若发现具体回归再针对性加。

具体覆盖契约（详见 `specs/playmode-test-suite/spec.md` 的 Scenarios）：

| 模块             | 覆盖契约                                                                                  |
| ---------------- | ----------------------------------------------------------------------------------------- |
| Bootstrap        | 测试基础设施可启动，`ModuleSystem` 干净启动 / 清理                                        |
| ResourceManager  | EditorSimulate 初始化、`LoadAssetAsync<GameObject>` 成功、句柄释放、并发加载、重复初始化  |
| SceneManager     | `LoadSceneAsync` 成功并真实激活、`UnloadSceneAsync` 清理、连续切换不泄漏                  |
| UIManager        | `OpenWindowAsync` 真实异步打开、Layer 落在正确层、关闭后释放、Model 绑定可观察            |
| EntityManager    | Spawn 出真实 GameObject、Recycle 后池命中、跨帧批量回收                                   |
| SoundManager     | 加载音频资源、Play / Stop 生命周期、不泄漏 AudioSource                                    |
| UniTask + Timer  | `UniTask.Yield(PlayerLoopTiming.Update)` 跨帧、`UniTask.Delay` 真实时间、Timer Tick 触发  |

## Risks / Trade-offs

- **YooAsset 全局静态状态**：`YooAssets.Initialize()` 是进程级单例。若 TearDown 没调到 `YooAssets.Destroy()` 或仍有 Package 残留，下一个测试会因为重复初始化失败 → 由 `PlayModeTestBase.TearDown` 兜底；T1 冒烟测试专门验证"连跑两次都能成功"。
- **EditorSimulate 构建耗时**：`EditorSimulateModeHelper.SimulateBuild(...)` 每次 InitializeAsync 都会跑一次模拟构建，PlayMode 测试套件可能整体慢 30s-1min → Mitigation：基类只在 `[OneTimeSetUp]` 里跑一次模拟构建并缓存清单（如果框架允许）；如果不能缓存，接受首次跑慢，本地体验为主。
- **`DontDestroyOnLoad` 残留**：UIManager / EntityManager 创建的 root GameObject 默认进 `DontDestroyOnLoad`，测试若不清理会跨用例污染 → 基类的 `root` GameObject 是测试新建的，TearDown 强制 destroy；同时基类提供 `AssertNoDontDestroyOnLoadResidue()` 辅助断言关键测试可调用。
- **HotFix 程序集引用**：`GameLogic.Tests.PlayMode.asmdef` 引用 `GameLogic`，`GameLogic` 又引用 `GameProto` 等。若引用关系扩张引入循环依赖 → 通过 GUID 显式引用，且只在 Editor 平台编译，问题局限。
- **测试套件慢导致跳过倾向**：PlayMode 测试 > 5s 容易让人懒得跑 → 文档同时提供"按 fixture 单跑"的 Unity Test Runner 操作步骤，不强制全跑。
- **YooAsset 在 Editor 下的 SBP 锁**：`EditorSimulateModeHelper.SimulateBuild` 会调用 Scriptable Build Pipeline，多用例并行可能锁文件 → NUnit PlayMode 默认串行，无风险，但需要在 README 注明"不要开 ParallelizableAttribute"。
- **PlayMode 测试在 Unity Skills 自动模式下的副作用**：自动模式可能改场景；测试需要明确独立 Bootstrap 场景或代码内创建 → 基类用代码内创建 `GameObject` + 不依赖任何打开的场景。

## Migration Plan

无生产数据迁移。落地步骤（不展开实现细节，详见 `tasks.md`）：

1. 添加 `GameLogic.Tests.PlayMode.asmdef` 与 `PlayModeTestBase.cs` 骨架；用最小 `BootstrapTest` 验证测试基础设施能启动并干净退出。
2. 按模块逐个新增 fixture（顺序：Resource → Scene → Entity → UI → Sound → UniTask/Timer），每个 fixture 独立提交，便于回退。
3. 运行 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` 验证编译。
4. Unity Test Runner > PlayMode 全量跑通。
5. 更新 `Assets/GameScripts/HotFix/GameLogic/Tests/README.md`、`CLAUDE.md` 文档。

回滚：删除 `Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/` 整个目录即可，不影响生产代码。

## Open Questions（实施期已解决）

- ~~`YooAssets.Destroy()` 是否在 v2.3.18 中存在？~~ → **存在**，但 `package.DestroyAsync().WaitForAsyncComplete()`（同步等待）在所有模式下都抛 `NotImplementedException`（`AsyncOperationBase.cs:114`）— `DestroyOperation` 没有重写 `InternalWaitForAsyncComplete`。这是 EF 生产代码 `ResourceManager.Shutdown()` 第 411 行潜在的 bug（生产路径下 Shutdown 极少被调用，所以没暴露）。
- ~~`ResourceManager.Shutdown()` 是否已经包含 YooAsset 反初始化？~~ → 代码上**包含**，但运行时会因上面的 `WaitForAsyncComplete` 炸。**两次错误尝试的最终修正**：
  1. ❌ 首次尝试"完全跳过 destroy，让 YooAsset 静态状态跨测试保留"：失败。`ResourcePackage.InitializeAsync` 第 89 行（CheckInitializeParameters → line 154）发现 `_isInitialize == true` 直接抛 `ResourcePackage is initialized yet.`，重建 ResourceManager 的 fallback 路径在 line 91 之后才执行，根本走不到。
  2. ✅ 最终方案：`PlayModeTestBase.TeardownResourceManagerAsync()` 反射读 ResourceManager 内部 `_packages` 集合，**异步** `await package.DestroyAsync().Task`（`AsyncOperationBase.Task` 是合法 awaitable，绕开同步 wait 的坑），然后 `YooAssets.RemovePackage` + 最后 `YooAssets.Destroy()` 完整反初始化。下个 SetUp 干净启动。生产 API 仍然零改动。
- 测试 root GameObject 是否需要 `HideFlags.DontSaveInEditor` 以避免极少数情况被 Editor 序列化？默认先不加，T1 落地验证后决定。
- asmdef `includePlatforms` 设置：调研后明确不能用 `["Editor"]`（会被 Test Runner 识别为 EditMode 测试），必须用 `[]`（所有平台）+ `defineConstraints: ["UNITY_INCLUDE_TESTS"]` 阻止 Player 构建。spec.md 与 tasks.md 已同步更正。
