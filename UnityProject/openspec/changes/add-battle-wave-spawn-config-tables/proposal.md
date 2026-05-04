## Why

战斗波次已经有 `PayloadId` 预留字段，但还没有用于描述“一个战斗波次内分批刷怪”的配置结构。为了后续运行时能够按批次生成怪物，并在前一批怪物全部死亡后再进入下一批，需要先补齐最小刷怪配置表。

## What Changes

- 新增战斗波次刷怪批次配置结构，用于描述某个战斗波次下的刷怪批次顺序、怪物标识和数量。
- 新增最小怪物配置结构，用于让刷怪批次可以引用怪物基础信息或占位资源标识。
- 本变更只创建配置表结构和字段，不生成 C# 配置代码或 bytes 资源。
- 本变更不实现运行时刷怪、怪物实体、血量、死亡事件、战斗结算或场景表现。
- 本变更不修改宝箱、商店、卡牌、奖励、玩家初始状态等非刷怪配置。

## Capabilities

### New Capabilities
- `battle-wave-spawn-config`: 定义战斗波次分批刷怪所需的最小配置表结构。

### Modified Capabilities
无。

## Impact

- 影响仓库外配置数据目录 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas`。
- 后续生成配置代码时会影响 `GameConfig.Tables` 中可用的表集合和对应 bytes 资源。
- 后续战斗波次运行时可以通过 `TbLevelWave.PayloadId` 定位刷怪配置，并按批次顺序读取怪物生成信息。
