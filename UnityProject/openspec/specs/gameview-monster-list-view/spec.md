# gameview-monster-list-view Specification

## Purpose
TBD - created by archiving change gameview-extract-status-and-monsters. Update Purpose after archive.
## Requirements
### Requirement: MonsterItemView 必须封装单只怪物视图渲染

`MonsterItemView` SHALL 封装单只怪物的 UI 渲染：名称、HP 进度条、HP 文本（含护甲附加显示）、PendingCard 意图渲染、buff bar。SHALL 接收 `MonsterRuntime` 与 `aliveMonsterCount` 作为构造参数。

#### Scenario: 渲染怪物名称

- **WHEN** 用 `MonsterRuntime { Config = { Name = "史莱姆" } }` 构造
- **THEN** `name-text` Label 的 `text` SHALL 为 `"史莱姆"`

#### Scenario: HP 进度条按百分比

- **WHEN** 怪物 `Hp = 5`，`MaxHp = 20`
- **THEN** `hp-bar` 元素的 `style.width` SHALL 为 `25%`

#### Scenario: HP 文本含护甲附加

- **WHEN** 怪物 `Hp = 5`，`MaxHp = 20`，`Armor = 3`
- **THEN** `hp-text` Label 的 `text` SHALL 为 `"HP:5/20 护甲:3"`

#### Scenario: HP 文本无护甲简洁

- **WHEN** 怪物 `Hp = 5`，`MaxHp = 20`，`Armor = 0`
- **THEN** `hp-text` Label 的 `text` SHALL 为 `"HP:5/20"`

### Requirement: MonsterItemView 必须按 PendingCards 渲染意图

`MonsterItemView` SHALL 清空 `intent-container`，并为 `MonsterRuntime.PendingCards` 中每张卡创建一个 `.intent-card` 容器，包含按 `TbCardEffect` 表中该卡所有 effect 行生成的 `.intent-icon` 标签。每条 effect 标签的 CSS 类与文本规则：

- `EffectKind.Damage` → 类 `intent-icon-damage`，文本为 `displayValue`（若 `TargetMode=SplitAcrossAll` 且 `aliveCount>0`，`displayValue = max(1, Value / aliveCount)`，否则 `= Value`）
- `EffectKind.Shield` → 类 `intent-icon-shield`，文本为 `displayValue`
- `EffectKind.DamageDot` → 类 `intent-icon-dot`，文本为 `"{displayValue}×{Duration}"`
- `EffectKind.EnergyGain` → 类 `intent-icon-energy`，文本为 `"+{displayValue}"`
- 其他 → 文本为 `displayValue`，无附加类

#### Scenario: Damage 意图渲染

- **WHEN** 怪物 PendingCards 中包含一张卡，effect 为 `{ Kind=Damage, Value=8 }`，`TargetMode=Single`
- **THEN** `intent-container` SHALL 包含一个 `.intent-card`
- **AND** 其中 SHALL 包含一个 Label，`text="8"`，应用 `intent-icon` 与 `intent-icon-damage` 类

#### Scenario: SplitAcrossAll 平分

- **WHEN** PendingCard 的 effect 为 `{ Kind=Damage, Value=12 }` 且 `TargetMode=SplitAcrossAll`，`aliveMonsterCount=4`
- **THEN** 对应 Label 的 `text` SHALL 为 `"3"`

#### Scenario: SplitAcrossAll 至少 1 点

- **WHEN** effect 为 `{ Kind=Damage, Value=2 }` 且 `aliveMonsterCount=10`
- **THEN** 对应 Label 的 `text` SHALL 为 `"1"`（`max(1, 2/10)`）

#### Scenario: DoT 意图渲染

- **WHEN** effect 为 `{ Kind=DamageDot, Value=3, Duration=4 }`
- **THEN** Label `text` SHALL 为 `"3×4"`，应用 `intent-icon-dot` 类

#### Scenario: 兼容旧 intent-text 标签

- **WHEN** UXML 模板中存在历史 `intent-text` Label
- **THEN** `MonsterItemView` SHALL 将该 Label 的 `text` 设置为空字符串以避免遗留文本污染

### Requirement: MonsterItemView 必须渲染怪物 Buff 状态条

`MonsterItemView` SHALL 清空 `buff-bar` 容器并按 `MonsterRuntime.Buffs` 列表顺序为每条非空 `BuffRuntime` 添加一个 Label，应用 `buff-icon` 类；`EffectKind.DamageDot` 类型 buff 额外应用 `buff-icon-dot` 类；Label 文本 SHALL 为 `"{Value}×{RemainingTurns}"`。

#### Scenario: 空 buff 列表清空容器

- **WHEN** 怪物 `Buffs` 为空
- **THEN** `buff-bar` 子元素数 SHALL 为 0

#### Scenario: DoT buff 渲染

- **WHEN** 怪物 `Buffs` 包含 `{ Kind=DamageDot, Value=2, RemainingTurns=3 }`
- **THEN** `buff-bar` SHALL 包含一个 Label，`text="2×3"`，应用 `buff-icon` 与 `buff-icon-dot` 类
