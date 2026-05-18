## Context

项目当前运行时 UI 采用 UI Toolkit：`GameLogicEntry` 查找 `UIDocument` 并创建 `Navigator`，EF.UI 目录提供 `Screen` / `Shell` / `Region` / `ViewModelBase` / `ReactiveProperty`，主界面和局内界面通过 UXML/USS 加载与 `VisualElement` 查询完成绑定。

迁移前的 UGUI 框架仍可从 Git 历史 `f210f2f^` 恢复，当前资源目录也保留了 `MainView_OLD.prefab` 和 `GameView_Old.prefab` 备份。UGUI 包 `com.unity.ugui` 仍在 `Packages/manifest.json` 中，恢复不需要新增外部依赖。

主要复杂度在业务 UI：旧 `GameView : UIView` 只是局内窗口空壳，而当前卡牌 Rogue 的手牌、怪物、拖拽、预览、目标选择和奖励界面都在 UITK 版本上继续演进。框架可恢复，完整战斗 UI 需要重新映射到 UGUI 组件。

## Goals / Non-Goals

**Goals:**

- 恢复 UGUI / Canvas / Prefab 驱动的运行时 UI 框架。
- 恢复 `IUIManager`、`UIManager`、`UIView`、`UIController`、`UIWindowDescriptor`、`UIRuntimeContext`、`UIWindowHandle`、`UIWindowState`、`UIBindingCollection`、`ControllerEventBinder`、`UHub` 等运行时契约。
- 让 `GameEntry` 在 AOT Runtime 层重新注册 `IUIManager`。
- 让 `GameLogicEntry` 从 Entry 的 `ReferenceCollector` 读取 Background / Normal / Popup / Overlay / UICamera，并注册 UI 层级根节点。
- 将主菜单和局内流程改回 `IUIManager.OpenWindowAsync<TView,TController>()` 打开窗口，流程离开时显式关闭窗口。
- 移除游戏运行时对 `UIDocument`、`VisualElement`、`VisualTreeAsset`、`StyleSheet`、UXML 和 USS 的依赖。
- 保持当前非 UI 玩法系统由 `GameProcedure` 管理生命周期，避免把 `CardSystem`、`BattleSystem` 等耦回 UI 框架。

**Non-Goals:**

- 不移除 Unity 内置 `com.unity.modules.uielements`，该模块属于 Unity 基础模块且可能被编辑器或第三方包使用。
- 不默认重写 `ReferenceCollectorEditor` 的编辑器 Inspector；编辑器 UIElements 不属于运行时 UI 回退范围。
- 不引入新的 UI 框架或第三方依赖。
- 不重做卡牌 Rogue 玩法规则、配置表结构或战斗系统算法。

## Decisions

### 1. 以旧 UGUI 框架为基线恢复，而不是在 UITK Navigator 上包一层适配器

恢复目标是使用旧版 `UIManager` / `UIView` / `UIController` / `ReferenceCollector` 工作流。继续保留 Navigator 再做 UGUI 适配会产生两套生命周期语义，且仍保留运行时 UITK 类型。

替代方案：保留 `INavigator` 接口，把实现换成 UGUI。该方案能减少流程层改动，但会让 API 名称与实际 UGUI 窗口语义不一致，并留下 Screen / ViewModel 规格负担。

### 2. AOT Runtime 注册 UIManager，HotFix 只负责层级绑定

`UIManager` 属于 EF Runtime 管理器，依赖 `IResourceManager` 和 `ModelManager`，应由 `GameEntry.Awake()` 注册到 `ModuleSystem`。热更层 `GameLogicEntry` 获取 `IUIManager` 后，只把场景中的 UGUI 层级根节点注册进去。

替代方案：在 HotFix 层创建 UIManager。该方案会破坏 EF 管理器统一注册模式，也会让 AOT / HotFix 边界不清晰。

### 3. 流程层继续拥有玩法 System 生命周期

虽然回到 MVC UI，`GameProcedure` 仍负责创建并 Dispose `GameModel`、`CardSystem`、`MonsterSystem`、`BattleSystem`、`WaveSystem` 和本地事件总线。`GameController` 和 `GameView` 只承担 UGUI 输入、显示和命令转发，不直接持有全局单例玩法逻辑。

替代方案：把 System 创建移动到 `GameController.OnEnter()`。这会让窗口生命周期与流程生命周期耦合，关卡完成切回主菜单时更容易留下事件订阅或 System 残留。

### 4. 使用 Prefab + ReferenceCollector 作为 UI 资源与绑定边界

运行时窗口资源使用 YooAsset addressable Prefab，如 `"MainView"`、`"GameView"`。Prefab 根对象挂载 `UIView` 子类和 `ReferenceCollector`，字段通过 UHub 或显式引用绑定。

替代方案：运行时代码动态创建所有 UGUI 节点。该方案可减少 Prefab 迁移成本，但会丢失 Unity 编辑器可视化布局优势，也与项目已有 ReferenceCollector 工具链不一致。

### 5. 分两层处理测试

纯逻辑和流程契约继续优先使用 EditMode 测试；依赖实际 Prefab、Canvas、GraphicRaycaster 和 RectTransform 交互的行为用最小 PlayMode 或手动 Unity 验证覆盖。任务中先恢复可编译和流程测试，再补关键 UI 绑定测试。

替代方案：所有 UI 行为都写 EditMode 测试。UGUI 的事件系统和布局在 EditMode 下可模拟但不完全等价，容易形成脆弱测试。

## Risks / Trade-offs

- 旧 Prefab 与当前玩法 UI 字段不一致 → 先用 `MainView_OLD.prefab` / `GameView_Old.prefab` 做恢复基线，再逐项补齐卡牌、怪物、状态、奖励和按钮绑定。
- 删除 UITK 运行时类型会导致大量测试失效 → 先删除或改写框架层 UITK 测试，再以 UGUI 生命周期测试替代。
- `GameView` 从 UITK 迁回 UGUI 后拖拽/预览手感可能退化 → 先保证命令流和状态显示正确，再单独调优 UGUI 拖拽表现。
- Addressable 地址可能仍指向 UXML/USS → 实施时必须检查 YooAsset/资源分组地址，确保 `MainView` / `GameView` 指向 Prefab。
- 当前工作区已有未提交修改 → 实施前需要确认变更范围，避免覆盖用户对配置、测试和 UXML 的现有改动。

## Migration Plan

1. 恢复 EF.UI 旧 UGUI 框架代码，并移除运行时 UITK 框架代码。
2. 修改 `GameEntry` 重新注册 `IUIManager`。
3. 修改 `GameLogicEntry` 从 Entry/ReferenceCollector 初始化 UI 层级，移除 `UIDocument` / `Navigator` 初始化。
4. 恢复或重建 `MainView` / `MainController`，并使主菜单流程通过 `IUIManager` 打开。
5. 恢复或重建 `GameView` / `GameController`，把当前玩法系统的 UI 命令与显示映射到 UGUI。
6. 删除运行时 UXML/USS 资源和依赖 `UnityEngine.UIElements` 的运行时代码。
7. 改写测试与 README，执行编译检查、相关 EditMode 测试和 Unity 场景验证。

回滚策略：如果 UGUI 战斗 UI 迁移中断，可在同一变更分支回退业务 UI 迁移文件，保留 proposal/design/spec/tasks；不要在主工作区直接混合提交半迁移状态。

## Open Questions

- 是否要求连编辑器工具 `ReferenceCollectorEditor` 中的 UIElements Inspector 一并改回 IMGUI？本设计默认不要求。
- `GameView_Old.prefab` 是否足以作为局内 UI 迁移基线，还是需要重新制作 Prefab？实施时需要在 Unity 中检查引用和布局。
- 当前卡牌拖拽、放大预览和目标选择是否必须在本变更内做到与 UITK 版本完全同等手感，还是先保证功能正确再另开 polish 变更？
