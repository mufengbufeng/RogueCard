## MODIFIED Requirements

### Requirement: 怪物配置表必须提供刷怪引用目标
系统 MUST 在配置数据目录中提供最小怪物配置表结构，用于让刷怪批次通过稳定怪物标识引用要生成的怪物，并且 MUST 包含可用于联调的测试怪物记录。

#### Scenario: 创建怪物配置表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbMonster` 的怪物配置表结构
- **AND** 该结构 MUST 至少包含怪物标识、名称、描述和资源标识字段

#### Scenario: 填写测试怪物记录
- **WHEN** 检查 `monster.xlsx` 的数据行
- **THEN** 系统 MUST 存在至少三条测试怪物记录
- **AND** 每条测试怪物记录 MUST 使用 `int` 怪物标识
- **AND** 每条测试怪物记录 MUST 填写名称、描述和资源标识

### Requirement: 战斗波次刷怪方案必须可由波次负载标识定位
系统 MUST 在配置数据目录中提供战斗波次刷怪方案表结构，用于作为 `Battle` 类型关卡波次的负载配置，并且 MUST 包含可被测试 Battle 波次引用的刷怪方案记录。

#### Scenario: 创建战斗波次刷怪方案表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbBattleWaveSpawn` 的刷怪方案表结构
- **AND** 该结构 MUST 至少包含刷怪方案标识、名称、描述和刷怪批次标识列表字段

#### Scenario: 填写测试刷怪方案记录
- **WHEN** 检查 `battle_wave_spawn.xlsx` 的数据行
- **THEN** 系统 MUST 存在至少两条测试刷怪方案记录
- **AND** 每条测试刷怪方案记录 MUST 使用 `int` 刷怪方案标识
- **AND** 每条测试刷怪方案记录 MUST 引用至少一个刷怪批次标识

### Requirement: 刷怪批次表必须表达顺序生成怪物
系统 MUST 在配置数据目录中提供刷怪批次表结构，用于描述一个战斗波次刷怪方案内按顺序生成的怪物批次，并且 MUST 包含可被测试刷怪方案引用的刷怪批次记录。

#### Scenario: 创建刷怪批次表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas` 下的配置源数据
- **THEN** 系统 MUST 存在用于生成 `TbBattleWaveSpawnBatch` 的刷怪批次表结构
- **AND** 该结构 MUST 至少包含批次标识、所属刷怪方案标识、顺序、怪物标识和数量字段

#### Scenario: 填写测试刷怪批次记录
- **WHEN** 检查 `battle_wave_spawn_batch.xlsx` 的数据行
- **THEN** 系统 MUST 存在至少四条测试刷怪批次记录
- **AND** 每条测试刷怪批次记录 MUST 使用 `int` 批次标识
- **AND** 每条测试刷怪批次记录 MUST 引用一个存在的刷怪方案标识
- **AND** 每条测试刷怪批次记录 MUST 引用一个存在的怪物标识
- **AND** 每条测试刷怪批次记录 MUST 填写大于零的怪物数量
