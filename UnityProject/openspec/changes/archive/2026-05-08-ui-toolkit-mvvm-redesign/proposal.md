## Why

当前 UI 框架采用 MVC 模式，UIView 是纯 C# 类包装 VisualElement，Controller 通过 C# event 转发交互。这种设计与 UI Toolkit 的哲学不符：VisualElement 本身就是 View、UI Toolkit 有自己的数据绑定模式、输入处理应使用 Manipulator 而非手动 RegisterCallback。此外，当前框架缺少弹窗栈管理和 Screen 内部子区域切换能力，导航逻辑散落在 UIManager 中职责不清。需要重新设计为 MVVM + Data Binding 模式，让框架贴合 UI Toolkit 原生方式。

## What Changes

- **BREAKING** 移除 UIView、UIController、UIManager、UIRuntimeContext、UIWindowDescriptor 及相关绑定/事件辅助类
- **BREAKING** 引入 Screen\<TViewModel\>（VisualElement 子类）替代 UIView + UIController，Screen 就是 View 本身
- **BREAKING** 引入 ViewModelBase + ReactiveProperty\<T\> 替代 BindProperty 手动绑定，ViewModel.Dispose() 自动清理所有监听者
- 新增 Navigator 导航服务，专职管理 Screen 内容替换和 Popup 栈式管理
- 新增 Shell 层级容器（ScreenLayer / PopupLayer / SystemLayer），替代外部手动注册层级节点
- 新增 ScreenRegistry 注册表，集中管理 Screen 名称 → UXML 路径 + 类型的映射
- 新增 Region 组件，支持 Screen 内部动态切换子区域内容（如 Battle → Reward）
- Controller 角色由 Procedure 承担，Procedure 创建 ViewModel、订阅命令意图、调用 Navigator

## Capabilities

### New Capabilities
- `reactive-property`: 响应式属性 ReactiveProperty\<T\>、ViewModelBase 自动追踪与 Dispose 清理机制
- `ui-navigation`: Navigator 导航服务、Shell 层级容器、ScreenRegistry 注册表、Screen 生命周期管理
- `ui-region`: Screen 内部可切换内容区域 Region，支持动态加载 UXML 子模板到命名插槽

### Modified Capabilities
- `ui-system`: **BREAKING** 移除 UISystem/ControllerEventBinder/UIRuntimeContext，System 初始化改为直接接收 ViewModel，不再依赖 UI 框架的 Context 注入
- `game-ui-data-binding`: **BREAKING** GameView 从 UIView 改为 Screen\<GameViewModel\>，手动 BindProperty 改为订阅 ReactiveProperty.Changed，手动 UQuery + setter 改为 ViewModel 驱动
- `single-main-ui-entry`: **BREAKING** 主界面启动从 UIManager.OpenWindowAsync 改为 Navigator.NavigateToAsync，MainController 并入 MainMenuProcedure
- `main-to-game-view-flow`: **BREAKING** 局内流程从 UIManager.OpenWindowAsync 改为 Navigator.NavigateToAsync，GameController 并入 GameProcedure

## Impact

- **核心文件新增**：ViewModelBase.cs、ReactiveProperty.cs、Navigator.cs、Shell.cs、Screen.cs、Region.cs、ScreenRegistry.cs、ScreenDescriptor.cs
- **核心文件删除**：UIView.cs、UIController.cs、UIManager.cs、UIRuntimeContext.cs、UIWindowDescriptor.cs、UIBindingCollection.cs、ControllerEventBinder.cs、UIPropertyBinding.cs
- **接口变更**：IUIManager → INavigator，注册/打开/关闭 API 全部重写
- **业务层迁移**：MainView/MainController → MainMenuScreen + MainViewModel，GameView/GameController → GameScreen + GameViewModel
- **Procedure 变更**：MainMenuProcedure 和 GameProcedure 接管原 Controller 职责（创建 ViewModel、订阅命令、调用 System）
- **测试影响**：现有 EditMode 测试需重写，但 ViewModel 可纯 C# 单元测试，不再依赖 Unity Context
