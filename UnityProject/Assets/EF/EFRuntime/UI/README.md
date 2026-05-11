# EF UI 系统

基于 **UI Toolkit (UITK)** 的 MVVM 风格 UI 框架。`Shell` 把 `UIDocument.rootVisualElement`
拆成三层命名容器，`Navigator` 按命名约定 + 类型反射在层之间替换 `Screen` / 推弹窗，
`Screen<TViewModel>` 通过 `ReactiveProperty<T>` 与 `ViewModelBase` 完成数据绑定。
整套框架不依赖 UGUI、不依赖 MonoBehaviour（除 `UIDocument` 自身）、不依赖反射资产配置。

> **API 更新（convention-based-screen-resolution）**：
> 本框架已改为基于命名约定的 Screen 解析模式。新增 Screen **不再需要**在
> `GameLogicEntry` 或任何中心列表注册；Navigator 按 `{Stem}View → {Stem}Uxml/{Stem}Uss`
> 命名约定加载资源，按 `Popup<T>` vs `Screen<T>` 基类继承关系分流到 PopupLayer / ScreenLayer。
> 旧 `NavigateToAsync` / `PushPopupAsync` / `PopPopup` API 已合并为
> `OpenAsync<TScreen>()` / `OpenAsync(string)` / `Close()` / `CloseAll()`；
> `ScreenRegistry` 已删除。

## 目录

- [架构总览](#架构总览)
- [核心组件](#核心组件)
- [生命周期](#生命周期)
- [数据绑定模式](#数据绑定模式)
- [Procedure ↔ Screen 协作](#procedure--screen-协作)
- [Root.uxml 约定](#rootuxml-约定)
- [快速开始](#快速开始)
- [测试入口](#测试入口)
- [遗留与占位](#遗留与占位)

---

## 架构总览

```
┌──────────────────────────────────────────────┐
│ UIDocument (Scene)                           │
│   rootVisualElement                          │
│     └── Root.uxml                            │
│           ├── screen-layer  ← 单 Screen      │
│           ├── popup-layer   ← Popup 栈       │
│           └── system-layer  ← Toast/Loading  │
└──────────────────────────────────────────────┘
        │ Shell 解析三个命名层
        ▼
┌──────────────┐  Navigator  ┌──────────────────────┐
│  Procedure   │────────────▶│  Screen<TViewModel>  │
│ (业务流程)   │             │  (VisualElement)     │
└──────────────┘             └──────────────────────┘
        │ 创建 + 持有                   │ OnSetup
        ▼                                ▼
┌──────────────────┐  Changed   ┌──────────────────┐
│  ViewModelBase   │───────────▶│ ReactiveProperty │
│  (Prop 工厂追踪) │            │ <T>              │
└──────────────────┘            └──────────────────┘
```

- **Shell**：从 `rootVisualElement` 解析 `screen-layer` / `popup-layer` / `system-layer`。
- **Navigator**：`OpenAsync<TScreen>()` 或 `OpenAsync("StemView")` 按类型/字符串打开；目标类型派生自 `Popup<>` 时入栈到 PopupLayer，否则替换 ScreenLayer。
- **Screen&lt;TViewModel&gt; / Popup&lt;TViewModel&gt;**：继承 `VisualElement`，UXML 内容作为子节点 `CloneTree` 挂入。`UxmlLocation` / `UssLocation` 虚属性默认按 `{Stem}View → {Stem}Uxml / {Stem}Uss` 推导。
- **ViewModelBase**：通过 `Prop<T>` 创建并追踪 `ReactiveProperty`，`Dispose` 时一次性 `ClearListeners`。
- **Procedure** 拥有 ViewModel + 订阅命令意图事件，View 仅做 UQuery + 绑定。

---

## 核心组件

| 类型                     | 职责                                                | 关键 API                                                                          | 约束 / 注意                                                                       |
| ------------------------ | --------------------------------------------------- | --------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `Shell`                  | 持有三个命名层 `VisualElement` 引用                 | `ScreenLayer` / `PopupLayer` / `SystemLayer`                                      | `root` 为 null 抛 `ArgumentNullException`；缺层抛 `InvalidOperationException`     |
| `INavigator`             | 导航服务接口                                        | `OpenAsync<TScreen>()` / `OpenAsync(string)` / `Close()` / `CloseAll()` / `Shutdown` | 由 `Navigator` 实现；构造依赖 `Shell` + `IResourceManager`                       |
| `Navigator`              | Screen 替换 + Popup 栈 + 命名约定解析 + 反射类型缓存 | 同上                                                                              | 按 `Popup<>` 基类继承分流；字符串重载首次调用全程序集扫描 + 字典缓存；同名冲突抛异常 |
| `Popup<TViewModel>`      | 弹窗类型 marker 基类                                | (无方法，仅类型标记)                                                              | 派生自此类的 Screen 走 PopupLayer 栈；非 Popup 派生类型走 ScreenLayer 替换        |
| `Screen` (非泛型)        | 让 `Navigator` 通过基类引用 Screen，规避泛型协变    | `LoadContent(vta)` / `Setup(viewModel)` / `OnShow` / `OnHide` / `OnDispose`       | `Activator.CreateInstance` + 非泛型基类是 Navigator 真实路径，避免 `InvalidCastException` |
| `Screen<TViewModel>`     | 强类型 Screen 基类                                  | `OnSetup` (子类重写)                                                              | `Setup` 类型不匹配抛 `ArgumentException`；`OnDispose` 自动 Dispose ViewModel + 自脱树 |
| `ViewModelBase`          | 创建并追踪 `ReactiveProperty`，统一清理监听者       | `Prop<T>(initialValue)` / `Dispose()`                                             | `Dispose` 幂等；只清监听者，不清属性值                                            |
| `ReactiveProperty<T>`    | 值变化触发 `Changed` 事件                           | `Value` / `Changed` / `ClearListeners()`                                          | 仅在新旧值不等时触发；`Value` setter 用 `EqualityComparer<T>.Default`             |
| `Region`                 | Screen 内可切换内容插槽                             | `ShowAsync(uxmlLocation)` / `Show(VisualElement)` / `Clear()` / `CurrentContent`  | 构造时传入 UXML 中的空容器 + `IResourceManager`；加载失败仅记 `Log.Warning`       |
| `LocalEventBus`          | 窗口内 System 间事件总线，独立于全局 `EventHub`     | `GetChannel<T>() where T : struct`                                                | 实现 `EF.Event.IEventPublisher`；`Dispose` 释放所有 Channel                       |

### 三处非显而易见的行为

**1. `Screen` 用非泛型基类规避协变**

`Navigator` 通过 `Activator.CreateInstance(descriptor.ScreenType)` 构造实例，
然后以非泛型 `Screen` 引用调用 `LoadContent` / `Setup(ViewModelBase)` / `OnShow` / `OnDispose`。
泛型 `Screen<TViewModel>` 把 `Setup` 标记为 `sealed override`，在内部检查并强转类型，
错误类型抛 `ArgumentException` 立即失败，而不是等到使用 `ViewModel` 时才崩。

**2. `Navigator.OpenAsync` 打开 Popup 时的异常路径回滚**

当目标类型派生自 `Popup<>` 时，`OpenAsync` 走 PopupLayer 路径。打开期间任何阶段抛异常
（资源加载失败、`Activator.CreateInstance` 失败、`Setup` 类型错误等），
`Navigator` 都会 `RemoveFromHierarchy` 已添加到 `PopupLayer` 的 overlay 和 popup，
确保 PopupLayer 不会留下半透明遮罩或空白节点。Overlay 是 `VisualElement`
背景色 `(0, 0, 0, 0.6)` 的全屏 `Position.Absolute`。

**3. `ViewModelBase.Prop<T>` 自动追踪 + Dispose 清理**

```csharp
// ViewModel 构造时只用 Prop，不要 new ReactiveProperty<T>()
StatusText = Prop<string>("初始文本");
CanStart   = Prop(true);
```

`ViewModelBase` 内部用 `List<ReactivePropertyBase>` 记录所有 `Prop` 出来的属性，
`Dispose` 时遍历调用 `ClearListeners()`。`Dispose` 幂等，多次调用安全，
且**不会清空属性值** —— `Dispose` 后 `vm.StatusText.Value = "x"` 仍可读写，只是没人收到通知。

---

## 生命周期

### `OpenAsync<TScreen>(viewModel, ct)` / `OpenAsync(string viewName, viewModel, ct)`

```
OpenAsync (按类型 / 按字符串名都进同一内部流程)
  │
  ├── 字符串重载：先反射查 _typeCache，未命中扫描 AppDomain 程序集填充
  │     ↑ 同名冲突抛 InvalidOperationException 并提示用类型重载
  │
  ├── 实例化 Screen：Activator.CreateInstance(screenType)
  ├── 判断是否 Popup：IsSubclassOfRawGeneric(screenType, typeof(Popup<>))
  │
  ├── 加载 UXML（必需）：ResourceManager.LoadAssetAsync<VisualTreeAsset>(screen.UxmlLocation)
  │     ↑ 默认按 {Stem}View → {Stem}Uxml 命名约定推导，子类可 override
  │     ↑ 失败抛 InvalidOperationException
  │
  ├── 加载 USS（可选）：ResourceManager.LoadAssetAsync<StyleSheet>(screen.UssLocation)
  │     ↑ 缺失：DEBUG 警告一次 + Release 静默，Screen 仍正常加载
  │
  ├── 解析 ViewModel 类型：沿继承链找 Screen<>/Popup<> 闭合泛型，取第一个泛型参数
  ├── ViewModel：调用方传入则用，否则 Activator.CreateInstance(viewModelType)
  │
  ├── 分流：
  │   ├── isPopup → MountPopup：CreateOverlay → PopupLayer.Add(overlay) + PopupLayer.Add(popup)
  │   │              → Setup → OnShow → _popupStack.Push
  │   │              ↑ 任意阶段抛异常时回滚 overlay/popup
  │   │
  │   └── !isPopup → MountScreen：关闭 _currentScreen → ScreenLayer.Add(screen)
  │                  → Setup → OnShow → _currentScreen = screen
```

### `Close()` / `CloseAll()` / `Shutdown()`

```
Close()
  ├── _popupStack 空 → 直接返回（不影响 ScreenLayer）
  └── pop → entry.Popup.OnHide() → entry.Popup.OnDispose() → entry.Overlay.RemoveFromHierarchy()

CloseAll()
  └── 循环 Close 所有 Popup（保留 ScreenLayer 当前 Screen）

Shutdown()
  ├── 弹窗栈逐个：try { OnHide() } catch { } / try { OnDispose() } catch { } / overlay 脱树
  ├── _currentScreen：try { OnHide() } catch { } / try { OnDispose() } catch { }
  └── ScreenLayer.Clear() / PopupLayer.Clear() / SystemLayer.Clear()
```

`Shutdown` 用 `try { } catch { }` 兜底，保证单个 ViewModel `Dispose` 抛异常**不会阻断**后续清理。

---

## 数据绑定模式

标准模式：在 `Screen.OnSetup` 中（1）UQuery 抓元素 → （2）订阅 `ReactiveProperty.Changed` →
（3）注册命令意图回调 → （4）用 `vm.Xxx.Value` 同步初始值。

来自 `MainView.OnSetup`（路径：`Assets/GameScripts/HotFix/GameLogic/UI/Main/MainView.cs`）：

```csharp
protected override void OnSetup()
{
    _statusLabel    = this.Q<Label>("status-text");
    _levelNameLabel = this.Q<Label>("level-name");
    _levelDescLabel = this.Q<Label>("level-desc");
    _feedbackLabel  = this.Q<Label>("feedback-text");
    _startBtn       = this.Q<Button>("start-btn");

    // 数据绑定：ViewModel → VisualElement
    ViewModel.StatusText.Changed += v => SetText(_statusLabel, v);
    ViewModel.LevelName.Changed  += v => SetText(_levelNameLabel, v);
    ViewModel.LevelDesc.Changed  += v => SetText(_levelDescLabel, v);
    ViewModel.CanStart.Changed   += v =>
    {
        if (_startBtn != null) _startBtn.SetEnabled(v);
    };

    // 命令绑定：VisualElement → ViewModel
    if (_startBtn != null)
    {
        _startBtn.RegisterCallback<ClickEvent>(_ => ViewModel.RequestStart());
    }

    // 初始值同步（订阅写在赋值前，所以这里需要主动刷一次 UI）
    SetText(_statusLabel,    ViewModel.StatusText.Value);
    SetText(_levelNameLabel, ViewModel.LevelName.Value);
    SetText(_levelDescLabel, ViewModel.LevelDesc.Value);
}
```

对应的 `MainViewModel`（`Assets/GameScripts/HotFix/GameLogic/UI/Main/MainViewModel.cs`）：

```csharp
public class MainViewModel : ViewModelBase
{
    public ReactiveProperty<string> StatusText { get; private set; }
    public ReactiveProperty<string> LevelName  { get; private set; }
    public ReactiveProperty<string> LevelDesc  { get; private set; }
    public ReactiveProperty<bool>   CanStart   { get; private set; }

    public event Action StartRequested;            // 命令意图事件
    public void RequestStart() => StartRequested?.Invoke();

    public MainViewModel()
    {
        StatusText = Prop<string>();               // 自动追踪
        LevelName  = Prop<string>();
        LevelDesc  = Prop<string>();
        CanStart   = Prop(true);                   // 初始值
    }
}
```

**清理路径**：`Screen<TViewModel>.OnDispose` 默认调用 `ViewModel.Dispose()`（清理所有
`Prop` 出来的监听者）+ 把 `Screen` 从树中 `RemoveFromHierarchy`。Procedure 自己持有的
`StartRequested` 订阅需要在 `OnLeave` 里手动 `-=` 解开（见下一节）。

---

## Procedure ↔ Screen 协作

Procedure 是 ViewModel 的**唯一所有者**：在 `OnEnter` 创建 ViewModel、填充数据、订阅命令意图事件、
调用 `_navigator.OpenAsync<TScreen>(vm)`；在 `OnLeave` 解订阅。Screen 只通过
`ReactiveProperty` 读 / `RegisterCallback` 写，**不直接持有 Procedure 引用**。

来自 `MainMenuProcedure`（`Assets/GameScripts/HotFix/GameLogic/Procedure/Main/MainMenuProcedure.cs`）：

```csharp
public class MainMenuProcedure : ProcedureBase
{
    private INavigator _navigator;
    private MainViewModel _viewModel;

    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        _navigator = GameLogicEntry.Navigator;        // 延后到 OnEnter 取，确保已初始化
        EnterAsync().Forget();
    }

    private async UniTaskVoid EnterAsync()
    {
        _viewModel = new MainViewModel();
        PopulateFromConfig(_viewModel);               // 从配置表填 ReactiveProperty.Value
        _viewModel.StartRequested += OnStartRequested;

        await _navigator.OpenAsync<MainView>(_viewModel);
    }

    private void OnStartRequested()
    {
        // 处理命令意图：切流程
        GameProcedure.PendingLevelId = _viewModel.DefaultLevelId;
        ChangeState<GameProcedure>(_procedureOwner);
    }

    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        if (_viewModel != null)
        {
            _viewModel.StartRequested -= OnStartRequested;
            _viewModel = null;
        }
    }
}
```

**协作要点**：

- ViewModel 暴露 `event Action StartRequested` 之类的"命令意图"，View 通过 `vm.RequestStart()` 触发。
- Procedure 订阅 `StartRequested` 处理业务逻辑（切流程、写 Model、上报埋点）。
- `Navigator` 切到下一个 Screen 时会调旧 Screen 的 `OnDispose`，自动清理 ViewModel 的 `ReactiveProperty` 监听者；但 Procedure 自己加上的 `+= OnStartRequested` 必须自己 `-=`。

---

## Root.uxml 约定

`Assets/AssetRaw/UI/Root.uxml`：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="root" style="flex-grow: 1; width: 100%; height: 100%;">
        <ui:VisualElement name="screen-layer" picking-mode="Ignore"
                          style="position: absolute; left: 0; top: 0; right: 0; bottom: 0;" />
        <ui:VisualElement name="popup-layer"  picking-mode="Ignore"
                          style="position: absolute; left: 0; top: 0; right: 0; bottom: 0;" />
        <ui:VisualElement name="system-layer" picking-mode="Ignore"
                          style="position: absolute; left: 0; top: 0; right: 0; bottom: 0;" />
    </ui:VisualElement>
</ui:UXML>
```

- 三层 `picking-mode="Ignore"`：空层不拦截点击事件，避免 popup-layer 在没弹窗时挡住主界面。
- **`PanelSettings.ScaleMode`** 直接驱动 `rootVisualElement` 尺寸，框架不再额外撑满。
- **推荐**：把 `UIDocument.SourceAsset` 设为 `Root.uxml`。
- **回退**：如果 `UIDocument` 没配 `SourceAsset`，`GameLogicEntry.InitializeNavigator` 会
  在运行时 `LoadAssetSync<VisualTreeAsset>("Root")` 并 `CloneTree(root)`（仅用于兜底）。

---

## 快速开始

**第 1 步：写一个 `{Stem}View` 类 + 同名 UXML/USS 资源**

```csharp
// Assets/GameScripts/HotFix/GameLogic/UI/MyFeature/MyFeatureView.cs
public sealed class MyFeatureView : Screen<MyFeatureViewModel>
{
    protected override void OnSetup()
    {
        // UQuery + 绑定 ReactiveProperty + 注册命令意图
    }
}
```

资源放在 `Assets/AssetRaw/UI/MyFeature/`：`MyFeatureUxml.uxml` + `MyFeatureUss.uss`（USS 可选）。

**第 2 步：构建 Navigator（启动期，仅一次）—— 来自 `GameLogicEntry.InitializeNavigator`**：

```csharp
var uiDocument = Object.FindFirstObjectByType<UIDocument>();
var shell = new Shell(uiDocument.rootVisualElement);
_navigator = new Navigator(shell, _resourceManager);
// 不再需要任何 Screen 注册——新增 Screen 由命名约定 + 反射在 OpenAsync 内部解析
```

**第 3 步：在 Procedure 中创建 ViewModel + 订阅命令意图**：

```csharp
var vm = new MyFeatureViewModel();
vm.StatusText.Value = "准备就绪";
vm.SomeCommand    += OnSomeCommand;
```

**第 4 步：通过 Navigator 打开 Screen**：

```csharp
await _navigator.OpenAsync<MyFeatureView>(vm);

// 或者按字符串（数据驱动场景）：
await _navigator.OpenAsync("MyFeatureView", vm);
```

`Navigator` 会按命名约定加载 `MyFeatureUxml.uxml`（必需）和 `MyFeatureUss.uss`（可选）、
构造 `MyFeatureView` 实例、触发 `OnSetup` 完成绑定。

**弹窗 Popup**：把基类换成 `Popup<TViewModel>`，Navigator 自动改走 PopupLayer 栈式管理：

```csharp
public sealed class SettingsView : Popup<SettingsViewModel> { ... }
await _navigator.OpenAsync<SettingsView>(vm);   // 入栈
_navigator.Close();                              // 关闭顶层弹窗
```

### `LocalEventBus` vs 全局 `EventHub`

- **全局 `EventHub`**：跨窗口 / 跨流程 / 跨系统的广播事件（如战斗结算 → UI 多处刷新）。
  通过 `ModuleSystem.Get<IEventManager>()` 或 `GameLogicEntry.Event` 访问。
- **`LocalEventBus`**：单个 Screen / 复合窗口内部的"系统间通信"。生命周期跟随 Screen，
  `Dispose` 时自动释放所有 Channel，避免向已关闭窗口推送。两者都实现 `IEventPublisher`，
  共用 `EventChannel<T> where T : struct` 类型契约。

---

## 测试入口

EditMode 测试在程序集 `GameLogic.Tests.EditMode`，目录 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Framework/`：

| 文件                       | 一句话目的                                                              |
| -------------------------- | ----------------------------------------------------------------------- |
| `ShellAndRegistryTests`         | 验证 `Shell` 解析三层 / 缺层抛异常（ScreenRegistry 删除后该文件仅保留 Shell 部分） |
| `ScreenConventionTests`         | 验证 `Screen<T>.UxmlLocation` / `UssLocation` 默认按 `{Stem}View → {Stem}Uxml/Uss` 推导，子类可 override，Popup 派生类型走相同约定 |
| `NavigatorTypeResolutionTests`  | 验证 `Navigator` 字符串重载的早期失败路径（不存在类型 / 空字符串 / 构造参数 null），`Close` / `CloseAll` 在空栈下静默 |
| `ScreenLifecycleTests`          | 验证 `Activator.CreateInstance` + 非泛型 `Screen` 引用 + `Setup(ViewModelBase)` 完整路径，错误类型快速失败，`OnDispose` 销毁 ViewModel + 自脱树 |
| `ReactivePropertyTests`         | 验证 `Value` 仅在变化时触发 `Changed`、`ClearListeners` 后不再回调，`ViewModelBase.Prop` 追踪 + `Dispose` 清理 + 幂等性 |

修改 `Shell` / `Navigator` / `Screen` / `Popup` / `ReactiveProperty` / `ViewModelBase` 时，先把这些测试跑过。

---

## 遗留与占位

- **`UILayer` 枚举**（`UILayer.cs`）：旧 UI 工具链的遗留类型，仅供
  `ReferenceCollectorScriptGenerator` 模板字符串引用，**新框架不消费**。新代码不要引用 `UILayer.*`。
- **`UHub/` 目录**：当前为空，**预留给后续按窗口聚合的 UI Hub 抽象**（计划中：把窗口内的
  `Region` / `LocalEventBus` / 子系统统一打包），本框架版本不依赖它。

---

## 依赖项

- **Unity 6000.3+**（UI Toolkit Runtime）
- **Cysharp.Threading.Tasks (UniTask)** —— 异步导航
- **EF.Resource.IResourceManager** —— UXML 资源加载（YooAsset）
- **EF.Event.IEventPublisher / EventChannel&lt;T&gt;** —— `LocalEventBus` 复用
- **EF.Debugger.Log** —— 失败路径日志
