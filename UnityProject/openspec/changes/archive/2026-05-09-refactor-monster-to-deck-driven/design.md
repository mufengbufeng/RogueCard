## Context

用户已确认行为对称（怪物有真牌堆+能量+抽牌），但又选了路线 2（前 X 回合按剧本固定）。这两个选择本身有张力：剧本回合不需要抽牌，所以"完全行为对称"在那些回合等于退化为"伪抽牌"。本变更通过 `Order` / `Count` 字段把两种行为合在一张表里，让运行时按回合数自然分流。

## Goals / Non-Goals

**Goals:**

- 怪物使用 Change 1 的 `CardEffectExecutor` + `IBattleActor` 执行行动，不再有独立的 `IntentType` 处理路径
- 史莱姆（基础 HP 30 / 攻 3 / 盾 3）按"剧本 [攻, 盾, 攻] + 兜底权重 70/30"运行
- 怪物的 MaxEnergy / HandLimit 在 `TbMonster` 中独立可配
- 玩家在 Prepare 末尾能看到怪物本回合的 PendingCards（用于 UI 意图展示）
- 单测覆盖剧本路径与兜底路径

**Non-Goals:**

- 不实现复杂 AI（行为树 / 状态机 / 博弈算法）——AI 只做"按 Cost 降序贪心选牌"
- 不实现怪物方的"多目标选择 UI"——怪物面对 1 个玩家时所有 TargetMode 退化为单目标全额
- 不实现怪物的能量牌策略（怪物不应配 OwnerKind=Monster 的 EnergyGain 卡，由设计师自律）
- 不实现 Boss 战的多阶段 / 转阶段切换牌组（远期 Boss 变更）
- 不实现"怪物之间相互治疗 / Buff"——MVP 怪物只对玩家施法或自身加盾

## Decisions

### 1. 路线 2：剧本前置 + AI 兜底

`TbMonsterDeck` 表同时承载两类行：

```
Order > 0  + Count = 1   → 第 N 回合的剧本卡（不进抽牌堆，按回合直接出）
Order = 0  + Count ≥ 1   → 进抽牌堆，每张卡放 Count 份
```

每回合 Prepare：
1. 计算当前是怪物的第几回合 N（`monster.TurnsAlive + 1`）
2. 若存在 `Order == N` 的行 → `pendingCards = [那张卡]`，跳过抽牌
3. 否则 → `MonsterCardSystem.Draw(monster, monster.HandLimit)` → `MonsterAiBrain.SelectFromHand(monster)` → `pendingCards = AI 选定`

**选择原因：**
- 保留用户原始"前 X 回合固定"的设计意图
- 让"行为对称"的真抽牌流程在剧本结束后才登场，避免 MVP 阶段被 AI 复杂度拖垮
- 一张表搞定两种模式，不引入第二张"剧本表"

**Alternatives considered:**
- 拆 `TbMonsterScript` + `TbMonsterDeck` 两张表：语义最清晰但配置面积翻倍
- 路线 1 纯 AI：需要单独 AI 策略表，且第一回合就要 AI 推理，不易调

### 2. 字段从 Weight 改为 Count

探索阶段一度提的 `Weight` 字段在路线 2 下有歧义（是 AI 选牌权重？还是构造抽牌堆时的份数？）。本变更钉死为 `Count`：抽牌堆中此卡的份数。

**选择原因：**
- 真抽牌已经引入 RNG，再叠 Weight 加权选牌是双重 RNG，调试困难
- "在抽牌堆里放 N 份"是配置师能直接理解的物理意义

### 3. AI 选牌策略：贪心 Cost 降序

`MonsterAiBrain.SelectFromHand`：
1. 从手牌中按 `Cost` 降序排序
2. 依次尝试出牌：能量够则选入 pending、扣临时能量、移到弃牌；能量不够则跳过
3. 直到没有能出的牌为止

**选择原因：**
- 不需要外部 AI 配置表，行为可预测
- 怪物牌组通常是设计师筛选过的（攻击 + 护盾），贪心已能产出合理行动
- Boss 战需要更聪明的 AI 时，再扩 `TbMonsterAi` 表

### 4. 怪物 Hand / DrawPile / DiscardPile 由 MonsterCardSystem 持有

`MonsterCardSystem` 是独立服务，包装了"为某只怪物抽牌、洗牌"的逻辑。`MonsterRuntime` 持有自己的牌堆字段，但操作通过 `MonsterCardSystem` 进行（保持与 `CardSystem`（玩家版）对称）。

**选择原因：** 跟玩家方 CardSystem 对称易理解；如果未来怪物需要"中场打入一张牌"等定制操作，扩展点都集中在 MonsterCardSystem。

### 5. MonsterTurn 结束清空手牌

每只怪物在 `ExecuteTurn` 完成后调用 `MonsterCardSystem.DiscardAllHand(monster)`。

**选择原因：**
- STS 风格的对称（怪物没有跨回合手牌策略）
- 简化 AI（每回合从空手牌开始决策）
- 避免"上回合留了一张大招卡"这种隐性策略空间

### 6. 怪物面对单一玩家时 TargetMode 的退化规则

- `SingleAuto` / `SingleManual` / `All` / `SplitAcrossAll` → 全部退化为对玩家单体全额
- `Self` → 不变，作用于怪物自身（怪物用护盾卡走这条路径）

**选择原因：** MVP 阶段只有玩家一个我方目标，分散无意义。Executor 内 `targets.Count == 1` 时 `SplitAcrossAll` 自然取整为 `value/1`，已经正确，无需特判。

### 7. PendingCards 字段挂在 MonsterRuntime 上

为支持 Change 4 的"意图 UI 渲染"，`MonsterRuntime.PendingCards : IList<Card>` 在 Prepare 阶段填入，MonsterTurn 阶段消费。

**选择原因：** UI 层只需要订阅 Monsters 列表变化即可读到 PendingCards；不需要单独的 ViewModel 字段。

### 8. monster-intent-config 整体废止

旧 capability 中的所有 4 条 Requirements 在本变更里全部 REMOVED，对应的 `TbMonsterIntent` / `MonsterIntentType` 枚举 / `monster_intent.xlsx` 同步删除。

**选择原因：** 旧模型与新模型语义上没有可保留的交集；保留旧 capability 会让后续 Reader 困惑哪个是真。

## Risks / Trade-offs

- [风险] 删除 `TbMonsterIntent` 时 Luban 缓存或生成代码遗留 → [缓解] 任务里强制要求生成完毕后扫描 `Assets/GameScripts/HotFix/GameProto/` 确认 `MonsterIntent` 类型已消失
- [风险] 史莱姆只有 1 张手牌 + 剧本固定 3 回合，行为对称的"复杂度"几乎全部体现在代码里而看不到玩法收益 → [缓解] 在 design 中接受这一点，把价值兑现留给后续 Boss 变更
- [风险] 怪物的 `OwnerKind=Monster` 卡牌如果被错误加入玩家牌堆 → [缓解] `CardSystem.InitDeck` 已经在 Change 1 中筛选 `OwnerKind in {Player, Both}`，本变更只验证不重做
- [风险] AI 贪心策略导致怪物每回合都打 Cost 最高的盾，不打玩家 → [缓解] 怪物牌堆里护盾卡数量是设计师可控的（Count 字段），调参就行；不在 MVP 引入额外 AI 配置
- [风险] "前 X 回合按剧本"的 X 不是显式字段而是隐式由 max(Order) 决定 → [缓解] design 钉死规则：`X = max(Order > 0)`，超过 X 的回合一律走兜底

## Open Questions

- 怪物的 `MaxEnergy` 在 MVP 是否真的需要？史莱姆只用 1 能量、1 张卡，AI 不会"省能量"——是否应在第一版固定 `MaxEnergy = 1` 简化？建议**保留字段为可配**，但 MVP 数据全部填 1
- 兜底抽牌堆耗尽时怎么办？是否要洗弃牌堆回来？建议**是**：复用玩家的 Fisher-Yates 洗牌，避免怪物"无牌可打"的尴尬
- 剧本回合是否仍消耗能量？建议**否**：剧本卡是设计师强制行为，不走能量校验。这样 MVP 时段"史莱姆 1 能量打剧本攻击 3 + 同回合护盾 3"也能跑（虽然不打算这么配）
