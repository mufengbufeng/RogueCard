# gameview-target-selection-flow Specification

## Purpose
TBD - created by archiving change gameview-extract-battle-coordinator. Update Purpose after archive.
## Requirements
### Requirement: HandFanView 必须提供 RequestGhostCleanup 与 RequestGhostRebound 公开 API

`HandFanView` SHALL 暴露：

- `RequestGhostCleanup()` —— 立即销毁 UGUI ghost（不触发回弹动画）；用于"目标选择确认"场景
- `RequestGhostRebound(int handIdx)` —— 启动协同回弹动画：ghost 立即销毁、其他卡 transition 0.15s 回到 N 张布局、被拖卡 opacity 立即恢复、`options.ReboundDurationMs` 后状态归 Idle

两个方法 SHALL 在非拖拽 / 非"由 BattlePanelView 持有 ghost" 状态下被调用时通过 `Log.Warning` 记录并幂等返回（不抛异常）。

#### Scenario: RequestGhostCleanup 立即销毁 ghost

- **WHEN** `_handFanView.RequestGhostCleanup()` 被调用
- **THEN** UGUI ghost 对象 SHALL 被移除或隐藏
- **AND** 不应启动 transition 动画

#### Scenario: RequestGhostRebound 启动协同回弹

- **WHEN** `_handFanView.RequestGhostRebound(2)` 被调用
- **THEN** ghost SHALL 立即销毁
- **AND** 其他卡 SHALL 应用 `transitionDuration = 0.15s`
- **AND** 被拖卡 SHALL 立即恢复 opacity
- **AND** `options.ReboundDurationMs` 后内部状态 SHALL 归 Idle

#### Scenario: 非预期状态调用时安全降级

- **WHEN** 当前 `Idle` 态调 `RequestGhostRebound(2)`
- **THEN** SHALL 通过 `Log.Warning` 记录
- **AND** SHALL NOT 抛异常

### Requirement: TargetSelector 必须在 UGUI 怪物项上进入目标选择态

`TargetSelector` SHALL 接收 UGUI 怪物列表、手牌视图、目标上下文和取消区域。进入目标选择态时，TargetSelector SHALL 高亮所有存活怪物项并绑定临时点击回调。点击怪物 SHALL 调用 `ITargetContext.UseCardOnMonster(handIdx, monsterIdx)`，并调 `HandFanView.RequestGhostCleanup()`。

#### Scenario: SingleManual 卡进入目标选择

- **WHEN** `TargetSelector.Enter(2)` 被调用
- **THEN** 所有存活怪物项 SHALL 进入可选目标视觉状态
- **AND** `TargetSelector.IsActive` SHALL 为 true

#### Scenario: 点击怪物确认目标

- **WHEN** 目标选择态中点击 monster index 1
- **THEN** `ITargetContext.UseCardOnMonster(2, 1)` SHALL 被调用
- **AND** `HandFanView.RequestGhostCleanup()` SHALL 被调用
- **AND** 目标选择态 SHALL 退出

### Requirement: TargetSelector 必须支持取消并触发回弹

目标选择态中，玩家点击取消区域或外部调用 `Cancel()` SHALL 退出目标选择态、移除怪物高亮和临时回调，并调用 `HandFanView.RequestGhostRebound(handIdx)`。

#### Scenario: 点击空白取消目标选择

- **WHEN** 目标选择态中玩家点击取消区域
- **THEN** `HandFanView.RequestGhostRebound(selectedHandIdx)` SHALL 被调用
- **AND** 所有怪物项 SHALL 退出可选目标视觉状态
- **AND** `TargetSelector.IsActive` SHALL 为 false

#### Scenario: 外部取消目标选择

- **WHEN** `TargetSelector.Cancel()` 被调用
- **THEN** SHALL 与点击空白取消执行相同清理

#### Scenario: Dispose 时取消活跃目标选择

- **WHEN** `TargetSelector.Dispose()` 在 active 状态下被调用
- **THEN** SHALL 退出目标选择态
- **AND** SHALL 解绑所有临时 UGUI 事件
