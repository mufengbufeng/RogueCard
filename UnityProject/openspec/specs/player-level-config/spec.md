# player-level-config Specification

## Purpose

定义 MVP 角色等级配置表的最小结构和测试数据要求，支撑后续玩家经验、升级、基础能量和手牌上限逻辑接入。

## Requirements

### Requirement: 角色等级配置表必须定义 MVP 成长字段
系统 MUST 在配置数据目录中提供角色等级配置表结构，用于描述玩家等级、达到该等级所需经验、基础能量和手牌上限，并且等级标识 MUST 使用 `int`。

#### Scenario: 创建角色等级配置表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbPlayerLevel` 的角色等级配置表结构
- **AND** 该结构 MUST 至少包含等级标识、升级所需经验、基础能量和手牌上限字段
- **AND** 等级标识字段 MUST 使用 `int`

### Requirement: 角色等级配置必须提供 MVP 连续等级数据
系统 MUST 在角色等级配置表中提供可用于 MVP 联调的连续等级记录，使后续玩家经验和升级逻辑可以读取基础阈值。

#### Scenario: 填写 MVP 等级记录
- **WHEN** 检查 `player_level.xlsx` 的数据行
- **THEN** 系统 MUST 存在至少五条从 1 级开始的连续等级记录
- **AND** 1 级的升级所需经验 MUST 为 0
- **AND** 每条等级记录 MUST 填写基础能量和手牌上限
- **AND** 每条等级记录的基础能量和手牌上限 MUST 大于 0

### Requirement: 角色等级配置表必须注册到 Luban 表清单
系统 MUST 在 Luban 表清单中注册角色等级配置表，使配置生成流程能够识别该表。

#### Scenario: 注册角色等级配置表
- **WHEN** 检查 `__tables__.xlsx` 的表注册数据
- **THEN** 系统 MUST 存在 `player.TbPlayerLevel` 注册记录
- **AND** 该注册记录 MUST 使用 `PlayerLevel` 作为记录类名
- **AND** 该注册记录 MUST 从 `player_level.xlsx` 读取结构和数据
