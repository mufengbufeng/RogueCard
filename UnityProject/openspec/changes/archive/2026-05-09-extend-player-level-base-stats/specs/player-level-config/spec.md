## MODIFIED Requirements

### Requirement: 角色等级配置表必须定义 MVP 成长字段

系统 MUST 在配置数据目录中提供角色等级配置表结构，用于描述玩家等级、达到该等级所需经验、**基础生命**、基础能量和手牌上限，并且等级标识 MUST 使用 `int`。

#### Scenario: 创建角色等级配置表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbPlayerLevel` 的角色等级配置表结构
- **AND** 该结构 MUST 包含等级标识 (int) / 升级所需经验 (int) / **基础生命 (int)** / 基础能量 (int) / 手牌上限 (int) 字段
- **AND** 等级标识字段 MUST 使用 `int`
- **AND** 基础生命字段命名 MUST 为 `BaseHp`

### Requirement: 角色等级配置必须提供 MVP 连续等级数据

系统 MUST 在角色等级配置表中提供至少 5 条从 1 级开始的连续等级记录，每条记录的所有字段 MUST 大于等于 0，且 1 级与 5 级的 `BaseHp` 必须落在 100~140 范围内（设计要求："基础 100、升级增加 10"）。

#### Scenario: 填写 MVP 等级记录
- **WHEN** 检查 `player_level.xlsx` 的数据行
- **THEN** 系统 MUST 存在至少五条从 1 级开始的连续等级记录
- **AND** 1 级的升级所需经验 MUST 为 0
- **AND** 每条等级记录 MUST 填写 `BaseHp` / `BaseEnergy` / `HandLimit`
- **AND** 1 级的 `BaseHp` MUST 等于 100
- **AND** 5 级的 `BaseHp` MUST 等于 140
- **AND** 每条记录的 `BaseEnergy` MUST 等于 3
- **AND** 每条记录的 `HandLimit` MUST 等于 10
