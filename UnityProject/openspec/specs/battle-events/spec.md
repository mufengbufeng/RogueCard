# battle-events Specification

## Purpose

定义战斗系统中的核心事件类型，包括出牌、回合结束、怪物死亡、战斗结束和关卡完成事件。

## Requirements

### Requirement: 定义 CardPlayedEvent
系统 SHALL 定义 CardPlayedEvent 只读结构体，包含 CardId（int）字段。CardSystem.Play 成功执行后 SHALL 发布此事件。

#### Scenario: 出牌后发布事件
- **WHEN** CardSystem.Play 成功执行一次出牌操作
- **THEN** SHALL 通过事件发布器发布 CardPlayedEvent，包含使用的卡牌标识

### Requirement: 定义 TurnEndedEvent
系统 SHALL 定义 TurnEndedEvent 只读结构体。BattleSystem.EndTurn 执行完毕后 SHALL 发布此事件。

#### Scenario: 回合结束后发布事件
- **WHEN** 玩家结束回合，BattleSystem.EndTurn 执行完毕
- **THEN** SHALL 发布 TurnEndedEvent

### Requirement: 定义 MonsterDeathEvent
系统 SHALL 定义 MonsterDeathEvent 只读结构体，包含 MonsterId（int）字段。当怪物血量降至 0 或以下时 SHALL 发布此事件。

#### Scenario: 怪物死亡发布事件
- **WHEN** 卡牌效果或伤害计算导致某个怪物血量 <= 0
- **THEN** SHALL 发布 MonsterDeathEvent，包含该怪物的标识

### Requirement: 定义 BattleEndedEvent
系统 SHALL 定义 BattleEndedEvent 只读结构体，包含 IsVictory（bool）字段。当战斗批次全部清除或玩家死亡时 SHALL 发布此事件。

#### Scenario: 战斗胜利发布事件
- **WHEN** 当前波次所有批次的怪物全部被消灭
- **THEN** SHALL 发布 BattleEndedEvent，IsVictory = true

#### Scenario: 战斗失败发布事件
- **WHEN** 玩家血量降至 0
- **THEN** SHALL 发布 BattleEndedEvent，IsVictory = false

### Requirement: 定义 LevelCompleteEvent
系统 SHALL 定义 LevelCompleteEvent 只读结构体，包含 LevelId（int）字段。当所有波次完成时 SHALL 发布此事件，并同步发布到全局 EventHub。

#### Scenario: 关卡完成发布事件
- **WHEN** 所有关卡波次全部完成
- **THEN** SHALL 发布 LevelCompleteEvent 到局部和全局事件总线
