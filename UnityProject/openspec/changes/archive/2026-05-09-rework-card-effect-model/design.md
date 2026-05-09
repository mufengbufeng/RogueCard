## Context

战斗系统的根基是"卡牌效果"。当前的 `EffectType` 字符串 + 单 `Value` 模型在 MVP 阶段已经撑不住 5 类卡的语义差异（投射要分散、法术要 DoT、能量要修改资源池），更不可能复用给怪物。

本变更专注于"重塑卡牌效果模型 + 提供运行时执行器"，是后续怪物对称化（Change 2）、角色等级数据（Change 3）、UI 反馈（Change 4）的共同前置。

## Goals / Non-Goals

**Goals:**

- 让一张卡可以表达"造成 6 伤害 + 同时给自身 3 护盾"等组合效果，全部用配置而非代码
- 让玩家和怪物使用同一套卡牌定义和同一个执行器
- DoT、AoE、分散伤害等机制通过数据配置而非新代码分支
- 为后续 Buff / Debuff 扩展预留枚举和数据结构空位
- MVP 5 张基础卡全部用新模型重新配置，覆盖 5 类玩法

**Non-Goals:**

- 不实现怪物方的牌组、手牌、抽牌、AI（留给 Change 2）
- 不实现玩家等级 → HP 的计算（留给 Change 3）
- 不实现拖拽出牌的多目标选择 UI 或失败提示（留给 Change 4）
- 不实现遗物 / 圣物 / 状态机的扩展事件（远期）
- 不引入"卡牌升级"或"圣物修改卡牌"机制

## Decisions

### 1. 双表模型（TbCard + TbCardEffect）

采用 B 方案：一张卡是一行 `TbCard`，挂 1~N 条 `TbCardEffect`。每条效果独立配置 `Kind` + `Value` + `Duration`。

**选择原因：**
- 组合效果（攻+盾、伤害+回能）不需要扩 schema
- DoT、AoE 等"修饰性"效果与基础伤害解耦
- 怪物的"攻击 3"等行动天然就是一条 effect，可以原样复用
- 后续加 Buff / Debuff 类型只是新 `Kind` 枚举值，不动表结构

**Alternatives considered:**

- A 方案（在 TbCard 上加 Type/TargetMode/DamageBase/DurationTurns 等字段）：改动小，但部分字段对部分类型语义为空（Shield 卡的 DamageBase 没意义），后续加 Buff 字段会越加越多

### 2. EffectKind 枚举（不用 string）

`TbCardEffect.Kind` 使用 Luban 枚举 `EffectKind`：

```
Damage         // 即时伤害
Shield         // 自身或他者加盾
DamageDot      // 持续伤害（按 Duration 在每回合末扣血）
EnergyGain     // Caster 能量 +Value（仅当前回合有效）
```

**选择原因：** 字符串型容易拼错且无 IDE 提示；枚举给运行时 switch 一个完备性提示。

### 3. TargetMode 枚举

`TbCard.TargetMode` 决定 `CardEffectExecutor` 收到的目标列表来源：

```
SingleAuto      // 自动取第一个存活敌方（近战默认）
SingleManual    // 玩家拖到具体目标上（法术单体）
All             // 全部存活敌方（法术多体）
SplitAcrossAll  // 全部存活敌方，但 Damage Value 平均分配（投射）
Self            // Caster 自身（护盾、能量牌）
```

**选择原因：** 把"目标计算"集中到一个枚举驱动的策略层，Executor 不关心 UI 怎么选目标。

### 4. OwnerKind 字段做卡库归属

`TbCard.OwnerKind ∈ { Player, Monster, Both }` 区分卡的归属。

**选择原因：**
- 玩家初始牌组只筛 `Player | Both` 的 `IsBasic == true`
- 怪物牌组只能引用 `Monster | Both` 的卡
- 后续奖励池过滤、卡库展示也用得上

**Alternatives considered:**
- 拆 `TbPlayerCard` / `TbMonsterCard` 两表：完全隔离但要再定义共享 DTO，多一层抽象，得不偿失

### 5. IBattleActor 统一抽象

`IBattleActor` 提供 `Hp / MaxHp / Armor / Energy / MaxEnergy / Buffs / IsDead / TakeDamage(amount) / AddArmor(amount) / GainEnergy(amount) / AddBuff(buff)` 等只读 + 修改方法。

`PlayerActor` 包装 `GameModel` 的玩家字段实现这个接口；`MonsterRuntime` 直接实现。

**选择原因：** Executor 只认抽象，玩家和怪物无差别。这是行为对称设计的根基。

### 6. CardEffectExecutor 是纯函数式服务

签名：

```csharp
public static class CardEffectExecutor {
    public static void Execute(
        Card cardConfig,
        IBattleActor caster,
        IList<IBattleActor> targets,
        IBattleEventSink events
    );
}
```

**选择原因：**
- 无内部状态 → 容易单测
- 玩家出牌、怪物出牌、Buff Tick 都通过同一个入口
- `IBattleEventSink` 把事件发布解耦（CardSystem 注入即可）

### 7. DoT / Buff 用列表表示

`IBattleActor.Buffs : IList<BuffRuntime>`，`BuffRuntime` 字段 `{ Kind, Value, RemainingTurns, SourceActor }`。

DoT 在 `MonsterTurn` 开始时由 `BattleSystem` 统一 tick：遍历所有 Actor 的 Buffs，对每条 `Kind == DamageDot` 的 buff 调用 `target.TakeDamage(buff.Value)`，然后 `RemainingTurns--`，归零的删除。

**选择原因：**
- DoT、Buff、Debuff 后续都能复用同一容器
- Tick 时机集中在一处便于测试

**Alternatives considered:**
- 在 Actor 上加单独的 `DotDamage / DotTurns` 字段：MVP 能跑但 Buff 体系一来就要重做

### 8. SplitAcrossAll 的取整规则

投射卡的总 Damage Value 平均分给 N 个存活敌方时使用 `Math.Max(1, totalValue / N)`，不分配余数。即 6 伤打 4 个敌方时每个 1 伤（而不是 1.5）。

**选择原因：** 简单可预测；MVP 阶段不在意精度。后续要更精细可在 design 中补 Scenario。

### 9. EnergyGain 仅修改 caster.CurrentEnergy

能量牌（Cost=0, EnergyGain=2）打出时立即把 `caster.CurrentEnergy += 2`。回合结束时 `Energy → MaxEnergy` 重置（已有逻辑），所以"+2"只在当前回合内生效。

**选择原因：** 用户已确认"能量就是出牌次数"，同一池语义。

## Risks / Trade-offs

- [风险] 配置表新增一张 → Luban 生成出错或字段对不上 → [缓解] 任务列表里强制要求生成一次并跑 EditMode 测试
- [风险] `IBattleActor` 抽象引入后玩家方原 `GameModel.ModifyPlayerHp` 等方法面临"双入口"（Actor 接口 vs 直接调） → [缓解] PlayerActor 实现内只调 GameModel 现有方法，不绕过 PropertyChanged，保证 ViewModel 不会丢通知
- [风险] DoT 的 tick 时机选错（开始 vs 结束）影响策略平衡 → [缓解] design 里钉死"MonsterTurn 开始 tick"，并写入 spec 的 Scenario
- [风险] 5 张基础卡的具体数值（投射 6 总伤、法术 8+2x、护盾 5）可能不平衡 → [缓解] 数值放在配置表，本变更只关注模型正确性，平衡留给后续

## Open Questions

- 法术 DoT 的"持续 X 回合"是否包含本回合？建议**不包含**：`Duration=3` 表示本回合即时打 8 伤，之后接下来 3 个回合的 MonsterTurn 起点各扣 2 伤
- 怪物的"投射卡"目标只有玩家一个，SplitAcrossAll 此时是否退化为单目标全额？建议**是**：N=1 时 totalValue / 1 = totalValue
- `OwnerKind=Both` 这个枚举值在 MVP 是否真的有用？建议**保留为预留位**，5 张基础卡里全部用 `Player`，怪物用 `Monster`
