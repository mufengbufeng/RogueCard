## ADDED Requirements

### Requirement: UIManager 必须通过 UGUI Prefab 创建窗口

`IUIManager.OpenWindowAsync<TView,TController>()` SHALL 通过 `IResourceManager.LoadAssetAsync<GameObject>` 加载窗口 Prefab，实例化后挂到目标 `UILayer` 的 Transform 根节点，并为实例创建 `UIView` 与 `UIController` 生命周期上下文。

#### Scenario: 打开主界面窗口
- **WHEN** 调用 `OpenWindowAsync<MainView, MainController>("MainView", UILayer.Normal, ...)`
- **THEN** UIManager SHALL 加载 addressable `"MainView"` 对应的 Prefab
- **AND** 实例化对象 SHALL 挂到 Normal 层级根节点
- **AND** 实例 SHALL 拥有 `MainView` 组件
- **AND** UIManager SHALL 创建并初始化 `MainController`

#### Scenario: Prefab 缺少 View 组件时动态添加
- **WHEN** 加载到的 Prefab 根对象缺少请求的 `TView` 组件
- **THEN** UIManager SHALL 在实例根对象上添加 `TView` 组件
- **AND** SHALL 继续执行窗口初始化

### Requirement: UIManager 必须管理窗口生命周期顺序

UIManager SHALL 按固定顺序执行窗口生命周期：创建或复用实例、构造运行上下文、`Controller.Initialize`、`Controller.PrepareAsync`、`View.Initialize`、`View.Bindings`、`View.Open`、`Controller.Enter`。关闭窗口时 SHALL 执行 `Controller.Exit`、`View.Close`，并根据窗口描述决定缓存或释放。

#### Scenario: 窗口首次打开生命周期
- **WHEN** UIManager 首次打开一个未缓存窗口
- **THEN** Controller SHALL 先于 View 接收 Initialize
- **AND** View SHALL 在 Open 前执行 Bindings
- **AND** Controller SHALL 在 View.Open 后执行 Enter

#### Scenario: 关闭非缓存窗口
- **WHEN** 关闭 `cacheOnClose=false` 的窗口
- **THEN** UIManager SHALL 调用 Controller.Exit 和 View.Close
- **AND** SHALL 调用 View.Release、Controller.Release 和 Controller.Dispose
- **AND** SHALL 销毁窗口 GameObject

#### Scenario: 关闭缓存窗口
- **WHEN** 关闭 `cacheOnClose=true` 的窗口
- **THEN** UIManager SHALL 调用 Controller.Exit 和 View.Close
- **AND** SHALL 隐藏窗口 GameObject
- **AND** SHALL 将实例放入缓存栈供后续打开复用

### Requirement: UIManager 必须支持四层 UGUI 层级根节点

UIManager SHALL 支持 `UILayer.Background`、`UILayer.Normal`、`UILayer.Popup`、`UILayer.Overlay` 四个 UGUI 层级。`RegisterLayerRoot(layer, transform)` SHALL 注册对应层级根节点。打开窗口时，若目标层级未注册，UIManager SHALL 使用 fallback root；若两者都不存在，打开 SHALL 失败并给出明确错误。

#### Scenario: 注册并使用 Normal 层级
- **WHEN** `RegisterLayerRoot(UILayer.Normal, normalRoot)` 已被调用
- **AND** 打开窗口目标层级为 `UILayer.Normal`
- **THEN** 窗口实例 SHALL 挂到 `normalRoot`

#### Scenario: 层级缺失时使用 fallback
- **WHEN** 目标层级未注册但 fallback root 已设置
- **THEN** 窗口实例 SHALL 挂到 fallback root

#### Scenario: 无层级根节点时打开失败
- **WHEN** 目标层级未注册且 fallback root 也为空
- **THEN** UIManager SHALL 抛出 InvalidOperationException
- **AND** 错误信息 SHALL 包含目标 `UILayer`

### Requirement: UIView 必须支持 ReferenceCollector 与 UHub 自动绑定

`UIView` SHALL 继承 `MonoBehaviour`，并提供 UHub 访问入口。View 在初始化阶段 SHALL 能通过 `UHub.Initialize()` 从同一 GameObject 的 `ReferenceCollector` 绑定字段、事件和组件引用。

#### Scenario: View 初始化时绑定字段
- **WHEN** `MainView.OnInitialize()` 调用 `UHub.Initialize()`
- **AND** Prefab 根对象存在 `ReferenceCollector`
- **THEN** UHub SHALL 根据 ReferenceCollector 数据填充 `MainView` 中匹配的字段

#### Scenario: View 关闭或释放时清理绑定
- **WHEN** `UIView.InternalRelease()` 被调用
- **THEN** UHub SHALL Dispose 已绑定事件
- **AND** UIBindingCollection SHALL Dispose 已注册的数据绑定

### Requirement: Runtime 必须注册 UGUI UIManager

`GameEntry.Awake()` SHALL 在 AOT Runtime 层创建并注册 `IUIManager`，注册实例 SHALL 为依赖当前 `IResourceManager` 与 `ModelManager` 的 `UIManager`。

#### Scenario: 热更入口获取 UIManager
- **WHEN** `GameEntry.Awake()` 完成 EF 管理器注册
- **AND** `GameLogicEntry.Init()` 调用 `ModuleSystem.Get<IUIManager>()`
- **THEN** SHALL 返回已注册的 UGUI `UIManager` 实例

#### Scenario: ModuleSystem.Update 驱动 UIManager
- **WHEN** 热更入口初始化完成且 ModuleSystem 开始 Update
- **THEN** UIManager.Update SHALL 驱动所有已打开窗口的 View 和 Controller 更新
