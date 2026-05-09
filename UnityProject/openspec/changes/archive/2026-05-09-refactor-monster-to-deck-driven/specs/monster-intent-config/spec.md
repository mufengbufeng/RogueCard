## REMOVED Requirements

### Requirement: 怪物意图表必须支持权重随机和序列循环混合模式

**Reason:** `monster-intent-config` capability 整体被 `monster-deck-config` 替换。怪物行动从"独立 Intent 枚举"改为"引用卡牌 + 牌组驱动"，原 `TbMonsterIntent` 表 / `MonsterIntentType` 枚举 / 相关运行时分支全部废止。

**Migration:** 请见 ADDED capability `monster-deck-config` 中的 `TbMonsterDeck` 表与"怪物牌组必须支持剧本前置 + 兜底权重池混合模式" Requirement。

### Requirement: 意图类型枚举必须覆盖第一版战斗行为

**Reason:** `MonsterIntentType` 枚举被废止；怪物行为通过 `EffectKind`（在 `card-effect-runtime` capability 中定义）表达，复用玩家方的 4 类效果（Damage / Shield / DamageDot / EnergyGain）。

**Migration:** 攻击意图 → `OwnerKind=Monster` 的卡 + `EffectKind=Damage` effect；防御意图 → `OwnerKind=Monster` 的卡 + `EffectKind=Shield` effect。

### Requirement: 怪物意图选取必须遵循混合模式优先级规则

**Reason:** "Order 优先 / Weight 兜底"的混合规则迁移到 `monster-deck-config` 的 `TbMonsterDeck` 表，由 `MonsterAiBrain` 在运行时按 `Order > 0` 走剧本、`Order = 0` 走抽牌堆贪心选牌实现。

**Migration:** 请见 ADDED Requirement「怪物 AI 决策必须按剧本优先 / 兜底次之的顺序生成本回合行动」。

### Requirement: 怪物意图表结构必须支持后续扩展 Buff 和 Debuff 类型

**Reason:** 扩展 Buff / Debuff 的能力迁移到 `card-effect-runtime` 的 `EffectKind` 枚举（已在 Change 1 预留扩展位）。`TbMonsterDeck` 通过引用 `TbCard` 自动获得 Buff / Debuff 表达能力，不需要在意图表自己再扩枚举。

**Migration:** 后续添加 Buff 卡时，新增一条 `TbCard` + `TbCardEffect` 行 + 在怪物的 `TbMonsterDeck` 行引用即可，无需修改怪物表结构。
