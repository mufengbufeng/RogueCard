# ui-framework-docs Specification

## Purpose

把 `Assets/EF/EFRuntime/UI/README.md` 必须解释的 UI 框架知识建模为可验证的文档契约：覆盖架构概览、核心类型 API、生命周期、数据绑定模式、Procedure 协作示例、Root.uxml 约定、测试入口与遗留说明。后续如果新增 / 重命名 UI 框架类型，可以通过修改本 capability 的 requirement / scenario 触发 README 同步。

## Requirements

### Requirement: README 必须反映当前 UITK + MVVM 架构

`Assets/EF/EFRuntime/UI/README.md` SHALL 描述目录下当前实际存在的 UITK + MVVM 框架（`Shell` / `INavigator` / `Navigator` / `ScreenRegistry` / `ScreenDescriptor` / `Screen` / `Screen<TViewModel>` / `ViewModelBase` / `ReactiveProperty<T>` / `Region` / `LocalEventBus`），且 MUST NOT 出现已被删除的 UGUI MVC 类型与 API。

#### Scenario: README 不再引用旧 UGUI MVC API

- **WHEN** 在 `Assets/EF/EFRuntime/UI/README.md` 中搜索 `UIView` / `UIController` / `UIWindowDescriptor` / `UIBindingCollection` / `BindProperty` / `OpenWindowAsync` / `IUIManager` / `INotifyPropertyChanged`
- **THEN** 这些标识符 MUST 全部不存在（计 0 个匹配）

#### Scenario: README 覆盖当前 UITK 框架的核心类型

- **WHEN** 在 README 中搜索每一项：`Shell`、`INavigator`、`Navigator`、`ScreenRegistry`、`ScreenDescriptor`、`Screen`、`Screen<TViewModel>`、`ViewModelBase`、`ReactiveProperty`、`Region`、`LocalEventBus`
- **THEN** 每个标识符 MUST 至少出现一次，且其上下文 MUST 给出该类型的职责说明

#### Scenario: README 标识符与源码 1:1 对齐

- **WHEN** 抽取 README 中出现的 UI 框架公共方法名（如 `NavigateToAsync` / `PushPopupAsync` / `PopPopup` / `Shutdown` / `Register` / `LoadContent` / `Setup` / `OnSetup` / `OnShow` / `OnHide` / `OnDispose` / `Prop` / `ClearListeners` / `ShowAsync` / `Show` / `Clear` / `GetChannel`）
- **THEN** 每个名字 MUST 能在 `Assets/EF/EFRuntime/UI/*.cs` 中通过 grep 命中同名定义

### Requirement: README 必须解释 Shell 三层布局与 Root.uxml 约定

README SHALL 说明 `Shell` 从 `UIDocument.rootVisualElement` 解析 `screen-layer` / `popup-layer` / `system-layer` 三个命名 `VisualElement`，并解释 `Root.uxml` 的命名约定与回退路径。

#### Scenario: README 列出三个层级及其职责

- **WHEN** 读者阅读 README 中的"层级"或"Shell"章节
- **THEN** README MUST 同时出现 `screen-layer` / `popup-layer` / `system-layer` 三个名字
- **AND** README MUST 标注：`screen-layer` 同时只显示一个 Screen（替换式）、`popup-layer` 栈式管理、`system-layer` 用于 Toast/Loading 等

#### Scenario: README 描述 Root.uxml 约定与 PanelSettings 适配

- **WHEN** 读者搜索 `Root.uxml` 或 `PanelSettings`
- **THEN** README MUST 说明：`UIDocument.SourceAsset` 推荐配置为 `Root.uxml`，由 `PanelSettings.ScaleMode` 驱动整树尺寸
- **AND** README MUST 提及"未配置 SourceAsset 时框架会回退到运行时加载 `Root.uxml`"这一事实

#### Scenario: README 说明 Shell 构造前置条件

- **WHEN** 读者阅读 `Shell(VisualElement root)` 相关说明
- **THEN** README MUST 标注：缺少任一命名层级会抛 `InvalidOperationException`，`root` 为 null 抛 `ArgumentNullException`

### Requirement: README 必须给出 Navigator 完整生命周期

README SHALL 通过流程图或编号列表，描述 `NavigateToAsync` / `PushPopupAsync` / `PopPopup` / `Shutdown` 的完整执行顺序，并明确异常路径的回滚行为。

#### Scenario: NavigateToAsync 流程被完整描述

- **WHEN** 读者搜索"生命周期"或"NavigateToAsync"
- **THEN** README MUST 按顺序描述：旧 Screen `OnHide` → `OnDispose` → 加载 UXML 资源 → `Activator.CreateInstance` 构造 Screen → `LoadContent(vta)` → `Add` 到 `ScreenLayer` → `Setup(viewModel)` → `OnShow`

#### Scenario: PushPopupAsync 异常路径被显式说明

- **WHEN** 读者阅读 `PushPopupAsync` 相关章节
- **THEN** README MUST 描述：失败时已添加到 `PopupLayer` 的 overlay 与 popup 通过 `RemoveFromHierarchy` 回滚，避免悬挂节点
- **AND** README MUST 提及 overlay 是半透明黑色遮罩（`backgroundColor = (0, 0, 0, 0.6)`）

#### Scenario: Shutdown 行为被记录

- **WHEN** 读者搜索 `Shutdown`
- **THEN** README MUST 说明：依次清理弹窗栈、当前 Screen、`ScreenLayer.Clear()` / `PopupLayer.Clear()` / `SystemLayer.Clear()`
- **AND** README MUST 标注：所有清理调用使用 `try/catch` 兜底，单个 ViewModel 抛异常不会阻断后续清理

### Requirement: README 必须演示 ReactiveProperty 数据绑定模式

README SHALL 给出标准的 `Screen.OnSetup` 数据绑定示例，演示 ViewModel → View 的 `Changed` 订阅、初始值同步、命令意图（事件）回调，**示例代码 MUST 来自仓库中真实文件的逐字片段**。

#### Scenario: 示例代码可在仓库中追溯

- **WHEN** 抽取 README 中的 ViewModel / Screen / Procedure 示例代码片段中的标识符
- **THEN** 每个类名（如 `MainViewModel` / `MainMenuScreen` / `MainMenuProcedure`）MUST 在 `Assets/GameScripts/HotFix/GameLogic/` 下存在同名 `.cs` 文件
- **AND** 每个 `ReactiveProperty` 字段名（如 `StatusText` / `LevelName` / `LevelDesc` / `CanStart`）MUST 在对应 `.cs` 文件中存在同名属性

#### Scenario: 标准绑定模式被覆盖

- **WHEN** 读者阅读"数据绑定"章节
- **THEN** README MUST 演示 `vm.Xxx.Changed += value => …` 的订阅模式
- **AND** README MUST 演示初始值同步（绑定后立即用 `vm.Xxx.Value` 设置一次 UI 状态）
- **AND** README MUST 演示 `Button.RegisterCallback<ClickEvent>(_ => vm.RequestXxx())` 形式的命令意图回调

#### Scenario: ViewModel 自动清理被说明

- **WHEN** 读者阅读 `ViewModelBase` / `Prop<T>` 相关章节
- **THEN** README MUST 说明：用 `Prop<T>(initial)` 创建的 `ReactiveProperty` 会被 `ViewModelBase` 追踪，`Dispose()` 时自动 `ClearListeners`
- **AND** README MUST 标注：`Screen<TViewModel>.OnDispose` 默认会调用 `ViewModel.Dispose()` 并把 Screen 自脱树

### Requirement: README 必须给出从 Procedure 到 Screen 的最小可运行路径

README SHALL 给出一段端到端示例，覆盖"在 `GameLogicEntry.InitializeNavigator` 中注册 Screen → 在 Procedure 中创建 ViewModel + 订阅命令意图 → `_navigator.NavigateToAsync(name, vm)`"的最小路径。

#### Scenario: 示例覆盖端到端路径

- **WHEN** 读者阅读"快速开始"或类似章节
- **THEN** README MUST 同时出现：`registry.Register<TScreen, TViewModel>(name, uxmlLocation)`、`vm.SomeRequested += handler`、`_navigator.NavigateToAsync(name, vm)`
- **AND** 所有名字 MUST 与 `GameLogicEntry.cs` / `MainMenuProcedure.cs` 中的真实写法一致

#### Scenario: 注册位置被明确

- **WHEN** 读者询问"Screen 在哪里注册"
- **THEN** README MUST 指向 `GameLogicEntry.InitializeNavigator` 作为当前注册入口
- **AND** README MUST 提及：注册依赖 `IResourceManager`（用于加载 `VisualTreeAsset`），`Navigator` 通过构造函数注入

### Requirement: README 必须区分活代码与遗留代码

README SHALL 显式标注 `UILayer` 枚举与 `UHub/` 目录的当前状态，避免读者误以为它们是新框架的一部分。

#### Scenario: UILayer 枚举被标为遗留

- **WHEN** 读者搜索 `UILayer`
- **THEN** README MUST 说明：`UILayer` 是旧 UI 工具链遗留枚举，仅 `ReferenceCollectorScriptGenerator` 模板字符串引用，新框架不消费

#### Scenario: UHub 目录被标为占位

- **WHEN** 读者搜索 `UHub`
- **THEN** README MUST 说明：`UHub/` 当前为空目录，作为未来按窗口聚合 UI Hub 抽象的占位，本框架版本不依赖它

### Requirement: README 必须提供测试入口指引

README SHALL 列出 `GameLogic.Tests.EditMode/Framework/` 下与 UI 框架相关的 EditMode 测试文件，作为"活文档与回归基线"。

#### Scenario: 测试文件被列出

- **WHEN** 读者阅读"测试"或类似章节
- **THEN** README MUST 同时出现：`ShellAndRegistryTests`、`ScreenLifecycleTests`、`ReactivePropertyTests`
- **AND** 每个文件名 MUST 附一句话目的（不展开断言细节）

### Requirement: README 必须保持可读体量与中文行文

README 的整体长度 SHALL 控制在合理范围内，且自然语言部分使用中文（简体）。

#### Scenario: 文件体量控制

- **WHEN** 测量 `Assets/EF/EFRuntime/UI/README.md` 行数
- **THEN** 总行数 MUST ≤ 500 行（目标 ≤ 350 行）

#### Scenario: 自然语言使用中文

- **WHEN** 阅读 README 章节标题与正文段落
- **THEN** 所有自然语言内容 MUST 使用中文（简体）
- **AND** 类名 / 方法名 / 字段名 / UQuery 名（如 `screen-layer` / `start-btn`）MUST 保留英文原样
