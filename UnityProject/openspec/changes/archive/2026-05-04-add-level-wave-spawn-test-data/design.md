## Context

当前 `level.xlsx`、`level_wave.xlsx`、`monster.xlsx`、`battle_wave_spawn.xlsx` 和 `battle_wave_spawn_batch.xlsx` 已经具备表头结构，但尚未填充可用于后续关卡流程和战斗刷怪联调的测试数据。`monster`、`battle_wave_spawn`、`battle_wave_spawn_batch` 已使用 `int` 标识；`level` 与 `level_wave` 仍使用 `string` 标识，和项目 Luban 约定不一致。

本变更位于配置源数据目录 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas`，该目录在 Unity 工程目录外。实施时需要直接修改 Excel 源文件，并在输出中明确列出需要后续补齐的测试资源标识。

## Goals / Non-Goals

**Goals:**

- 将 `level.xlsx` 与 `level_wave.xlsx` 的主键和引用字段调整为 `int` 风格。
- 填充一套默认测试关卡，覆盖 Battle、Chest、Shop、Battle 的最小波次链路。
- 填充三种测试怪物、两个刷怪方案和四个刷怪批次，保证所有引用 id 能闭合。
- 记录测试怪物资源标识，供后续资源制作或占位 Prefab 添加。

**Non-Goals:**

- 不创建或修改 Unity Prefab、材质、动画、场景或 Addressable/YooAsset 资源配置。
- 不生成 Luban C# 代码或 bytes 资源。
- 不接入运行时代码读取默认关卡、波次推进或刷怪逻辑。
- 不设计正式怪物数值、AI、掉落、卡牌奖励、宝箱奖励或商店商品。

## Decisions

### 1. 先统一标识类型，再填测试数据

`level.id`、`level.wave_ids`、`level_wave.id`、`level_wave.level_id` 调整为 `int` 或 `int` 引用列表后再填数据；不保留字符串兼容字段。

- 选择原因：测试数据会成为后续联调入口，如果继续使用字符串标识，会和已经使用 `int` 的怪物及刷怪表产生混用。
- 替代方案：暂时沿用字符串关卡标识，只填数据。该方式修改更小，但会违反项目配置约定，并增加后续迁移成本。

### 2. 使用固定数字段区分不同配置表

测试数据使用清晰的数字段：关卡 `1`，波次 `1101-1104`，怪物 `1001-1003`，刷怪方案 `2001-2002`，刷怪批次 `300101-300202`。

- 选择原因：数字段便于人工检查引用关系，也能避免不同表之间的 id 视觉混淆。
- 替代方案：从 `1` 开始为每张表独立递增。该方式更短，但跨表排查时不如分段 id 直观。

### 3. 默认关卡覆盖四个节点

默认关卡配置为 Battle → Chest → Shop → Battle。两个 Battle 节点分别引用两个刷怪方案；Chest 和 Shop 暂不配置 payload。

- 选择原因：这条链路能同时验证战斗节点和非战斗占位节点，又不会引入奖励、商店商品等尚未设计的数据。
- 替代方案：只配置一个 Battle 波次。该方式更小，但无法验证波次类型分发和非战斗占位。

### 4. 怪物资源只记录占位标识

测试怪物的 `asset_id` 记录为 `Monster/Slime_Training`、`Monster/Archer_Dummy`、`Monster/Guard_Training`，本变更不创建对应资源。

- 选择原因：用户已接受资源名可以在文档中告知后续添加，当前变更聚焦配置数据。
- 替代方案：同步创建 Unity 占位 Prefab。该方式能减少后续资源缺口，但会扩大配置数据变更的范围，并可能触碰 Unity 资源导入流程。

## Risks / Trade-offs

- [风险] `TbLevel` 和 `TbLevelWave` 重新生成后字段类型变化导致旧运行时代码编译失败 → [缓解] 本变更明确标记为 breaking，后续接入运行时代码时按 int 读取。
- [风险] Excel 修改在 Unity 工程目录外，git 状态可能无法完整反映 → [缓解] 实施完成后明确列出实际修改的配置源文件路径。
- [风险] `payload_id` 对 Chest 和 Shop 暂为空，后续奖励或商店配置接入时仍需扩展 → [缓解] 本变更只承诺 Battle 测试联调，非战斗节点保留占位。
- [风险] 资源标识对应资源不存在会导致运行时加载失败 → [缓解] 输出资源清单，后续资源接入前补齐占位资源或加载降级逻辑。

## Migration Plan

1. 修改 `level.xlsx` 和 `level_wave.xlsx` 表头类型，使主键和引用字段符合 `int` 约定。
2. 写入测试关卡、波次、怪物、刷怪方案和刷怪批次数据。
3. 需要生成配置时，重新运行项目现有 Luban 生成流程。
4. 后续运行时代码接入时，将默认关卡和波次引用按 int 处理。

## Open Questions

- `level_wave.payload_id` 是否应立即声明为 `int?`，还是后续根据不同波次类型拆分为更强类型字段；本变更建议先使用 `int?`，Battle 行填写刷怪方案 id，非 Battle 行留空。
