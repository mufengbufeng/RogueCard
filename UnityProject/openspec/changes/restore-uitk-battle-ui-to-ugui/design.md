## Context

项目运行时 UI 已经回到 UGUI：`IUIManager.OpenWindowAsync<TView,TController>()` 通过 Prefab 打开窗口，`UIView` 使用 `ReferenceCollector` / UHub 绑定组件，`MainView` 和 `GameView` 都以 UGUI Prefab 作为入口。问题在于局内战斗 UI 的功能还停留在过渡态：当前 `GameView` 动态创建摘要文本和命令按钮，只能完成最低限度命令转发，不能恢复 UITK 阶段的完整战斗交互。

历史 UITK 版本已经把局内 UI 拆成多个可测试子模块：玩家状态、怪物列表、手牌扇形、拖拽状态机、卡牌预览、目标选择、回合控制和 BattlePanel 协调器。其中 `CardDragController`、`FanLayoutCalc`、`HandFanLayoutOptions` 已经是 UI 无关或可适配边界，适合直接复用；依赖 `VisualElement`、UXML、USS 的视图层需要重建为 UGUI 适配层。

当前约束：

- 运行时不能重新依赖 `UnityEngine.UIElements`、UXML、USS、`VisualTreeAsset` 或 `UIDocument`。
- 热更新游戏逻辑继续在 `Assets/GameScripts/HotFix/GameLogic/`。
- UI 资源继续使用 `Assets/AssetRaw/UI/Main/MainView.prefab` 和 `Assets/AssetRaw/UI/Game/GameView.prefab`。
- `GameProcedure` 继续拥有 `GameModel`、`CardSystem`、`MonsterSystem`、`BattleSystem`、`WaveSystem` 生命周期，UI 只发命令意图。

## Goals / Non-Goals

**Goals:**

- 在 UGUI `GameView.prefab` 中恢复 UITK 阶段已经具备的局内 UI 功能。
- 使用 UGUI 子视图适配层替代 UITK `VisualElement` 子模块。
- 保留并复用 `GameViewModel` 切片接口、`ReactiveProperty`、`CardDragController`、`FanLayoutCalc` 和命令转发边界。
- 让 Prefab/ReferenceCollector 成为运行时 UI 绑定边界，减少代码中动态创建正式 UI 节点。
- 通过 EditMode 测试覆盖可纯逻辑验证的渲染、状态机适配和命令流，通过 Unity 编辑器验证 Prefab 与实际指针交互。

**Non-Goals:**

- 不恢复运行时 UITK 框架或 UXML/USS 资源。
- 不重做主菜单视觉与流程；`MainView.prefab` 只做必要绑定校验。
- 不改变卡牌效果、怪物 AI、回合推进、配置表结构或关卡流程规则。
- 不在本变更中引入新的 UI 框架或第三方 tween 库。
- 不要求编辑器工具 UIElements Inspector 改写为 IMGUI。

## Decisions

### 1. 使用 UGUI 子视图适配层，而不是恢复 UITK 文件

`PlayerStatusView`、`MonsterListView`、`HandFanView`、`TargetSelector` 等名称和职责保留，但实现类型改为 UGUI。子视图接收 `RectTransform`、`Button`、`Image`、`TextMeshProUGUI`、`CanvasGroup` 等组件，并通过切片接口订阅数据。

替代方案是把旧 UITK 文件恢复回来并在 `GameView` 内混用 `VisualElement`。该方案会与 UGUI 回退目标冲突，也会重新引入 `UIDocument`/UXML/USS 地址和运行时框架负担。

### 2. 复用 CardDragController，新增 UGUI IDragSurface 实现

拖拽规则、SingleManual 判断、insert slot 计算、回弹调度已经集中在 `CardDragController`。UGUI 层只实现 `IDragSurface`：把 `worldBound` 映射为 `RectTransformUtility` 屏幕/画布坐标命中，把透明度映射为 `CanvasGroup.alpha`，把 picking 映射为 `CanvasGroup.blocksRaycasts` 或 `Graphic.raycastTarget`，把过渡映射为协程/计时器驱动的 RectTransform 动画。

替代方案是从头在 `GameView` 里手写拖拽逻辑。该方案会复制旧状态机规则，测试覆盖成本高，也更容易破坏现有拖拽单元测试。

### 3. 用 Prefab 模板承载重复 UI 项

`GameView.prefab` 内应提供隐藏模板或子 Prefab 引用：手牌卡、怪物项、意图图标、Buff 图标等。运行时通过实例化模板生成列表项，刷新前销毁或回收到本地池中。正式 UI 组件必须来自 Prefab/ReferenceCollector，而不是由 `GameView` 临时创建文本和按钮。

替代方案是继续运行时 `new GameObject()` 创建所有节点。该方案适合兜底测试，但不适合真实游戏 UI；布局、样式和引用很难在 Unity 编辑器中维护。

### 4. 保持 GameProcedure 与 UI 的职责分离

`GameProcedure` 仍负责创建玩法系统、绑定 `GameViewModel` 命令事件和清理流程。`GameController` 只接收 `GameViewModel`，把 UGUI View 的事件转发到 ViewModel 命令；`GameView` 和子视图不直接调用 `CardSystem`、`BattleSystem` 或修改 `GameModel`。

替代方案是把 `CardSystem` 等注入到 `GameController`。这会把窗口生命周期与战斗生命周期耦合，窗口关闭/重开时更容易留下事件订阅和 System 残留。

### 5. 分层验证

纯 C# 或可在 EditMode 构造 GameObject 的行为使用测试先行：UGUI 子视图渲染、ReferenceCollector key、状态机回调、命令转发、Dispose 解绑。真实 Prefab、CanvasScaler、GraphicRaycaster、拖拽手感和 pointer 输入用 Unity 编辑器或 PlayMode 手动验证补齐。

替代方案是把所有 UI 行为都做 EditMode 自动化。UGUI 的布局、事件系统和 Canvas 更新在 EditMode 下不能完全等价，容易写出脆弱测试。

## Risks / Trade-offs

- Prefab 引用缺失或 key 命名不一致 -> 在 spec 中固定 ReferenceCollector key，并新增 Prefab 绑定检查任务。
- UGUI 坐标命中与 UITK `worldBound` 不完全等价 -> 在 `IDragSurface` 适配层集中处理坐标转换，优先用 `RectTransformUtility.RectangleContainsScreenPoint`。
- 拖拽动画手感短期不如 USS transition -> 先保证状态和命令正确，再用 `HandFanLayoutOptions` 调整过渡时长与插值。
- EditMode 无法完整模拟 pointer capture -> 状态机继续用 mock surface 测，UGUI adapter 只测试方法调用和可观察状态，最终指针交互交给 Unity 编辑器验证。
- 当前工作区已有大量未提交迁移文件 -> 实施时只改本变更范围内文件，不回滚用户或其他变更。

## Migration Plan

1. 固定 `GameView.prefab` 的 UGUI 结构和 ReferenceCollector key，补齐手牌、怪物、状态、奖励、预览、drop-zone、toast 相关节点。
2. 增加 UGUI 条目视图：卡牌项、怪物项、Buff/意图渲染辅助。
3. 增加 UGUI 手牌视图并实现 `IDragSurface`，接入 `CardDragController` 与 `FanLayoutCalc`。
4. 增加 UGUI 目标选择、回合控制和 BattlePanel 协调器。
5. 改造 `GameView` 从动态摘要渲染切换为子视图装配和面板显隐，保留 `GameController` 的 ViewModel 事件桥接。
6. 删除或降级临时动态 UI 兜底，确保运行时代码没有 `UnityEngine.UIElements`。
7. 运行编译检查、相关 EditMode 测试，并在 Unity 编辑器中验证主菜单进入战斗、出牌、手动选目标、结束回合、奖励确认和返回主菜单。

回滚策略：如果 UGUI 拖拽适配中断，可保留 Prefab 结构和静态渲染，临时禁用拖拽入口并继续使用点击命令测试；不要恢复运行时 UITK 资源作为回滚方案。

## Open Questions

- `GameView.prefab` 的卡牌和怪物条目是否使用内嵌隐藏模板，还是拆成独立 Prefab addressable？默认优先内嵌隐藏模板，减少资源地址管理。
- 是否需要在本变更内恢复与 UITK 完全一致的视觉样式？默认先恢复功能和可维护 Prefab 结构，视觉 polish 可后续单独处理。
- UGUI 目标选择取消是否需要支持键盘 ESC？默认支持空白/取消区域点击；ESC 若项目已有输入系统可接入则一并实现，否则作为后续增强。
