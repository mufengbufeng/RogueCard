# ui-navigation Specification

## Purpose

定义 MVVM UI 框架的导航服务、Shell 层级容器、ScreenRegistry 注册表和 Screen 生命周期。Navigator 专职管理 Screen 内容替换和 Popup 栈式导航；Shell 通过 Root.uxml 在 UIDocument.rootVisualElement 内声明三个层级容器（screen-layer / popup-layer / system-layer），让 PanelSettings 的 ScaleMode 直接驱动 UI 树尺寸。

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

### Requirement: Navigator.NavigateToAsync 必须替换 Screen 层内容
Navigator.NavigateToAsync(screenName, viewModel) SHALL 关闭当前 Screen（如有），从 ScreenRegistry 获取描述符，加载 UXML 资源，创建 Screen 实例，挂载到 ScreenLayer，注入 ViewModel 并调用 Setup 和 OnShow。

#### Scenario: 首次导航到主界面
- **WHEN** 调用 NavigateToAsync("MainMenu", mainViewModel) 且 ScreenLayer 为空
- **THEN** Navigator SHALL 加载 MainMenu 的 UXML
- **AND** 创建 MainMenuScreen 实例并添加到 ScreenLayer
- **AND** 调用 Screen.Setup(mainViewModel) 和 Screen.OnShow()

#### Scenario: 从主界面切换到局内界面
- **WHEN** 调用 NavigateToAsync("Game", gameViewModel) 且 ScreenLayer 上已有 MainMenuScreen
- **THEN** Navigator SHALL 先调用当前 Screen 的 OnHide 和 OnDispose
- **AND** SHALL 清空 ScreenLayer
- **AND** SHALL 创建 GameScreen 并添加到 ScreenLayer

#### Scenario: 导航到未注册的 Screen
- **WHEN** 调用 NavigateToAsync("Unknown", vm) 且 ScreenRegistry 中无此名称
- **THEN** SHALL 抛出 KeyNotFoundException

### Requirement: Navigator.PushPopupAsync 必须将弹窗入栈
Navigator.PushPopupAsync(popupName, viewModel) SHALL 加载弹窗 UXML，创建半透明遮罩层，将弹窗添加到 PopupLayer，弹窗在遮罩之上。多次 PushPopup 的弹窗 SHALL 按后进先出顺序叠加。

#### Scenario: 推入第一个弹窗
- **WHEN** 调用 PushPopupAsync("ActivityPopup", vm)
- **THEN** PopupLayer SHALL 包含一个遮罩元素和一个弹窗 Screen
- **AND** 弹窗 SHALL 调用 OnShow()

#### Scenario: 推入第二个弹窗叠加在第一个之上
- **WHEN** 已有一个弹窗在 PopupLayer，再调用 PushPopupAsync
- **THEN** 新弹窗 SHALL 添加到 PopupLayer 的末尾（渲染在最上层）

### Requirement: Navigator.PopPopup 必须按栈顺序关闭弹窗
Navigator.PopPopup() SHALL 移除 PopupLayer 中最后添加的弹窗和对应的遮罩层，调用弹窗的 OnHide 和 OnDispose。Screen.OnDispose 内部 SHALL 调用 ViewModel.Dispose()。

#### Scenario: 弹出最顶层弹窗
- **WHEN** PopupLayer 中有 2 个弹窗，调用 PopPopup()
- **THEN** 最后添加的弹窗 SHALL 被移除
- **AND** 第一个弹窗 SHALL 保持显示

#### Scenario: 弹窗栈为空时 PopPopup
- **WHEN** PopupLayer 中无弹窗，调用 PopPopup()
- **THEN** SHALL 不抛异常，直接返回

### Requirement: ScreenRegistry 必须支持注册和查询
ScreenRegistry SHALL 提供 Register<TScreen, TViewModel>(name, uxmlLocation) 方法注册 Screen 描述。Get(name) SHALL 返回 ScreenDescriptor。重复注册同名 Screen SHALL 抛出异常。

#### Scenario: 注册并查询 Screen
- **WHEN** 注册 Register<MainMenuScreen, MainViewModel>("MainMenu", "UI/MainMenu")
- **AND** 调用 Get("MainMenu")
- **THEN** SHALL 返回包含正确 ScreenType、ViewModelType、Location 的 ScreenDescriptor

#### Scenario: 重复注册同名 Screen
- **WHEN** 两次调用 Register 使用相同 name
- **THEN** SHALL 抛出 InvalidOperationException

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
