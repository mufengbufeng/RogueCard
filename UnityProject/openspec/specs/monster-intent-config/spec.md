# monster-intent-config Specification

## Purpose

定义怪物意图配置表的结构和运行时选取规则，支持权重随机和序列循环混合模式，并预留后续扩展 Buff 和 Debuff 类型。

## Requirements

### Requirement: 怪物意图表必须支持权重随机和序列循环混合模式
系统 MUST 在配置数据目录中提供怪物意图表结构 TbMonsterIntent，同时包含 Order（序列顺序）和 Weight（权重）字段，支持运行时按规则选择意图选取策略。

#### Scenario: 创建怪物意图表结构
- **WHEN** 检查配置源数据目录下的配置结构
- **THEN** 系统 MUST 存在用于生成 TbMonsterIntent 的怪物意图表结构
- **AND** 该结构 MUST 至少包含意图标识（int）、怪物标识（int）、序列顺序（int）、意图类型枚举、数值（int）和权重（int）字段

### Requirement: 意图类型枚举必须覆盖第一版战斗行为
系统 MUST 提供 MonsterIntentType 枚举，覆盖攻击和防御两类基础战斗行为。

#### Scenario: 创建意图类型枚举
- **WHEN** 检查配置生成的枚举定义
- **THEN** 系统 MUST 存在 MonsterIntentType 枚举
- **AND** 该枚举 MUST 包含 Attack（攻击）和 Defend（防御）两个枚举值

### Requirement: 怪物意图选取必须遵循混合模式优先级规则
运行时读取 TbMonsterIntent 数据时，MUST 按以下规则决定选取策略：怪物拥有 Order > 0 且 Weight == 0 的意图时使用序列循环模式；怪物拥有 Weight > 0 且 Order == 0 的意图时使用权重随机模式；两者都有值时 Order 循环优先。

#### Scenario: 按 Order 序列循环选取 Boss 意图
- **WHEN** 某怪物在 TbMonsterIntent 中有 Order > 0 且 Weight == 0 的多行意图
- **THEN** 运行时 MUST 按 Order 从小到大依次选取
- **AND** 到达最大 Order 后 MUST 回到最小 Order

#### Scenario: 按 Weight 随机选取普通怪物意图
- **WHEN** 某怪物在 TbMonsterIntent 中有 Weight > 0 且 Order == 0 的多行意图
- **THEN** 运行时 MUST 按权重比例随机选取一个意图

### Requirement: 怪物意图表结构必须支持后续扩展 Buff 和 Debuff 类型
TbMonsterIntent 的 IntentType 字段 MUST 使用枚举类型，首版包含 Attack 和 Defend，但 MUST 预留后续添加 Buff 和 Debuff 枚举值的能力。

#### Scenario: 枚举结构可扩展
- **WHEN** 后续需要新增 Buff 或 Debuff 意图类型
- **THEN** 只需在枚举中添加新值和对应 TbMonsterIntent 数据行
- **AND** MUST NOT 需要修改表结构或运行时选取逻辑
