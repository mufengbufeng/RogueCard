## REMOVED Requirements

### Requirement: Screen 类必须遵守 `{Stem}View` 命名约定

**Reason**: 运行时 UI 不再使用 `Screen<TViewModel>` 或 `Popup<TViewModel>` 类型。

**Migration**: 窗口 View 类 SHALL 继承 `UIView`，并按 `{Stem}View` 命名，例如 `MainView`、`GameView`。

### Requirement: ViewModel 类必须遵守 `{Stem}ViewModel` 命名约定

**Reason**: 主 UI 框架回到 UGUI MVC，不再要求每个窗口有 `ViewModelBase` 派生类型。

**Migration**: 窗口交互由 `{Stem}Controller : UIController` 协调，数据状态可使用 `ModelManager` 中的 Model 或流程层传入的 userData。

### Requirement: UXML 资源必须遵守 `{Stem}Uxml` 命名约定

**Reason**: 运行时 UI 不再使用 UXML 资源。

**Migration**: 窗口 Prefab SHALL 通过 YooAsset addressable 以 `{Stem}View` 或显式 location 注册，例如 `"MainView"`。

### Requirement: USS 资源遵守 `{Stem}Uss` 命名约定且加载可选

**Reason**: 运行时 UI 不再使用 USS 样式表。

**Migration**: UGUI 视觉样式 SHALL 保存在 Prefab、组件属性、材质、Sprite、TMP FontAsset 或运行时代码中。

### Requirement: Popup<TViewModel> 必须作为弹窗标记基类

**Reason**: 弹窗不再通过 `Popup<TViewModel>` marker 分流。

**Migration**: 弹窗 View 仍继承 `UIView`，打开时指定 `UILayer.Popup` 或 `UILayer.Overlay`。

### Requirement: Screen / Popup 必须支持自动 ViewModel 类型解析

**Reason**: Screen 泛型 ViewModel 解析被移除。

**Migration**: 调用方通过 `OpenWindowAsync<TView,TController>()` 显式指定 View 与 Controller 类型。

## ADDED Requirements

### Requirement: UGUI 窗口必须遵守 View/Controller/Prefab 命名约定

每个 UGUI 窗口 SHALL 包含一个 `{Stem}View : UIView` 类型、一个 `{Stem}Controller : UIController` 类型，以及一个可通过 YooAsset 加载的 `{Stem}View` Prefab。`{Stem}` SHALL 使用 PascalCase，并表达窗口概念标识。

#### Scenario: Main 窗口命名
- **WHEN** 实现主菜单窗口
- **THEN** View 类型 SHALL 命名为 `MainView`
- **AND** Controller 类型 SHALL 命名为 `MainController`
- **AND** Prefab 地址 SHALL 为 `"MainView"` 或显式传给 `OpenWindowAsync` 的等价地址

#### Scenario: Game 窗口命名
- **WHEN** 实现局内窗口
- **THEN** View 类型 SHALL 命名为 `GameView`
- **AND** Controller 类型 SHALL 命名为 `GameController`
- **AND** Prefab 地址 SHALL 为 `"GameView"` 或显式传给 `OpenWindowAsync` 的等价地址

### Requirement: UGUI Prefab 必须提供 ReferenceCollector 绑定入口

需要自动绑定字段的 UGUI 窗口 Prefab 根对象 SHALL 挂载 `ReferenceCollector`，并收集该窗口 View 需要访问的 Button、Image、TextMeshProUGUI、RectTransform、ScrollRect 或其他组件引用。

#### Scenario: MainView 自动绑定开始按钮
- **WHEN** `MainView` 需要访问开始按钮
- **THEN** `MainView` Prefab 的 ReferenceCollector SHALL 包含开始按钮引用
- **AND** UHub 初始化后 `MainView` SHALL 能获得对应 Button 字段

#### Scenario: 缺少必需引用时记录错误
- **WHEN** View 初始化后必需 UI 组件仍为空
- **THEN** View SHALL 记录包含组件名和窗口名的错误或警告日志
