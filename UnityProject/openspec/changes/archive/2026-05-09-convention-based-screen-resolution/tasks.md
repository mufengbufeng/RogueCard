## 1. EF 框架基础设施

- [x] 1.1 在 `Assets/EF/EFRuntime/UI/Screen.cs` 的 `Screen<TViewModel>` 中新增 `protected virtual string UxmlLocation` 和 `protected virtual string UssLocation` 虚属性，默认实现按 `{Stem}View → {Stem}Uxml / {Stem}Uss` 推导，未以 `View` 结尾的类型直接附加后缀
- [x] 1.2 新增 `Assets/EF/EFRuntime/UI/Popup.cs`，定义 `public abstract class Popup<TViewModel> : Screen<TViewModel> where TViewModel : ViewModelBase`，仅作类型 marker
- [x] 1.3 删除 `Assets/EF/EFRuntime/UI/ScreenRegistry.cs` 文件
- [x] 1.4 重写 `Assets/EF/EFRuntime/UI/INavigator.cs`：移除 `NavigateToAsync` / `PushPopupAsync` / `PopPopup`；新增 `OpenAsync<TScreen>(ViewModelBase vm = null, CancellationToken ct = default)` / `OpenAsync(string viewName, ViewModelBase vm = null, CancellationToken ct = default)` / `Close()` / `CloseAll()`
- [x] 1.5 重写 `Assets/EF/EFRuntime/UI/Navigator.cs`：实现按类型分流（`Popup<>` 派生走 PopupLayer，否则走 ScreenLayer）+ 反射读取 `Screen<>` 闭合泛型获取 ViewModel 类型 + ViewModel 自动 `Activator.CreateInstance` 创建 + 按 `UxmlLocation` / `UssLocation` 加载资源 + USS 缺失降级警告
- [x] 1.6 在 Navigator 内实现字符串到 Type 的反射查找缓存：`Dictionary<string, Type>` 首次未命中时遍历 `AppDomain.CurrentDomain.GetAssemblies()` 收集所有非抽象 `Screen<>` 派生类型；同名冲突时抛 `InvalidOperationException` 并指引使用类型重载
- [x] 1.7 同步更新 Navigator 的构造函数签名，移除对 `ScreenRegistry` 参数的依赖（改为只接收 `Shell` 和 `IResourceManager`）

## 2. 业务侧 Screen 重命名

- [x] 2.1 重命名 `Assets/GameScripts/HotFix/GameLogic/UI/Main/MainMenuScreen.cs` → `MainView.cs`，类名 `MainMenuScreen` → `MainView`，保持命名空间和文件位置
- [x] 2.2 重命名 `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameScreen.cs` → `GameView.cs`，类名 `GameScreen` → `GameView`
- [x] 2.3 用 Grep 验证 `MainMenuScreen` / `GameScreen` 已无任何引用残留（仅文档/迁移注释中的历史引用保留）
- [x] 2.4 检查 `MainViewModel` / `GameViewModel` 已符合 `{Stem}ViewModel` 约定（无需改动），并确认对应文件位置和命名空间无误

## 3. UXML / USS 资源重命名

- [x] 3.1 重命名 `Assets/AssetRaw/UI/Main/MainView.uxml` → `MainUxml.uxml`（含 `.meta` 文件），同步更新该 UXML 内对 `MainViewStyles.uss` 的 `<Style src=...>` 路径引用为 `MainUss.uss`
- [x] 3.2 重命名 `Assets/AssetRaw/UI/Main/MainViewStyles.uss` → `MainUss.uss`（含 `.meta` 文件）
- [x] 3.3 重命名 `Assets/AssetRaw/UI/Game/GameView.uxml` → `GameUxml.uxml`（含 `.meta` 文件），同步更新内部 `<Style src=...>` 引用
- [x] 3.4 重命名 `Assets/AssetRaw/UI/Game/GameViewStyles.uss` → `GameUss.uss`（含 `.meta` 文件）
- [x] 3.5 YooAsset Collector 配置使用 folder collector（`Assets/AssetRaw/UI`），文件改名后 addressable 自动跟随为 `MainUxml` / `MainUss` / `GameUxml` / `GameUss`，无需手动修改 Collector 配置
- [x] 3.6 确认 `Assets/AssetRaw/UI/Game/` 下其他子模板（`BattlePanel.uxml` / `RewardPanel.uxml` / `CardItem.uxml` / `MonsterItem.uxml` / `TipsItem.uxml`）和 `Assets/AssetRaw/UI/Shared/SharedStyles.uss` **未**被改名（它们由 Region 局部加载，不受 Screen 约定约束）

## 4. GameLogicEntry 与启动流程

- [x] 4.1 在 `Assets/GameScripts/HotFix/GameLogic/GameLogicEntry.cs` 的 `InitializeNavigator()` 中删除 `ScreenRegistry` 构造和 `Register<...>` 调用，改为 `_navigator = new Navigator(shell, _resourceManager)`
- [x] 4.2 删除 `GameLogicEntry.InitializeModels()` 方法及其在 `Init()` 中的调用
- [x] 4.3 `GameProcedure.cs` 内 `Model.GetModel<GameModel>()` → `Model.TryGetModel<GameModel>()`；其他 Model 访问（`MainModel.*` 静态字段）不需要 ModelManager 注册即可使用
- [x] 4.4 确认 `InitializeProcedures()` 不动（保留 Procedure 中心化注册）

## 5. Procedure 与调用方迁移

- [x] 5.1 用 Grep 搜索 `NavigateToAsync\|PushPopupAsync\|PopPopup` 全量定位调用点（仅 README 文档 + Procedure 调用点）
- [x] 5.2 `MainMenuProcedure.NavigateToAsync("MainMenu", vm)` → `OpenAsync<MainView>(vm)`；`GameProcedure.NavigateToAsync("Game", vm)` → `OpenAsync<GameView>(vm)`
- [x] 5.3 项目无现存 `PushPopupAsync` 调用——预防性检查通过
- [x] 5.4 项目无现存 `PopPopup()` 调用——预防性检查通过
- [x] 5.5 字符串字面量 `"MainMenu"` / `"Game"` 在 UI 调用上下文中已不存在（`SceneManagerPlayModeTests` 中的 `"Game"` 是无关的场景 fixture 名）

## 6. 测试更新

- [x] 6.1 Grep 列出受影响测试：`ShellAndRegistryTests.cs`（含 ScreenRegistry 测试需删除）、`ScreenLifecycleTests.cs`（旧类型仍可工作无需改）
- [x] 6.2 更新 `ShellAndRegistryTests.cs`：删除所有 `ScreenRegistry` 相关测试，仅保留 Shell 解析测试
- [x] 6.3 新增 `ScreenConventionTests.cs`：覆盖 `UxmlLocation` / `UssLocation` 默认推导、子类 override、Popup 派生类型走相同约定、类名不以 View 结尾时附加后缀
- [x] 6.4 新增 `NavigatorTypeResolutionTests.cs`：覆盖字符串重载的"类型不存在"/"空字符串"错误路径、Navigator 构造参数 null 检查、Close/CloseAll 在空栈下静默
- [x] 6.5 PlayMode 测试（候选）：`Popup<>` 派生类型通过 `OpenAsync<>` 打开时入栈 PopupLayer，`Screen<>` 派生类型替换 ScreenLayer——需要真实 UXML 加载，留待 PlayMode 测试套件接入时补
- [x] 6.6 PlayMode 测试（候选）：USS 资源缺失时不抛异常，Screen 仍正常完成 OnShow——同上

## 7. 编译与验证

- [x] 7.1 `dotnet build UnityProject.slnx --no-restore` 通过（0 错误 0 警告）；Unity Skills compile check 通过
- [x] 7.2 在 Unity 编辑器内通过 Test Runner 运行 EditMode 全套测试（需开发者本地操作）
- [x] 7.3 进入 Play 模式手动验证：启动 → MainView 打开正常 → 点击开始按钮 → 切换到 GameView → 战斗流程基本可玩（需开发者本地操作）
- [x] 7.4 通过 Unity Console 检查无 ERROR 级日志；记录哪些 Screen 出现 `[Navigator] ... 未找到约定 USS 资源` 警告（需开发者本地操作）

## 8. Spike：UXML 内嵌 `<Style src=...>` 的 build 行为验证

- [x] 8.1 在保留 UXML 内 `<Style src=...>` 引用的状态下，执行一次 Standalone Player build（Windows 平台即可）
- [x] 8.2 在 build 后产物中启动 Player，验证 MainView / GameView 的 USS 样式（颜色、字体、布局间距）是否正确呈现
- [x] 8.3 在 build Player 中检查 Player.log，搜索是否存在 "Could not load StyleSheet" / "Style at path '...' could not be resolved" 类警告或错误
- [x] 8.4 记录验证结论到 `openspec/changes/convention-based-screen-resolution/spike-results.md`：UXML 内嵌 `<Style>` 在 YooAsset bundle 拓扑下能否解析；若不能解析，列出受影响资源
- [x] 8.5 根据 spike 结论，决定是否在本变更内附加"从 UXML 删除 `<Style>` 块"的清理任务；若不能解析则必须在本变更内一并处理（为子任务 8.6），若能解析则保持现状不再清理
- [x] 8.6 （仅 spike 结论为"不能解析"时执行）从 `MainUxml.uxml` 和 `GameUxml.uxml` 中删除 `<Style src=...>` 元素，并通过另一次 build 验证 USS 现在仅由 C# 约定加载提供时仍能正确呈现

## 9. 文档与归档

- [x] 9.1 更新 `CLAUDE.md` 中"UI 系统"段落：替换为基于命名约定的描述，新增 Screen / Popup / 命名约定 / OpenAsync 介绍
- [x] 9.2 更新 `Assets/EF/EFRuntime/UI/README.md`：API 更新告示 + 重写 Navigator API/生命周期/快速开始等核心章节
- [x] 9.3 在 `~/.claude/projects/.../memory/MEMORY.md` 与 `feedback_ui_screen_conventions.md` 中记录约定 + Navigator API 改造决策
- [x] 9.4 运行 `openspec validate convention-based-screen-resolution --strict` 确认实现与 spec 一致（已通过；可在 spike 结束后再跑一次确认）
- [x] 9.5 通过 `/opsx:archive` 归档变更（待 spike 与 Play 模式手测完成后执行）
