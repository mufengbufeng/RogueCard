## 1. 测试基线与迁移边界

- [x] 1.1 新增或恢复 UIManager EditMode 生命周期测试，先覆盖 `OpenWindowAsync<TView,TController>` 加载 Prefab、挂载层级、Controller/View 初始化顺序和关闭释放顺序。
- [x] 1.2 新增 `GameEntry` 管理器注册测试或最小编译契约测试，验证 `IUIManager` 在热更入口读取前已由 Runtime 注册。
- [x] 1.3 新增 `GameLogicEntry` 初始化测试或可替代的场景契约检查，验证 Entry/ReferenceCollector 中 Background、Normal、Popup、Overlay、UICamera 会注册到 UIManager。
- [x] 1.4 改写主菜单到局内流程 EditMode 测试，验证 `MainMenuProcedure` 和 `GameProcedure` 调用 `IUIManager.OpenWindowAsync` / `CloseWindowAsync`，且不再依赖 `Navigator`。
- [x] 1.5 标记待删除或改写的 UITK 测试清单，包括 `NavigatorTypeResolutionTests`、`RegionTests`、`ScreenConventionTests`、`ScreenLifecycleTests`、`ShellAndRegistryTests`、`UiVisualTreeAssetTests` 以及依赖 `VisualElement` 的局内 UI 测试。

## 2. 恢复 UGUI 框架核心

- [x] 2.1 从 `f210f2f^` 或等价历史恢复 `IUIManager`、`UIManager`、`UIView`、`UIController`、`UIRuntimeContext`、`UIWindowDescriptor`、`UIWindowHandle`、`UIWindowState`、`UIBindingCollection`、`ControllerEventBinder`。
- [x] 2.2 从历史恢复 `UHub` 目录下的 Attributes、ComponentBinder、EventBindings、IEventBinding、UHubBindingConfig、UHubComponent，并确保与当前 ReferenceCollector 编译兼容。
- [x] 2.3 移除运行时 UITK 框架类型 `INavigator`、`Navigator`、`Screen`、`Popup`、`Shell`、`Region`、`ViewModelBase`，或确认无运行时代码继续引用它们后删除。
- [x] 2.4 保留 `LocalEventBus` 与 `ReactiveProperty` 中仍被非 UI 玩法系统需要的类型；若仅 UITK UI 使用，则在后续步骤移除引用后再删除。
- [x] 2.5 运行最小 UIManager 生命周期测试，确认失败原因来自待实现的 UGUI 框架差异而非测试环境错误。

## 3. Runtime 与热更入口改回 UGUI

- [x] 3.1 修改 `GameEntry.Awake()`，重新注册 `ModuleSystem.Register<IUIManager>(new UIManager(_resourceManager, _modelManager))`。
- [x] 3.2 修改 `GameLogicEntry` 字段和属性，把 `Navigator` 改回 `IUIManager UI`，并移除 `using UnityEngine.UIElements`。
- [x] 3.3 将 `GameLogicEntry.InitializeNavigator()` 替换为 UGUI 层级初始化逻辑：查找 Entry、读取 ReferenceCollector、注册 Background / Normal / Popup / Overlay，并缓存 UICamera。
- [x] 3.4 确认 `GameLogicEntry.InitializeProcedures()` 仍启动 `InitProcedure`、`MainMenuProcedure`、`GameProcedure`，且不再要求场景存在 UIDocument。
- [x] 3.5 运行入口相关测试，确认 `ModuleSystem.Get<IUIManager>()` 和层级注册路径通过。

## 4. 主菜单 UGUI 窗口

- [x] 4.1 恢复或重建 `MainView : UIView`，通过 UHub / ReferenceCollector 绑定 UGUI Button 与 TextMeshProUGUI，并在按钮点击时发出开始游戏事件。
- [x] 4.2 恢复或重建 `MainController : UIController`，负责初始化主界面模型、刷新文本、处理开始按钮事件并发布默认关卡进入请求。
- [x] 4.3 修改 `MainMenuProcedure`，进入时通过 `IUIManager.OpenWindowAsync<MainView, MainController>("MainView", UILayer.Normal, ...)` 打开主界面，离开时关闭 `"MainView"`。
- [x] 4.4 将默认关卡配置读取继续保留在流程或 Controller 的明确位置，确保 TbLevel 缺省时仍有安全回退。
- [x] 4.5 恢复或重命名 `Assets/AssetRaw/UI/Main/MainView_OLD.prefab` 为运行时 `MainView` Prefab，并检查 ReferenceCollector 引用完整。
- [x] 4.6 运行主菜单 Controller 和主菜单到局内流程测试，确认按钮事件能切换到 GameProcedure。

## 5. 局内 UGUI 窗口与玩法命令流

- [x] 5.1 恢复或重建 `GameView : UIView`，绑定玩家状态、战斗面板、奖励面板、手牌容器、怪物容器、结束回合按钮、失败提示等 UGUI 组件。
- [x] 5.2 恢复或重建 `GameController : UIController`，只处理 UGUI 交互与 View 刷新，不直接创建或持有玩法 System 生命周期。
- [x] 5.3 修改 `GameProcedure`，进入时继续创建 GameModel、CardSystem、MonsterSystem、MonsterCardSystem、BattleSystem、WaveSystem 和 LocalEventBus，并通过 `IUIManager.OpenWindowAsync<GameView, GameController>("GameView", UILayer.Normal, ...)` 打开局内窗口。
- [x] 5.4 建立 GameProcedure 与 UGUI GameView/GameController 的命令通道，确保使用卡牌、指定目标、结束回合、奖励确认能转发到对应 System 或流程方法。
- [x] 5.5 将玩家状态、怪物列表、手牌列表、buff、意图和失败 toast 的刷新从 VisualElement 实现迁移到 UGUI 组件实现。
- [x] 5.6 将战斗/奖励子区域切换改为 UGUI Panel 或 CanvasGroup 显隐，移除 `Region.ShowAsync("BattlePanel")` / `ShowAsync("RewardPanel")` 路径。
- [x] 5.7 恢复或重建 `Assets/AssetRaw/UI/Game/GameView_Old.prefab` 为运行时 `GameView` Prefab，并补齐当前玩法所需 ReferenceCollector 引用。
- [x] 5.8 为局内命令流新增或改写测试，至少覆盖结束回合、普通出牌、手动选目标出牌和关卡完成切回主菜单。

## 6. 移除运行时 UITK 资源与测试

- [x] 6.1 删除运行时 UXML/USS 资源：`Root.uxml`、`SharedStyles.uss`、`MainUxml.uxml`、`MainUss.uss`、`GameUxml.uxml`、`GameUss.uss`、`BattlePanel.uxml`、`RewardPanel.uxml`、`CardItem.uxml`、`MonsterItem.uxml`、`TipsItem.uxml` 及对应 meta。
- [x] 6.2 删除或改写 `MainViewModel`、`GameViewModel` 中仅为 UITK Screen 绑定存在的代码；若其属性仍用于玩法状态桥接，则迁移为非 UITK 命名的上下文或 Model。
- [x] 6.3 删除或改写依赖 `UnityEngine.UIElements` 的运行时局内 UI 子模块：BattlePanelView、PlayerStatusView、MonsterListView、MonsterItemView、HandFanView、TargetSelector、TurnControlView、CardItemView、BuffBarRenderer、CardPreviewController、IPreviewSurface。
- [x] 6.4 保留可复用的纯逻辑拖拽/布局计算代码（如不依赖 UIElements 的状态机、FanLayoutCalc），并为 UGUI 适配层提供接口。
- [x] 6.5 删除或改写所有依赖 VisualElement 的 EditMode 测试，替换为 UGUI 组件、纯逻辑接口或流程层测试。
- [x] 6.6 使用 `rg "UnityEngine.UIElements|VisualElement|UIDocument|VisualTreeAsset|StyleSheet|UXML|USS|Uxml|Uss" Assets/GameScripts Assets/EF/EFRuntime` 确认运行时代码无 UITK 残留；允许 `Assets/EF/EFEditor` 编辑器工具按本变更 Non-Goals 保留。

## 7. 文档、资源地址与最终验证

- [x] 7.1 更新 `Assets/EF/EFRuntime/UI/README.md`，改为描述 UGUI MVC 架构、四层 Canvas、UIManager 生命周期、UIView/UHub/ReferenceCollector 绑定和测试入口。
- [x] 7.2 更新 AGENTS/项目文档中仍描述运行时 UI Toolkit 的章节，使其与 UGUI 回退后的架构一致。
- [x] 7.3 检查 YooAsset/资源分组地址，确保 `"MainView"` 和 `"GameView"` 指向 UGUI Prefab，而不是 UXML 或 `_OLD` 备份名。
- [x] 7.4 在 Unity 场景中检查 Entry 的 ReferenceCollector，确认 Background、Normal、Popup、Overlay、UICamera 引用完整。
- [x] 7.5 运行脚本编译检查：`python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`；如 Unity Skills 不可用，则运行 `dotnet build UnityProject.slnx --no-restore`。
- [x] 7.6 运行相关 EditMode 测试：UIManager 生命周期、MainController、MainMenuToGameProcedure、GameProcedure 关卡完成清理与局内命令流测试。
- [x] 7.7 在 Unity 编辑器中手动验证：启动进入 MainView、点击开始进入 GameView、出牌、结束回合、完成关卡返回 MainView。
