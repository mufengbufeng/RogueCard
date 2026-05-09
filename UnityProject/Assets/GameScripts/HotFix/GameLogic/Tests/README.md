# GameLogic Tests

本目录托管 RogueCard 项目的自动化测试，分为 EditMode 与 PlayMode 两层。

## 目录结构

```
Tests/
├── EditMode/                 GameLogic.Tests.EditMode.asmdef（Editor-only）
│   ├── Framework/            纯逻辑模块的单元测试 fixture
│   └── *.cs
└── PlayMode/                 GameLogic.Tests.PlayMode.asmdef（UNITY_INCLUDE_TESTS 约束）
    ├── Framework/
    │   └── PlayModeTestBase.cs   PlayMode 通用基类
    ├── BootstrapTest.cs          基类与隔离机制冒烟
    ├── ResourceManagerPlayModeTests.cs
    ├── SceneManagerPlayModeTests.cs
    ├── EntityManagerPlayModeTests.cs
    └── UniTaskFrameDrivenTests.cs
```

## EditMode 测试

针对纯逻辑模块（FSM / Model / ObjectPool / ReactiveProperty / EventChannel / Save / Region / Screen 生命周期等）编写。`asmdef` 配置：`includePlatforms=["Editor"]` + `defineConstraints=["UNITY_INCLUDE_TESTS"]`，全部使用 mock，不进行任何 IO / 资源加载，运行极快。

触发方式：

- Unity 编辑器：`Window > General > Test Runner > EditMode` 标签 → Run All
- 命令行（Unity 未打开）：`"D:\DocApp\UnityEditor\6000.3.12f1\Editor\Unity.exe" -batchmode -quit -runTests -testPlatform EditMode -testResults results.xml -projectPath .`

## PlayMode 测试

覆盖 EF 框架运行时基础设施在真实 PlayerLoop 下的可观察契约：

| 模块 | Fixture | 覆盖契约 |
| --- | --- | --- |
| Bootstrap | `BootstrapTest.cs` | 基类隔离 / ModuleSystem 干净启动 |
| ResourceManager | `ResourceManagerPlayModeTests.cs` | YooAsset EditorSimulate 初始化 / 异步加载 / 句柄释放 / 重复初始化幂等 / 并发加载 |
| SceneManager | `SceneManagerPlayModeTests.cs` | 异步加载 / 卸载 / 连续切换的 Unity 侧状态干净 |
| EntityManager | `EntityManagerPlayModeTests.cs` | 真实 prefab Spawn / Recycle 命中对象池 / 跨帧批量回收 |
| UniTask + Timer | `UniTaskFrameDrivenTests.cs` | UniTask.Yield 跨帧 / UniTask.Delay 真实时间 / TimerManager 真实帧驱动 |

### asmdef 关键约束

`Assets/GameScripts/HotFix/GameLogic/Tests/PlayMode/GameLogic.Tests.PlayMode.asmdef`：

- `includePlatforms`: `[]`（**所有平台**，不能限制为 `["Editor"]`，否则 Test Runner 会把它识别为 EditMode 测试）
- `defineConstraints`: `["UNITY_INCLUDE_TESTS"]`（仅在测试编译期编译，不进入 Player）
- `references`: GameLogic / GameProto / EF.Runtime / UniTask / YooAsset / UnityEngine.TestRunner / UnityEditor.TestRunner（GUID 引用）
- `precompiledReferences`: `["nunit.framework.dll"]`
- `autoReferenced`: `false`，`overrideReferences`: `true`

### PlayModeTestBase 用法

所有 PlayMode 测试继承 `PlayModeTestBase`。基类负责：

1. `[UnitySetUp]`：抓取入场前 ModuleSystem 注册数 → `ModuleSystem.ShutdownAll()` 清空残留 → 创建测试根 `TestRoot` (`DontDestroyOnLoad`) → 克隆生产 `EFResourceModeConfig` 强制 EditorSimulate 模式 → `await ResourceManager.InitializeAsync(...)`。
2. `[UnityTearDown]`：调用子类 `OnTearDownAsync` → `Unregister<IResourceManager>(shutdown: false)`（**不调 ResourceManager.Shutdown**，因 YooAsset 2.3.18 的 `DestroyOperation.WaitForAsyncComplete()` 抛 `NotImplementedException`） → `Resource.ReleaseAll()` → 反射读 `_packages` 异步 `await package.DestroyAsync().Task` 逐个销毁 → `YooAssets.RemovePackage` → `YooAssets.Destroy()` → `ModuleSystem.ShutdownAll()` → 销毁 `TestRoot` → `UniTask.Yield()` 推进一帧。

子类钩子：

- `protected override UniTask OnSetUpAsync()`：注册其他被测模块（SceneManager / EntityManager / TimerManager 等）。
- `protected override UniTask OnTearDownAsync()`：可选额外清理。

工具方法：

- `protected UniTask FrameDelay(int frames)`：跨帧推进。
- `protected UniTask<AssetHandle> LoadFixtureAsync<T>(string location)`：薄封装 `Resource.LoadAssetAsync<T>`。
- `protected void AssertNoLeakedHandles()`：断言 ResourceManager 内部 `_trackedHandles` 已清空。

### 测试用资源 fixture

复用 `Assets/AssetRaw/` 下既有资源，**零新增专用 prefab/scene**：

- GameObject prefab：`Assets/AssetRaw/Prefabs/GamePlay/GamePlay_CardItem.prefab`，YooAsset location `GamePlay_CardItem`（`AddressByFileName` 规则）。
- 场景：`Assets/AssetRaw/Scene/Game.unity`，YooAsset location `Game`，**测试中务必使用 `LoadSceneMode.Additive`**，否则会把 PlayMode Test Runner 自身的 InitTestScene 卸载掉。

### 触发方式

#### 1. Unity 编辑器手动触发（推荐）

`Window > General > Test Runner > PlayMode` 标签 → 选中要跑的 fixture → Run。每个测试 1~3 秒，全套约 30~60 秒（首次会跑一遍 `EditorSimulateModeHelper.SimulateBuild`）。

#### 2. Unity Skills（已开启服务时）

```bash
# 列出所有 PlayMode 测试
curl -s -X POST -H "Content-Type: application/json" -H "X-Agent-Id: ClaudeCode" \
  -d '{"testMode":"PlayMode"}' http://localhost:8091/skill/test_list

# 运行（filter 可按 fullName 或类名过滤）
curl -s -X POST -H "Content-Type: application/json" -H "X-Agent-Id: ClaudeCode" \
  -d '{"testMode":"PlayMode","filter":"BootstrapTest"}' http://localhost:8091/skill/test_run
```

注意：当前 Unity Skills 1.8.2 的 `test_run` 对 PlayMode 测试结果回流不稳定，跑结果建议看 Unity Test Runner 面板。

### 编译验证（不跑测试）

```bash
python .claude/skills/unity-compile-check/scripts/unity_compile_check.py
```

脚本会优先通过 Unity Skills `debug_force_recompile` + `debug_get_errors` 验证；不可用时回退 `dotnet build UnityProject.slnx`。

### 已知限制 / 不在范围内

- **不覆盖 UI Toolkit Navigator/Screen**：UI 系统已迁移到 `Navigator` + `Shell` + `Screen<TViewModel>` + `ReactiveProperty`，需要在测试 assembly 里定义最小 `TestScreen + TestViewModel + .uxml` fixture。剥离为后续 `add-uitoolkit-playmode-tests` 变更。
- **不覆盖 SoundManager**：项目内当前不存在 `.wav/.mp3/.ogg` 资源；待资源补齐后另开变更覆盖。
- **不在 CI 跑**：本期只支持本地手动跑；CI batchmode 接入留作单独变更。
- **不覆盖 HybridCLR / Procedure / 端到端流程**：依赖热更元数据状态，过于脆弱。
- **生产代码已知不一致（不在本变更修复）**：
  - `EF.Scene.ISceneManager` 未继承 `IEFManager`，测试以具体类型 `SceneManager` 注册到 ModuleSystem。
  - `SceneManager.UnloadSceneAsync` 直接调 `sceneHandle.UnloadAsync()` 跳过 `ResourceManager.UnloadScene` 路径，导致 ResourceManager 内部 `_trackedHandles` 在每次场景 Unload 后残留 SceneHandle 引用，需要 Shutdown 时统一清理。
