# gameview-target-selection-flow Specification

## Purpose
TBD - created by archiving change gameview-extract-battle-coordinator. Update Purpose after archive.
## Requirements
### Requirement: HandFanView 必须提供 RequestGhostCleanup 与 RequestGhostRebound 公开 API

`HandFanView` SHALL 暴露：

- `RequestGhostCleanup()` —— 立即销毁 ghost（不触发回弹动画）；用于"目标选择确认"场景
- `RequestGhostRebound(int handIdx)` —— 启动协同回弹动画：ghost 立即销毁、其他卡 transition 0.15s 回到 N 张布局、被拖卡 opacity 立即恢复、`options.ReboundDurationMs` 后状态归 Idle

两个方法 SHALL 在非拖拽 / 非"由 BattlePanelView 持有 ghost" 状态下被调用时通过 `Log.Warning` 记录并幂等返回（不抛异常）。

#### Scenario: RequestGhostCleanup 立即销毁 ghost

- **WHEN** `_handFanView.RequestGhostCleanup()` 被调用
- **THEN** ghost VisualElement SHALL 被移除
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
