# EF.UI UGUI 框架

`Assets/EF/EFRuntime/UI` 是运行时 UGUI / Canvas / Prefab 驱动的 UI 框架。窗口由 `UIManager` 通过 YooAsset 地址加载 Prefab，实例化后挂到 UGUI 层级根节点，再协调 `UIView` 与 `UIController` 的生命周期。

编辑器工具或第三方包内部使用的 UIElements 不属于本运行时 UI 框架。

## 核心类型

- `IUIManager`：UI 管理器接口，提供窗口注册、打开、关闭、查询和层级根节点注册。
- `UIManager`：框架实现，负责 Prefab 加载、实例缓存、生命周期调度和逐帧更新。
- `UIView`：所有窗口 View 的基类，继承 `MonoBehaviour`，负责 UGUI 组件访问、事件绑定和显示刷新。
- `UIController`：窗口 Controller 基类，负责响应 View 事件、读取 Model、处理进入和退出逻辑。
- `UIWindowDescriptor`：窗口描述，包含窗口名、资源地址、View 类型、Controller 工厂、层级和缓存策略。
- `UIWindowHandle`：打开窗口后返回的句柄，可读取 View、Controller、状态并关闭实例。
- `UIWindowState`：窗口加载、打开、关闭、缓存和销毁状态。
- `UIRuntimeContext`：Controller/View 共享的运行时上下文，包含 `IUIManager`、`ModelManager`、窗口描述和层级根节点。
- `UILayer`：UGUI 四层枚举，包含 `Background`、`Normal`、`Popup`、`Overlay`。
- `UIBindingCollection`：View 持有的数据绑定集合，在窗口释放时统一 Dispose。
- `ControllerEventBinder`：Controller 侧事件订阅管理，保证退出或释放时取消订阅。
- `UHub`：`UIView` 的自动绑定入口，通过 `ReferenceCollector` 填充 Button、Text、Image、RectTransform 等字段。

## 层级约定

启动时 `GameLogicEntry` 从场景 `Entry` 的 `ReferenceCollector` 读取 UI 根节点并注册给 `IUIManager`：

- `Background`：背景层。
- `Normal`：主界面、局内界面等常规窗口。
- `Popup`：弹窗和模态窗口。
- `Overlay`：加载遮罩、顶层提示等最高层 UI。

推荐场景中显式配置 `Background`、`Normal`、`Popup`、`Overlay` 和 `UICamera`。如果旧场景只配置 `UIRoot`，入口会在 `UIRoot` 下运行时创建四层 RectTransform，并为 `UIRoot` 补齐 `Canvas`、`CanvasScaler` 和 `GraphicRaycaster`。

## 窗口命名

每个运行时窗口使用三件套：

- `{Stem}View : UIView`
- `{Stem}Controller : UIController`
- `{Stem}View.prefab`

例如主界面使用 `MainView`、`MainController` 和 YooAsset 地址 `"MainView"`；局内界面使用 `GameView`、`GameController` 和地址 `"GameView"`。资源收集规则当前为 `AddressByFileName`，因此 Prefab 文件名就是默认加载地址。

## 生命周期

首次打开窗口时，`UIManager` 按固定顺序执行：

1. 加载 Prefab 并实例化到目标 `UILayer` 根节点。
2. 获取或动态添加请求的 `UIView` 组件。
3. 创建 `UIController`。
4. `Controller.Initialize`。
5. `Controller.PrepareAsync`。
6. `View.Initialize`。
7. `View.Bindings`。
8. `View.PrepareAsync`。
9. `View.Open` 与 `View.Refresh`。
10. `Controller.Enter`。

关闭非缓存窗口时执行 `Controller.Exit`、`View.Close`、`View.Release`、`Controller.Release`、`Controller.Dispose`，随后销毁窗口 GameObject 并释放资源句柄。关闭缓存窗口时执行退出和关闭后隐藏实例，放入缓存栈供下次复用。

## View 与 Controller 分工

`UIView` 只处理 UGUI 控件、显示刷新和输入事件转发，不直接驱动流程或玩法系统。`OnInitialize` 中通常调用 `UHub.Initialize()`，`OnBindings` 中绑定 Button、Toggle 等事件，`OnRelease` 中清理 View 自己暴露的事件。

`UIController` 负责读取 Model、订阅 View 事件、处理命令和刷新 View。Controller 可以通过 `TryGetModel<TModel>()` 从 `ModelManager` 获取模型，也可以通过 `OpenWindowAsync` 的 `userData` 接收流程层传入的上下文。

局内 `GameView.prefab` 使用 UGUI 子视图恢复战斗交互：`GameView` 通过 `ReferenceCollector` 绑定玩家状态条、怪物容器、手牌容器、drop-zone、preview-layer、失败 toast、奖励面板和条目模板；`PlayerStatusView`、`MonsterListView`、`HandFanView`、`TargetSelector`、`TurnControlView`、`BattlePanelView` 只接收 UGUI 组件和上下文切片，不依赖运行时 UI Toolkit。手牌卡牌、怪物项、Buff/意图图标和预览卡牌均从 Prefab 模板实例化，拖拽和预览通过 `RectTransform`、`CanvasGroup`、`Button`、`Image` 和 `TextMeshProUGUI` 适配。

最小打开示例：

```csharp
await GameLogicEntry.UI.OpenWindowAsync<MainView, MainController>(
    "MainView",
    UILayer.Normal,
    cacheOnClose: true,
    allowMultiple: false);
```

## ReferenceCollector 与 UHub

Prefab 根对象应挂载 `ReferenceCollector`，收集 View 需要访问的 UGUI 组件。字段名默认按 `_startGameBtn` → `StartGameBtn` 推断，也可以使用 `[UHubBind("EndBtn")]` 指定旧 Prefab 中的 key。

常见 View 初始化：

```csharp
protected override void OnInitialize()
{
    base.OnInitialize();
    UHub.Initialize();
}
```

`UIView.InternalRelease()` 会 Dispose `UHub` 和 `UIBindingCollection`，因此通过 `BindEvent` 注册的 UnityEvent 会在窗口释放时取消订阅。

## 测试入口

优先使用 EditMode 测试覆盖框架生命周期和纯逻辑契约：

- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/UIManagerLifecycleTests.cs`
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/GameLogicEntryUiInitializationTests.cs`
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/ReferenceCollectorRuleTests.cs`

涉及真实 Prefab、Canvas、GraphicRaycaster、EventSystem 和输入交互的验证建议在 Unity Test Runner 或编辑器手动流程中完成。
