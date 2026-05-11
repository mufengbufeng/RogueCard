## ADDED Requirements

### Requirement: MonsterListView 必须订阅切片接口

`MonsterListView` SHALL 通过构造函数接收一个实现 `IMonsterListContext` 的对象，SHALL NOT 直接引用 `GameViewModel` 或 `GameModel`，SHALL NOT 访问 `Hand`、`PlayerHp` 等非怪物相关字段。

#### Scenario: 通过切片接口构造

- **WHEN** 调用 `new MonsterListView(monsterContainer, context, monsterItemTemplate)`
- **THEN** 视图 SHALL 成功构造，订阅 `context.Monsters.Changed`，并触发首次刷新

### Requirement: MonsterListView 必须基于 Monsters 列表全量重建怪物项

`MonsterListView` SHALL 在 `IMonsterListContext.Monsters.Value` 变化时清空 `monster-container` 内已有怪物项，并按列表顺序为每只 `Hp > 0` 的怪物通过 `monsterItemTemplate.CloneTree()` 创建新项；死亡怪物（`Hp <= 0`）SHALL NOT 创建项。

#### Scenario: 怪物列表更新时全量重建

- **WHEN** `Monsters.Value` 从 `[A, B, C]`（全部存活）变为 `[A, B, D]`（C 死亡，新增 D）
- **THEN** `monster-container` 子元素数 SHALL 为 3
- **AND** SHALL 按 `[A, B, D]` 顺序排列

#### Scenario: 怪物 Hp 降为 0 时下一次刷新移除该项

- **WHEN** 当前显示 `[A, B]`，`Monsters.Value` 重新发布且 B 的 `Hp = 0`
- **THEN** `monster-container` 子元素数 SHALL 为 1
- **AND** SHALL 仅包含 A 对应的怪物项

### Requirement: MonsterListView 必须为 SplitAcrossAll 计算存活怪物数

`MonsterListView` SHALL 在每次刷新前计算当前存活怪物数（`Monsters.Value.Count(m => m != null && !m.IsDead)`），并将该值传递给每个 `MonsterItemView` 用于 SplitAcrossAll 类卡牌的伤害平分显示。

#### Scenario: 存活数随死亡变化

- **WHEN** `Monsters.Value` 包含 4 只怪物，其中 1 只 `IsDead=true`
- **THEN** 传给每个 `MonsterItemView` 的 `aliveMonsterCount` SHALL 为 3

### Requirement: MonsterListView 必须支持显式 Dispose

`MonsterListView` SHALL 实现 `IDisposable`，`Dispose()` SHALL 解绑 `Monsters.Changed` 订阅，SHALL 释放所有已创建的 `MonsterItemView`（调用各自 `Dispose()`），SHALL NOT 触发新的渲染。

#### Scenario: Dispose 后列表变化不再渲染

- **WHEN** 已 `view.Dispose()` 且之后 `context.Monsters.Value` 被修改
- **THEN** `monster-container` SHALL NOT 接收新增子元素

## ADDED Requirements

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
