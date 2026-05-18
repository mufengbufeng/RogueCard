# ui-screen-conventions Specification

## Purpose

定义 EF.UI 框架下 UGUI 窗口的命名和资源约定。每个窗口围绕 `{Stem}View : UIView`、`{Stem}Controller : UIController` 和 `{Stem}View` Prefab 组织，Prefab 通过 ReferenceCollector / UHub 暴露组件绑定入口。
## Requirements
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
