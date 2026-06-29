# EF.UI UGUI 框架

`Assets/EF/EFRuntime/UI` 是运行时 UGUI / Canvas / Prefab 驱动的 UI 框架。窗口由 `UIManager` 通过资源系统加载 Prefab，实例化到已注册的 UGUI 层级根节点，再协调 `UIView` 与 `UIController` 的生命周期。

编辑器工具或第三方包内部使用的 UIElements 不属于本运行时 UI 框架。

## 当前架构

框架采用轻量 MVC：

- `ModelManager` 管理数据 Model，View 只读取展示所需数据，Controller 负责写入和流程协调。
- `UIView` 继承 `MonoBehaviour`，只处理 UGUI 组件、显示刷新和输入事件转发。
- `UIController` 是普通 C# 对象，持有 View 引用，响应 View 事件、读取或更新 Model、转发命令。
- `UIManager` 负责 Prefab 加载、实例挂载、缓存复用、生命周期顺序和逐帧更新。
- `ReferenceCollector` + `UHub` 负责 View 字段与 Prefab 组件的自动绑定。

热更新层在 `GameLogicEntry.InitializeContainer()` 中把 Controller 创建交给 VContainer，因此 Controller 可以使用 `[Inject]` 接收服务。未设置工厂时，框架使用反射创建 Controller。

## 核心类型

- `IUIManager`：UI 管理器接口，提供窗口注册、打开、关闭、查询、层级注册和 Controller 工厂设置。
- `UIManager`：框架实现，负责 Prefab 加载、实例缓存、生命周期调度和每帧更新。
- `IUIControllerFactory`：Controller 创建工厂接口，用于把 Controller 构造接入 DI 容器。
- `UIView`：所有窗口 View 的基类，提供 `UHub`、`Bindings`、`BindProperty`、`BindEvent` 和 View 生命周期。
- `UIController`：所有窗口 Controller 的基类，提供 `GetView<TView>()`、`TryGetModel<TModel>()`、`BindEvent` 和 Controller 生命周期。
- `UIWindowDescriptor`：窗口描述，包含窗口名、资源地址、View 类型、Controller 类型、层级、缓存策略和多实例策略。
- `UIWindowHandle`：打开窗口后返回的句柄，可读取 View、Controller、状态，并关闭当前实例。
- `UIWindowState`：窗口实例状态，包含 `Loading`、`Opening`、`Opened`、`Closing`、`Closed`、`Destroyed`。
- `UIRuntimeContext`：Controller/View 共享的运行时上下文，包含 `IUIManager`、`ModelManager`、窗口描述和当前层级根节点。
- `UILayer`：UGUI 四层枚举：`Background`、`Normal`、`Popup`、`Overlay`。
- `UIBindingCollection`：View 持有的数据绑定集合，窗口释放时统一 Dispose。
- `ControllerEventBinder`：Controller 侧事件订阅管理，`OnExit` 后自动取消订阅。
- `LocalEventBus`：窗口或流程内局部事件总线，适合局内 System 间通信，独立于全局 `EventHub`。
- `ReactiveProperty<T>`：简单响应式属性，值变化时触发 `Changed`。
- `UHub`：`UIView` 的自动绑定入口，通过 `ReferenceCollector` 填充 Button、Text、Image、RectTransform 等字段，并统一管理 UnityEvent 解绑。

## 初始化入口

`GameLogicEntry.Init()` 中的 UI 初始化分两步：

1. `InitializeUI()` 从场景 `Entry` 的 `ReferenceCollector` 读取 `Background`、`Normal`、`Popup`、`Overlay`、`UICamera` 和 `UIRoot`。
2. `InitializeContainer()` 构建 VContainer，并调用 `_uiManager.SetControllerFactory(new VContainerUIControllerFactory(_container))`。

推荐场景中显式配置四个层级根节点和 `UICamera`。如果旧场景只配置了 `UIRoot`，入口会在运行时补齐：

- 为 `UIRoot` 添加 `Canvas`、`CanvasScaler`、`GraphicRaycaster`。
- 在 `UIRoot` 下查找或创建 `Background`、`Normal`、`Popup`、`Overlay` 四个 `RectTransform`。
- 把 `UIRoot` 设置为 fallback root，避免旧资源缺少层级配置时完全不可用。

如果目标层级未注册且没有 fallback root，打开窗口会抛出明确异常。

## 层级约定

- `Background`：背景层，多用于主界面背景、全屏渲染。
- `Normal`：常规层，用于主界面、局内界面等主要交互窗口。
- `Popup`：弹窗层，用于模态窗口或浮层。
- `Overlay`：最高层，用于加载遮罩、顶层提示等。

所有窗口 Prefab 都会实例化到 `UIWindowDescriptor.Layer` 对应的根节点下。缓存窗口再次打开时也会重新挂到当前层级根节点，并更新 `UIRuntimeContext.LayerRoot`。

## 窗口命名与打开

每个运行时窗口优先使用三件套：

- `{Stem}View : UIView`
- `{Stem}Controller : UIController`
- `{Stem}View.prefab`

例如主界面是 `MainView`、`MainController`、`MainView.prefab`，YooAsset 地址为 `"MainView"`；局内界面是 `GameView`、`GameController`、`GameView.prefab`，地址为 `"GameView"`。资源收集规则当前按文件名生成地址，因此 Prefab 文件名就是默认加载地址。

优先使用单泛型打开接口：

```csharp
await GameLogicEntry.UI.OpenWindowAsync<MainView>(
    "MainView",
    UILayer.Normal);
```

单泛型接口会按命名约定解析 Controller：

- `MainView` -> `MainController`
- `GameView` -> `GameController`
- 如果 View 名不以 `View` 结尾，则追加 `Controller`

解析失败时会提示期望的 Controller 类型名。需要绕过命名约定时，可以显式指定 Controller：

```csharp
await GameLogicEntry.UI.OpenWindowAsync<MainView, MainController>(
    "MainView",
    UILayer.Normal);
```

所有泛型打开接口默认使用：

- `layer: UILayer.Normal`
- `cacheOnClose: true`
- `allowMultiple: false`

需要关闭时销毁实例，或允许同窗口多实例时，使用完整参数：

```csharp
await GameLogicEntry.UI.OpenWindowAsync<GameView>(
    "GameView",
    UILayer.Normal,
    cacheOnClose: false,
    allowMultiple: false,
    userData: viewModel);
```

也可以先注册 `UIWindowDescriptor`，再按窗口名打开：

```csharp
uiManager.RegisterWindow(UIWindowDescriptor.Create<MainView>(
    nameof(MainView),
    "MainView",
    UILayer.Normal));

await uiManager.OpenWindowAsync(nameof(MainView));
```

## 生命周期

首次打开窗口时，`UIManager` 按固定顺序执行：

1. 加载 Prefab，并实例化到目标 `UILayer` 根节点。
2. 获取 Prefab 根节点上的目标 `UIView`，缺失时动态添加该 View 组件。
3. 通过当前 `IUIControllerFactory` 创建 `UIController`。
4. 创建 `UIRuntimeContext` 并挂接 View、Controller、资源句柄。
5. `Controller.OnInitialize()`。
6. `Controller.OnPrepareAsync(userData, cancellationToken)`。
7. `View.OnInitialize()`。
8. 如果 `UHub` 已被访问，则执行 `UHub.Initialize()`。
9. `View.OnBindings()`。
10. `View.OnPrepareAsync(userData, cancellationToken)`。
11. `View.OnOpen(userData)`。
12. `View.OnRefresh(userData)`。
13. `Controller.OnEnter(userData)`。

打开已处于 `Opened` 状态的单实例窗口时，不会重新创建实例，只会调用：

- `Controller.OnRefresh(userData)`
- `View.OnRefresh(userData)`

窗口处于 `Opened` 状态时，`UIManager.Update()` 每帧调用：

- `Controller.OnUpdate(elapseSeconds, realElapseSeconds)`
- `View.OnUpdate(elapseSeconds, realElapseSeconds)`

关闭非缓存窗口时执行：

1. `Controller.OnExit()`，随后清理 Controller 事件绑定。
2. `View.OnClose()`。
3. `View.OnRelease()`，随后 Dispose `UHub` 和 `UIBindingCollection`。
4. `Controller.OnRelease()`。
5. `Controller.Dispose()`。
6. 销毁 View GameObject，并释放资源句柄。

关闭缓存窗口时只执行 `OnExit`、`OnClose`，然后隐藏实例并放入缓存栈。下次打开复用缓存实例时会执行 `View.OnOpen(userData)`、`View.OnRefresh(userData)` 和 `Controller.OnEnter(userData)`，不会重复执行 `OnInitialize` / `OnPrepareAsync` / `OnBindings`。

## View 与 Controller 分工

`UIView` 只处理 UI 表现：

- 使用 `UHub` / `ReferenceCollector` 获取 UGUI 组件。
- 在 `OnBindings()` 中绑定 Button、Toggle 等 UnityEvent。
- 暴露 C# 事件给 Controller，例如 `OnStartGameRequested`、`EndTurnClicked`。
- 提供 `SetText`、`Render`、`SetInteractable` 这类显示刷新接口。
- 在 `OnRelease()` 中清空 View 自己暴露的事件和手动持有的资源。

`UIController` 负责业务协调：

- 使用 `GetView<TView>()` 获取强类型 View。
- 使用 `TryGetModel<TModel>()` 从 `ModelManager` 获取或创建 Model。
- 在 `OnEnter()` 中订阅 View 事件，使用 `BindEvent` 自动管理取消订阅。
- 处理玩家输入、更新 Model、发布事件或调用流程层传入的 `userData`。
- 在 `OnRefresh()` 中把 Model 或 ViewModel 数据刷新到 View。

Controller 不应直接持有 Unity 组件；View 不应直接驱动流程或玩法系统。

## ReferenceCollector 与 UHub

Prefab 根对象应挂载 `ReferenceCollector`，登记 View 需要访问的 UGUI 组件。View 字段推荐使用私有字段，并通过 `[UHubBind]` 明确绑定 key：

```csharp
[UHubBind("StartGameBtn")]
private Button _startGameBtn;
```

未标注时，字段名会按 `_startGameBtn` -> `StartGameBtn` 推断；属性名直接使用属性名。需要排除字段时使用 `[UHubIgnore]`。

常见 View 写法：

```csharp
protected override void OnInitialize()
{
    base.OnInitialize();
    UHub.Initialize();
}

protected override void OnBindings()
{
    base.OnBindings();
    BindEvent(_startGameBtn.onClick, () => OnStartGameRequested?.Invoke());
}
```

`UIView.InternalInitialize()` 会在 `OnInitialize()` 后检查 `UHub` 是否已访问；如果已访问，会自动调用一次 `UHub.Initialize()`。显式调用是当前项目的主流写法，可让绑定发生点更清楚。`UHub.Initialize()` 本身是幂等的。

`BindEvent` 支持无参数 UnityEvent，以及 `bool`、`float`、`int`、`string` 参数的 UnityEvent。通过 `BindEvent` 注册的事件会在 View 释放时自动解绑。

`BindProperty` 可绑定实现 `INotifyPropertyChanged` 的数据对象到 UI setter：

```csharp
BindProperty(viewModel, x => x.Title, value => _titleText.text = value);
```

绑定对象会加入 `UIBindingCollection`，窗口释放时统一 Dispose。

## 局内 UI 组织

`GameView.prefab` 使用 UGUI 子视图恢复局内战斗交互。`GameView` 通过 `ReferenceCollector` 和 `transform.Find` 获取玩家状态条、怪物容器、手牌容器、drop-zone、preview-layer、失败 toast、奖励面板和条目模板。

局内子视图如 `PlayerStatusView`、`MonsterListView`、`HandFanView`、`TargetSelector`、`TurnControlView`、`BattlePanelView` 只接收 UGUI 组件和上下文切片，不依赖 UI Toolkit。手牌卡牌、怪物项、Buff/意图图标和预览卡牌均从 Prefab 模板实例化，拖拽和预览通过 `RectTransform`、`CanvasGroup`、`Button`、`Image` 和 `TextMeshProUGUI` 适配。

局内流程由 `GameProcedure` 创建 `GameViewModel`、`LocalEventBus` 和各类 System，再通过 `userData` 传给 `GameView` / `GameController`。流程退出时关闭 `GameView`，并由 `GameProcedure.Cleanup()` 释放 System、本地事件总线和流程侧 ViewModel 订阅。

## 实践约定

- 新窗口优先使用 `{Stem}View` / `{Stem}Controller` / `{Stem}View.prefab` 命名。
- Prefab 根节点必须可挂载目标 `UIView`；如果没有脚本，`UIManager` 会动态添加，但正式资源仍建议显式挂载，方便编辑器检查。
- 常驻窗口可使用默认 `cacheOnClose: true`；局内、强状态窗口建议显式 `cacheOnClose: false`。
- 单实例窗口保持默认 `allowMultiple: false`；弹窗堆叠才考虑 `allowMultiple: true`。
- View 对外暴露意图事件，不直接切流程、不直接操作 System。
- Controller 订阅 View 或 EventChannel 事件时优先使用 `BindEvent`，避免关闭后残留订阅。
- 需要 DI 的 Controller 要注册到 `GameLogicEntry.InitializeContainer()` 的 VContainer。
- `userData` 适合传入流程级上下文或 ViewModel，不适合传全局服务；全局服务走 DI 或 `GameLogicEntry`。

## 测试入口

优先使用 EditMode 测试覆盖框架生命周期和纯逻辑契约：

- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/UIManagerLifecycleTests.cs`
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/GameLogicEntryUiInitializationTests.cs`
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/ReferenceCollectorRuleTests.cs`
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/GameControllerCommandFlowTests.cs`
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/GameViewIntegrationTests.cs`

涉及真实 Prefab、Canvas、GraphicRaycaster、EventSystem 和输入交互的验证建议在 Unity Test Runner 或编辑器手动流程中完成。
