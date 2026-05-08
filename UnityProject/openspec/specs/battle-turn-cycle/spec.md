# battle-turn-cycle Specification

## Purpose

定义战斗回合循环的阶段推进规则，包括准备阶段、玩家回合、怪物回合和检查阶段的顺序流转，以及各阶段的职责。

## Requirements

### Requirement: 战斗回合必须按固定阶段循环推进
系统 MUST 实现战斗回合循环，按 准备阶段 → 玩家回合 → 怪物回合 → 检查阶段 顺序推进，并在每个阶段执行对应逻辑。

#### Scenario: 战斗开始进入准备阶段
- **WHEN** 进入战斗波次
- **THEN** 系统 MUST 进入准备阶段
- **AND** 系统 MUST 刷新所有在场怪物的下一回合意图
- **AND** 系统 MUST 恢复玩家能量到最大值
- **AND** 系统 MUST 抽牌到手牌上限

#### Scenario: 准备阶段完成后进入玩家回合
- **WHEN** 准备阶段执行完毕
- **THEN** 系统 MUST 进入玩家回合
- **AND** 玩家 MUST 可以使用手牌

#### Scenario: 玩家结束回合后进入怪物回合
- **WHEN** 玩家点击结束回合或手牌用完
- **THEN** 系统 MUST 进入怪物回合
- **AND** 每个怪物 MUST 按其当前意图执行行为

#### Scenario: 怪物回合完成后进入检查阶段
- **WHEN** 所有怪物行为执行完毕
- **THEN** 系统 MUST 进入检查阶段
- **AND** 系统 MUST 检查所有怪物是否死亡
- **AND** 系统 MUST 检查玩家是否死亡

### Requirement: 准备阶段必须为每个怪物生成下一回合意图
准备阶段 MUST 为每个在场怪物从 TbMonsterIntent 生成意图。具有 Order 序列的怪物按循环顺序选取，具有 Weight 权重的怪物按权重随机选取。

#### Scenario: Boss 怪物按序列循环生成意图
- **WHEN** 怪物配置了 Order > 0 且 Weight == 0 的意图序列
- **THEN** 系统 MUST 按 Order 从小到大循环选取意图
- **AND** 每次准备阶段 MUST 选取序列中的下一个意图
- **AND** 到达序列末尾后 MUST 回到第一个意图

#### Scenario: 普通怪物按权重随机生成意图
- **WHEN** 怪物配置了 Weight > 0 且 Order == 0 的意图
- **THEN** 系统 MUST 按权重随机选取一个意图
- **AND** 权重越大的意图被选中的概率 MUST 越高

### Requirement: 怪物回合必须执行每个怪物的当前意图
怪物回合 MUST 按在场怪物顺序，对每个怪物执行其当前意图对应的战斗行为。

#### Scenario: 攻击意图对玩家造成伤害
- **WHEN** 怪物当前意图类型为 Attack
- **THEN** 怪物 MUST 对玩家造成意图 Value 值的伤害

#### Scenario: 防御意图为怪物增加护甲
- **WHEN** 怪物当前意图类型为 Defend
- **THEN** 怪物 MUST 获得意图 Value 值的护甲

### Requirement: 检查阶段必须判定战斗胜负和推进
检查阶段 MUST 根据怪物和玩家状态决定是否结束当前批次、推进波次或结束游戏。

#### Scenario: 当前批次怪物全灭
- **WHEN** 当前批次所有怪物血量降为 0
- **THEN** 系统 MUST 检查当前刷怪方案是否还有下一批次
- **AND** 若有，MUST 推进到下一批次并回到准备阶段
- **AND** 若无，MUST 标记战斗波次完成并推进到下一波次

#### Scenario: 玩家死亡
- **WHEN** 玩家血量降为 0
- **THEN** 系统 MUST 结束当前游戏并返回主界面
