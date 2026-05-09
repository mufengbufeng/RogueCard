## ADDED Requirements

### Requirement: PlayMode 测试套件位置与 asmdef 约束

PlayMode 测试套件 SHALL 位于 `Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/`，与现有 `EditMode/` 目录平级，并使用独立的 `GameLogic.Tests.PlayMode.asmdef` 程序集。该 asmdef MUST 满足：

- `includePlatforms` 为空数组 `[]`（所有平台），不能限制为 `Editor`，否则 Unity Test Runner 会将其识别为 EditMode 测试而非 PlayMode 测试。
- `defineConstraints` 包含 `UNITY_INCLUDE_TESTS`，确保仅在测试编译期编译，不进入正常 Player 构建。
- `precompiledReferences` 包含 `nunit.framework.dll`。
- `references` 至少包含 `GameLogic`、`GameProto`、`EF.Runtime`、`UniTask`、`YooAsset`、`UnityEngine.TestRunner`、`UnityEditor.TestRunner`。
- `autoReferenced` 为 `false`，`overrideReferences` 为 `true`。

#### Scenario: 编译时不进入 Player 构建

- **WHEN** 执行 Player 构建（任意非 Editor 平台），且未定义 `UNITY_INCLUDE_TESTS`
- **THEN** `GameLogic.Tests.PlayMode.asmdef` 因 `defineConstraints = ["UNITY_INCLUDE_TESTS"]` 不被编译进 Player

#### Scenario: Unity Test Runner 识别为 PlayMode 测试

- **WHEN** 在 Unity 编辑器打开 `Window > General > Test Runner`
- **THEN** PlayMode 标签页（而非 EditMode 标签页）显示 `GameLogic.Tests.PlayMode` assembly 下的所有测试方法

### Requirement: PlayModeTestBase 测试隔离基类

所有 PlayMode 测试 SHALL 继承自统一的 `PlayModeTestBase`（位于 `Tests/PlayMode/Framework/PlayModeTestBase.cs`），由基类负责测试间隔离与基础环境组装。基类 MUST 提供：

- `[UnitySetUp] IEnumerator SetUp()` 入口，在每个测试前调用 `ModuleSystem.ShutdownAll()`、创建测试专用根 GameObject、初始化最小化 `ResourceManager`（`EditorSimulate` 模式 + `DefaultPackage` 单包），并按需注册 `UIManager / EntityManager / SoundManager / TimerManager` 等被测模块。
- `[UnityTearDown] IEnumerator TearDown()` 入口，在每个测试后：先把 `ResourceManager` 从 ModuleSystem 移出**而不调用其 Shutdown**（YooAsset v2.3.18 的 `DestroyOperation.WaitForAsyncComplete()` 抛 `NotImplementedException`）；调 `Resource.ReleaseAll()` 释放追踪句柄；通过反射读出 `ResourceManager._packages` 集合并对每个 package **异步** `await package.DestroyAsync().Task`（绕开同步 wait 的坑），随后 `YooAssets.RemovePackage` + `YooAssets.Destroy()` 完整反初始化；最后 `ModuleSystem.ShutdownAll()` 关闭剩余模块、摧毁测试根 GameObject、`yield return null` 推进一帧。
- `protected UniTask FrameDelay(int frames)` 工具方法。
- `protected UniTask<T> LoadFixtureAsync<T>(string location)` 工具方法（薄封装 `ResourceManager.LoadAssetAsync`）。
- `protected void AssertNoLeakedHandles()` 工具方法，断言资源句柄全部释放。

#### Scenario: 测试间 ModuleSystem 与 YooAsset 静态状态都干净

- **WHEN** 在同一个测试运行中先后执行两个独立的 PlayMode 测试 A、B
- **THEN** 测试 B 的 `[UnitySetUp]` 开始时 `ModuleSystem.RegisteredServiceCount` 为 0
- **AND** 测试 B 的 `[UnitySetUp]` 开始时 `YooAssets.Initialized` 为 `false`，没有残留 Package（由测试基类 TearDown 异步 destroy + `YooAssets.Destroy()` 保证）

#### Scenario: 测试 GameObject 不残留至下一用例

- **WHEN** 测试 A 结束后 `[UnityTearDown]` 完成
- **THEN** 测试 A 通过基类 root 创建的所有 GameObject 已被销毁，`DontDestroyOnLoad` 场景中无残留

### Requirement: ResourceManager PlayMode 验证契约

PlayMode 套件 SHALL 在真实 YooAsset `EditorSimulate` 模式下验证 `ResourceManager` 的核心异步契约。最小覆盖：初始化、资源加载、句柄释放、并发加载、重复初始化幂等。

#### Scenario: 首次初始化进入 ready 状态

- **WHEN** 调用 `ResourceManager.InitializeAsync` 并使用最小化 `ResourceModeConfig`（单包 `DefaultPackage`、`EditorSimulate`）
- **THEN** `IsInitialized` 为 `true` 且 `Mode` 为 `EditorSimulate` 且 `DefaultPackageName` 为 `"DefaultPackage"`

#### Scenario: 异步加载 prefab 资源

- **WHEN** `ResourceManager` 已初始化后异步加载 `Assets/AssetRaw/Prefabs/GamePlay/GamePlay_CardItem.prefab`（YooAsset location key 为 `GamePlay_CardItem`，遵循 collector 的 AddressByFileName 规则）
- **THEN** 返回的 `GameObject` 不为 `null`，可作为 `Instantiate` 的源对象

#### Scenario: 句柄释放后引用归零

- **WHEN** 加载并使用资源后调用对应释放 API
- **THEN** `ResourceManager` 内部跟踪的句柄计数归零，`AssertNoLeakedHandles()` 通过

#### Scenario: 重复初始化幂等

- **WHEN** 在已初始化的状态下再次调用 `InitializeAsync`
- **THEN** 调用立即返回成功，进度回调（若提供）报告 `1f`，且不抛异常

#### Scenario: 并发加载相同资源

- **WHEN** 同一资源 location 被并发触发两次 `LoadAssetAsync`
- **THEN** 两次调用都成功返回有效对象，且 YooAsset 句柄计数与 `ResourceManager` 内部账本一致

### Requirement: SceneManager PlayMode 验证契约

PlayMode 套件 SHALL 验证 `SceneManager` 在真实 YooAsset 场景资源上的异步加载与卸载契约。

#### Scenario: 异步加载场景成功

- **WHEN** 调用 `SceneManager.LoadSceneAsync` 加载一个被 EditorSimulate 模拟构建覆盖的场景
- **THEN** Unity `SceneManagement.SceneManager` 中可枚举到该场景，且其 `isLoaded` 为 `true`

#### Scenario: 异步卸载场景释放资源

- **WHEN** 已加载的场景调用对应卸载 API
- **THEN** Unity `SceneManagement.SceneManager` 中不再包含该场景，对应 YooAsset 场景句柄释放

#### Scenario: 连续切换场景 Unity 侧状态保持干净

- **WHEN** 在同一个测试中连续 3 次"加载 → 卸载"同一场景
- **THEN** 每轮卸载后 `SceneManager.CurrentScene` 为 null，且 `UnityEngine.SceneManagement.SceneManager.GetSceneByName(...)` 不返回 `isLoaded == true` 的同名场景
- **NOTE** 不要求 `ResourceManager._trackedHandles` 在每轮立即归零：当前生产实现 `SceneManager.UnloadSceneAsync` 直接调 `sceneHandle.UnloadAsync()` 而未走 `ResourceManager.UnloadScene` 路径，导致 ResourceManager 内部账本里 SceneHandle 引用要等 Shutdown 时 ReleaseAll 才被清理。该不一致是 EF 既有问题，不在本变更修复范围内

### Requirement: EntityManager PlayMode 验证契约

PlayMode 套件 SHALL 验证 `EntityManager` 在真实 YooAsset 资源上的对象池生命周期。

#### Scenario: Spawn 实体使用真实 prefab

- **WHEN** 调用 `EntityManager.Spawn`（或等价 API）传入一个被 EditorSimulate 覆盖的 prefab location
- **THEN** 返回非 null 的 GameObject 实例，且其在场景层级中可见

#### Scenario: Recycle 后再次 Spawn 命中对象池

- **WHEN** 一个实体 Spawn 后被 Recycle，再次以相同参数 Spawn
- **THEN** 返回的实例与第一次 Spawn 的实例引用相同（即对象池命中），未触发新的资源加载

#### Scenario: 跨帧批量回收

- **WHEN** 在一帧内 Spawn 5 个实体，下一帧全部 Recycle
- **THEN** 第三帧检查时所有实例已脱离场景层级，且 `EntityManager` 内部活跃实体计数为 0

### Requirement: UniTask 与 Timer PlayMode 帧驱动验证契约

PlayMode 套件 SHALL 验证 `UniTask` 与 `TimerManager` 在 PlayMode `PlayerLoop` 上的真实跨帧行为，区别于 EditMode 的同步即时执行。

#### Scenario: UniTask.Yield 跨帧推进

- **WHEN** 在测试中 `await UniTask.Yield(PlayerLoopTiming.Update)`
- **THEN** `Time.frameCount` 至少前进 1

#### Scenario: UniTask.Delay 真实时间消耗

- **WHEN** 在测试中 `await UniTask.Delay(TimeSpan.FromMilliseconds(200))`
- **THEN** `Time.realtimeSinceStartup` 实际推进不少于 0.18 秒（容许 10% 抖动）

#### Scenario: Timer 在真实帧驱动下触发回调

- **WHEN** 通过 `TimerManager` 注册一个 0.1 秒后触发的一次性回调，并允许测试 `await` 0.2 秒
- **THEN** 回调恰好被调用一次

### Requirement: 文档同步

`Assets/GameScripts/HotFix/GameLogic/Tests/README.md` SHALL 包含 PlayMode 测试章节，说明：测试目录、asmdef 约束、`PlayModeTestBase` 用法、本地触发方式（Unity Test Runner > PlayMode 与 Unity Skills）、新增 fixture 的步骤约定。`CLAUDE.md` 的"构建与测试"段落 SHALL 在保留既有 EditMode 指引的前提下补充 PlayMode 触发与 Unity Skills 联动说明。

#### Scenario: 文档可指引新增 fixture

- **WHEN** 开发者首次为新模块编写 PlayMode 测试，参照 `Tests/README.md` 的 PlayMode 章节
- **THEN** 文档明确指出"继承 `PlayModeTestBase`、使用 `[UnityTest] + UniTask.ToCoroutine`、复用既有资源 fixture"，无需阅读源码即可完成第一份测试

#### Scenario: CLAUDE.md 指引覆盖 PlayMode

- **WHEN** 在 CLAUDE.md "构建与测试" 段落查找如何运行 PlayMode 测试
- **THEN** 该段落明确列出 Unity Test Runner > PlayMode 与 Unity Skills 两种触发路径，并提示"PlayMode 测试不在当前 CI 范围"
