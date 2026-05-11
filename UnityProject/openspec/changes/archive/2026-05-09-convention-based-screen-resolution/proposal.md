## Why

当前 UI 系统每新增一个 Screen，都必须回到 `GameLogicEntry.InitializeNavigator()` 中手动调用 `registry.Register<TScreen, TViewModel>(name, location)` 添加一行——这一步很容易忘，且把"加 Screen"和"改启动入口"这两件本不该耦合的事强行绑在一起。同时现状里 `MainMenuScreen` / `MainView.uxml` / `"MainMenu"` 三个不同的名字指向同一个功能，靠中心化注册表把它们粘合起来。

我们希望把"加 Screen 必须改入口"这件事内化掉：让框架按命名约定自动解析 UXML / USS / ViewModel，新增 Screen = 新写一个类 + 配同名资源，**不再触碰 `GameLogicEntry`**，并且类、UXML、USS、ViewModel 全部围绕一个共同的 `{Stem}` 名字组织，开发者搜一个名字就能定位一个功能的所有片段。

## What Changes

### 命名约定（强约束）

- 所有 Screen 类 SHALL 命名为 `{Stem}View`（如 `MainView` / `GameView` / `SettingsView`）
- 对应 ViewModel SHALL 命名为 `{Stem}ViewModel`
- 对应 UXML 资源 SHALL 命名为 `{Stem}Uxml`（如 `MainUxml.uxml`）
- 对应 USS 资源 SHALL 命名为 `{Stem}Uss`（如 `MainUss.uss`，可选）

### Screen / Popup 基类

- `Screen<TViewModel>` 增加 `UxmlLocation` / `UssLocation` 虚属性，默认按上述约定从类名推导
- 新增 `Popup<TViewModel> : Screen<TViewModel>` 标记基类，Navigator 据此分流到 PopupLayer 走栈式管理

### Navigator API 重塑

- 新增 `OpenAsync<TScreen>(ViewModelBase vm = null)` 按类型打开（首选 API）
- 保留字符串重载 `OpenAsync(string viewName, ViewModelBase vm = null)`，内部按类名反射查表（首次扫描后缓存），供 Luban 等配置表数据驱动调用
- **BREAKING**：`NavigateToAsync` / `PushPopupAsync` 重命名为 `OpenAsync`；Popup 不再用单独 API，由基类决定行为
- **BREAKING**：`ScreenRegistry` 删除。框架不再要求在启动期显式注册 Screen

### Model 懒加载

- `GameLogicEntry.InitializeModels()` 删除
- 业务侧 SHALL 通过 `ModelManager.TryGetModel<T>()`（已有方法）按需懒注册

### 资源加载约定

- UXML：通过 `IResourceManager` 按 `{Stem}Uxml` 加载（必须存在，缺失 SHALL 抛异常）
- USS：通过 `IResourceManager` 按 `{Stem}Uss` 加载（可选，缺失 DEBUG 警告 + Release 静默）
- 是否依赖 UXML 内嵌 `<Style src=...>` 由 spike 任务在 build 阶段验证后决定

### 命名重构（一次性）

- `MainMenuScreen` → `MainView`，`GameScreen` → `GameView`
- `Assets/AssetRaw/UI/Main/MainView.uxml` → `MainUxml.uxml`，`Assets/AssetRaw/UI/Main/MainViewStyles.uss` → `MainUss.uss`
- `Assets/AssetRaw/UI/Game/GameView.uxml` → `GameUxml.uxml`，`Assets/AssetRaw/UI/Game/GameViewStyles.uss` → `GameUss.uss`
- 字符串标识 `"MainMenu"` → `"MainView"`，`"Game"` → `"GameView"`（与类名一致）
- `Assets/AssetRaw/UI/Game/` 下其他 UXML（如 `BattlePanel` / `RewardPanel` / `CardItem` 等子模板）保留原名——它们由 Region 局部加载，不受 Screen 约定约束

### 不动

- Procedure 注册（FSM 构造期需要全部状态实例，列表数量稳定）
- `ViewModelBase` / `ReactiveProperty` API
- `Shell` / Layer 解析逻辑
- `Region` 子区域切换逻辑

## Capabilities

### New Capabilities

- `ui-screen-conventions`：定义 `{Stem}View / ViewModel / Uxml / Uss` 命名约定、`Popup<TViewModel>` 标记基类、UXML/USS 默认资源名推导规则、约定的可覆盖性（虚属性兜底）

### Modified Capabilities

- `ui-navigation`：删除 ScreenRegistry 显式注册要求，改为按类型/类名反射解析；`NavigateToAsync` / `PushPopupAsync` 合并为 `OpenAsync`，Popup 通过基类区分；新增按类型打开重载
- `single-main-ui-entry`：更新主界面跳转字符串标识 `"MainMenu"` → `"MainView"`、`"Game"` → `"GameView"`，并适配新 Navigator API 命名

## Impact

### 受影响代码

- `Assets/EF/EFRuntime/UI/Navigator.cs` — API 重塑（按类型打开 + 反射解析 + 类名缓存）
- `Assets/EF/EFRuntime/UI/INavigator.cs` — 接口签名更新
- `Assets/EF/EFRuntime/UI/Screen.cs` — 新增 `UxmlLocation` / `UssLocation` 虚属性
- `Assets/EF/EFRuntime/UI/Popup.cs` — 新增标记基类
- `Assets/EF/EFRuntime/UI/ScreenRegistry.cs` — 删除
- `Assets/GameScripts/HotFix/GameLogic/GameLogicEntry.cs` — 移除 `ScreenRegistry.Register` 调用、删除 `InitializeModels()`
- `Assets/GameScripts/HotFix/GameLogic/UI/Main/MainMenuScreen.cs` → `MainView.cs`（类名 + 文件名同步）
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameScreen.cs` → `GameView.cs`
- 所有引用 `MainMenuScreen` / `GameScreen` / `"MainMenu"` / `"Game"` / `NavigateToAsync` / `PushPopupAsync` 的代码（主要在 Procedure、ViewModel、Tests）

### 受影响资源

- `Assets/AssetRaw/UI/Main/MainView.uxml` → `MainUxml.uxml`（含 YooAsset addressable 重映射）
- `Assets/AssetRaw/UI/Main/MainViewStyles.uss` → `MainUss.uss`
- `Assets/AssetRaw/UI/Game/GameView.uxml` → `GameUxml.uxml`
- `Assets/AssetRaw/UI/Game/GameViewStyles.uss` → `GameUss.uss`

### 受影响测试

- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/` 下任何引用旧 Screen 类名 / 旧字符串标识 / 旧 Navigator API 的 EditMode 测试

### 行为变化

- 启动期不再扫描注册所有 Screen；首次按字符串打开时反射程序集（O(N) 一次性 + 缓存）
- USS 缺失从硬错误降级为 DEBUG 警告 + 静默
- Model 首次访问时自动注册（已通过 `TryGetModel<T>()` 支持，仅删除显式注册代码）

### 不影响

- HybridCLR 热更新流程
- YooAsset 资源 ID 之外的打包配置
- Procedure / FSM 流程
- Shell 层级 / PanelSettings
