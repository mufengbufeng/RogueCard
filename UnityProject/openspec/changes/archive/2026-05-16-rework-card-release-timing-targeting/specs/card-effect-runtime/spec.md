## MODIFIED Requirements

### Requirement: CardEffectExecutor 必须以纯函数式服务执行卡牌效果

系统 MUST 提供静态方法 `CardEffectExecutor.Execute(Card cardConfig, IBattleActor caster, IList<IBattleActor> targets, IBattleEventSink events)`，作为玩家出牌、怪物行动和释放调度层结算到期效果的统一执行入口。Executor MUST NOT 持有任何全局状态。目标优先级、随机补足和效果触发时机 MUST 由卡牌释放调度层在调用 Executor 之前处理。

#### Scenario: 单一执行入口
- **WHEN** CardSystem 处理玩家出牌、MonsterSystem 处理怪物行动，或卡牌释放调度层结算到期法术效果
- **THEN** 各方 MUST 调用同一个 `CardEffectExecutor.Execute` 方法执行具体效果
- **AND** Executor MUST NOT 区分调用方是玩家还是怪物（所有差异由 IBattleActor 多态吸收）

#### Scenario: Executor 接受已解析目标列表
- **WHEN** `CardEffectExecutor.Execute` 被调用
- **THEN** 调用方 MUST 已经根据 `CardReleaseKind`、`TargetMode`、`TargetCount` 和触发时机准备好候选目标列表
- **AND** Executor MUST NOT 实现近战攻击意图优先、投射随机补足或法术延迟调度规则

#### Scenario: 目标列表由 TargetMode 计算
- **WHEN** `CardEffectExecutor.Execute` 被调用
- **THEN** Executor MUST 根据 `cardConfig.TargetMode` 决定最终目标列表
- **AND** `SingleAuto` MUST 取调用方传入候选列表中的第一个 `IsDead == false` 的目标
- **AND** `SingleManual` MUST 直接使用调用方传入的 `targets[0]`
- **AND** `All` MUST 取所有 `IsDead == false` 的目标
- **AND** `SplitAcrossAll` MUST 取所有 `IsDead == false` 的目标，且对每条 `Kind == Damage` 的 Effect 把 Value 替换为 `Math.Max(1, value / targets.Count)`
- **AND** `Self` MUST 把目标设为 `caster`

### Requirement: CardEffectExecutor 必须分发 4 种 EffectKind 的处理

Executor MUST 遍历调用方传入或按 `cardConfig` 解析出的效果行，按 `Kind` 分发到对应处理逻辑。若效果行包含 `TriggerTiming`，调用方 MUST 只传入当前结算点允许执行的效果。

#### Scenario: Damage 效果处理
- **WHEN** Effect 的 `Kind == Damage`
- **THEN** Executor MUST 对每个目标调用 `target.TakeDamage(value)`
- **AND** `TakeDamage` 内部 MUST 先扣 Armor 再扣 Hp
- **AND** 如果 `target.Hp <= 0` MUST 通过 `events` 发布 `MonsterDeathEvent`（仅当 target 是怪物）

#### Scenario: Shield 效果处理
- **WHEN** Effect 的 `Kind == Shield`
- **THEN** Executor MUST 对每个目标调用 `target.AddArmor(value)`

#### Scenario: DamageDot 效果处理
- **WHEN** Effect 的 `Kind == DamageDot`
- **THEN** Executor MUST 对每个目标调用 `target.AddBuff(new BuffRuntime { Kind = DamageDot, Value = value, RemainingTurns = duration, SourceActor = caster })`
- **AND** Executor MUST NOT 在挂入 Buff 的同一次 `DamageDot` 效果处理中直接扣血

#### Scenario: EnergyGain 效果处理
- **WHEN** Effect 的 `Kind == EnergyGain`
- **THEN** Executor MUST 调用 `caster.GainEnergy(value)`
- **AND** `GainEnergy` MUST 直接增加 `caster.CurrentEnergy`，且 MUST 允许 `CurrentEnergy` 临时超过 `MaxEnergy`（设计语义："能量就是出牌次数"，能量牌让玩家本回合多打几张）；下一回合 `Prepare` 阶段会被重置回 `MaxEnergy`
