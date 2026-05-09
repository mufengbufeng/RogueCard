## Context

玩家初始 HP 当前是硬编码（`GameModel.DefaultPlayerHp = 50` 之类），而设计要求从等级表读出。本变更范围最小：补一个字段、填 5 行数据、把读表路径打通。不涉及"获得经验、自动升级"等动态逻辑——那些在后续奖励变更中处理。

## Goals / Non-Goals

**Goals:**

- `TbPlayerLevel` 提供完整的 1~5 级 MVP 数据，每行独立指定 `BaseHp / BaseEnergy / HandLimit / ExpToLevelUp`
- `BattleSystem.InitPlayerAttributes` 完全由表驱动，无 fallback 常量
- 进入战斗时玩家 HP 与 MaxHp 反映其当前等级
- 1 级玩家 HP = 100，5 级玩家 HP = 140

**Non-Goals:**

- 不实现"杀怪获得经验"逻辑（后续变更）
- 不实现"等级提升后修改在场玩家 HP"逻辑（后续变更）
- 不引入多角色或角色选择系统
- 不修改 ViewModel 已有的 `PlayerHp / PlayerMaxHp` 属性，只确保它们被正确初始化

## Decisions

### 1. 每级写满数据（不引入增量公式）

`TbPlayerLevel` 第 1~5 级每行直接填 `BaseHp = 100, 110, 120, 130, 140`，不引入 `HpPerLevel` 这类增量字段。

**选择原因:**
- 设计师可在表里直接调每级 HP（例如把 3 级压低为 115），不需要改公式
- 列数变少，schema 简洁
- MVP 只考虑 5 级，列出来不多

**Alternatives considered:**

- 单独一行 `TbPlayerBase + HpPerLevel` 公式：方便扩展到 100 级，但 MVP 用不到，且未来加角色时反而要再改

### 2. InitBattleAttributes 签名扩展为接收 maxHp

```csharp
// before
_model.InitBattleAttributes(maxEnergy, handLimit, GameModel.DefaultPlayerHp);

// after
_model.InitBattleAttributes(maxEnergy, handLimit, maxHp);
// 其中 maxHp 来自 TbPlayerLevel.GetOrDefault(level)?.BaseHp
```

**选择原因:** 显式参数比"内部读 GameModel 字段"更易测试。

### 3. 缺等级数据时使用 1 级兜底

`TbPlayerLevel.GetOrDefault(level)` 在传入超出范围的 level 时返回 1 级数据；如果 1 级也读不到则抛 `InvalidOperationException`（不再降级到常量）。

**选择原因:** "找不到 1 级"是配置错误，应该立即暴露而不是悄悄用 50 HP 跑战斗。

### 4. 不修改 PlayerActor / IBattleActor 接口

Change 1 引入的 `PlayerActor` 包装 `GameModel`，本变更只确保 `InitBattleAttributes` 把 `PlayerHp / PlayerMaxHp` 设到正确的值。`PlayerActor` 通过现有 `GameModel.PlayerHp` 等属性访问数据，无需任何改动。

**选择原因:** 边界清晰；Change 1 的接口稳定。

## Risks / Trade-offs

- [风险] 删除 `DefaultPlayerHp` 后某个隐藏调用路径会立即报错 → [缓解] 任务中 grep 全代码库确认没有遗漏；编译期会暴露
- [风险] Luban 生成代码后 `PlayerLevel.BaseHp` 字段名拼写大小写不一致 → [缓解] 任务中明确字段命名 `BaseHp`，与 `BaseEnergy` 保持同一驼峰风格
- [风险] 测试用例硬编码 `50 HP` → [缓解] 任务中扫一遍 `Tests/EditMode` 中的 50 / 100 字面量
- [风险] 与 Change 1（CardEffectExecutor）/ Change 2（MonsterSystem）改 BattleSystem 文件冲突 → [缓解] 任务里建议串行而非并行；冲突时优先以本变更为准（只改 InitPlayerAttributes 一段）

## Open Questions

- "经验"字段在 MVP 战斗中暂时不会被消耗——是否仍需要在等级表里填？建议**填**，1 级填 0、2~5 级填阶梯值，避免后续奖励变更再改 schema
- 是否需要在 `GameModel` 上加 `CurrentLevel` 字段？建议**是**，本变更顺手加上（默认值 1），由 `BattleSystem.InitPlayerAttributes` 读取
