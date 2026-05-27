## ADDED Requirements

### Requirement: MonsterListView 必须按 InstanceId 维持怪物屏幕位置稳定

`MonsterListView` SHALL 按 `MonsterRuntime.InstanceId` 缓存每只怪物在容器内的 `anchoredPosition`。同一只怪物（即 `InstanceId` 相同）在多次 `Monsters` 变更刷新后 SHALL 保持相同的 `anchoredPosition`，直至该怪物死亡（`IsDead == true`）或不再出现在 `Monsters.Value` 列表中。

`MonsterListView` SHALL NOT 依赖 `_context.Monsters.Value` 的对象引用是否变化来判定是否清空位置缓存；位置缓存的回收 SHALL 仅依据当前快照中存活怪物的 `InstanceId` 集合。

#### Scenario: 出牌后存活怪物位置不变

- **GIVEN** `Monsters.Value` 包含 3 只存活怪物 `A(InstanceId=1)`、`B(InstanceId=2)`、`C(InstanceId=3)`
- **AND** 首次 Refresh 后三只怪物分别位于 `anchoredPosition` `pA`、`pB`、`pC`
- **WHEN** `Monsters.Value` 被赋值为一个新数组（元素仍为同样的 `A`、`B`、`C` 引用，模拟 `SnapshotMonsters` 行为），且任一怪物的 `Hp`/`Armor`/`Buffs` 已变化
- **THEN** Refresh 后 `A`、`B`、`C` 对应的 `anchoredPosition` SHALL 仍分别为 `pA`、`pB`、`pC`

#### Scenario: 死亡怪物的位置被回收，存活怪物位置不变

- **GIVEN** `Monsters.Value` 包含存活的 `A`、`B`、`C`，分别位于 `pA`、`pB`、`pC`
- **WHEN** `B` 被标记为 `IsDead = true`，`Monsters.Value` 重新赋值（新数组，元素引用稳定）
- **THEN** Refresh 后 `B` 不再渲染
- **AND** `A`、`C` 的 `anchoredPosition` SHALL 仍为 `pA`、`pC`
- **AND** 后续若 B 的 `InstanceId` 不再出现在 `Monsters.Value` 中，其位置缓存条目 SHALL 被移除

#### Scenario: 新一波怪物使用新位置

- **GIVEN** 已渲染过 InstanceId 为 `{1, 2, 3}` 的怪物
- **WHEN** 切换到下一波，`Monsters.Value` 包含 InstanceId 为 `{4, 5}` 的全新怪物
- **THEN** `4`、`5` SHALL 各自分配新的随机/兜底位置
- **AND** `1`、`2`、`3` 的位置缓存 SHALL 被清除（不再占用空间冲突判定）
