## 1. 修正关卡配置表类型

- [x] 1.1 修改 `level.xlsx` 表头类型，使 `id` 使用 `int`，`wave_ids` 使用指向 `level.TbLevelWave` 的 `int` 列表引用。
- [x] 1.2 修改 `level_wave.xlsx` 表头类型，使 `id` 使用 `int`，`level_id` 使用指向 `level.TbLevel` 的 `int` 引用。
- [x] 1.3 修改 `level_wave.xlsx` 的 `payload_id` 类型，使 Battle 波次可以填写 `int` 刷怪方案标识，非 Battle 波次可以留空。

## 2. 填写测试配置数据

- [x] 2.1 在 `monster.xlsx` 写入三条测试怪物数据：训练史莱姆、木桩射手、训练守卫。
- [x] 2.2 在 `battle_wave_spawn_batch.xlsx` 写入四条测试刷怪批次数据，覆盖两个刷怪方案的顺序批次。
- [x] 2.3 在 `battle_wave_spawn.xlsx` 写入两个测试刷怪方案，并通过 `batch_ids` 引用对应批次。
- [x] 2.4 在 `level_wave.xlsx` 写入四条测试波次数据，按 Battle、Chest、Shop、Battle 排列。
- [x] 2.5 在 `level.xlsx` 写入一条默认测试关卡数据，并通过 `wave_ids` 引用四个测试波次。

## 3. 核验引用一致性

- [x] 3.1 检查默认关卡的 `wave_ids` 都能在 `level_wave.xlsx` 中找到。
- [x] 3.2 检查 Battle 波次的 `payload_id` 都能在 `battle_wave_spawn.xlsx` 中找到。
- [x] 3.3 检查刷怪方案的 `batch_ids` 都能在 `battle_wave_spawn_batch.xlsx` 中找到。
- [x] 3.4 检查刷怪批次的 `monster_id` 都能在 `monster.xlsx` 中找到，并且 `count` 大于零。

## 4. 输出实施结果

- [x] 4.1 汇总实际写入的测试数据和修改过的 Excel 文件路径。
- [x] 4.2 列出后续需要补齐的测试资源标识：`Monster/Slime_Training`、`Monster/Archer_Dummy`、`Monster/Guard_Training`。
- [x] 4.3 如果本地具备 Luban 生成命令，运行或说明配置生成验证结果；如果不可运行，明确说明未运行原因。
