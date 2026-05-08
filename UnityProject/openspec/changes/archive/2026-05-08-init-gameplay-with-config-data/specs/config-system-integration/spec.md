## ADDED Requirements

### Requirement: ConfigSystem 必须在热更新初始化时完成加载
系统 MUST 在 GameLogicEntry.Init() 中创建并初始化 ConfigSystem，确保所有配置表在后续游戏逻辑执行前可用。

#### Scenario: 热更新初始化后配置表全局可访问
- **WHEN** GameLogicEntry.Init() 执行完成
- **THEN** ConfigSystem MUST 已完成加载
- **AND** GameLogicEntry MUST 暴露 Tables 属性供全局访问
- **AND** 所有 Luban 配置表（TbLevel、TbLevelWave、TbMonster、TbBattleWaveSpawn、TbBattleWaveSpawnBatch、TbPlayerLevel、TbCard）MUST 可通过 Tables 属性访问

#### Scenario: 配置加载失败时不阻断启动
- **WHEN** ConfigSystem 加载过程中某个配置文件缺失或损坏
- **THEN** 系统 MUST 记录错误日志
- **AND** 系统 MUST NOT 因单个配置文件加载失败而阻断整个热更新初始化流程

### Requirement: 全局必须通过 GameLogicEntry 访问配置表
所有运行时代码 MUST 通过 GameLogicEntry.Config.Tables 访问配置数据，不得自行创建 ConfigSystem 实例。

#### Scenario: 通过 GameLogicEntry 获取关卡配置
- **WHEN** 游戏逻辑需要读取关卡配置
- **THEN** MUST 通过 GameLogicEntry.Config.Tables.TbLevel 获取
- **AND** MUST NOT 直接 new ConfigSystem 创建新实例
