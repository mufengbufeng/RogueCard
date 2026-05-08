## 1. 基础设施层：ReactiveProperty 和 ViewModelBase

- [x] 1.1 创建 ReactivePropertyBase 抽象基类（ClearListeners 方法）和 ReactiveProperty<T> 泛型类（Value get/set + Changed 事件 + 相等性判断跳过）
- [x] 1.2 创建 ViewModelBase 抽象类（Prop<T> 工厂方法 + 内部追踪列表 + Dispose 清理所有属性监听者 + 幂等 Dispose）
- [x] 1.3 编写 EditMode 测试：ReactiveProperty 值变化触发 Changed、值不变不触发、ClearListeners 后不触发
- [x] 1.4 编写 EditMode 测试：ViewModelBase.Prop 创建追踪、Dispose 清理所有属性、幂等 Dispose

## 2. UI 框架核心：Shell、Screen、ScreenRegistry

- [x] 2.1 创建 Shell 类（继承 VisualElement，构造时创建 ScreenLayer / PopupLayer / SystemLayer 三个全屏层级容器）
- [x] 2.2 创建 Screen<TViewModel> 抽象基类（继承 VisualElement，LoadContent(vta) 挂载克隆子树，Setup(vm) 注入 ViewModel 并调用 OnSetup，OnShow/OnHide/OnDispose 生命周期）
- [x] 2.3 创建 ScreenDescriptor 记录类和 ScreenRegistry 注册表（Register<TScreen, TViewModel>、Get、重复注册抛异常）
- [x] 2.4 编写 EditMode 测试：Shell 构造后包含三个层级、ScreenRegistry 注册和查询、重复注册抛异常

## 3. Navigator 导航服务

- [x] 3.1 创建 INavigator 接口（NavigateToAsync、PushPopupAsync、PopPopup）
- [x] 3.2 实现 Navigator（Shell 引用 + ScreenRegistry + IResourceManager，Screen 内容替换逻辑，Popup 栈 + 遮罩层管理）
- [x] 3.3 实现 Navigator.Shutdown（关闭所有 Screen 和 Popup，清理层级容器）
- [x] 3.4 编写 EditMode 测试：NavigateToAsync 首次导航、切换 Screen 时先关闭旧的、PushPopup 栈式管理、PopPopup LIFO、空栈 PopPopup 安全

## 4. Region 组件

- [x] 4.1 创建 Region 类（VisualElement 插槽 + IResourceManager 可选引用，ShowAsync(uxmlLocation) 动态加载，Show(element) 直接放置，Clear 清空，CurrentContent 暴露当前内容）
- [x] 4.2 编写 EditMode 测试：Region ShowAsync 加载内容、连续 Show 替换内容、Clear 清空、空 Region Clear 安全

## 5. 迁移 EF.UI 模块注册

- [x] 5.1 删除旧文件：UIView.cs、UIController.cs、UIManager.cs、UIRuntimeContext.cs、UIWindowDescriptor.cs 及相关绑定/事件辅助类
- [x] 5.2 更新 IUIManager.cs → INavigator.cs，更新 ModuleSystem 注册方式
- [x] 5.3 更新 GameLogicEntry.Init 中的模块注册：创建 Shell 挂到 UIDocument，创建 ScreenRegistry 注册所有 Screen，创建 Navigator 并注册到 ModuleSystem
- [x] 5.4 编译检查通过

## 6. 迁移主界面：MainView/MainController → MainMenuScreen + MainViewModel

- [x] 6.1 创建 MainViewModel（继承 ViewModelBase，StatusText / LevelName / LevelDesc / CanStart 响应式属性，StartRequested 命令意图事件）
- [x] 6.2 创建 MainMenuScreen（继承 Screen<MainViewModel>，OnSetup 中 UQuery + 订阅 ReactiveProperty.Changed + 注册按钮 ClickEvent 转发 ViewModel.RequestStart()）
- [x] 6.3 重写 MainMenuProcedure：创建 MainViewModel、从 TbLevel 填充数据、订阅 StartRequested、调用 Navigator.NavigateToAsync("MainMenu")
- [x] 6.4 删除旧 MainView.cs 和 MainController.cs
- [x] 6.5 编译检查通过

## 7. 迁移局内界面：GameView/GameController → GameScreen + GameViewModel

- [x] 7.1 创建 GameViewModel（继承 ViewModelBase，Phase / Monsters / Hand / Energy / PlayerHp / Rewards 等响应式属性，UseCard / EndTurn / SelectReward 命令意图事件）
- [x] 7.2 创建 GameScreen（继承 Screen<GameViewModel>，OnSetup 中 UQuery 常驻区域元素 + 绑定 ReactiveProperty + 监听 Phase 变化切换 Region）
- [x] 7.3 创建 BattlePanel.uxml 和 RewardPanel.uxml 子模板（从现有 GameView.uxml 拆分）
- [x] 7.4 重写 GameProcedure：创建 GameViewModel、创建并初始化 System、订阅 ViewModel 命令事件转发到 System、调用 Navigator.NavigateToAsync("Game")
- [x] 7.5 更新现有 System（CardSystem、MonsterSystem、BattleSystem、WaveSystem）移除 UISystem 基类依赖，改为通过 Init 方法接收 GameModel 和 IEventPublisher
- [x] 7.6 删除旧 GameView.cs 和 GameController.cs
- [x] 7.7 编译检查通过

## 8. 测试和清理

- [x] 8.1 更新 MainControllerTests 和相关 EditMode 测试以适配新架构
- [x] 8.2 删除残留的 UIBindingCollection、UIPropertyBinding、ControllerEventBinder、LocalEventBus 等辅助类
- [x] 8.3 全量编译检查通过
- [x] 8.4 运行 EditMode 测试全部通过
