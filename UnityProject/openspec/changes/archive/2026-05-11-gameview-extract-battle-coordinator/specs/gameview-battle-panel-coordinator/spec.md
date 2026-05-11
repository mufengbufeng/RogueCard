## ADDED Requirements

### Requirement: BattlePanelView 必须装配战斗子模块

`BattlePanelView` SHALL 在构造时接收 `(VisualElement content, IBattleContext context, VisualTreeAsset monsterItemTpl, VisualTreeAsset cardItemTpl)`，从 `content` 内查询 `monster-container`、`hand-fan`、`drop-zone`、`preview-layer`、`end-turn-btn`、`fail-toast` 共 6 个共享元素，并实例化以下子模块（按顺序）：

1. `MonsterListView` (绑定 `monster-container`)
2. `HandFanView` (绑定 `hand-fan` / `drop-zone` / `preview-layer`)
3. `TurnControlView` (绑定 `end-turn-btn` / `fail-toast`)
4. `TargetSelector` (持有 `MonsterListView` 与 `HandFanView` 引用)

#### Scenario: 构造完成后子模块全部就绪

- **WHEN** `new BattlePanelView(content, ctx, ...)`
- **THEN** `MonsterListView` / `HandFanView` / `TurnControlView` / `TargetSelector` SHALL 全部已构造
- **AND** 怪物列表与手牌 SHALL 完成首次刷新（继承 change 1/2 的首次刷新行为）
- **AND** `TargetSelector` SHALL 处于 `Idle` 状态

#### Scenario: 缺失关键元素时报错

- **WHEN** `content` 缺少 `hand-fan` 元素
- **THEN** SHALL 通过 `Log.Error` 记录错误（沿用现有 GameView 错误处理风格）
- **AND** SHALL NOT 抛出异常导致 BattlePanel 加载流程中断（保持容错）

### Requirement: BattlePanelView 必须订阅 HandFanView 事件并按需路由

`BattlePanelView` SHALL 订阅 `HandFanView` 的三个事件：

- `CardClicked(handIdx)` —— 由 `HandFanView` 内部 `CardPreviewController` 处理，`BattlePanelView` SHALL NOT 重复处理
- `CardDroppedOnZone(handIdx, needsManualTarget)` —— 路由：`needsManualTarget == true` → `_targetSelector.Enter(handIdx)`；否则 → `IBattleContext.UseCard(handIdx)`
- `CardDragCancelled(handIdx)` —— 由 `CardDragController` 内部回弹动画完成，`BattlePanelView` SHALL NOT 额外处理

#### Scenario: AutoTarget 卡直接调 UseCard

- **WHEN** `HandFanView.CardDroppedOnZone(2, false)` 触发
- **THEN** `IBattleContext.UseCard(2)` SHALL 被调用
- **AND** `_targetSelector.Enter(...)` SHALL NOT 被调用

#### Scenario: SingleManual 卡进入选目标

- **WHEN** `HandFanView.CardDroppedOnZone(3, true)` 触发
- **THEN** `_targetSelector.Enter(3)` SHALL 被调用
- **AND** `IBattleContext.UseCard(3)` SHALL NOT 立即被调用（需要等待玩家选目标后）

### Requirement: BattlePanelView 必须在 Phase 离开 PlayerTurn 时强制取消 TargetSelector

`BattlePanelView` SHALL 订阅 `IBattleContext.Phase.Changed`，当 `_targetSelector.IsActive && phase != PlayerTurn` 时调 `_targetSelector.Cancel()`。

#### Scenario: 怪物回合开始强制取消选目标

- **WHEN** `_targetSelector.IsActive == true` 且 `Phase.Value` 变为 `MonsterTurn`
- **THEN** `_targetSelector.Cancel()` SHALL 被调用
- **AND** ghost SHALL 被清理或回弹（具体行为见 `gameview-target-selection-flow`）

### Requirement: BattlePanelView 必须按反序释放子模块

`BattlePanelView` SHALL 实现 `IDisposable`，`Dispose()` SHALL：

1. 解绑 `Phase.Changed`
2. 解绑 `HandFanView` 三事件
3. 调 `_targetSelector.Dispose()`（先 dispose 编排器，避免 dispose 期间继续触发其他子模块）
4. 调 `_turnControlView.Dispose()`
5. 调 `_handFanView.Dispose()`
6. 调 `_monsterListView.Dispose()`
7. 清空字段引用
8. 幂等

#### Scenario: Region 切到 RewardPanel 时按序释放

- **WHEN** `BattlePanelView.Dispose()` 被调用（如 `Region` 切到 `RewardPanel`）
- **THEN** `_targetSelector.Dispose` SHALL 早于 `_handFanView.Dispose` 调用
- **AND** Dispose 后再触发 `IBattleContext.Hand.Value` 变化 SHALL NOT 引起任何 UI 操作或异常
