## MODIFIED Requirements

### Requirement: 主界面资源类型
主界面 SHALL 使用 UXML 资源（VisualTreeAsset）替代 Prefab。UIManager 注册窗口时，location 参数 SHALL 指向 UXML 资源路径。

#### Scenario: 通过 UXML 资源打开主界面
- **WHEN** InitProcedure 调用 UIManager.OpenWindowAsync<MainView, MainController> 并传入 UXML 资源路径
- **THEN** UIManager SHALL 加载 VisualTreeAsset，CloneTree 创建 VisualElement 树，实例化 MainView（纯 C# 类）

### Requirement: 层根注册使用 VisualElement
初始化时注册 UI 层级 SHALL 使用 `RegisterLayerElement(UILayer, VisualElement)` 替代 `RegisterLayerRoot(UILayer, Transform)`。层元素 SHALL 从场景中的 UIDocument 组件的 rootVisualElement 获取。

#### Scenario: 初始化时注册 Normal 层
- **WHEN** GameLogicEntry 初始化 UI 系统
- **THEN** SHALL 从场景中 Normal 层的 UIDocument 获取 rootVisualElement，调用 RegisterLayerElement 注册
