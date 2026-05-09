## ADDED Requirements

### Requirement: IBattleActor 抽象统一玩家和怪物的战斗接口

系统 MUST 提供 `IBattleActor` 接口，把战斗中"任何能受伤、能加盾、能持有 Buff"的实体行为统一。`PlayerActor`（包装 `GameModel` 玩家字段）和 `MonsterRuntime` MUST 同时实现该接口，使 `CardEffectExecutor` 不区分施法者/目标的具体类型。

#### Scenario: IBattleActor 接口契约
- **WHEN** 检查 `IBattleActor` 接口定义
- **THEN** 该接口 MUST 暴露只读属性 `Hp / MaxHp / Armor / CurrentEnergy / MaxEnergy / IsDead`
- **AND** 该接口 MUST 暴露 `Buffs : IList<BuffRuntime>` 用于持有当前生效的 Buff
- **AND** 该接口 MUST 暴露写方法 `TakeDamage(int amount)` / `AddArmor(int amount)` / `GainEnergy(int amount)` / `AddBuff(BuffRuntime buff)`

#### Scenario: PlayerActor 实现保留 PropertyChanged 通知
- **WHEN** `PlayerActor.TakeDamage` / `AddArmor` / `GainEnergy` 被调用
- **THEN** 实现 MUST 通过 `GameModel.ModifyPlayerHp` / `ModifyPlayerArmor` / `ModifyEnergy` 等已有方法间接修改
- **AND** MUST NOT 直接写底层字段，以保证 `PropertyChanged` 事件正确发布到 ViewModel

#### Scenario: MonsterRuntime 实现 IBattleActor
- **WHEN** 检查 `MonsterRuntime` 类型定义
- **THEN** 该类型 MUST 实现 `IBattleActor`
- **AND** MUST 包含 `CurrentEnergy / MaxEnergy / Buffs` 字段（即使在本变更内尚未被 MonsterSystem 使用）

### Requirement: BuffRuntime 必须能描述 DoT 与一次性 Buff

系统 MUST 提供 `BuffRuntime` 类型，至少包含 `Kind (EffectKind) / Value (int) / RemainingTurns (int) / SourceActor (IBattleActor)` 字段，能表达"持续 N 回合每回合扣 V 点伤害"的 DoT 与未来扩展的 Buff/Debuff。

#### Scenario: BuffRuntime 字段
- **WHEN** 检查 `BuffRuntime` 类型
- **THEN** 该类型 MUST 包含 `Kind`、`Value`、`RemainingTurns`、`SourceActor` 字段
- **AND** `Kind` MUST 使用 `EffectKind` 枚举

### Requirement: CardEffectExecutor 必须以纯函数式服务执行卡牌效果

系统 MUST 提供静态方法 `CardEffectExecutor.Execute(Card cardConfig, IBattleActor caster, IList<IBattleActor> targets, IBattleEventSink events)`，作为玩家出牌和怪物行动的唯一执行入口。Executor MUST NOT 持有任何全局状态。

#### Scenario: 单一执行入口
- **WHEN** CardSystem 处理玩家出牌或 MonsterSystem 处理怪物行动
- **THEN** 双方 MUST 调用同一个 `CardEffectExecutor.Execute` 方法
- **AND** Executor MUST NOT 区分调用方是玩家还是怪物（所有差异由 IBattleActor 多态吸收）

#### Scenario: 目标列表由 TargetMode 计算
- **WHEN** `CardEffectExecutor.Execute` 被调用
- **THEN** Executor MUST 根据 `cardConfig.TargetMode` 决定最终目标列表
- **AND** `SingleAuto` MUST 取第一个 `IsDead == false` 的敌方
- **AND** `SingleManual` MUST 直接使用调用方传入的 `targets[0]`
- **AND** `All` MUST 取所有 `IsDead == false` 的敌方
- **AND** `SplitAcrossAll` MUST 取所有 `IsDead == false` 的敌方，且对每条 `Kind == Damage` 的 Effect 把 Value 替换为 `Math.Max(1, value / targets.Count)`
- **AND** `Self` MUST 把目标设为 `caster`

### Requirement: CardEffectExecutor 必须分发 4 种 EffectKind 的处理

Executor MUST 遍历 `cardConfig` 的所有效果行，按 `Kind` 分发到对应处理逻辑。

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
- **AND** Executor MUST NOT 在打出当回合立即扣血（首次扣血发生在下一次 BattleSystem 的 DoT tick）

#### Scenario: EnergyGain 效果处理
- **WHEN** Effect 的 `Kind == EnergyGain`
- **THEN** Executor MUST 调用 `caster.GainEnergy(value)`
- **AND** `GainEnergy` MUST 直接增加 `caster.CurrentEnergy`，且 MUST 允许 `CurrentEnergy` 临时超过 `MaxEnergy`（设计语义："能量就是出牌次数"，能量牌让玩家本回合多打几张）；下一回合 `Prepare` 阶段会被重置回 `MaxEnergy`

> 说明：BattleSystem 在何时调用 Executor、何时 tick Buffs 等"调度时机"契约不在本 capability 范围内，请见 `game-systems` capability 中的 `BattleSystem 管理战斗阶段和胜负判定` Requirement。
