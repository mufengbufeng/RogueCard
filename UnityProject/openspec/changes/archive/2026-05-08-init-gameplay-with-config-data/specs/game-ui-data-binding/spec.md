## ADDED Requirements

### Requirement: GameView 必须根据运行时状态动态展示怪物
GameView MUST 在 MonsterRoot 区域内根据当前批次的怪物运行时列表，动态实例化 GamePlay_MonsterItem 预制体并展示怪物图像和名称。

#### Scenario: 进入战斗批次时展示怪物
- **WHEN** 进入新的战斗批次
- **THEN** GameView MUST 清空 MonsterRoot 下的旧子项
- **AND** GameView MUST 为每个在场怪物实例化 GamePlay_MonsterItem
- **AND** 每个 MonsterItem MUST 显示对应怪物的名称（NameText）
- **AND** 每个 MonsterItem MUST 显示对应怪物的图像（MonsterImg）

#### Scenario: 怪物死亡时更新展示
- **WHEN** 怪物血量降为 0
- **THEN** GameView MUST 在 MonsterRoot 中移除或隐藏对应 MonsterItem

### Requirement: GameView 必须展示所有怪物的下一回合意图
GameView MUST 在 TipsScrollRect 的 Content 区域内展示所有在场怪物的下一回合意图。

#### Scenario: 准备阶段刷新意图展示
- **WHEN** 进入准备阶段并生成所有怪物意图
- **THEN** GameView MUST 清空 TipsScrollRect Content 下的旧子项
- **AND** GameView MUST 为每个在场怪物实例化 GamePlay_TipsItem
- **AND** 每个 TipsItem MUST 根据意图类型显示对应图标（TipsIconImg）
- **AND** 每个 TipsItem MUST 显示意图数值（TipsValueText）

### Requirement: GameView 必须展示玩家手牌
GameView MUST 在 CardScrollRect 的 Content 区域内根据手牌列表动态实例化 GamePlay_CardItem 预制体。

#### Scenario: 抽牌后展示手牌
- **WHEN** 玩家抽牌到手牌中
- **THEN** GameView MUST 在 CardScrollRect Content 中为新手牌实例化 GamePlay_CardItem
- **AND** 每个 CardItem MUST 显示卡牌名称（NameText）
- **AND** 每个 CardItem MUST 显示能量消耗（ConsumptionText）

#### Scenario: 使用卡牌后更新手牌展示
- **WHEN** 玩家使用一张手牌
- **THEN** GameView MUST 从 CardScrollRect Content 中移除对应 CardItem

### Requirement: GameView 必须响应玩家出牌操作
GameView MUST 允许玩家在玩家回合点击手牌使用卡牌。使用卡牌时 MUST 扣除对应能量并触发效果结算。

#### Scenario: 点击手牌使用卡牌
- **WHEN** 玩家在玩家回合点击一张手牌
- **THEN** 系统 MUST 检查当前能量是否足够支付卡牌消耗
- **AND** 若能量足够，MUST 扣除能量、从手牌移除该卡牌、触发效果
- **AND** 若能量不足，MUST 不执行任何操作
