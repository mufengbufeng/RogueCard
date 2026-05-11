# game-ui-data-binding Specification

## Purpose

定义局内 GameView 的数据绑定与命令转发边界。GameView 作为顶层 Screen 只负责常驻状态面板装配、主 Region 路由，以及把 Region 内容交给战斗子视图协调器；具体玩家状态、怪物列表、手牌、目标选择、回合按钮和失败 toast 行为由拆分后的子模块 specs 约束。

## Requirements

### Requirement: GameView 必须通过 ReactiveProperty 驱动局内 UI 更新

GameView 与其子模块 SHALL 通过 `GameViewModel` 暴露的 `ReactiveProperty` 与事件驱动 UI 更新。GameView SHALL NOT 直接访问 `GameModel`；子模块 SHALL 通过切片接口订阅所需数据。

#### Scenario: 玩家状态变化由 PlayerStatusView 刷新
- **WHEN** `GameViewModel.PlayerHp.Value`、`Energy.Value`、`Phase.Value` 或 `PlayerBuffs.Value` 变化
- **THEN** `PlayerStatusView` SHALL 通过 `IPlayerStatusContext` 收到变化并刷新对应 UI
- **AND** `GameView` SHALL NOT 直接查询或更新玩家状态 UI 元素

#### Scenario: 战斗内容变化由 BattlePanelView 子模块刷新
- **WHEN** `GameViewModel.Monsters.Value` 或 `Hand.Value` 变化
- **THEN** `BattlePanelView` 装配的 `MonsterListView` 或 `HandFanView` SHALL 通过各自切片接口刷新
- **AND** `GameView` SHALL NOT 直接重建怪物项或手牌项

### Requirement: GameView 必须通过 ViewModel 命令意图转发用户操作

GameView 子模块 SHALL 将用户交互转发为 `GameViewModel` 命令意图调用，SHALL NOT 直接调用 `CardSystem`、`BattleSystem` 或修改 `GameModel`。

#### Scenario: 出牌命令转发
- **WHEN** 玩家把非手动选目标卡拖到 drop-zone 并释放
- **THEN** `BattlePanelView` SHALL 调用 `GameViewModel.UseCard(handIndex, -1)`
- **AND** SHALL NOT 直接调用 `CardSystem.Play`

#### Scenario: 手动目标出牌命令转发
- **WHEN** 玩家把 `TargetMode == SingleManual` 的卡拖到 drop-zone 后点击怪物
- **THEN** `TargetSelector` SHALL 调用 `GameViewModel.UseCardOnMonster(handIndex, monsterIndex)`
- **AND** SHALL NOT 直接修改怪物或玩家状态

#### Scenario: 结束回合命令转发
- **WHEN** 用户点击结束回合按钮
- **THEN** `TurnControlView` SHALL 调用 `GameViewModel.EndTurn()`
- **AND** SHALL NOT 直接调用 `BattleSystem.EndTurn`

### Requirement: GameView 必须支持 Region 切换 Battle 和 Reward 视图

GameView SHALL 包含一个 `Region` 用于主区域切换。当 `GameViewModel.Phase` 变化时，GameView SHALL 使用 UI 路由语义选择显示 `BattlePanel` 或 `RewardPanel`。

#### Scenario: 战斗阶段显示 BattlePanel
- **WHEN** `GameViewModel.Phase.Value` 为 `Prepare`、`PlayerTurn`、`MonsterTurn` 或 `Check`
- **THEN** GameView SHALL 通过 Region 加载并显示 `BattlePanel`

#### Scenario: 奖励阶段显示 RewardPanel
- **WHEN** `GameViewModel.Phase.Value` 为 `Reward`
- **THEN** GameView SHALL 通过 Region 加载并显示 `RewardPanel`

### Requirement: GameView 必须按子模块切片接口装配子视图

GameView SHALL 在 `OnSetup()` 中实例化常驻区域子模块 `PlayerStatusView`，并在 `BindBattleContent()` 中实例化 `BattlePanelView`。子模块 SHALL 接收窄切片接口或组合接口，不应要求完整 ViewModel 之外的额外全局状态。

#### Scenario: 常驻状态面板装配
- **WHEN** `GameView.OnSetup()` 执行
- **THEN** GameView SHALL 构造 `PlayerStatusView`
- **AND** SHALL 传入由 `GameViewModel` 实现的 `IPlayerStatusContext`

#### Scenario: 战斗面板装配
- **WHEN** Region 加载 `BattlePanel`
- **THEN** GameView SHALL 构造 `BattlePanelView`
- **AND** SHALL 传入由 `GameViewModel` 实现的 `IBattleContext`

#### Scenario: 离开战斗面板释放子模块
- **WHEN** GameView 从 battle route 切换到 reward route 或被 Dispose
- **THEN** GameView SHALL dispose 当前 `BattlePanelView`
