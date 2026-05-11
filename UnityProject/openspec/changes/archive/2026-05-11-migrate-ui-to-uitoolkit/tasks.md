## 1. 框架层核心重写

- [x] 1.1 重写 UIView 为纯 C# 抽象类（IDisposable），持有 VisualElement Root，提供 OnInitialize/OnBindings/OnOpen/OnRefresh/OnClose/OnPrepareAsync/OnRelease/OnUpdate 生命周期虚方法
- [x] 1.2 在 UIView 中实现 RegisterViewCallback<TEventType> 自动回调追踪与清理机制（内部维护 List<Action>，Dispose/OnRelease 时批量 UnregisterCallback）
- [x] 1.3 在 UIView 中保留 BindProperty<TSource, TValue> 方法，签名不变，依赖 UIBindingCollection
- [x] 1.4 修改 UIRuntimeContext：将 Transform LayerRoot 改为 VisualElement LayerElement，移除 LayerRootRectTransform 属性
- [x] 1.5 修改 IUIManager 接口：RegisterLayerRoot(UILayer, Transform) → RegisterLayerElement(UILayer, VisualElement)，SetFallbackRoot(Transform) → SetFallbackElement(VisualElement)
- [x] 1.6 修改 UIWindowDescriptor：Location 描述从 Prefab 路径改为 UXML 资源路径，ViewType 校验适配纯 C# UIView

## 2. UIManager 重写

- [x] 2.1 重写 UIManager.CreateOrReuseInstanceAsync：LoadAssetAsync<VisualTreeAsset> → CloneTree → layerElement.Add，UIView 通过 Activator.CreateInstance 创建
- [x] 2.2 重写 TryResolveView 逻辑：移除 GetComponent/AddComponent，改为 Activator.CreateInstance(descriptor.ViewType)
- [x] 2.3 重写窗口缓存机制：CloseWindowInternal 中 CacheOnClose=true 时调用 Root.RemoveFromHierarchy，复用时 layerElement.Add
- [x] 2.4 重写 DisposeInstance：移除 GameObject.Destroy，改为 Root.RemoveFromHierarchy + 释放 AssetHandle
- [x] 2.5 修改层根管理：_layerRoots(Dictionary<UILayer, Transform>) → _layerElements(Dictionary<UILayer, VisualElement>)
- [x] 2.6 修改 RegisterLayerElement / SetFallbackElement 实现
- [x] 2.7 保持 Update 循环驱动 Controller.InternalUpdate 和 View.InternalUpdate 不变

## 3. UIWindowHandle 适配

- [x] 3.1 更新 UIWindowHandle 构造函数中 UIView 类型引用（纯 C# 版）
- [x] 3.2 更新 TryGetView<TView> 约束（移除 MonoBehaviour 相关约束）

## 4. UIController 适配

- [x] 4.1 修改 UIController 的 View 属性类型注释（纯 C# UIView，非 MonoBehaviour）
- [x] 4.2 确保 GetView<TView>() 泛型约束适配纯 C# UIView
- [x] 4.3 确保 UIController 生命周期（OnInitialize/OnPrepareAsync/OnEnter/OnExit/OnRelease/OnUpdate）不依赖 MonoBehaviour API

## 5. UIDocument 层级场景结构

- [ ] 5.1 创建 UIRoot GameObject，下设 4 个 UIDocument 子 GameObject（Background/Normal/Popup/Overlay）
- [ ] 5.2 为每个 UIDocument 配置 PanelSettings，sortingOrder 分别为 0/10/20/30
- [x] 5.3 修改 GameLogicEntry.Init 中的层根注册代码：从 GetComponent/Find 获取 Transform 改为获取 UIDocument.rootVisualElement，调用 RegisterLayerElement

## 6. UXML/USS 资源文件创建

- [x] 6.1 创建 MainView.uxml（主菜单布局：开始按钮、状态文本、关卡名称、关卡描述、反馈文本）
- [x] 6.2 创建 MainView.uss（主菜单样式：独立 .uss 文件）
- [x] 6.3 创建 GameView.uxml（局内布局：背景、怪物区域、意图区域、手牌区域、信息区域、结束回合按钮）
- [x] 6.4 创建 GameView.uss（局内样式：独立 .uss 文件）
- [x] 6.5 创建 MonsterItem.uxml（怪物项模板：名称/HP Label）
- [x] 6.6 创建 TipsItem.uxml（意图项模板：意图类型和数值 Label）
- [x] 6.7 创建 CardItem.uxml（手牌项模板：卡牌名称和费用 Label）
- [x] 6.8 创建 shared.uss（公共样式：字体、颜色、间距等基础变量）

## 7. 游戏层 View 重写

- [x] 7.1 重写 MainView：继承纯 C# UIView，OnInitialize 中通过 Root.Q<Button/Label> 查找元素，OnBindings 中通过 RegisterViewCallback 注册 ClickEvent，移除所有 UHub 和 MonoBehaviour 依赖
- [x] 7.2 重写 GameView：继承纯 C# UIView，OnInitialize 中通过 Root.Q 查找 ScrollView/VisualElement，OnBindings 中注册 BindProperty 和 RegisterViewCallback，动态子项通过 CloneTree + UQuery 实现
- [x] 7.3 移除 GameView 和 MainView 中所有 CreateRuntimeText 回退逻辑（UXML 定义布局，不再需要运行时创建组件）

## 8. 游戏层 Controller 适配

- [x] 8.1 修改 GameController：确保 OnInitialize/OnPrepareAsync/OnEnter/OnExit 中引用的 View API 适配纯 C# UIView（无 MonoBehaviour 依赖）
- [x] 8.2 修改 MainController：同上

## 9. 删除废弃组件

- [x] 9.1 删除 Assets/EF/EFRuntime/UI/UHub/ 整个目录（UHubComponent.cs、ComponentBinder.cs、UHubBindingConfig.cs、事件绑定类等）
- [x] 9.2 确认 ReferenceCollector 及 ReferenceCollectorEditor 保留不动

## 10. 测试更新

- [x] 10.1 更新 EditMode 测试中的 UIView 相关测试：适配纯 C# UIView（不再需要场景/MonoBehaviour 环境）
- [x] 10.2 更新 EditMode 测试中的 UIManager 相关测试：适配 VisualTreeAsset/VisualElement（Mock 或简化测试）
- [x] 10.3 更新 MainMenuToGameProcedureTests 中与 UI 交互的测试断言
- [x] 10.4 为 RegisterViewCallback 自动清理编写单元测试

## 11. 编译验证与运行时验证

- [x] 11.1 运行编译检查确保 EFRuntime 和 HotFix 层无编译错误
- [ ] 11.2 在 Unity 编辑器中验证 MainView 正确加载 UXML 并显示
- [ ] 11.3 在 Unity 编辑器中验证 GameView 正确加载 UXML 并显示动态内容
- [ ] 11.4 验证层叠渲染顺序（Normal → Popup → Overlay）
- [ ] 11.5 验证窗口缓存和复用功能正常
