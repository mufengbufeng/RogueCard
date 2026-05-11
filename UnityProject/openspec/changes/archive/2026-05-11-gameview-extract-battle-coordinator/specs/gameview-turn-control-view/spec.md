## ADDED Requirements

### Requirement: TurnControlView 必须按 Phase 启用 / 禁用结束回合按钮

`TurnControlView` SHALL 通过构造函数接收 `(Button endTurnBtn, Label failToast, ITurnContext context)`。SHALL 注册 `endTurnBtn` 的 `ClickEvent` 转发到 `context.EndTurn()`。SHALL 订阅 `context.Phase.Changed`，当 `Phase.Value == BattlePhase.PlayerTurn` 时 `endTurnBtn.SetEnabled(true)`，否则 `SetEnabled(false)`。

#### Scenario: 玩家回合启用按钮

- **WHEN** `Phase.Value == BattlePhase.PlayerTurn`
- **THEN** `endTurnBtn.enabledSelf` SHALL 为 `true`

#### Scenario: 怪物回合禁用按钮

- **WHEN** `Phase.Value == BattlePhase.MonsterTurn`
- **THEN** `endTurnBtn.enabledSelf` SHALL 为 `false`

#### Scenario: 点击按钮转发 EndTurn

- **WHEN** 玩家点击 `endTurnBtn`
- **THEN** `ITurnContext.EndTurn()` SHALL 被调用

### Requirement: TurnControlView 必须显示出牌失败 toast 含中文映射

`TurnControlView` SHALL 订阅 `ITurnContext.CardPlayFailed` 事件。收到事件时按 reason 字符串映射中文：

- `"InsufficientEnergy"` → `"能量不足"`
- `"NotPlayerTurn"` → `"现在不是你的回合"`
- `"InvalidTarget"` → `"无效目标"`
- `"InvalidHandIndex"` → `"卡牌索引错误"`
- 其他 → `"出牌失败"`

设置 `failToast.text` 为映射后的文本，添加 CSS 类 `fail-toast--visible`。

#### Scenario: 能量不足映射

- **WHEN** `CardPlayFailed("InsufficientEnergy")` 触发
- **THEN** `failToast.text` SHALL 为 `"能量不足"`
- **AND** `failToast.classList` SHALL 包含 `fail-toast--visible`

#### Scenario: 未知 reason 默认映射

- **WHEN** `CardPlayFailed("SomeUnknownReason")` 触发
- **THEN** `failToast.text` SHALL 为 `"出牌失败"`

### Requirement: TurnControlView 必须在 1.2 秒后自动隐藏 fail toast 并支持新失败覆盖

`TurnControlView` SHALL 维护一个内部版本号 `_toastVersion`，每次显示 toast 时自增并捕获当前值。1200ms 后通过 `failToast.schedule.Execute(...).StartingIn(1200)` 检查版本号是否一致：一致 SHALL `RemoveFromClassList("fail-toast--visible")`；不一致 SHALL 不操作（说明已被新失败覆盖）。

#### Scenario: 1.2 秒后自动隐藏

- **WHEN** `CardPlayFailed("InsufficientEnergy")` 触发后等 1.5 秒（无新失败）
- **THEN** `failToast.classList` SHALL NOT 包含 `fail-toast--visible`

#### Scenario: 新失败覆盖旧失败

- **WHEN** 在 t=0 触发 `CardPlayFailed("A")`，在 t=500ms 触发 `CardPlayFailed("B")`，等到 t=1300ms（旧失败的 1.2s 已到，但新失败的 1.2s 未到）
- **THEN** `failToast.text` SHALL 为 `"B" 的中文映射`
- **AND** `failToast.classList` SHALL 仍包含 `fail-toast--visible`（新失败的版本号未到期）

#### Scenario: 新失败的 1.2 秒到时正常隐藏

- **WHEN** 同上，等到 t=1800ms（新失败的 1.2s 已到）
- **THEN** `failToast.classList` SHALL NOT 包含 `fail-toast--visible`

### Requirement: TurnControlView 必须支持 Dispose

`TurnControlView` SHALL 实现 `IDisposable`，`Dispose()` SHALL：

- 解绑 `endTurnBtn` 的 `ClickEvent` 回调
- 解绑 `Phase.Changed` 与 `CardPlayFailed`
- 自增 `_toastVersion`（让任何已调度的 schedule 检查不通过）
- 字段置空
- 幂等

#### Scenario: Dispose 后 Phase 变化不再操作按钮

- **WHEN** `_turnControlView.Dispose()` 后 `Phase.Value` 变化
- **THEN** `endTurnBtn` 的 enabled 状态 SHALL NOT 被改动

#### Scenario: Dispose 后 fail 事件不再显示 toast

- **WHEN** `_turnControlView.Dispose()` 后 `CardPlayFailed` 触发
- **THEN** `failToast` SHALL NOT 被修改
