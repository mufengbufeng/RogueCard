## 1. 角色等级配置

- [x] 1.1 新增 `player_level.xlsx`，按现有 Luban 表头格式定义 `id`、`required_exp`、`base_energy`、`hand_limit` 字段
- [x] 1.2 在 `player_level.xlsx` 中填写至少五条从 1 级开始的连续 MVP 等级数据，且 1 级 `required_exp` 为 0
- [x] 1.3 在 `__tables__.xlsx` 中注册 `player.TbPlayerLevel`，记录类名为 `PlayerLevel`，输入文件为 `player_level.xlsx`

## 2. 基础卡牌配置

- [x] 2.1 新增 `card.xlsx`，按现有 Luban 表头格式定义 `id`、`name`、`desc`、`cost`、`effect_type`、`value`、`is_basic`、`asset_id` 字段
- [x] 2.2 在 `card.xlsx` 中填写攻击、防御、能量回复三张基础卡牌数据，效果类型分别为 `Attack`、`Defense`、`EnergyRecover`
- [x] 2.3 在 `__tables__.xlsx` 中注册 `card.TbCard`，记录类名为 `Card`，输入文件为 `card.xlsx`

## 3. 配置校验

- [x] 3.1 检查新增表主键 `id` 均为 `int`，并确认没有新增 `string` 类型 id 或 id 引用字段
- [x] 3.2 使用 Excel 读取工具确认 `player_level.xlsx`、`card.xlsx` 和 `__tables__.xlsx` 的表头与数据符合规格
- [x] 3.3 如项目存在可用 Luban 生成命令，运行配置生成或校验命令并记录结果；若不可用，说明未运行原因
