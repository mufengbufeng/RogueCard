# game-runtime-context Specification

## Purpose

定义 GameModel 数据模型的职责，包括管理局内核心运行时状态、关卡波次配置引用、玩家战斗属性和怪物运行时列表。

## Requirements

### Requirement: GameModel 必须管理局内核心运行时状态
系统 MUST 提供 GameModel 数据模型，注册到 ModelManager，管理当前关卡、波次推进、战斗回合、能量和手牌等运行时状态。

#### Scenario: 创建 GameModel 并注册到 ModelManager
- **WHEN** GameLogicEntry 初始化数据模型
- **THEN** GameModel MUST 被注册到 ModelManager
- **AND** GameController MUST 能通过 GetModel<GameModel>() 获取实例

### Requirement: GameModel 必须持有当前关卡和波次配置引用
GameModel MUST 持有当前关卡配置（Level）、波次列表（LevelWave）、当前波次索引和当前批次索引，支持波次和批次顺序推进。

#### Scenario: 根据关卡 ID 构建运行时上下文
- **WHEN** GameController 接收到 int 类型的关卡 ID
- **THEN** 系统 MUST 从 TbLevel 查找对应关卡配置
- **AND** 系统 MUST 从 TbLevelWave 获取该关卡的所有波次并按 Order 排序
- **AND** 系统 MUST 将当前波次索引初始化为第一个波次

#### Scenario: 战斗波次推进到下一批次
- **WHEN** 当前批次所有怪物已死亡
- **THEN** 系统 MUST 检查当前刷怪方案是否还有下一批次
- **AND** 若有下一批次，MUST 推进到下一批次并生成新怪物
- **AND** 若无下一批次，MUST 标记当前战斗波次完成

#### Scenario: 波次推进到下一波次
- **WHEN** 当前波次完成（战斗波次所有批次清完，或其他类型波次处理完毕）
- **THEN** 系统 MUST 推进到下一个波次
- **AND** 若无更多波次，MUST 标记关卡完成

### Requirement: GameModel 必须管理玩家战斗属性
GameModel MUST 持有当前能量值、最大能量、手牌上限、手牌列表和弃牌堆。

#### Scenario: 初始化玩家战斗属性
- **WHEN** 进入战斗波次
- **THEN** 系统 MUST 从 TbPlayerLevel 读取当前等级的 BaseEnergy 和 HandLimit
- **AND** 系统 MUST 将当前能量设为最大能量值
- **AND** 系统 MUST 从 TbCard 获取基础卡牌构建初始牌库

### Requirement: GameModel 必须管理怪物运行时列表
GameModel MUST 持有当前在场怪物的运行时列表，每个怪物运行时实例 MUST 包含配置引用、当前血量、当前护甲和当前意图。

#### Scenario: 根据刷怪批次生成怪物运行时实例
- **WHEN** 进入新的刷怪批次
- **THEN** 系统 MUST 读取 TbBattleWaveSpawnBatch 中当前批次的所有行
- **AND** 对每一行，MUST 根据 MonsterId 从 TbMonster 获取配置
- **AND** 系统 MUST 为每个怪物创建运行时实例，包含配置引用、初始血量、零护甲和生成的意图
- **AND** 怪物数量 MUST 等于批次配置的 Count 字段
