## ADDED Requirements

### Requirement: 怪物配置表必须提供刷怪引用目标
系统 MUST 在配置数据目录中提供最小怪物配置表结构，用于让刷怪批次通过稳定怪物标识引用要生成的怪物。

#### Scenario: 创建怪物配置表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbMonster` 的怪物配置表结构
- **AND** 该结构 MUST 至少包含怪物标识（int）、名称、描述和资源标识字段

### Requirement: 战斗波次刷怪方案必须可由波次负载标识定位
系统 MUST 在配置数据目录中提供战斗波次刷怪方案表结构，用于作为 `Battle` 类型关卡波次的负载配置。

#### Scenario: 创建战斗波次刷怪方案表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbBattleWaveSpawn` 的刷怪方案表结构
- **AND** 该结构 MUST 至少包含刷怪方案标识（int）、名称、描述和刷怪批次标识列表（int 列表）字段

### Requirement: 刷怪批次表必须表达顺序生成怪物
系统 MUST 在配置数据目录中提供刷怪批次表结构，用于描述一个战斗波次刷怪方案内按顺序生成的怪物批次。

#### Scenario: 创建刷怪批次表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbBattleWaveSpawnBatch` 的刷怪批次表结构
- **AND** 该结构 MUST 至少包含批次标识（int）、所属刷怪方案标识（int）、顺序、怪物标识（int）和数量字段

### Requirement: 刷怪批次配置必须支持前一批死亡后进入下一批
系统 MUST 能通过刷怪批次顺序表达同一战斗波次内的多批次生成规则，让后续运行时可以在当前批次怪物全部死亡后读取下一批次。

#### Scenario: 表达多批次顺序
- **WHEN** 一个刷怪方案包含多个刷怪批次
- **THEN** 每个刷怪批次 MUST 拥有明确的顺序字段
- **AND** 后续运行时 MUST 能基于该顺序从第一批推进到最后一批

### Requirement: 配置表结构创建不得要求填写正式玩法数据
系统 MUST 允许本变更只创建表头和字段定义，不要求立即填写完整怪物或刷怪数据。

#### Scenario: 表格仅包含字段结构
- **WHEN** 新增配置表完成
- **THEN** 怪物配置表、战斗波次刷怪方案表和刷怪批次表 MUST 可以只包含字段定义或示例表头
- **AND** 系统 MUST 不要求同时新增怪物数值、AI、技能、奖励、卡牌或商店商品配置表
