## ADDED Requirements

### Requirement: Screen 类必须遵守 `{Stem}View` 命名约定

所有继承自 `Screen<TViewModel>` 或 `Popup<TViewModel>` 的非抽象类型 SHALL 以 `View` 结尾命名为 `{Stem}View`，其中 `{Stem}` 是该 Screen 的概念标识符（如 `Main`、`Game`、`Settings`）。`{Stem}` SHALL 由字母数字组成，首字母大写（PascalCase）。`{Stem}` SHALL NOT 为空字符串，即类名 SHALL NOT 仅为 `View`。

#### Scenario: 主界面 Screen 命名
- **WHEN** 实现项目主菜单 Screen 类型
- **THEN** 类名 SHALL 为 `MainView`（`{Stem}` = `Main`）
- **AND** SHALL NOT 命名为 `MainScreen` / `MainMenuScreen` / `MainPanel` 等其他形式

#### Scenario: 弹窗 Screen 命名
- **WHEN** 实现一个继承 `Popup<TViewModel>` 的弹窗类型
- **THEN** 类名 SHALL 仍以 `View` 结尾（如 `SettingsView`），而非 `SettingsPopup`
- **AND** "弹窗" 这一行为差异 SHALL 由继承 `Popup<TViewModel>` 而非 `Screen<TViewModel>` 表达

#### Scenario: 仅命名为 View 的类型
- **WHEN** 定义一个名为 `View` 的类型继承 `Screen<TViewModel>`
- **THEN** 该约定 SHALL 视为违反（`{Stem}` 为空）
- **AND** 文档/代码评审 SHALL 拒绝此类命名

### Requirement: ViewModel 类必须遵守 `{Stem}ViewModel` 命名约定

每个 `{Stem}View` 类 SHALL 对应一个名为 `{Stem}ViewModel` 的 ViewModel 类型，该类型 SHALL 继承 `ViewModelBase` 并作为 `Screen<TViewModel>` / `Popup<TViewModel>` 的泛型参数。`{Stem}` 部分 SHALL 与对应 Screen 完全一致。

#### Scenario: MainView 对应的 ViewModel
- **WHEN** 实现 `MainView : Screen<TViewModel>`
- **THEN** 对应 ViewModel 类型 SHALL 命名为 `MainViewModel`
- **AND** Screen 声明 SHALL 为 `MainView : Screen<MainViewModel>`

### Requirement: UXML 资源必须遵守 `{Stem}Uxml` 命名约定

每个 `{Stem}View` Screen 类对应的 UXML 资源 SHALL 通过 YooAsset 以 addressable `{Stem}Uxml` 注册（即资源文件名为 `{Stem}Uxml.uxml`）。框架 SHALL 通过 `Screen<T>.UxmlLocation` 虚属性按命名约定推导默认资源名，特殊情况下子类 MAY 通过 `override UxmlLocation` 指向自定义资源名。

#### Scenario: 默认资源名按约定推导
- **WHEN** Screen 类为 `MainView` 且未 override `UxmlLocation`
- **THEN** `UxmlLocation` SHALL 返回 `"MainUxml"`

#### Scenario: 子类 override 自定义资源名
- **WHEN** Screen 类继承 `Screen<TViewModel>` 并 override `UxmlLocation` 返回 `"Special/CustomLayout"`
- **THEN** Navigator SHALL 通过 `IResourceManager.LoadAssetAsync<VisualTreeAsset>("Special/CustomLayout")` 加载 UXML

#### Scenario: UXML 资源缺失时打开失败
- **WHEN** 调用 `Navigator.OpenAsync<MainView>()` 且 addressable `MainUxml`（或 override 后的资源名）在 YooAsset 中不存在
- **THEN** Navigator SHALL 抛出异常，错误信息 SHALL 包含期望的资源名和发起类型名

### Requirement: USS 资源遵守 `{Stem}Uss` 命名约定且加载可选

每个 `{Stem}View` Screen 类对应的 USS 资源 SHALL 通过 YooAsset 以 addressable `{Stem}Uss` 注册（即资源文件名为 `{Stem}Uss.uss`）。USS 资源是**可选的**——若资源缺失，Screen SHALL 仍能正常加载和显示，由其他来源的样式（UXML 内嵌 `<Style>` / 全局共享 USS）提供视觉。框架 SHALL 通过 `Screen<T>.UssLocation` 虚属性按命名约定推导默认资源名。

#### Scenario: 默认 USS 资源名按约定推导
- **WHEN** Screen 类为 `MainView` 且未 override `UssLocation`
- **THEN** `UssLocation` SHALL 返回 `"MainUss"`

#### Scenario: USS 资源存在时挂载到 Screen 根
- **WHEN** 打开 `MainView` 且 addressable `MainUss` 在 YooAsset 中存在
- **THEN** Navigator SHALL 通过 `IResourceManager.LoadAssetAsync<StyleSheet>("MainUss")` 加载样式表
- **AND** SHALL 将该 StyleSheet 添加到 Screen 实例的 `styleSheets` 集合

#### Scenario: USS 资源缺失时降级警告
- **WHEN** 打开 `MainView` 且 addressable `MainUss` 在 YooAsset 中不存在
- **THEN** Navigator SHALL NOT 抛出异常
- **AND** Screen SHALL 继续完成加载和显示流程
- **AND** DEBUG 构建 SHALL 通过 `Log.Warning` 记录一次警告，包含期望的资源名和 Screen 类型名
- **AND** Release 构建 SHALL 静默继续

### Requirement: Popup<TViewModel> 必须作为弹窗标记基类

框架 SHALL 提供 `Popup<TViewModel>` 抽象类，定义为 `public abstract class Popup<TViewModel> : Screen<TViewModel> where TViewModel : ViewModelBase`。该基类 SHALL 仅用作类型 marker——Navigator 通过反射判断目标类型是否派生自 `Popup<>` 来分流到 PopupLayer 走栈式管理；`Popup<T>` 自身 SHALL NOT 包含栈管理或遮罩相关逻辑。

#### Scenario: 继承 Popup<T> 的 Screen 走 PopupLayer
- **WHEN** 类型 `SettingsView` 声明为 `: Popup<SettingsViewModel>` 并通过 `Navigator.OpenAsync<SettingsView>()` 打开
- **THEN** Navigator SHALL 将 SettingsView 实例添加到 Shell.PopupLayer
- **AND** SHALL NOT 替换 Shell.ScreenLayer 的内容

#### Scenario: 继承 Screen<T> 的类走 ScreenLayer
- **WHEN** 类型 `MainView` 声明为 `: Screen<MainViewModel>` 并通过 `Navigator.OpenAsync<MainView>()` 打开
- **THEN** Navigator SHALL 将 MainView 实例添加到 Shell.ScreenLayer 并替换原内容
- **AND** SHALL NOT 添加到 PopupLayer

### Requirement: Screen / Popup 必须支持自动 ViewModel 类型解析

`Screen<TViewModel>` 的泛型参数 SHALL 唯一确定该 Screen 对应的 ViewModel 类型。Navigator SHALL 通过反射沿继承链找到 `Screen<>` 闭合泛型，提取其类型参数作为 ViewModel 类型，无需调用方在打开时显式声明。

#### Scenario: 自动解析 ViewModel 类型
- **WHEN** 调用 `Navigator.OpenAsync<MainView>()` 且 `MainView : Screen<MainViewModel>`
- **THEN** Navigator SHALL 通过反射推断 ViewModel 类型为 `MainViewModel`

#### Scenario: ViewModel 自动实例化
- **WHEN** 调用 `Navigator.OpenAsync<MainView>()` 且未传入 ViewModel 参数
- **THEN** Navigator SHALL 通过 `Activator.CreateInstance(typeof(MainViewModel))` 自动创建 ViewModel 实例
- **AND** SHALL 调用 Screen.Setup 注入该实例

#### Scenario: 调用方显式传入 ViewModel
- **WHEN** 调用 `Navigator.OpenAsync<MainView>(customViewModel)` 且 `customViewModel` 是 `MainViewModel` 实例
- **THEN** Navigator SHALL 使用传入的实例
- **AND** SHALL NOT 自动创建新 ViewModel
