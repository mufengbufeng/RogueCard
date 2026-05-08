# game-ui-data-binding Specification

## Purpose

定义 GameScreen 的数据绑定规则。GameScreen 通过订阅 GameViewModel 的 ReactiveProperty 驱动所有 UI 更新（怪物列表、手牌列表、玩家状态、战斗阶段），并通过 ViewModel 命令意图（UseCard、EndTurn、SelectReward）转发用户操作。GameScreen 内嵌一个 Region 用于在 BattlePanel 与 RewardPanel 之间切换主区域内容。

## Requirements

### Requirement: GameScreen 必须通过 ReactiveProperty 驱动所有 UI 更新
GameScreen SHALL 在 OnSetup() 中订阅 GameViewModel 的所有 ReactiveProperty.Changed 事件。任何 UI 更新 SHALL 由 ViewModel 属性变化驱动，Screen SHALL NOT 直接访问 Model 或 Config。

#### Scenario: 怪物列表变化时刷新 UI
- **WHEN** ViewModel.Monsters.Value 被设置为新的怪物列表
- **THEN** GameScreen SHALL 收到 Changed 回调
- **AND** GameScreen SHALL 清空旧怪物元素并重新实例化怪物子项

#### Scenario: 手牌列表变化时刷新 UI
- **WHEN** ViewModel.Hand.Value 被设置为新的手牌列表
- **THEN** GameScreen SHALL 收到 Changed 回调
- **AND** GameScreen SHALL 刷新手牌区域

### Requirement: GameScreen 必须通过 ViewModel 命令意图转发用户操作
GameScreen SHALL 将用户交互（点击手牌、点击结束回合）转发为 ViewModel 的命令意图调用（UseCard、EndTurn）。GameScreen SHALL NOT 包含游戏逻辑。

#### Scenario: 点击手牌转发到 ViewModel
- **WHEN** 用户点击手牌区域的一张卡牌
- **THEN** GameScreen SHALL 调用 ViewModel.UseCard(handIndex)
- **AND** SHALL NOT 直接调用 CardSystem 或修改 Model

#### Scenario: 点击结束回合转发到 ViewModel
- **WHEN** 用户点击结束回合按钮
- **THEN** GameScreen SHALL 调用 ViewModel.EndTurn()
- **AND** SHALL NOT 直接调用 BattleSystem

### Requirement: GameScreen 必须支持 Region 切换 Battle 和 Reward 视图
GameScreen SHALL 包含一个 Region 用于主区域切换。当 ViewModel.Phase 变化时，GameScreen SHALL 通过 Region 切换显示 BattlePanel 或 RewardPanel。Phase 类型为 BattlePhase 枚举（Idle/Prepare/PlayerTurn/MonsterTurn/Check/Reward）。

#### Scenario: Phase 变为 PlayerTurn 时显示战斗面板
- **WHEN** ViewModel.Phase.Value 变为 BattlePhase.PlayerTurn（或 Prepare/MonsterTurn/Check）
- **THEN** GameScreen SHALL 通过 Region 加载并显示 BattlePanel UXML

#### Scenario: Phase 变为 Reward 时显示奖励面板
- **WHEN** ViewModel.Phase.Value 变为 BattlePhase.Reward
- **THEN** GameScreen SHALL 通过 Region 加载并显示 RewardPanel UXML
