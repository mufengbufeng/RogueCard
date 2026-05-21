# gameview-monster-list-view Specification

## Purpose
TBD - created by archiving change gameview-extract-status-and-monsters. Update Purpose after archive.
## Requirements
### Requirement: MonsterItemView 必须封装单只怪物视图渲染

`MonsterItemView` SHALL 封装单只怪物的 UGUI 渲染：名称、HP 进度条、HP 文本（含护甲附加显示）、PendingCard 意图渲染、buff bar。SHALL 接收实例化出的怪物项根对象、`MonsterRuntime` 与 `aliveMonsterCount` 作为构造或初始化参数。

#### Scenario: 渲染怪物名称

- **WHEN** 用 `MonsterRuntime { Config = { Name = "史莱姆" } }` 初始化
- **THEN** 怪物名称 `TextMeshProUGUI.text` SHALL 为 `"史莱姆"`

#### Scenario: HP 进度条按百分比

- **WHEN** 怪物 `Hp = 5`，`MaxHp = 20`
- **THEN** 怪物 HP 进度条可见比例 SHALL 为 `0.25`

#### Scenario: HP 文本含护甲附加

- **WHEN** 怪物 `Hp = 5`，`MaxHp = 20`，`Armor = 3`
- **THEN** HP 文本 SHALL 为 `"HP:5/20 护甲:3"`

#### Scenario: HP 文本无护甲简洁

- **WHEN** 怪物 `Hp = 5`，`MaxHp = 20`，`Armor = 0`
- **THEN** HP 文本 SHALL 为 `"HP:5/20"`

### Requirement: MonsterItemView 必须按 PendingCards 渲染意图

`MonsterItemView` SHALL 清空意图容器，并为 `MonsterRuntime.PendingCards` 中每张卡创建一个意图卡容器，包含按 `TbCardEffect` 表中该卡所有 effect 行生成的意图图标。每条 effect 图标的视觉与文本规则：

- `EffectKind.Damage` → 伤害视觉，文本为 `displayValue`（若 `TargetMode=SplitAcrossAll` 且 `aliveCount>0`，`displayValue = max(1, Value / aliveCount)`，否则 `= Value`）
- `EffectKind.Shield` → 护盾视觉，文本为 `displayValue`
- `EffectKind.DamageDot` → DoT 视觉，文本为 `"{displayValue}×{Duration}"`
- `EffectKind.EnergyGain` → 能量视觉，文本为 `"+{displayValue}"`
- 其他 → 文本为 `displayValue`

#### Scenario: Damage 意图渲染

- **WHEN** 怪物 PendingCards 中包含一张卡，effect 为 `{ Kind=Damage, Value=8 }`，`TargetMode=Single`
- **THEN** 意图容器 SHALL 包含一个意图卡
- **AND** 其中 SHALL 包含一个图标，文本为 `"8"`，使用伤害视觉

#### Scenario: SplitAcrossAll 平分

- **WHEN** PendingCard 的 effect 为 `{ Kind=Damage, Value=12 }` 且 `TargetMode=SplitAcrossAll`，`aliveMonsterCount=4`
- **THEN** 对应图标文本 SHALL 为 `"3"`

#### Scenario: SplitAcrossAll 至少 1 点

- **WHEN** effect 为 `{ Kind=Damage, Value=2 }` 且 `aliveMonsterCount=10`
- **THEN** 对应图标文本 SHALL 为 `"1"`（`max(1, 2/10)`）

#### Scenario: DoT 意图渲染

- **WHEN** effect 为 `{ Kind=DamageDot, Value=3, Duration=4 }`
- **THEN** 图标文本 SHALL 为 `"3×4"`，使用 DoT 视觉

### Requirement: MonsterItemView 必须渲染怪物 Buff 状态条

`MonsterItemView` SHALL 清空 Buff 容器并按 `MonsterRuntime.Buffs` 列表顺序为每条非空 `BuffRuntime` 添加一个 Buff 图标实例；`EffectKind.DamageDot` 类型 buff SHALL 使用 DoT 视觉样式；图标文本 SHALL 为 `"{Value}×{RemainingTurns}"`。

#### Scenario: 空 buff 列表清空容器

- **WHEN** 怪物 `Buffs` 为空
- **THEN** Buff 容器子元素数 SHALL 为 0

#### Scenario: DoT buff 渲染

- **WHEN** 怪物 `Buffs` 包含 `{ Kind=DamageDot, Value=2, RemainingTurns=3 }`
- **THEN** Buff 容器 SHALL 包含一个图标
- **AND** 图标文本 SHALL 为 `"2×3"`
- **AND** 图标 SHALL 使用 DoT 视觉样式

### Requirement: MonsterListView 必须用 UGUI 模板刷新存活怪物列表

`MonsterListView` SHALL 接收怪物容器、怪物项模板和 `IMonsterListContext`。当 `Monsters` 变化时，MonsterListView SHALL 清理旧运行时怪物项，并按 Monsters 列表顺序为每只未死亡怪物实例化 UGUI 怪物项模板。

#### Scenario: 只渲染存活怪物

- **WHEN** `Monsters.Value` 包含 3 只怪物，其中第 2 只 `IsDead == true`
- **THEN** 怪物容器 SHALL 只包含 2 个运行时怪物项
- **AND** 每个运行时怪物项 SHALL 保留原始 Monsters 索引用于目标选择

#### Scenario: Dispose 后不再刷新

- **WHEN** `MonsterListView.Dispose()` 已调用
- **AND** 之后 `Monsters.Value` 变化
- **THEN** MonsterListView SHALL NOT 创建或销毁任何 UGUI 项
