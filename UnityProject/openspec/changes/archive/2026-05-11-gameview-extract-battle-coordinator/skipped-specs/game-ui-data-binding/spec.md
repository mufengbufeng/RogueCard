## MODIFIED Requirements

### Requirement: GameScreen 必须支持 Region 切换 Battle 和 Reward 视图

`GameView` SHALL 包含一个 `Region` 用于主区域切换。当 `ViewModel.Phase` 变化时，`GameView` SHALL 通过 `Region` 切换显示 `BattlePanel` 或 `RewardPanel`。`Phase` 类型为 `BattlePhase` 枚举（`Idle/Prepare/PlayerTurn/MonsterTurn/Check/Reward`）。`GameView` SHALL 在 `Region` 切换到 `BattlePanel` 后实例化 `BattlePanelView` 装配子模块；切换到 `RewardPanel` 后注册 `reward-confirm-btn` 转发到 `ViewModel.SelectReward`。`GameView` SHALL 在切换前 `Dispose` 旧 `BattlePanelView`（若存在），SHALL 在 `OnDispose` 时 `Dispose` 全部子模块。

#### Scenario: Phase 变为 PlayerTurn 时显示战斗面板

- **WHEN** `ViewModel.Phase.Value` 变为 `BattlePhase.PlayerTurn`（或 `Prepare/MonsterTurn/Check`）
- **THEN** `GameView` SHALL 通过 `Region` 加载并显示 `BattlePanel` UXML
- **AND** SHALL 实例化 `BattlePanelView` 装配子模块

#### Scenario: Phase 变为 Reward 时显示奖励面板

- **WHEN** `ViewModel.Phase.Value` 变为 `BattlePhase.Reward`
- **THEN** `GameView` SHALL 通过 `Region` 加载并显示 `RewardPanel` UXML
- **AND** SHALL `Dispose` 旧 `BattlePanelView`（若存在）
- **AND** SHALL 注册 `reward-confirm-btn` 的 `ClickEvent` 转发到 `ViewModel.SelectReward()`

#### Scenario: GameView OnDispose 释放全部子模块

- **WHEN** `GameView.OnDispose()` 被调用
- **THEN** `PlayerStatusView.Dispose()` SHALL 被调用
- **AND** `BattlePanelView.Dispose()` SHALL 被调用（若存在）
- **AND** `Region.Clear()` SHALL 被调用

## REMOVED Requirements

### Requirement: 拖出选目标态相关行为（出 SingleManual 卡 → 选目标）

**Reason**: 行为已从 `GameView.EnterSelectingTarget` / `OnMonsterTargetClicked` / `CancelSelectingTarget` 等私有方法迁移到 `gameview-target-selection-flow` capability，由 `TargetSelector` 协调器与 `MonsterListView.EnterTargetMode` / `HandFanView.RequestGhostRebound` 公开 API 协同实现。
**Migration**: 见 `gameview-target-selection-flow/spec.md` 中 "TargetSelector 必须支持 Enter / 怪物点击 / Cancel 三阶段流程" / "TargetSelector 必须区分确认与取消两种退出路径" / "MonsterListView 必须提供 EnterTargetMode / ExitTargetMode 公开 API" / "HandFanView 必须提供 RequestGhostCleanup 与 RequestGhostRebound 公开 API"。

### Requirement: 出牌失败 toast 显示行为

**Reason**: 行为已从 `GameView.OnCardPlayFailed` 迁移到 `gameview-turn-control-view` capability，由 `TurnControlView` 子模块封装含中文映射、版本号"新失败覆盖旧失败"机制、1.2s 自动隐藏。
**Migration**: 见 `gameview-turn-control-view/spec.md` 中 "TurnControlView 必须显示出牌失败 toast 含中文映射" / "TurnControlView 必须在 1.2 秒后自动隐藏 fail toast 并支持新失败覆盖"。

### Requirement: 结束回合按钮启用与点击转发

**Reason**: 行为已从 `GameView.RefreshInfo` 中的 `_endTurnBtn.SetEnabled(...)` 与 `BindBattleContent` 中的 `_endTurnBtn.RegisterCallback<ClickEvent>(...)` 迁移到 `gameview-turn-control-view` capability。
**Migration**: 见 `gameview-turn-control-view/spec.md` 中 "TurnControlView 必须按 Phase 启用 / 禁用结束回合按钮"。
