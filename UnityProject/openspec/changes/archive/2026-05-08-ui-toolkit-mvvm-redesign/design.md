## Context

当前 EF UI 框架基于自研 MVC 模式：UIView（纯 C#）包装 VisualElement、UIController 协调 View 和 Model、UIManager 管理窗口生命周期。项目已迁移到 UI Toolkit（UXML + USS），但框架层仍以 UGUI 时代的心智模型运作：手动 UQuery、手动事件注册/清理、手动 BindProperty。

核心矛盾：
- UIView 不是 VisualElement，而是在外部包装了一层，每次都要手动穿透到 Root
- 手写的数据绑定（UIPropertyBinding），未利用 UI Toolkit 的响应式模式
- Controller 通过 C# event 转发 View 交互，增加间接层
- 无弹窗栈管理，无 Screen 内部子区域切换能力

约束：
- 框架代码在 `Assets/EF/EFRuntime/UI/`，属于 AOT 层
- 业务代码（Screen、ViewModel）在 `Assets/GameScripts/HotFix/`，属于热更新层
- 必须通过 HybridCLR 热更新加载，框架 API 不能依赖泛型反射
- 异步操作使用 UniTask
- 资源加载走 YooAsset（IResourceManager）

## Goals / Non-Goals

**Goals:**
- Screen 就是 VisualElement 子类，UXML 内容作为其子节点挂载
- ViewModel 是纯 C# 数据容器（ReactiveProperty + 命令意图事件），可脱离 Unity 独立测试
- Navigator 专职管理 Screen 内容替换和 Popup 栈式导航
- Region 支持 Screen 内部动态切换子区域内容
- ViewModel.Dispose() 自动清理所有 ReactiveProperty 监听者，Screen 端无需手动取消订阅
- Procedure 承担原 Controller 职责（创建 ViewModel、订阅命令、调用 System）

**Non-Goals:**
- 不实现 UI 过渡动画（Screen 切换直接替换）
- 不实现 Screen 缓存/池化机制
- 不使用 Unity 6 原生 Data Binding API（Runtime 支持有限）
- 不使用 Manipulator 模式（保持简单，直接 RegisterCallback）
- 不实现自定义 UXML 元素（UxmlFactory/UxmlTraits），Screen 通过 CloneTree + Add 挂载内容

## Decisions

### 1. Screen 继承 VisualElement 而非纯 C# 包装

**选择**：`Screen<TViewModel> : VisualElement`
**替代方案**：纯 C# 类持有 VisualElement 引用（当前 UIView 方式）
**理由**：Screen 是 VisualElement 后可直接参与元素树操作（Q<T>、Add、RemoveFromHierarchy），不需要额外持有 Root 引用。UXML 克隆内容作为 Screen 的子节点，树结构清晰：`Layer → Screen → TemplateContainer → 实际元素`。

### 2. ReactiveProperty 手写而非使用 Unity 原生绑定

**选择**：自研 `ReactiveProperty<T>` + `ViewModelBase.Prop<T>()` 工厂方法
**替代方案**：使用 Unity 6 `INotifyBindablePropertyChanged` + `SetBinding()`
**理由**：Unity 6 Runtime 绑定 API 主要为 Editor 设计，Runtime 能力有限。自研方案更简单、可控、可测试。ViewModelBase 通过 `Prop<T>()` 工厂方法自动追踪所有属性，Dispose 时一键清理。

### 3. ViewModel 命令通过 C# event 暴露意图

**选择**：ViewModel 暴露 `event Action` / `event Action<T>` 意图事件
**替代方案**：ICommand 接口模式（WPF 风格）
**理由**：ICommand 模式增加复杂度，对卡牌游戏来说收益不大。C# event 足够表达意图，Procedure 订阅后执行逻辑。ViewModel 不持有 Navigator 引用，保持可测试性。

### 4. Controller 角色并入 Procedure

**选择**：Procedure 创建 ViewModel、订阅命令、创建 System、调用 Navigator
**替代方案**：保留轻量 Coordinator 层
**理由**：卡牌游戏 Screen 数量有限（Main、Game），Procedure 本身就是流程协调器，额外加一层 Coordinator 徒增间接。Procedure 已经管理游戏流程状态，接管 UI 协调职责是自然的。

### 5. Shell 框架自建层级容器

**选择**：Shell 在构造时创建 ScreenLayer / PopupLayer / SystemLayer
**替代方案**：外部通过 `RegisterLayerElement()` 注册（当前方式）
**理由**：层级是框架内部概念，不应暴露给外部。Shell 构造时创建所有层级容器，挂到 UIDocument.rootVisualElement 下。

### 6. Region 作为 Screen 内部组件

**选择**：Region 持有 VisualElement 插槽引用，按需 CloneTree 加载子内容
**替代方案**：通过 Navigator 管理嵌套导航
**理由**：Region 是 Screen 内部的 UI 细节，不需要 Navigator 参与。Screen 自己管理 Region 内容切换，ViewModel 通过 ReactiveProperty 驱动切换时机。

## Risks / Trade-offs

- **[Breaking Change]** 全部 UI 代码需重写 → 逐个 Screen 迁移，先建框架再迁业务
- **[模板嵌套深度]** 树结构多了一层 Screen wrapper → 对 UQuery 和性能影响可忽略
- **[Procedure 膨胀]** Procedure 承担 Controller 职责后代码量增加 → 保持 Procedure 只做协调，逻辑在 System 中
- **[异步 Region]** Region.ShowAsync 加载 VTA 是异步操作 → 切换瞬间可能有短暂空白，后续可加 loading 指示器
- **[事件泄漏]** ViewModel 命令意图事件（event Action）不在 Dispose 自动清理范围内 → Procedure 退出时必须 -= 取消订阅
