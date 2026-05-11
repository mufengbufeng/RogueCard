# ui-navigation Specification

## Purpose

定义 MVVM UI 框架的导航服务、Shell 层级容器、Screen 生命周期与按命名约定 + 类型反射的 Screen 解析机制。Navigator 通过统一入口 `OpenAsync` 按目标类型自动分流：派生自 `Popup<>` 的类型入栈到 PopupLayer，其余 `Screen<>` 派生类型替换 ScreenLayer 内容。Shell 通过 UQuery 在 Root.uxml 内解析三个命名层级容器（screen-layer / popup-layer / system-layer），让 PanelSettings 的 ScaleMode 直接驱动 UI 树尺寸。

## Requirements

### Requirement: Shell 必须从 rootVisualElement 解析层级容器
Shell SHALL 在构造时接收 UIDocument.rootVisualElement（或其子容器），并通过 UQuery 解析三个命名 VisualElement 作为层级容器：name="screen-layer" / "popup-layer" / "system-layer"。这些层级容器在 Root.uxml 中声明，由 UIDocument 通过 SourceAsset 加载，从而让 PanelSettings 的 ScaleMode 自动驱动 UI 树尺寸。Shell 自身 SHALL NOT 继承 VisualElement，仅作为对层级容器的轻量包装。

#### Scenario: Shell 从 root 中解析层级
- **WHEN** 传入一个包含 screen-layer / popup-layer / system-layer 三个子节点的 root VisualElement
- **THEN** Shell.ScreenLayer / PopupLayer / SystemLayer SHALL 分别引用对应名称的 VisualElement

#### Scenario: 缺少必需层级时构造失败
- **WHEN** root 中缺少任一命名层级
- **THEN** Shell 构造函数 SHALL 抛出 InvalidOperationException

#### Scenario: root 为 null 时构造失败
- **WHEN** 传入 null 作为 root
- **THEN** Shell 构造函数 SHALL 抛出 ArgumentNullException

### Requirement: Root.uxml 必须声明三个层级容器
框架 SHALL 提供 Root.uxml 模板，包含 name 为 "screen-layer" / "popup-layer" / "system-layer" 的 VisualElement，每个层级 SHALL 是 position:absolute 全屏定位且 picking-mode=Ignore（避免空层拦截点击）。UIDocument SHALL 通过 SourceAsset 引用此 UXML，让 PanelSettings 的 ScaleMode 直接驱动 UI 树尺寸。

#### Scenario: Root.uxml 包含三个全屏层级
- **WHEN** 加载 Root.uxml
- **THEN** SHALL 包含 name="screen-layer" / "popup-layer" / "system-layer" 三个 VisualElement
- **AND** 每个层级 SHALL 是 absolute 0/0/0/0 全屏定位
- **AND** 每个层级 picking-mode SHALL 为 Ignore

### Requirement: Navigator.OpenAsync 必须替换 Screen 层内容
Navigator.OpenAsync(screenType, viewModel) SHALL 关闭当前 Screen（如有），按命名约定（或 Screen 子类 override 的 `UxmlLocation` / `UssLocation`）加载 UXML / USS 资源，创建 Screen 实例，挂载到 ScreenLayer，注入 ViewModel 并调用 Setup 和 OnShow。Navigator SHALL 同时提供按类型重载 `OpenAsync<TScreen>(ViewModelBase vm = null)` 和按字符串重载 `OpenAsync(string viewName, ViewModelBase vm = null)`，两者语义等价。当目标类型派生自 `Popup<>` 时，SHALL 走 PopupLayer 入栈而非替换 ScreenLayer（见 PushPopup 相关 Requirement）。当未传入 ViewModel 时，SHALL 通过 `Activator.CreateInstance` 按 ViewModel 类型自动创建。

#### Scenario: 首次按类型导航到主界面
- **WHEN** 调用 `OpenAsync<MainView>(mainViewModel)` 且 ScreenLayer 为空
- **THEN** Navigator SHALL 按 `MainView.UxmlLocation`（默认 `"MainUxml"`）加载 UXML
- **AND** 创建 MainView 实例并添加到 ScreenLayer
- **AND** 调用 Screen.Setup(mainViewModel) 和 Screen.OnShow()

#### Scenario: 按字符串导航到主界面
- **WHEN** 调用 `OpenAsync("MainView", mainViewModel)` 且 ScreenLayer 为空
- **THEN** Navigator SHALL 通过反射查找名为 `MainView` 的 Screen 派生类型
- **AND** 后续行为 SHALL 与按类型重载一致

#### Scenario: 从主界面切换到局内界面
- **WHEN** 调用 `OpenAsync<GameView>(gameViewModel)` 且 ScreenLayer 上已有 MainView
- **THEN** Navigator SHALL 先调用当前 Screen 的 OnHide 和 OnDispose
- **AND** SHALL 清空 ScreenLayer
- **AND** SHALL 创建 GameView 并添加到 ScreenLayer

#### Scenario: 未传入 ViewModel 时自动创建
- **WHEN** 调用 `OpenAsync<MainView>()` 不传 ViewModel
- **THEN** Navigator SHALL 通过 `Activator.CreateInstance(typeof(MainViewModel))` 自动创建实例
- **AND** SHALL 注入该实例并调用 Screen.Setup

#### Scenario: 按字符串导航到不存在的 Screen 类型
- **WHEN** 调用 `OpenAsync("Unknown", vm)` 且 AppDomain 内无名为 `Unknown` 的 Screen 派生类型
- **THEN** SHALL 抛出 KeyNotFoundException
- **AND** 错误信息 SHALL 包含期望的类型名

### Requirement: Navigator 必须将 Popup 类型的目标入栈到 PopupLayer
Navigator.OpenAsync 当目标类型派生自 `Popup<>` 时 SHALL 加载弹窗 UXML / USS，创建半透明遮罩层，将弹窗添加到 PopupLayer，弹窗在遮罩之上。多次 OpenAsync 派生自 Popup<> 的类型 SHALL 按后进先出顺序叠加。`OpenAsync` 的按类型/按字符串两种重载 SHALL 均支持 Popup 分流。

#### Scenario: 推入第一个弹窗
- **WHEN** 调用 `OpenAsync<SettingsView>(vm)` 且 `SettingsView : Popup<SettingsViewModel>`
- **THEN** PopupLayer SHALL 包含一个遮罩元素和一个弹窗 Screen
- **AND** 弹窗 SHALL 调用 OnShow()
- **AND** ScreenLayer 内容 SHALL NOT 被改动

#### Scenario: 推入第二个弹窗叠加在第一个之上
- **WHEN** 已有一个弹窗在 PopupLayer，再调用 OpenAsync 派生自 Popup<> 的类型
- **THEN** 新弹窗 SHALL 添加到 PopupLayer 的末尾（渲染在最上层）

### Requirement: Navigator.Close 必须按栈顺序关闭顶层弹窗
Navigator.Close() SHALL 移除 PopupLayer 中最后添加的弹窗和对应的遮罩层，调用弹窗的 OnHide 和 OnDispose。Screen.OnDispose 内部 SHALL 调用 ViewModel.Dispose()。Navigator 同时 SHALL 提供 CloseAll() 关闭所有 Popup（保留当前 ScreenLayer 内容）。

#### Scenario: 关闭最顶层弹窗
- **WHEN** PopupLayer 中有 2 个弹窗，调用 Close()
- **THEN** 最后添加的弹窗 SHALL 被移除
- **AND** 第一个弹窗 SHALL 保持显示

#### Scenario: 弹窗栈为空时 Close
- **WHEN** PopupLayer 中无弹窗，调用 Close()
- **THEN** SHALL 不抛异常，直接返回

#### Scenario: CloseAll 关闭所有 Popup
- **WHEN** PopupLayer 中有 2 个弹窗，调用 CloseAll()
- **THEN** 所有弹窗 SHALL 被移除并依次调用 OnDispose
- **AND** ScreenLayer 内容 SHALL 保持不变

### Requirement: Navigator 必须支持按字符串名查找 Screen 类型并缓存

Navigator.OpenAsync(string viewName, ...) SHALL 通过反射在当前 AppDomain 的所有已加载程序集中查找名为 `viewName` 的非抽象 `Screen<>` 派生类型。Navigator SHALL 在内部维护 `Dictionary<string, Type>` 缓存，命中即直接返回；未命中时 SHALL 完整扫描程序集并缓存结果。命中多个同名类型（不同命名空间）时 SHALL 抛出 InvalidOperationException 提示使用类型重载 `OpenAsync<TScreen>()`。

#### Scenario: 首次按字符串查找类型
- **WHEN** 第一次调用 `OpenAsync("MainView", vm)` 且缓存为空
- **THEN** Navigator SHALL 遍历 AppDomain.CurrentDomain.GetAssemblies() 查找名为 `MainView` 且派生自 `Screen<>` 的非抽象类型
- **AND** SHALL 将查找结果存入字典缓存

#### Scenario: 后续按字符串查找命中缓存
- **WHEN** 已经有过一次 `OpenAsync("MainView", vm)` 调用，再次调用同名打开
- **THEN** Navigator SHALL 直接从缓存返回类型
- **AND** SHALL NOT 重新扫描程序集

#### Scenario: 同名类型冲突
- **WHEN** 两个不同命名空间的非抽象类型都名为 `MainView` 且都派生自 `Screen<>`，调用 `OpenAsync("MainView", vm)`
- **THEN** Navigator SHALL 抛出 InvalidOperationException
- **AND** 错误信息 SHALL 列出冲突的两个类型全名
- **AND** SHALL 提示使用 `OpenAsync<TScreen>()` 类型重载消除歧义

### Requirement: Screen 必须管理 UXML 内容挂载和 ViewModel 注入
Screen 提供非泛型基类（Navigator 通过此类型引用任意 Screen 实例）和泛型派生类 Screen<TViewModel>（暴露强类型 ViewModel 给子类）。LoadContent(vta) SHALL 将 VTA 克隆内容作为子节点添加。Setup(viewModel) SHALL 验证类型并存储 ViewModel 引用，调用 OnSetup()。OnSetup 为抽象方法，子类在此执行 UQuery 和数据绑定。

#### Scenario: LoadContent 挂载 UXML
- **WHEN** 调用 LoadContent(visualTreeAsset)
- **THEN** Screen 的子节点 SHALL 包含一个 TemplateContainer
- **AND** TemplateContainer 的 flexGrow SHALL 为 1

#### Scenario: Setup 注入 ViewModel 并触发绑定
- **WHEN** 调用 Setup(viewModel)
- **THEN** Screen.ViewModel SHALL 返回注入的 viewModel
- **AND** OnSetup() SHALL 被调用一次

#### Scenario: Setup 接收错误类型 ViewModel 时抛异常
- **WHEN** 调用 Setup 传入与 Screen<TViewModel> 类型不匹配的 ViewModel
- **THEN** SHALL 抛出 ArgumentException

### Requirement: Screen 生命周期必须按固定顺序执行
Screen 生命周期顺序 SHALL 为：LoadContent → Setup → OnShow → [OnHide] → OnDispose。OnDispose SHALL 调用 ViewModel.Dispose() 并从元素树中移除自身。

#### Scenario: 完整生命周期
- **WHEN** Navigator 创建并显示一个 Screen 后关闭它
- **THEN** 调用顺序 SHALL 为 LoadContent → Setup → OnShow → OnHide → OnDispose
- **AND** OnDispose 后 Screen SHALL 不在元素树中
- **AND** ViewModel.Dispose() SHALL 被调用
