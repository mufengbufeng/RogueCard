## Why

当前 `GameLogic.Tests.EditMode` 仅覆盖纯逻辑模块（FSM、Model、ObjectPool、ReactiveProperty 等），全部依赖 mock 与即时同步执行。EF 框架真正承载游戏运行的 **YooAsset 资源初始化、异步资源加载、句柄释放、场景切换、UIManager 真实异步开窗、EntityManager 对象池生命周期、UniTask 帧驱动、Timer 时间推进** 等运行时基础设施从未被自动化覆盖；任何回归只能等开发期手动启动游戏才能发现，反馈链路过长且不可重复。引入 PlayMode 测试可以在 Editor 内以最低成本搭建可复现的运行时验证闭环，把这些"只有进游戏才能发现"的 bug 提前到 PR 阶段。

## What Changes

- 新增 `Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/` 测试目录，与现有 `EditMode/` 平级，独立 `GameLogic.Tests.PlayMode.asmdef`（`UNITY_INCLUDE_TESTS` 约束、Editor-only、引用 `GameLogic / EF.Runtime / UniTask / YooAsset / nunit.framework`）。
- 新增 PlayMode 测试套件，按模块分组：
  - **Bootstrap 冒烟**：验证 ModuleSystem 注册/卸载、PlayMode 测试基础设施可用。
  - **ResourceManager**：YooAsset `EditorSimulate` 模式真实初始化、异步加载 prefab/资源、句柄释放、并发加载、重复初始化保护。
  - **SceneManager**：异步加载 / 卸载 / 切换场景；与 ResourceManager 协同。
  - **EntityManager**：真实 prefab Spawn / Recycle、对象池命中率、跨帧释放。
  - **UniTask + Timer**：PlayMode 下基于 PlayerLoop 的 `UniTask.Yield`、`UniTask.Delay`、Timer 真实时间推进。
- 引入测试隔离基础设施：`PlayModeTestBase` 抽象类统一处理 `[UnitySetUp] / [UnityTearDown]`、ModuleSystem 重置、YooAsset 销毁、`DontDestroyOnLoad` 残留清理、UniTask 异常吞抛策略。
- 在 `EF.Runtime` 增补极少量测试钩子（仅在 `UNITY_INCLUDE_TESTS` 下编译），用来强制重置 `ModuleSystem` 等全局状态；不改变生产 API。
- 文档：补充 `Assets/GameScripts/HotFix/GameLogic/Tests/README.md` PlayMode 章节；在 `CLAUDE.md` "构建与测试" 段落补 PlayMode 触发与 Unity Skills 联动说明。

**不在本次范围内**：
- HybridCLR DLL 加载链路与 GameEntry 启动流程（Editor 下 AOT 直连，PlayMode 测试覆盖意义有限，留作后续）。
- Procedure 流程切换 / 完整游戏逻辑层端到端测试（依赖热更元数据状态，过于脆弱）。
- CI batchmode 集成（先把套件打稳，CI 单独立项）。
- `OfflinePlay / HostPlay / WebPlay` YooAsset 模式（需打包产物，不在本期）。
- **UI Toolkit Navigator/Screen PlayMode 测试**：实施期发现 UI 系统已迁移到 `Navigator` + `Shell`（3 层）+ `Screen<TViewModel>` + `ReactiveProperty`，与最初基于旧 `UIManager / OpenWindowAsync` 的设计完全错位；此部分剥离为后续单独变更 `add-uitoolkit-playmode-tests`，需要先在测试 assembly 内定义最小 `TestScreen` + `TestViewModel` + `.uxml` fixture 才能合理覆盖。
- **SoundManager PlayMode 测试**：项目内当前不存在任何 `.wav/.mp3/.ogg` 资源；写一个全 `[Ignore]` 的 fixture 收益极低，待美术补齐音频或单独定 fixture 时再开新变更覆盖。

## Capabilities

### New Capabilities

- `playmode-test-suite`: 定义 RogueCard 项目 PlayMode 测试套件的组织约定、测试隔离机制、YooAsset/UniTask/UI/Entity 各模块的可观察验证契约，以及本地触发与诊断流程。

### Modified Capabilities

<!-- 本次变更不修改任何已有 capability 的对外行为契约；仅新增 ModuleSystem 测试钩子在 UNITY_INCLUDE_TESTS 下生效，不影响生产规约。 -->

## Impact

- **新增代码**：
  - `Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/`（asmdef + Framework/PlayModeTestBase.cs + 各模块测试 fixtures）。
- **新增测试钩子（仅 UNITY_INCLUDE_TESTS）**：
  - `Assets/EF/EFRuntime/Module/ModuleSystem.cs`（或同位置 partial）增加 `internal static void ResetForTests()`。
  - 如确认 `ResourceManager` 销毁后 YooAsset 静态状态未完全清理，按需补 `DestroyYooAssetForTests()` 钩子（待 design.md 调研后定）。
- **依赖**：无新包；仅复用已有 `com.unity.ext.nunit 2.0.5`、`UniTask`、`YooAsset 2.3.18`。
- **资源**：复用 `Assets/AssetRaw/UI/Main/MainView.prefab` 等已存在资产作为 fixture，不新增专用测试 prefab，避免污染主资源目录。
- **构建**：测试 asmdef 仅 Editor 平台编译，不进入 Player；不影响 HybridCLR / 主包体积。
- **文档**：`CLAUDE.md` 测试章节、`Tests/README.md` 同步更新。
- **风险**：YooAsset 全局静态状态在多用例间残留，可能导致测试相互污染 → 由 `PlayModeTestBase` 统一处置；设计细节进 design.md。
