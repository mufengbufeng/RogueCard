# level-wave-spawn-test-data Specification

## Purpose

定义默认关卡链路的测试配置数据要求，用于验证从默认关卡入口到 Battle、Chest、Shop 波次以及 Battle 刷怪方案、批次和怪物配置之间的引用闭环。

## Requirements

### Requirement: 测试配置必须提供默认关卡闭环数据
系统 MUST 在配置源数据中提供一套默认测试关卡数据，用于验证从默认入口进入关卡后按顺序推进 Battle、Chest、Shop 和 Battle 波次。

#### Scenario: 默认测试关卡包含四个波次
- **WHEN** 检查 `level.xlsx` 中的默认关卡数据
- **THEN** 系统 MUST 存在一条 `is_default` 为 `true` 的测试关卡记录
- **AND** 该关卡 MUST 引用四个按顺序排列的波次标识

#### Scenario: 默认测试关卡覆盖三类波次
- **WHEN** 检查默认测试关卡引用的 `level_wave.xlsx` 波次数据
- **THEN** 系统 MUST 至少包含两个 `Battle` 波次
- **AND** 系统 MUST 至少包含一个 `Chest` 波次
- **AND** 系统 MUST 至少包含一个 `Shop` 波次

### Requirement: Battle 测试波次必须关联刷怪方案
系统 MUST 让每个 Battle 类型测试波次通过负载标识定位一个可用刷怪方案。

#### Scenario: Battle 波次负载可定位刷怪方案
- **WHEN** 检查默认测试关卡中的 Battle 类型波次
- **THEN** 每个 Battle 波次的 `payload_id` MUST 填写一个刷怪方案标识
- **AND** 该标识 MUST 能在 `battle_wave_spawn.xlsx` 中找到对应记录

### Requirement: 测试数据引用必须闭合
系统 MUST 保证默认测试关卡、波次、刷怪方案、刷怪批次和怪物之间的所有 id 引用都能在对应配置表中找到目标记录。

#### Scenario: 关卡到怪物链路引用完整
- **WHEN** 从默认关卡读取 `wave_ids`、从 Battle 波次读取 `payload_id`、从刷怪方案读取 `batch_ids`、从刷怪批次读取 `monster_id`
- **THEN** 每一级引用 MUST 能在对应表中找到目标记录
- **AND** 系统 MUST 不存在悬空的测试数据引用

### Requirement: 测试怪物资源标识必须明确记录
系统 MUST 在怪物测试配置中记录后续需要补齐的占位资源标识。

#### Scenario: 测试怪物包含资源标识
- **WHEN** 检查 `monster.xlsx` 中的测试怪物记录
- **THEN** 每个测试怪物 MUST 填写非空 `asset_id`
- **AND** 资源标识 MUST 能作为后续资源制作或资源映射的输入
