## Context

EasyFramework 的 UI 系统采用 MVC 架构（UIController / UIView / ModelBase），通过 UIManager 管理窗口生命周期。当前 UIView 继承 MonoBehaviour，通过 ReferenceCollector + UHub 实现 UGUI 组件的自动绑定。UIManager 通过 YooAsset 加载 GameObject Prefab 并 Instantiate 到 Transform 层级树中。

现有基础设施：
- `BindProperty` / `UIBindingCollection`：基于 `INotifyPropertyChanged` 的响应式绑定，与渲染层无关
- `ControllerEventBinder`：Controller 级别的事件自动清理
- `UISystem`：游戏逻辑独立层，持有 Model 和 EventPublisher
- `UIRuntimeContext`：运行时上下文，持有 ModelManager、EventPublisher 等
- `UIWindowDescriptor`：窗口元数据描述

项目仅有两个 UI 视图：MainView（主菜单）和 GameView（局内），外加 3 个子项 Prefab（MonsterItem、TipsItem、CardItem）。

## Goals / Non-Goals

**Goals:**
- UIView 从 MonoBehaviour 解耦为纯 C# 类，消除 GameObject 生命周期依赖
- UIManager 基于 VisualTreeAsset + VisualElement 实现窗口管理
- 利用 UI Toolkit 的 UQuery 替代 UHub 自动绑定，减少样板代码
- 多 UIDocument 分层架构，每层独立 PanelSettings 控制渲染排序
- 独立 .uss 样式文件，支持主题化和热替换
- UIView 基类提供 RegisterViewCallback 自动回调清理机制
- 保持 MVC 架构不变，保持 BindProperty / UISystem / UIController 核心接口不变

**Non-Goals:**
- 不修改 UISystem 抽象基类及其子类（CardSystem、MonsterSystem 等）
- 不修改 EventChannel / IEventPublisher 的实现
- 不修改 ModelManager 的注册/获取机制
- 不修改 UIController 的核心生命周期（OnInitialize / OnPrepareAsync / OnEnter / OnExit）
- 不删除 ReferenceCollector 及其编辑器（非 UI 场景仍使用）
- 不引入第三方 UI 框架或数据绑定库
- 不做 UI 动画系统设计（后续单独变更）

## Decisions

### D1: UIView 变为纯 C# 类，持有 VisualElement Root

**选择**: `public abstract class UIView : IDisposable`，不再继承 MonoBehaviour。

**替代方案**: UIView 仍是 MonoBehaviour，挂在与 UIDocument 同一个 GameObject 上，通过 `uidocument.rootVisualElement` 操作 UI。

**理由**: 纯 C# 类更轻量，不依赖 GameObject 生命周期，测试更简单（不需要场景）。UIManager 负责创建 UIView 实例和 CloneTree，View 只关心 VisualElement 树。

### D2: 多 UIDocument 层级架构

**选择**: 每层（Background / Normal / Popup / Overlay）使用独立的 UIDocument 组件，通过 PanelSettings.sortingOrder 控制渲染顺序。

**替代方案 A**: 单个 UIDocument，在 rootVisualElement 下创建四个容器 VisualElement。更简单但无法独立控制每层的缩放、排序和渲染设置。

**替代方案 B**: 单个 UIDocument + 多个 PanelSettings 通过代码动态切换。过于复杂。

**理由**: 多 UIDocument 方案与现有 UILayer 枚举一一对应，每层可独立配置 PanelSettings（如 Popup 层设置半透明背景），概念清晰。Unity 6 中多个 UIDocument 的性能开销可忽略。

### D3: RegisterViewCallback 自动清理机制

**选择**: UIView 基类提供 `RegisterViewCallback<TEventType>(VisualElement, EventCallback<TEventType>)` 辅助方法，内部追踪所有注册，OnRelease 时批量 UnregisterCallback。

**替代方案 A**: 手动管理，每个 View 在 OnClose 中显式 UnregisterCallback。更透明但容易遗漏。

**替代方案 B**: 利用 VisualElement 的 Dispose 事件树自动清理。UI Toolkit 的 RemoveFromHierarchy 不会自动清理 RegisterCallback 注册的托管委托。

**理由**: 与现有 ControllerEventBinder 模式一致，减少样板代码和遗漏风险。

### D4: 独立 .uss 文件管理样式

**选择**: 每个窗口一个主 .uss 文件，公共样式抽取到 shared.uss。UXML 通过 `<Style src="path.uss" />` 引用。

**替代方案**: 样式内联在 UXML 中。简单但不利于热替换和主题化。

**理由**: 独立 .uss 文件便于 YooAsset 热更新替换，后续支持主题切换只需替换 .uss 文件。

### D5: 资源加载路径不变，仅类型变化

**选择**: `UIWindowDescriptor.Location` 仍然是 YooAsset 资源路径字符串，但资源类型从 `GameObject`（Prefab）变为 `VisualTreeAsset`（UXML）。子项 Prefab 同样迁移为 VisualTreeAsset。

**理由**: YooAsset 支持加载 VisualTreeAsset，资源路径管理机制不需要变化。只需将 .prefab 文件替换为 .uxml 文件，保持路径命名约定一致。

### D6: 窗口缓存从 SetActive 改为 RemoveFromHierarchy

**选择**: 缓存窗口时 `root.RemoveFromHierarchy()` 并存入 Stack；复用时 `layerElement.Add(cachedRoot)`。显示/隐藏通过 `root.style.display = DisplayStyle.Flex / DisplayStyle.None`。

**替代方案**: 保留 VisualElement 在树中但设 `display: none`。仍然占用布局计算。

**理由**: RemoveFromHierarchy 完全脱离渲染树，性能更好。复用时 Add 回去即可，VisualElement 不像 GameObject 需要 Destroy/Instantiate。

### D7: UIController 调整最小化

**选择**: UIController 的 `View` 属性类型仍为 `UIView`（纯 C# 版）。Controller 不需要操作 VisualElement，只通过 View 暴露的 C# 事件和 Model 交互。

**理由**: Controller 已经是薄转发层，不应直接操作 UI 元素。所有 UI 操作都在 View 内部完成。

## Risks / Trade-offs

**[BREAKING] UIView 不再是 MonoBehaviour** → 所有继承 UIView 的类（GameView、MainView）不能使用 transform、gameObject、Start/Update 等 MonoBehaviour API。需要逐一审查并替换为 VisualElement 等价操作。

**[BREAKING] IUIManager 接口变更** → `RegisterLayerRoot(UILayer, Transform)` 和 `SetFallbackRoot(Transform)` 签名变化，所有调用方（如 GameLogicEntry.Init）需要适配。

**YooAsset 加载 VisualTreeAsset 的资源管理** → 需要确认 YooAsset 的 `LoadAssetAsync<VisualTreeAsset>()` 返回的 AssetHandle 释放行为与 GameObject 一致。VisualTreeAsset 是纯数据资产，不涉及实例化计数，释放策略可能不同。

**UI Toolkit 的 TextMeshPro 集成** → UI Toolkit 使用原生 `Label` / `TextField` 而非 TextMeshPro。如果项目依赖 TMP 的高级排版功能（如富文本标签、字体图集），需要验证 UI Toolkit 的 TextElement 是否满足需求。Unity 6 中 UI Toolkit 的文本渲染质量已大幅提升。

**子项 Prefab 迁移** → GamePlay_MonsterItem、GamePlay_TipsItem、GamePlay_CardItem 三个子项 Prefab 需要重新制作为 UXML 模板。如果有美术在 Prefab 中做的布局调整，需要手动迁移到 UXML/USS。

**场景设置变更** → 需要在场景中创建新的 UIDocument 层级结构（替代现有 Canvas）。这是一次性操作，但需要在场景中手动设置或通过编辑器脚本自动化。

## Migration Plan

1. 框架层先行：重写 UIView、UIManager、IUIManager、UIRuntimeContext、UIWindowDescriptor
2. 创建 UIDocument 层级场景结构
3. 制作 UXML/USS 资源文件替代现有 Prefab
4. 重写 GameView 和 MainView
5. 小改 GameController 和 MainController
6. 更新 GameLogicEntry.Init 中的层根注册调用
7. 删除 UHub/ 目录
8. 更新 EditMode 测试
9. 编译验证和运行时验证
