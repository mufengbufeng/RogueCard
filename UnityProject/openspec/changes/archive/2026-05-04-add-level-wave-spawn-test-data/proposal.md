## Why

关卡、波次、怪物和刷怪方案表目前只有表头，没有可用于后续运行时联调的最小测试数据。与此同时，`level` 与 `level_wave` 表仍使用 `string` 主键，和项目 Luban 配置约定中 `id` 统一使用 `int` 的规则不一致，继续填数据会放大引用类型不一致的问题。

## What Changes

- 新增一套最小闭环测试配置数据，覆盖默认关卡、战斗波次、宝箱占位、商店占位、怪物、刷怪方案和刷怪批次。
- 修改 `level.xlsx` 和 `level_wave.xlsx` 的标识字段类型，使关卡、波次及其引用统一使用 `int`。
- 让 Battle 类型波次的 `payload_id` 可引用 `battle_wave_spawn.xlsx` 中的刷怪方案 `id`。
- 记录测试怪物所需的占位资源标识，供后续 Unity 资源补齐。
- **BREAKING**：`TbLevel`、`TbLevelWave` 的主键及相关引用从 `string` 调整为 `int`，依赖旧字符串标识的生成代码或运行时代码需要同步更新。

## Capabilities

### New Capabilities
- `level-wave-spawn-test-data`: 描述用于关卡波次和战斗刷怪联调的最小测试配置数据集。

### Modified Capabilities
- `level-wave-config`: 关卡和波次标识字段需要符合 Luban `int` 标识约定，并允许 Battle 波次通过 int 负载标识关联刷怪方案。
- `battle-wave-spawn-config`: 怪物、刷怪方案和刷怪批次配置需要具备可联调的测试数据，并记录占位资源标识要求。

## Impact

- 配置源文件：`D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\level.xlsx`、`level_wave.xlsx`、`monster.xlsx`、`battle_wave_spawn.xlsx`、`battle_wave_spawn_batch.xlsx`。
- Luban 生成产物：后续重新生成配置后，`TbLevel`、`TbLevelWave` 的 `id` 和引用字段类型会变化。
- 后续运行时：任何读取默认关卡、波次列表或 Battle 波次负载的代码应按 int 标识处理。
- 资源制作：需要补齐测试怪物资源标识对应的占位 Prefab 或资源映射。
