# game-ui-data-binding Specification

## Purpose

定义局内 GameView 的数据绑定与命令转发边界。GameView 作为顶层 UGUI 窗口只负责常驻状态面板装配、BattlePanel/RewardPanel 切换，以及把战斗内容交给战斗子视图协调器；具体玩家状态、怪物列表、手牌、目标选择、回合按钮和失败 toast 行为由拆分后的子模块 specs 约束。

## Requirements

### Requirement: GameView 必须通过 ReactiveProperty 驱动局内 UI 更新

GameView 与其 UGUI 子模块 SHALL 通过 `GameViewModel` 暴露的 `ReactiveProperty` 与事件驱动 UI 更新。GameView SHALL NOT 直接访问 `GameModel`；子模块 SHALL 通过切片接口订阅所需数据，并使用 Prefab 绑定到的 UGUI 组件刷新显示。事件波次展示数据 SHALL 通过 `GameViewModel` 暴露的响应式状态驱动 RewardPanel 文本。

#### Scenario: 玩家状态变化由 PlayerStatusView 刷新
- **WHEN** `GameViewModel.PlayerHp.Value`、`Energy.Value`、`Phase.Value` 或 `PlayerBuffs.Value` 变化
- **THEN** `PlayerStatusView` SHALL 通过 `IPlayerStatusContext` 收到变化并刷新对应 UGUI 文本、进度条或 Buff 容器
- **AND** `GameView` SHALL NOT 直接查询或更新玩家状态 UI 元素

#### Scenario: 战斗内容变化由 BattlePanelView 子模块刷新
- **WHEN** `GameViewModel.Monsters.Value` 或 `Hand.Value` 变化
- **THEN** `BattlePanelView` 装配的 `MonsterListView` 或 `HandFanView` SHALL 通过各自切片接口刷新 UGUI 条目
- **AND** `GameView` SHALL NOT 直接重建怪物项或手牌项

#### Scenario: 事件波次文案变化由 RewardPanel 刷新
- **WHEN** `GameViewModel` 的当前波次标题、描述、继续文案或等待确认状态变化
- **THEN** GameView SHALL 刷新 UGUI `RewardPanel` 对应标题、描述和确认按钮文本
- **AND** GameView SHALL NOT 直接访问 `GameModel` 查询波次配置

### Requirement: GameView 必须通过 ViewModel 命令意图转发用户操作

GameView 子模块 SHALL 将 UGUI 用户交互转发为 `GameViewModel` 命令意图调用，SHALL NOT 直接调用 `CardSystem`、`BattleSystem`、`WaveSystem` 或修改 `GameModel`。

#### Scenario: 出牌命令转发
- **WHEN** 玩家把非手动选目标卡拖到 UGUI drop-zone 并释放
- **THEN** `BattlePanelView` SHALL 调用 `GameViewModel.UseCard(handIndex, -1)`
- **AND** SHALL NOT 直接调用 `CardSystem.Play`

#### Scenario: 手动目标出牌命令转发
- **WHEN** 玩家把 `TargetMode == SingleManual` 的卡拖到 UGUI drop-zone 后点击怪物
- **THEN** `TargetSelector` SHALL 调用 `GameViewModel.UseCardOnMonster(handIndex, monsterIndex)`
- **AND** SHALL NOT 直接修改怪物或玩家状态

#### Scenario: 结束回合命令转发
- **WHEN** 用户点击 UGUI 结束回合按钮
- **THEN** `TurnControlView` SHALL 调用 `GameViewModel.EndTurn()`
- **AND** SHALL NOT 直接调用 `BattleSystem.EndTurn`

#### Scenario: 事件波次确认命令转发
- **WHEN** 用户点击 UGUI RewardPanel 的确认按钮
- **THEN** GameView SHALL 调用 `GameViewModel.SelectReward()` 或等价确认命令
- **AND** SHALL NOT 直接调用 `WaveSystem` 或流程状态机

### Requirement: GameView 必须支持面板切换 Battle 和 Reward 视图

GameView SHALL 通过 `GameView.prefab` 内的 UGUI `BattlePanel` 与 `RewardPanel` 进行主区域切换。当 `GameViewModel.Phase` 或事件确认状态变化时，GameView SHALL 显隐对应面板并装配或释放相关子视图。GameView SHALL NOT 使用 UITK `Region`、UXML 或 `VisualTreeAsset` 加载 Battle/Reward 内容。

#### Scenario: 战斗阶段显示 BattlePanel
- **WHEN** `GameViewModel.Phase.Value` 为 `Prepare`、`PlayerTurn`、`MonsterTurn` 或 `Check`
- **AND** `GameViewModel` 未处于事件波次等待确认状态
- **THEN** GameView SHALL 显示 UGUI `BattlePanel`
- **AND** SHALL 确保 `BattlePanelView` 已装配

#### Scenario: 奖励阶段显示 RewardPanel
- **WHEN** `GameViewModel.Phase.Value` 为 `Reward`
- **THEN** GameView SHALL 显示 UGUI `RewardPanel`
- **AND** SHALL 释放当前 `BattlePanelView`

#### Scenario: 事件波次等待确认时显示 RewardPanel
- **WHEN** `GameViewModel` 处于事件波次等待确认状态
- **THEN** GameView SHALL 显示 UGUI `RewardPanel`
- **AND** SHALL 释放当前 `BattlePanelView`
- **AND** SHALL 保持 RewardPanel 的标题、描述和按钮文本来自当前波次展示状态

### Requirement: GameView 必须按子模块切片接口装配子视图

GameView SHALL 在 `OnOpen` 或等价初始化阶段实例化常驻区域子模块 `PlayerStatusView`，并在进入战斗面板时实例化 `BattlePanelView`。子模块 SHALL 接收窄切片接口或组合接口，不应要求完整 ViewModel 之外的额外全局状态。

#### Scenario: 常驻状态面板装配
- **WHEN** UGUI `GameView` 打开并收到 `GameViewModel`
- **THEN** GameView SHALL 构造 `PlayerStatusView`
- **AND** SHALL 传入由 `GameViewModel` 实现的 `IPlayerStatusContext`

#### Scenario: 战斗面板装配
- **WHEN** UGUI `BattlePanel` 被显示
- **THEN** GameView SHALL 构造 `BattlePanelView`
- **AND** SHALL 传入由 `GameViewModel` 实现的 `IBattleContext`

#### Scenario: 离开战斗面板释放子模块
- **WHEN** GameView 从 battle route 切换到 reward route 或被释放
- **THEN** GameView SHALL dispose 当前 `BattlePanelView`
