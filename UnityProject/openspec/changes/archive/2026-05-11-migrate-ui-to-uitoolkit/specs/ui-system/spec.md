## MODIFIED Requirements

### Requirement: UIManager 资源加载和实例化
UIManager.CreateOrReuseInstanceAsync SHALL 通过 `_resourceManager.LoadAssetAsync<VisualTreeAsset>(location)` 加载 UXML 资源，通过 `vta.CloneTree()` 创建 VisualElement 树实例，将 TemplateContainer 添加到对应层的 VisualElement 容器中。

#### Scenario: 首次加载 UXML 资源并创建窗口
- **WHEN** UIManager 需要创建一个新窗口实例
- **THEN** SHALL 通过 YooAsset 加载 VisualTreeAsset，调用 CloneTree() 生成 VisualElement 树
- **AND** 将生成的 TemplateContainer 添加到对应 UILayer 的 VisualElement 容器

#### Scenario: TryResolveView 不再需要 GetComponent
- **WHEN** UIManager 创建 UIView 实例
- **THEN** SHALL 通过 `Activator.CreateInstance(descriptor.ViewType)` 创建纯 C# UIView 实例（不再需要从 GameObject 获取 MonoBehaviour 组件）

### Requirement: UIRuntimeContext 使用 VisualElement
UIRuntimeContext SHALL 使用 `VisualElement LayerElement` 替代 `Transform LayerRoot`。`LayerRootRectTransform` 属性 SHALL 被移除。

#### Scenario: 获取层元素
- **WHEN** Controller 或 System 需要访问当前层容器
- **THEN** SHALL 通过 `Context.LayerElement` 获取 VisualElement 实例

### Requirement: UIWindowDescriptor 资源类型描述
UIWindowDescriptor 的 Location 属性 SHALL 指向 UXML 资源路径（而非 Prefab 路径）。ViewType 的约束 SHALL 改为纯 C# UIView 子类（不再要求 MonoBehaviour）。TryResolveView 逻辑 SHALL 被替换为直接通过 Activator.CreateInstance 创建 UIView。

#### Scenario: 描述符验证 ViewType
- **WHEN** 创建 UIWindowDescriptor 并传入 ViewType
- **THEN** SHALL 验证 ViewType 是 UIView 的子类（纯 C# 类，不依赖 MonoBehaviour）

### Requirement: UIManager 窗口缓存机制
UIManager 关闭窗口时，如果 CacheOnClose 为 true，SHALL 调用 `root.RemoveFromHierarchy()` 将 VisualElement 从渲染树移除并缓存。复用时 SHALL 调用 `layerElement.Add(cachedRoot)` 重新挂载。

#### Scenario: 缓存窗口
- **WHEN** 关闭一个 CacheOnClose=true 的窗口
- **THEN** SHALL 调用 `view.Root.RemoveFromHierarchy()`，将实例存入缓存栈

#### Scenario: 复用缓存窗口
- **WHEN** 打开一个已有缓存实例的窗口
- **THEN** SHALL 从缓存栈取出实例，调用 `layerElement.Add(cachedRoot)` 重新挂载

### Requirement: UIManager 生命周期时序保持不变
UIManager.CreateOrReuseInstanceAsync SHALL 保持以下生命周期顺序不变：
1. Controller.InternalInitialize
2. Controller.InternalPrepareAsync
3. View.InternalInitialize（传入 VisualElement Root 和 Context）
4. View.InternalBindings
5. View.InternalPrepareAsync
6. View.InternalOpen
7. Controller.InternalEnter

#### Scenario: 新窗口的完整生命周期
- **WHEN** 首次打开一个窗口
- **THEN** SHALL 严格按照上述 7 步顺序执行，View.InternalInitialize 接收 CloneTree 生成的 VisualElement Root
