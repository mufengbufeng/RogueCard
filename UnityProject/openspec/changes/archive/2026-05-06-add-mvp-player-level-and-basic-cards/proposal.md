## Why

当前 MVP 已有默认关卡、波次和怪物测试配置，但玩家成长与基础卡牌仍缺少可由 Luban 生成的源数据，后续战斗、升级和出牌系统无法稳定引用这些配置。

本变更补齐角色等级与三张基础卡牌的最小配置契约，为后续玩家战斗状态、卡牌牌堆、出牌效果和升级奖励模块提供可验证的数据基础。

## What Changes

- 新增角色等级配置能力，定义 `player_level.xlsx` 与 `player.TbPlayerLevel` 的最小字段结构。
- 新增基础卡牌配置能力，定义 `card.xlsx` 与 `card.TbCard` 的最小字段结构。
- 在角色等级配置中至少提供等级、升级所需经验、基础能量和手牌上限字段。
- 在基础卡牌配置中至少提供攻击、防御、能量回复三张 MVP 基础卡牌记录。
- 在 `__tables__.xlsx` 中注册新增配置表，使 Luban 能读取并生成对应数据。
- 所有表主键 `id` 与 id 引用字段遵循项目 Luban 配置约定，统一使用 `int`。
- 本变更不实现卡牌运行时效果、玩家升级逻辑、战斗 UI、奖励选择或资源 Prefab。

## Capabilities

### New Capabilities

- `player-level-config`: 定义 MVP 角色等级配置表结构与最小等级数据，用于描述等级、升级经验、基础能量和手牌上限。
- `basic-card-config`: 定义 MVP 基础卡牌配置表结构与攻击、防御、能量回复三张基础卡牌数据。

### Modified Capabilities

无。

## Impact

- 影响配置源数据目录：`D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas`。
- 预计新增 `player_level.xlsx`、`card.xlsx`，并更新 `__tables__.xlsx`。
- 预计影响后续 Luban 生成数据类型：`player.TbPlayerLevel`、`card.TbCard`。
- 不直接影响 Unity Runtime、HotFix 代码、Prefab、场景或运行时流程。
