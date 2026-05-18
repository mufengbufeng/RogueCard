## REMOVED Requirements

### Requirement: Shell 必须从 rootVisualElement 解析层级容器

**Reason**: 运行时 UI 不再使用 UIDocument、rootVisualElement 或 VisualElement 层级容器。

**Migration**: 使用 `IUIManager.RegisterLayerRoot(UILayer, Transform)` 注册 UGUI Canvas 下的 Background / Normal / Popup / Overlay Transform。

### Requirement: Root.uxml 必须声明三个层级容器

**Reason**: UGUI 运行时不再加载 `Root.uxml`。

**Migration**: 启动场景中的 Entry / Canvas 层级 SHALL 提供 UGUI 层级根节点，并通过 ReferenceCollector 暴露给 `GameLogicEntry`。

### Requirement: Navigator.OpenAsync 必须替换 Screen 层内容

**Reason**: Navigator / Screen 导航被 UGUI `UIManager.OpenWindowAsync` 替代。

**Migration**: 主界面和局内界面通过 `OpenWindowAsync<TView,TController>()` 打开，流程离开时通过 `CloseWindowAsync(windowName)` 显式关闭。

### Requirement: Navigator 必须将 Popup 类型的目标入栈到 PopupLayer

**Reason**: Popup 不再通过 `Popup<TViewModel>` marker 与 VisualElement 遮罩实现。

**Migration**: 弹窗使用 `UILayer.Popup` 或 `UILayer.Overlay` 的 UGUI Prefab；遮罩由对应 Prefab 或 UIController 管理。

### Requirement: Navigator.Close 必须按栈顺序关闭顶层弹窗

**Reason**: Navigator 弹窗栈被移除。

**Migration**: UGUI 弹窗关闭使用 `IUIManager.CloseWindowAsync(windowName)`，需要栈式弹窗时由 UIManager 的 active window 列表或上层 Controller 约束。

### Requirement: Navigator 必须支持按字符串名查找 Screen 类型并缓存

**Reason**: UGUI 窗口不再通过 Screen 类型反射解析。

**Migration**: 使用 `UIWindowDescriptor` 注册或 `OpenWindowAsync<TView,TController>(location, ...)` 的强类型 API 打开窗口。

### Requirement: Screen 必须管理 UXML 内容挂载和 ViewModel 注入

**Reason**: `Screen<TViewModel>` 与 UXML 内容挂载机制被移除。

**Migration**: `UIView` 作为 MonoBehaviour 挂载到 Prefab 根对象，`UIController` 通过 `UIRuntimeContext` 协调 Model 与 View。

### Requirement: Screen 生命周期必须按固定顺序执行

**Reason**: Screen 生命周期不再存在。

**Migration**: 使用 UGUI 窗口生命周期：Controller.Initialize / PrepareAsync / View.Initialize / View.Open / Controller.Enter / Controller.Exit / View.Close / Release。

## ADDED Requirements

### Requirement: UI 导航必须通过 IUIManager 打开和关闭窗口

运行时 UI 导航 SHALL 通过 `IUIManager` 完成。调用方 SHALL 使用 `OpenWindowAsync<TView,TController>()` 打开窗口，并在流程离开或关闭弹窗时使用 `CloseWindowAsync(windowName)` 或 `CloseAllAsync()` 清理窗口。

#### Scenario: 主菜单流程打开 MainView
- **WHEN** `MainMenuProcedure.OnEnter` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.OpenWindowAsync<MainView, MainController>("MainView", UILayer.Normal, ...)`
- **AND** SHALL NOT 调用 `Navigator.OpenAsync`

#### Scenario: 局内流程打开 GameView
- **WHEN** `GameProcedure.OnEnter` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.OpenWindowAsync<GameView, GameController>("GameView", UILayer.Normal, ...)`
- **AND** SHALL NOT 加载 `GameUxml`

#### Scenario: 流程离开时关闭所属窗口
- **WHEN** `MainMenuProcedure.OnLeave` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.CloseWindowAsync("MainView")`
- **WHEN** `GameProcedure.OnLeave` 被调用
- **THEN** 系统 SHALL 调用 `IUIManager.CloseWindowAsync("GameView")`
