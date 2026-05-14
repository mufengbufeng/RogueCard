## MODIFIED Requirements

### Requirement: 基础卡牌配置表必须支持卡牌+效果分离结构

系统 MUST 在配置数据目录中提供两张联合表：`TbCard` 描述卡牌身份、释放策略与目标策略，`TbCardEffect` 描述每张卡挂的若干条具体效果及触发时机。一张 `TbCard` 记录 MUST 能被零条到多条 `TbCardEffect` 记录引用。

#### Scenario: 创建 TbCard 表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\card.xlsx`
- **THEN** 系统 MUST 存在用于生成 `TbCard` 的表结构
- **AND** 该结构 MUST 包含 `Id` (int) / `Name` / `Description` / `Cost` (int) / `OwnerKind` (枚举) / `CardReleaseKind` (枚举) / `TargetMode` (枚举) / `TargetCount` (int) / `IsBasic` (bool) / `ResourceId` 字段
- **AND** MUST NOT 再包含 `EffectType` / `Value` 字段

#### Scenario: 创建 TbCardEffect 表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\card_effect.xlsx`
- **THEN** 系统 MUST 存在用于生成 `TbCardEffect` 的表结构
- **AND** 该结构 MUST 包含 `Id` (int) / `CardId` (int#ref=card.TbCard) / `Kind` (枚举 EffectKind) / `TriggerTiming` (枚举 EffectTriggerTiming) / `Value` (int) / `Duration` (int) 字段

#### Scenario: 注册新表到 Luban 表清单
- **WHEN** 检查 `__tables__.xlsx`
- **THEN** 系统 MUST 存在 `card.TbCard` 注册记录，记录类名 `Card`
- **AND** 系统 MUST 存在 `card.TbCardEffect` 注册记录，记录类名 `CardEffect`
- **AND** 两条记录的 input 路径 MUST 分别指向 `card.xlsx` 和 `card_effect.xlsx`

### Requirement: TbCard 必须使用枚举字段表达归属与目标模式

`TbCard.OwnerKind` MUST 使用枚举 `OwnerKind { Player, Monster, Both }`。`TbCard.CardReleaseKind` MUST 使用枚举 `CardReleaseKind { Melee, Projectile, Spell }`。`TbCard.TargetMode` MUST 使用枚举 `TargetMode { SingleAuto, SingleManual, All, SplitAcrossAll, Self }`。

#### Scenario: 枚举值定义
- **WHEN** 检查 Luban 枚举定义
- **THEN** 系统 MUST 存在 `OwnerKind` 枚举，至少包含 `Player`、`Monster`、`Both` 三个值
- **AND** 系统 MUST 存在 `CardReleaseKind` 枚举，至少包含 `Melee`、`Projectile`、`Spell` 三个值
- **AND** 系统 MUST 存在 `TargetMode` 枚举，至少包含 `SingleAuto`、`SingleManual`、`All`、`SplitAcrossAll`、`Self` 五个值

#### Scenario: TargetCount 语义定义
- **WHEN** 检查 `TbCard.TargetCount` 字段
- **THEN** `TargetCount > 0` MUST 表示该卡期望命中的最大目标数量
- **AND** `TargetCount <= 0` MUST 表示命中所有合法目标

### Requirement: TbCardEffect 必须使用 EffectKind 枚举

`TbCardEffect.Kind` MUST 使用枚举 `EffectKind`，至少包含 `Damage`、`Shield`、`DamageDot`、`EnergyGain` 四个值。`TbCardEffect.TriggerTiming` MUST 使用枚举 `EffectTriggerTiming`，至少包含 `Immediate`、`EnemyTurnStart`、`EnemyTurnEnd` 三个值。

#### Scenario: 创建 EffectKind 枚举
- **WHEN** 检查 Luban 枚举定义
- **THEN** 系统 MUST 存在 `EffectKind` 枚举
- **AND** 该枚举 MUST 包含 `Damage`、`Shield`、`DamageDot`、`EnergyGain` 四个值
- **AND** 该枚举 MUST 预留扩展空间（后续可添加 Buff / Debuff 类型）

#### Scenario: 创建 EffectTriggerTiming 枚举
- **WHEN** 检查 Luban 枚举定义
- **THEN** 系统 MUST 存在 `EffectTriggerTiming` 枚举
- **AND** 该枚举 MUST 包含 `Immediate`、`EnemyTurnStart`、`EnemyTurnEnd` 三个值

### Requirement: MVP 基础卡牌必须覆盖 5 类玩法

`TbCard` MUST 提供 5 张 `IsBasic == true` 且 `OwnerKind == Player` 的基础卡牌，覆盖近战 / 投射 / 法术 / 能量 / 护盾五类玩法；每张卡 MUST 配置 `CardReleaseKind`，并配置至少一条 `TbCardEffect` 行。

#### Scenario: 近战卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张近战卡，`Cost = 1`，`CardReleaseKind = Melee`，`TargetMode = SingleAuto`，`TargetCount = 1`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = Damage`，`TriggerTiming = Immediate`，`Value = 6`，`Duration = 0`

#### Scenario: 投射卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张投射卡，`Cost = 1`，`CardReleaseKind = Projectile`，`TargetMode = SplitAcrossAll`
- **AND** 该卡 `TargetCount` MUST 大于 0 或等于 0（等于 0 表示所有合法目标）
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = Damage`，`TriggerTiming = Immediate`，`Value = 6`，`Duration = 0`

#### Scenario: 法术卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张法术卡，`Cost = 1`，`CardReleaseKind = Spell`，`TargetMode = SingleManual` 或 `All`
- **AND** 该卡 MUST 至少挂两条 `TbCardEffect`：一条 `Kind = Damage`、`TriggerTiming = Immediate`、`Value = 8`、`Duration = 0`；一条 `Kind = DamageDot`、`TriggerTiming = EnemyTurnStart` 或 `EnemyTurnEnd`、`Value = 2`、`Duration >= 1`

#### Scenario: 能量卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张能量卡，`Cost = 0`，`TargetMode = Self`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = EnergyGain`，`TriggerTiming = Immediate`，`Value = 2`，`Duration = 0`

#### Scenario: 护盾卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张护盾卡，`Cost = 1`，`TargetMode = Self`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = Shield`，`TriggerTiming = Immediate`，`Value = 5`，`Duration = 0`
