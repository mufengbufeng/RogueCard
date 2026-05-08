## ADDED Requirements

### Requirement: 多 UIDocument 层级架构
系统 SHALL 为每个 UILayer（Background / Normal / Popup / Overlay）使用独立的 UIDocument 组件。每个 UIDocument SHALL 配置对应的 PanelSettings，通过 `sortingOrder` 控制渲染层级（Background=0, Normal=10, Popup=20, Overlay=30）。

#### Scenario: 场景中创建四层 UIDocument
- **WHEN** 游戏初始化时设置 UI 层级
- **THEN** 场景中 SHALL 存在 4 个 UIDocument 组件，分别对应 Background、Normal、Popup、Overlay 层
- **AND** 每个 UIDocument 的 PanelSettings.sortingOrder SHALL 分别为 0、10、20、30

#### Scenario: Popup 层窗口覆盖 Normal 层
- **WHEN** Normal 层有打开的窗口，同时打开 Popup 层的窗口
- **THEN** Popup 层的窗口 SHALL 渲染在 Normal 层之上

### Requirement: 层元素注册接口
IUIManager SHALL 提供 `RegisterLayerElement(UILayer layer, VisualElement element)` 方法替代原有的 `RegisterLayerRoot(UILayer, Transform)`。UIManager 内部 SHALL 通过 `Dictionary<UILayer, VisualElement>` 管理各层的根 VisualElement。

#### Scenario: 注册层根元素
- **WHEN** 初始化代码调用 `RegisterLayerElement(UILayer.Normal, normalRoot)`
- **THEN** UIManager SHALL 将该 VisualElement 记录为 Normal 层的容器

#### Scenario: 窗口打开时挂载到对应层
- **WHEN** 打开一个 UILayer.Popup 级别的窗口
- **THEN** 窗口的 VisualElement Root SHALL 被添加到 Popup 层的 VisualElement 容器中

### Requirement: Fallback 元素设置
IUIManager SHALL 提供 `SetFallbackElement(VisualElement element)` 方法替代 `SetFallbackRoot(Transform)`。当目标层没有注册元素时，使用此 fallback 元素作为窗口挂载目标。

#### Scenario: 未注册层使用 fallback
- **WHEN** 打开一个层未注册的窗口
- **THEN** 窗口 SHALL 被挂载到 fallback VisualElement

### Requirement: 层内窗口顺序
同一层内多个窗口 SHALL 按打开顺序排列（后打开的在上面）。UIManager SHALL 通过 `layerElement.Add(root)` 追加，确保后添加的窗口显示在前面。

#### Scenario: 同层多窗口渲染顺序
- **WHEN** 在 Normal 层依次打开窗口 A 和窗口 B
- **THEN** 窗口 B 的 VisualElement SHALL 在窗口 A 之后添加到层容器中，渲染在 A 之上
