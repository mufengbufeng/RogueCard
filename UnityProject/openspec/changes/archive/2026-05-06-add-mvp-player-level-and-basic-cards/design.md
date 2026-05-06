## Context

配置源数据位于 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas`。当前已有 `level.xlsx` 作为关卡主表，`monster.xlsx`、`battle_wave_spawn.xlsx` 和 `battle_wave_spawn_batch.xlsx` 作为怪物与刷怪测试配置，并已在 `__tables__.xlsx` 注册。

本变更需要补齐 MVP 战斗闭环后续会依赖的玩家等级与基础卡牌配置。由于 `level.xlsx` 已被关卡主表占用，角色等级配置必须使用独立文件与独立模块名，避免与关卡配置语义冲突。

## Goals / Non-Goals

**Goals:**

- 新增 `player_level.xlsx`，注册为 `player.TbPlayerLevel`，提供 MVP 角色等级配置。
- 新增 `card.xlsx`，注册为 `card.TbCard`，提供攻击、防御、能量回复三张基础卡牌配置。
- 保证新增表主键 `id` 使用 `int`，并延续现有 Luban 表头结构：`##var`、`##type`、`##group`、`##` 和数据行。
- 为后续玩家战斗状态、牌堆、出牌效果和升级奖励实现提供稳定配置 id。

**Non-Goals:**

- 不实现玩家经验结算、升级判定、卡牌效果执行或牌堆运行时逻辑。
- 不新增卡牌类型枚举、效果结构体或多段效果配置。
- 不修改已有关卡、波次、怪物和刷怪表数据。
- 不制作卡牌 Prefab、图标资源、战斗 UI 或奖励 UI。

## Decisions

### 1. 角色等级表使用 `player_level.xlsx`，不复用 `level.xlsx`

`level.xlsx` 现有含义是关卡主表，并注册为 `level.TbLevel`。角色等级表使用 `player_level.xlsx` 与 `player.TbPlayerLevel`，避免 Luban 类型名和业务语义混淆。

替代方案是把角色等级字段加入 `level.xlsx`，但这会把“关卡等级”和“角色等级”混在同一表中，后续关卡流程和玩家成长都更难维护。

### 2. 基础卡牌表先用单表扁平字段表达 MVP 效果

`card.xlsx` 使用 `cost`、`effect_type`、`value` 等扁平字段表达三张基础卡牌：攻击、防御、能量回复。`effect_type` 首版使用 `string`，避免为了三张基础卡牌提前引入枚举、bean 或多效果列表。

替代方案是新增 `card.ECardEffectType` 枚举。该方式类型更强，但会扩大本次配置范围，并要求同步维护 `__enums__.xlsx`；在 MVP 只有三种效果且运行时代码尚未实现时，收益有限。

### 3. MVP 等级数据采用少量可联调记录

首版等级数据提供从 1 级开始的少量连续等级。`id` 即等级，`required_exp` 表示达到该等级所需累计经验；1 级为 0，便于后续升级判定直接按累计经验查表。

替代方案是 `required_exp` 表示从上一等级升到本级的增量经验。增量经验也可行，但后续判断升级时需要累计计算；MVP 阶段累计阈值更直观。

### 4. 基础卡牌 id 预留独立段

三张基础卡牌使用稳定 `int` id，例如 `1001`、`1002`、`1003`。这些 id 后续可被初始牌组、奖励池或测试用例引用。

替代方案是使用从 1 开始的 id。该方式更短，但与其他配置表小 id 语义容易混淆；使用 1000 段更便于识别卡牌配置。

## Risks / Trade-offs

- [风险] `effect_type` 使用 `string` 缺少编译期约束 → [缓解] 在 spec 中限定 MVP 必填值集合，后续卡牌系统稳定后可单独变更为枚举。
- [风险] 只配置三张基础卡牌，无法覆盖奖励池丰富度 → [缓解] 本变更只服务 MVP 基础出牌闭环，奖励扩展由后续变更补充。
- [风险] Excel 新增表后未同步注册到 `__tables__.xlsx` 会导致 Luban 不生成类型 → [缓解] tasks 明确包含注册表更新和读取校验。
- [风险] 新增 Excel 文件可能未被 Unity 侧生成代码立即引用 → [缓解] 本变更范围限制为源配置，运行时接入由后续 OpenSpec 变更处理。
