## ADDED Requirements

### Requirement: UIView 纯 C# 基类持有 VisualElement Root
UIView SHALL 为纯 C# 抽象类（`public abstract class UIView : IDisposable`），不继承 MonoBehaviour。UIView SHALL 持有一个 `VisualElement Root` 属性，代表 UXML CloneTree 的根节点。

#### Scenario: UIView 创建并持有 VisualElement Root
- **WHEN** UIManager 通过 `view.InternalInitialize(root, context)` 初始化 UIView
- **THEN** UIView 的 Root 属性 SHALL 被设置为传入的 VisualElement 实例

#### Scenario: UIView 释放时清理 VisualElement
- **WHEN** UIView 的 Dispose 方法被调用
- **THEN** SHALL 从父节点移除 Root（`Root.RemoveFromHierarchy()`），清空所有绑定，将 Root 和 Context 设为 null

### Requirement: UIView 通过 UQuery 查找元素
UIView SHALL 使用 `root.Q<T>("name")` 或 `root.Q<T>(className: "...")` 进行元素查找，不依赖任何自动绑定框架。

#### Scenario: 在 OnInitialize 中查找命名元素
- **WHEN** UIView 子类在 OnInitialize 中调用 `Root.Q<Button>("start-btn")`
- **THEN** SHALL 返回 UXML 中 name 为 "start-btn" 的 Button 元素，如果不存在则返回 null

### Requirement: UIView RegisterViewCallback 自动回调清理
UIView SHALL 提供 `RegisterViewCallback<TEventType>(VisualElement target, EventCallback<TEventType> handler)` 方法，内部追踪所有注册的回调。当 UIView 被释放时（OnRelease / Dispose），SHALL 批量对所有已追踪的目标元素执行 `UnregisterCallback`。

#### Scenario: 注册并在释放时自动清理回调
- **WHEN** UIView 子类通过 `RegisterViewCallback` 注册了多个 ClickEvent 回调
- **AND** UIView 被释放（Dispose）
- **THEN** 所有已注册的回调 SHALL 被自动 UnregisterCallback，已追踪列表被清空

#### Scenario: RegisterViewCallback 追踪多个目标元素
- **WHEN** UIView 对不同的 VisualElement 分别调用了 RegisterViewCallback
- **THEN** 每个目标元素和回调对 SHALL 被独立追踪，释放时各自正确 UnregisterCallback

### Requirement: UIView 显示与隐藏控制
UIView SHALL 通过 `Root.style.display` 控制显示隐藏：`DisplayStyle.Flex` 表示显示，`DisplayStyle.None` 表示隐藏。

#### Scenario: InternalOpen 显示视图
- **WHEN** UIManager 调用 UIView 的 InternalOpen
- **THEN** SHALL 设置 `Root.style.display = DisplayStyle.Flex`

#### Scenario: InternalClose 隐藏视图（缓存模式）
- **WHEN** UIManager 调用 UIView 的 InternalClose 且窗口配置为缓存
- **THEN** SHALL 设置 `Root.style.display = DisplayStyle.None`

### Requirement: UIView 生命周期方法保持兼容
UIView SHALL 保持与之前版本相同的生命周期虚方法签名：OnInitialize、OnBindings、OnOpen、OnRefresh、OnClose、OnPrepareAsync、OnRelease、OnUpdate。方法签名中不出现 UGUI 特定类型。

#### Scenario: 生命周期按序调用
- **WHEN** UIManager 创建 UIView 实例并初始化
- **THEN** SHALL 按以下顺序调用：InternalInitialize → InternalBindings → InternalPrepareAsync → InternalOpen → OnRefresh

### Requirement: UIView BindProperty 机制保持不变
UIView 的 `BindProperty<TSource, TValue>` 方法 SHALL 继续基于 `INotifyPropertyChanged` 工作，与渲染层无关。setter 参数中的操作从 UGUI 组件变为 VisualElement 属性，但 BindProperty 本身的签名和行为不变。

#### Scenario: BindProperty 绑定 Model 属性到 View 刷新
- **WHEN** UIView 在 OnBindings 中调用 `BindProperty(model, m => m.PlayerHp, v => { ... })`
- **THEN** SHALL 在 Model.PlayerHp 变更时触发 setter 回调，行为与 UGUI 版本完全一致

### Requirement: UIView 动态子项通过 CloneTree 实例化
UIView 动态创建子项 SHALL 通过 `VisualTreeAsset.CloneTree()` 实例化 UXML 模板，并通过 `parentElement.Add(item)` 挂载到容器中。移除子项 SHALL 通过 `item.RemoveFromHierarchy()` 完成。

#### Scenario: 动态创建列表子项
- **WHEN** GameView 需要为每个怪物创建显示项
- **THEN** SHALL 通过 `monsterItemVta.CloneTree()` 创建 TemplateContainer，然后 `container.Add(templateContainer)` 添加到滚动列表

#### Scenario: 动态移除列表子项
- **WHEN** 怪物死亡需要移除对应显示项
- **THEN** SHALL 调用 `item.RemoveFromHierarchy()` 从 VisualElement 树中移除
