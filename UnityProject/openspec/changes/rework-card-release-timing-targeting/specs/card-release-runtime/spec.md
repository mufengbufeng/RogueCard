## ADDED Requirements

### Requirement: 卡牌释放运行时必须按 CardReleaseKind 调度释放策略

系统 MUST 提供卡牌释放调度层，按 `CardReleaseKind` 区分近战、投射、法术三类释放策略。该调度层 MUST 在调用 `CardEffectExecutor` 之前完成目标解析和效果触发时机过滤。

#### Scenario: 近战释放立即结算
- **WHEN** 玩家在 `PlayerTurn` 打出 `CardReleaseKind == Melee` 的卡牌
- **THEN** 系统 MUST 在本次 `CardSystem.Play` 调用内立即解析目标
- **AND** 系统 MUST 立即调用 `CardEffectExecutor` 执行该卡 `Immediate` 效果

#### Scenario: 投射释放立即结算
- **WHEN** 玩家在 `PlayerTurn` 打出 `CardReleaseKind == Projectile` 的卡牌
- **THEN** 系统 MUST 在本次 `CardSystem.Play` 调用内立即解析投射目标
- **AND** 系统 MUST 立即调用 `CardEffectExecutor` 执行该卡 `Immediate` 效果

#### Scenario: 法术释放按效果触发时机拆分
- **WHEN** 玩家在 `PlayerTurn` 打出 `CardReleaseKind == Spell` 的卡牌
- **THEN** 系统 MUST 立即完成出牌消耗与出牌事件发布
- **AND** 系统 MUST 立即执行 `Immediate` 效果
- **AND** 系统 MUST 登记 `EnemyTurnStart` 与 `EnemyTurnEnd` 效果，等待对应战斗结算点执行

### Requirement: 攻击意图必须由 PendingCards 效果推导

系统 MUST 根据怪物 `PendingCards` 中引用的卡牌效果判断该怪物是否具有攻击意图。任意 PendingCard 包含 `EffectKind.Damage` 或 `EffectKind.DamageDot` 时，该怪物 MUST 被视为具有攻击意图。

#### Scenario: Damage PendingCard 产生攻击意图
- **WHEN** 一只存活怪物的 `PendingCards` 中存在一张卡，其效果列表包含 `EffectKind.Damage`
- **THEN** 卡牌释放调度层 MUST 将该怪物判定为具有攻击意图

#### Scenario: DamageDot PendingCard 产生攻击意图
- **WHEN** 一只存活怪物的 `PendingCards` 中存在一张卡，其效果列表包含 `EffectKind.DamageDot`
- **THEN** 卡牌释放调度层 MUST 将该怪物判定为具有攻击意图

#### Scenario: 非伤害 PendingCard 不产生攻击意图
- **WHEN** 一只存活怪物的 `PendingCards` 只包含 `Shield` 或 `EnergyGain` 效果
- **THEN** 卡牌释放调度层 MUST NOT 将该怪物判定为具有攻击意图

#### Scenario: 死亡怪物不参与攻击意图目标池
- **WHEN** 一只怪物 `IsDead == true`
- **THEN** 卡牌释放调度层 MUST NOT 把该怪物加入近战或投射的候选目标

### Requirement: 近战目标必须优先攻击有攻击意图的敌人

`CardReleaseKind == Melee` 的自动目标选择 MUST 优先选择具有攻击意图的存活敌人。若不存在攻击意图敌人，则 MUST 回退到普通存活敌人目标池。

#### Scenario: 近战优先命中攻击意图敌人
- **WHEN** 玩家打出近战卡，场上同时存在有攻击意图和无攻击意图的存活敌人
- **THEN** 系统 MUST 选择一个有攻击意图的敌人作为目标
- **AND** 系统 MUST NOT 在仍有攻击意图敌人可选时选择无攻击意图敌人

#### Scenario: 近战无攻击意图时回退存活敌人
- **WHEN** 玩家打出近战卡，场上没有任何有攻击意图的存活敌人，但存在其他存活敌人
- **THEN** 系统 MUST 从其他存活敌人中选择目标

#### Scenario: 近战无存活敌人时不执行效果
- **WHEN** 玩家打出近战卡，场上没有任何存活敌人
- **THEN** 系统 MUST 不调用 `CardEffectExecutor` 执行敌方目标效果
- **AND** 系统 MUST 保持出牌失败或空目标行为与现有 `CardSystem` 校验规则一致

### Requirement: 投射目标必须优先攻击意图并随机补足其他存活敌人

`CardReleaseKind == Projectile` 的目标选择 MUST 使用 `TargetCount` 表达期望命中数量。系统 MUST 先选择具有攻击意图的存活敌人；若数量不足，MUST 从其他存活敌人中随机补足。

#### Scenario: 投射优先选择攻击意图敌人
- **WHEN** 玩家打出投射卡，`TargetCount = 2`，场上至少存在 2 个有攻击意图的存活敌人
- **THEN** 系统 MUST 选择 2 个有攻击意图的敌人作为目标

#### Scenario: 投射人数不足时从其他存活敌人补足
- **WHEN** 玩家打出投射卡，`TargetCount = 3`，场上只有 1 个有攻击意图的存活敌人且另有 2 个无攻击意图的存活敌人
- **THEN** 系统 MUST 选择该攻击意图敌人
- **AND** 系统 MUST 从其他存活敌人中随机补足 2 个目标

#### Scenario: 投射目标数超过存活敌人时命中所有存活敌人
- **WHEN** 玩家打出投射卡，`TargetCount = 5`，场上只有 3 个存活敌人
- **THEN** 系统 MUST 只选择这 3 个存活敌人
- **AND** 系统 MUST NOT 选择死亡敌人或重复选择同一敌人

#### Scenario: 投射 TargetCount 非正数时命中所有合法目标
- **WHEN** 玩家打出投射卡，`TargetCount <= 0`
- **THEN** 系统 MUST 把所有存活敌人作为投射目标
- **AND** 系统 MUST 仍按攻击意图优先的顺序组织目标列表

### Requirement: 法术延迟效果必须在敌人回合开始或结束结算

`CardReleaseKind == Spell` 的非立即效果 MUST 在配置的触发时机结算。`EnemyTurnStart` 效果 MUST 在怪物行动之前结算；`EnemyTurnEnd` 效果 MUST 在怪物行动之后、`Check` 阶段之前结算。

#### Scenario: EnemyTurnStart 法术效果在怪物行动前结算
- **WHEN** 玩家打出法术卡，该卡存在 `TriggerTiming == EnemyTurnStart` 的效果
- **THEN** 系统 MUST 在下一次敌人回合开始、怪物执行 `PendingCards` 之前结算该效果

#### Scenario: EnemyTurnEnd 法术效果在怪物行动后结算
- **WHEN** 玩家打出法术卡，该卡存在 `TriggerTiming == EnemyTurnEnd` 的效果
- **THEN** 系统 MUST 在怪物执行完 `PendingCards` 之后结算该效果
- **AND** 系统 MUST 在结算完成后再进入 `Check` 阶段

#### Scenario: Immediate 法术效果不等待敌人回合
- **WHEN** 玩家打出法术卡，该卡存在 `TriggerTiming == Immediate` 的效果
- **THEN** 系统 MUST 在出牌调用内立即结算该效果
