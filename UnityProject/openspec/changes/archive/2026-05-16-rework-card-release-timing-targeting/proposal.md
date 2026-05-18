## Why

当前卡牌结算只由 `TargetMode` 和 `EffectKind` 间接表达，已经无法稳定区分近战、投射、法术三类释放语义。目标选择也只按“第一个存活敌人/全部存活敌人”处理，无法支持“优先打有攻击意图的敌人”和“投射目标不足时随机补足其他存活敌人”的规则。

本变更要把“释放类型、目标优先级、效果触发时机”从现有效果执行器中拆出来，形成可配置、可测试、可扩展的卡牌释放调度层。

## What Changes

- 为卡牌配置新增 `CardReleaseKind`，至少包含 `Melee`、`Projectile`、`Spell`。
- 为卡牌配置新增 `TargetCount`，用于表达投射等卡牌的期望命中人数；`TargetCount <= 0` 表示命中所有合法目标。
- 为卡牌效果配置新增触发时机，至少包含 `Immediate`、`EnemyTurnStart`、`EnemyTurnEnd`。
- 近战卡玩家释放后立即结算，自动目标优先选择有攻击意图的存活敌人。
- 投射卡玩家释放后立即结算，优先选择有攻击意图的存活敌人；目标数量不足时从其他存活敌人随机补足。
- 法术卡玩家释放动作立即完成，但每条效果按自身触发时机在立即、敌人回合开始或敌人回合结束结算。
- “有攻击意图”由怪物 `PendingCards` 中是否存在 `Damage` 或 `DamageDot` 效果推导。
- 保持 `CardEffectExecutor` 作为唯一效果执行入口，但它只执行已到期、已确定目标的效果，不负责释放策略或目标排序。
- 在怪物行动后、进入 `Check` 前新增敌人回合结束效果结算点。

## Capabilities

### New Capabilities
- `card-release-runtime`: 定义卡牌释放调度、攻击意图目标优先级、投射随机补足、法术延迟结算的运行时行为。

### Modified Capabilities
- `basic-card-config`: `TbCard` 增加 `CardReleaseKind`，`TbCardEffect` 增加触发时机，基础近战/投射/法术卡配置语义更新。
- `card-effect-runtime`: `CardEffectExecutor` 继续作为统一执行入口，但目标解析与触发时机从 Executor 中迁出到释放调度层。
- `game-systems`: `CardSystem` 出牌流程改为调用释放调度层；`BattleSystem` 在敌人回合开始/结束提供法术延迟效果结算点。

## Impact

- 配置与生成代码：Luban 卡牌表、效果表、枚举定义、生成后的 `GameConfig.card` 类型。
- 热更新运行时：`CardSystem`、`BattleSystem`、`CardEffectExecutor`，以及新增的卡牌释放调度/目标解析服务。
- 怪物意图判断：复用 `MonsterRuntime.PendingCards` 与 `TbCardEffect` 解析。
- 测试：新增 EditMode 测试覆盖近战目标优先、投射随机补足、法术立即/敌人回合开始/敌人回合结束结算。
