## REMOVED Requirements

### Requirement: GameView 必须根据运行时状态动态展示怪物
**Reason**: GameView 从 UIView 重写为 Screen<GameViewModel>，数据绑定方式从 BindProperty 改为 ReactiveProperty.Changed
**Migration**: GameScreen.OnSetup() 中订阅 ViewModel.Monsters.Changed，回调中刷新 UI

### Requirement: GameView 必须展示所有怪物的下一回合意图
**Reason**: 同上，数据绑定方式变更
**Migration**: 怪物意图数据包含在 MonsterRuntime 中，GameScreen 订阅 ViewModel.Monsters.Changed 时一并刷新意图展示

### Requirement: GameView 必须展示玩家手牌
**Reason**: 同上，数据绑定方式变更
**Migration**: GameScreen 订阅 ViewModel.Hand.Changed 刷新手牌区域

### Requirement: GameView 必须响应玩家出牌操作
**Reason**: 交互方式从 C# event + Controller BindEvent 改为 ViewModel 命令意图
**Migration**: GameScreen 注册 ClickEvent 回调，调用 ViewModel.UseCard(index)

## ADDED Requirements

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
