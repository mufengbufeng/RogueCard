## Why

卡牌 Rogue 第一阶段已经有主界面关卡入口，但默认关卡信息仍由代码常量提供，后续关卡运行时上下文和波次推进缺少可读取的正式配置结构。现在需要在配置数据目录中补齐关卡主表、波次表和波次类型枚举，让入口请求、关卡上下文和三类波次分发拥有稳定的数据来源。

## What Changes

- 在 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下新增关卡主表结构，用于描述关卡标识、展示信息、默认入口标记和波次列表。
- 在同一路径下新增关卡波次表结构，用于描述关卡内波次顺序、波次类型、展示文案、继续文案和预留负载标识。
- 新增波次类型枚举结构，包含战斗、宝箱和商店三类第一阶段节点。
- 本变更只创建表格结构和字段，不要求填写正式玩法数据。
- 本变更不实现关卡运行时代码、波次推进逻辑、战斗、奖励、宝箱开启或商店购买行为。

## Capabilities

### New Capabilities
- `level-wave-config`: 定义第一阶段关卡骨架所需的关卡配置、波次配置和波次类型枚举结构。

### Modified Capabilities
无。

## Impact

- 影响仓库外配置数据目录 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas`。
- 后续生成配置代码时会影响 `GameConfig.Tables` 中可用的表集合和对应 bytes 资源。
- 后续 `add-level-runtime-context`、关卡波次推进和非战斗波次占位变更可以消费这些配置结构。
