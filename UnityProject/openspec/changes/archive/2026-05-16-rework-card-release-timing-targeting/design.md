## Context

当前战斗代码已经完成了“玩家和怪物共用 `CardEffectExecutor`”的基础：玩家出牌由 `CardSystem.Play` 进入，怪物行动由 `MonsterSystem.ExecuteTurn` 进入，最终都调用 `CardEffectExecutor.Execute`。怪物在 Prepare 阶段生成 `PendingCards`，UI 也已经按 `PendingCards` 渲染怪物意图。

问题在于，现有 `TargetMode` 同时承担了“目标形状”和“卡牌类型”的隐含语义：`SingleAuto` 被当作近战，`SplitAcrossAll` 被当作投射，`SingleManual`/`All` 被当作法术。这会让释放时机、目标优先级、投射补足、法术延迟结算都挤在一个枚举里，后续很难扩展。

## Goals / Non-Goals

**Goals:**

- 用 `CardReleaseKind` 明确区分近战、投射、法术释放策略。
- 用 `TargetCount` 表达投射类卡牌期望命中人数，并定义目标不足时的补足规则。
- 用效果触发时机表达 `Immediate`、`EnemyTurnStart`、`EnemyTurnEnd` 三类结算点。
- 保持 `CardEffectExecutor` 的职责简单：只执行已到期、已选好目标的效果。
- 复用怪物 `PendingCards` 推导攻击意图，避免在运行时状态里复制一份“意图类型”。

**Non-Goals:**

- 不实现完整 Buff/Debuff 系统，只把现有 `DamageDot` 纳入触发时机模型。
- 不修改 UI 拖拽、手动选目标、怪物意图展示的交互形态。
- 不引入复杂权重选目标、仇恨值、站位距离或 3x3 阵型规则。
- 不改变怪物牌组、抽牌、剧本优先和贪心出牌规则。

## Decisions

### 1. 新增 CardReleaseKind，而不是扩展 TargetMode

`CardReleaseKind` 表达释放策略：

```text
Melee       立即结算，自动目标优先攻击有攻击意图的敌人
Projectile 立即结算，优先攻击有攻击意图的敌人，不足时随机补足其他存活敌人
Spell       释放立即完成，每条效果按 TriggerTiming 决定实际结算点
```

`TargetMode` 继续表达目标形状，例如单体、全体、自身、平分。这样可以避免 `TargetMode` 同时表达“怎么选”和“何时结算”。

替代方案是新增 `TargetMode` 值，例如 `SingleIntentPriority`、`ProjectileIntentPriority`。该方案初期改动较小，但会让目标枚举继续膨胀，而且法术延迟结算仍无处表达。

### 2. 新增 TargetCount，投射目标不足时从其他存活敌人补足

投射卡需要一个可测试的目标数量。`TargetCount > 0` 表示期望命中数量；`TargetCount <= 0` 表示命中所有合法目标，兼容现有 `All` / `SplitAcrossAll` 的全体语义。

投射目标选择顺序：

1. 收集存活敌人。
2. 在存活敌人中筛选有攻击意图的敌人。
3. 先选攻击意图敌人，最多 `TargetCount` 个。
4. 若数量不足，从其他存活敌人中随机补足。

随机补足只发生在“其他存活敌人”范围内，这是用户已确认的规则。

### 3. 攻击意图由 PendingCards 的效果推导

怪物是否有攻击意图由其 `PendingCards` 中是否存在 `EffectKind.Damage` 或 `EffectKind.DamageDot` 效果推导。该判断应走同一套 `TbCardEffect` 解析逻辑，而不是新增 `MonsterRuntime.IntentKind` 字段。

选择原因：

- `PendingCards` 是 Prepare 阶段已经生成的真实行动来源。
- UI 当前已经通过 `PendingCards` 展示意图，运行时目标选择复用同一事实来源。
- 后续如果一张牌同时有伤害和护盾，也天然算攻击意图。

### 4. 引入效果触发时机，法术释放与结算解耦

`TbCardEffect` 增加触发时机字段，建议枚举名为 `EffectTriggerTiming`：

```text
Immediate       释放时立即结算
EnemyTurnStart  敌人回合开始结算
EnemyTurnEnd    敌人回合结束结算
```

法术卡出牌时立即完成扣能量、移除手牌、进弃牌堆、发布出牌事件。`Immediate` 效果当场执行；非立即效果登记为延迟效果，由 `BattleSystem` 在对应结算点执行。

现有 `DamageDot` 可以作为 `EnemyTurnStart` 的首个落地用例。为了兼容已有 Buff UI 与 `BuffRuntime`，实现可以先把延迟伤害继续存入 `BuffRuntime`，但设计上应以触发时机为语义来源。

### 5. 新增释放调度服务，CardEffectExecutor 保持纯执行器

建议新增运行时服务，例如 `CardReleaseResolver` 或 `CardReleaseService`，职责包括：

- 读取 `CardReleaseKind`、`TargetMode`、`TargetCount`。
- 根据怪物攻击意图和存活状态解析目标列表。
- 按效果触发时机拆分立即效果和延迟效果。
- 调用 `CardEffectExecutor.Execute` 执行已到期效果。

`CardEffectExecutor` 不再负责最终目标解析细节，也不负责决定效果何时触发。这样玩家出牌和怪物行动仍可以共享执行器，但释放调度规则不会污染底层效果处理。

### 6. BattleSystem 增加敌人回合结束结算点

当前 `MonsterTurn` 顺序是：

```text
TickBuffs()
MonsterSystem.ExecuteTurn()
Publish TurnEndedEvent
SetPhase(Check)
```

变更后应形成：

```text
Resolve EnemyTurnStart effects
MonsterSystem.ExecuteTurn()
Resolve EnemyTurnEnd effects
Publish TurnEndedEvent
SetPhase(Check)
```

如果敌人回合开始效果击杀玩家，保持现有行为：立即失败并跳过怪物行动。如果敌人回合结束效果击杀玩家或怪物，随后由 Check 阶段统一处理胜负与批次推进。

## Risks / Trade-offs

- [风险] 配置表新增字段后 Luban 生成类变化，测试反射构造 `Card` / `CardEffect` 需要同步更新 → [缓解] 在任务中先更新配置/生成代码和测试构造工具，再实现运行时。
- [风险] 随机补足目标导致测试不稳定 → [缓解] 释放调度服务应允许注入或包装随机源，EditMode 测试使用确定性随机。
- [风险] `CardEffectExecutor` 现有测试假设它负责 `TargetMode` 解析 → [缓解] 先用测试刻画新边界，再把目标解析测试迁移到释放调度服务。
- [风险] `DamageDot` 既是 EffectKind 又被触发时机描述，语义可能重复 → [缓解] 本变更只把触发时机作为调度字段，`DamageDot` 仍表示持续伤害效果类型。
- [风险] 敌人回合结束结算点可能影响胜负判定顺序 → [缓解] specs 明确 EnemyTurnEnd 在怪物行动之后、Check 之前执行。
