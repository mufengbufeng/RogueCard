## REMOVED Requirements

### Requirement: 基础卡牌配置表必须定义 MVP 卡牌字段

**Reason:** 字段集变更——`EffectType` / `Value` 被结构化的 `TbCardEffect` 表替代，新增 `OwnerKind` / `TargetMode` 字段。原 requirement 描述的字段集已不再准确。

**Migration:** 请见 ADDED Requirement「基础卡牌配置表必须支持卡牌+效果分离结构」。

### Requirement: MVP 卡牌效果类型必须限制为基础效果集合

**Reason:** 原 requirement 把 `EffectType` 限制为 `Attack` / `Defense` / `EnergyRecover` 三个字符串值，而新模型用 `EffectKind` 枚举（Damage / Shield / DamageDot / EnergyGain）+ 多效果组合表达更丰富的卡牌语义。

**Migration:** 请见 ADDED Requirement「MVP 基础卡牌必须覆盖 5 类玩法」。

### Requirement: 基础卡牌配置必须提供三张 MVP 基础卡牌

**Reason:** MVP 卡牌从 3 张升级为 5 张（近战 / 投射 / 法术 / 能量 / 护盾），原"攻击 + 防御 + 能量回复"三张已无法覆盖战斗系统需要。

**Migration:** 请见 ADDED Requirement「MVP 基础卡牌必须覆盖 5 类玩法」。

## ADDED Requirements

### Requirement: 基础卡牌配置表必须支持卡牌+效果分离结构

系统 MUST 在配置数据目录中提供两张联合表：`TbCard` 描述卡牌身份与目标策略，`TbCardEffect` 描述每张卡挂的若干条具体效果。一张 `TbCard` 记录 MUST 能被零条到多条 `TbCardEffect` 记录引用。

#### Scenario: 创建 TbCard 表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\card.xlsx`
- **THEN** 系统 MUST 存在用于生成 `TbCard` 的表结构
- **AND** 该结构 MUST 包含 `Id` (int) / `Name` / `Description` / `Cost` (int) / `OwnerKind` (枚举) / `TargetMode` (枚举) / `IsBasic` (bool) / `ResourceId` 字段
- **AND** MUST NOT 再包含 `EffectType` / `Value` 字段

#### Scenario: 创建 TbCardEffect 表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\card_effect.xlsx`
- **THEN** 系统 MUST 存在用于生成 `TbCardEffect` 的表结构
- **AND** 该结构 MUST 包含 `Id` (int) / `CardId` (int#ref=card.TbCard) / `Kind` (枚举 EffectKind) / `Value` (int) / `Duration` (int) 字段

#### Scenario: 注册新表到 Luban 表清单
- **WHEN** 检查 `__tables__.xlsx`
- **THEN** 系统 MUST 存在 `card.TbCard` 注册记录，记录类名 `Card`
- **AND** 系统 MUST 存在 `card.TbCardEffect` 注册记录，记录类名 `CardEffect`
- **AND** 两条记录的 input 路径 MUST 分别指向 `card.xlsx` 和 `card_effect.xlsx`

### Requirement: TbCard 必须使用枚举字段表达归属与目标模式

`TbCard.OwnerKind` MUST 使用枚举 `OwnerKind { Player, Monster, Both }`。`TbCard.TargetMode` MUST 使用枚举 `TargetMode { SingleAuto, SingleManual, All, SplitAcrossAll, Self }`。

#### Scenario: 枚举值定义
- **WHEN** 检查 Luban 枚举定义
- **THEN** 系统 MUST 存在 `OwnerKind` 枚举，至少包含 `Player`、`Monster`、`Both` 三个值
- **AND** 系统 MUST 存在 `TargetMode` 枚举，至少包含 `SingleAuto`、`SingleManual`、`All`、`SplitAcrossAll`、`Self` 五个值

### Requirement: TbCardEffect 必须使用 EffectKind 枚举

`TbCardEffect.Kind` MUST 使用枚举 `EffectKind`，至少包含 `Damage`、`Shield`、`DamageDot`、`EnergyGain` 四个值。

#### Scenario: 创建 EffectKind 枚举
- **WHEN** 检查 Luban 枚举定义
- **THEN** 系统 MUST 存在 `EffectKind` 枚举
- **AND** 该枚举 MUST 包含 `Damage`、`Shield`、`DamageDot`、`EnergyGain` 四个值
- **AND** 该枚举 MUST 预留扩展空间（后续可添加 Buff / Debuff 类型）

### Requirement: MVP 基础卡牌必须覆盖 5 类玩法

`TbCard` MUST 提供 5 张 `IsBasic == true` 且 `OwnerKind == Player` 的基础卡牌，覆盖近战 / 投射 / 法术 / 能量 / 护盾五类玩法；每张卡 MUST 配置至少一条 `TbCardEffect` 行。

#### Scenario: 近战卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张近战卡，`Cost = 1`，`TargetMode = SingleAuto`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = Damage`，`Value = 6`，`Duration = 0`

#### Scenario: 投射卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张投射卡，`Cost = 1`，`TargetMode = SplitAcrossAll`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = Damage`，`Value = 6`，`Duration = 0`

#### Scenario: 法术卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张法术卡，`Cost = 1`，`TargetMode = SingleManual` 或 `All`
- **AND** 该卡 MUST 至少挂两条 `TbCardEffect`：一条 `Kind = Damage`、`Value = 8`、`Duration = 0`；一条 `Kind = DamageDot`、`Value = 2`、`Duration ≥ 1`

#### Scenario: 能量卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张能量卡，`Cost = 0`，`TargetMode = Self`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = EnergyGain`，`Value = 2`，`Duration = 0`

#### Scenario: 护盾卡牌配置
- **WHEN** 检查 `card.xlsx` 与 `card_effect.xlsx`
- **THEN** 系统 MUST 存在一张护盾卡，`Cost = 1`，`TargetMode = Self`
- **AND** 该卡 MUST 至少挂一条 `TbCardEffect`，`Kind = Shield`，`Value = 5`，`Duration = 0`
