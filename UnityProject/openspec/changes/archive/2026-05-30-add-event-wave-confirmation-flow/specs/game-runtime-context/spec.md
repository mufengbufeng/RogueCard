## MODIFIED Requirements

### Requirement: GameModel 必须持有当前关卡和波次配置引用
GameModel MUST 持有当前关卡配置（Level）、波次列表（LevelWave）、当前波次索引、当前批次索引，以及当前事件波次展示状态，支持战斗波次和事件波次的顺序推进。

#### Scenario: 根据关卡 ID 构建运行时上下文
- **WHEN** GameController 接收到 int 类型的关卡 ID
- **THEN** 系统 MUST 从 TbLevel 查找对应关卡配置
- **AND** 系统 MUST 从 TbLevelWave 获取该关卡的所有波次并按 Order 排序
- **AND** 系统 MUST 将当前波次索引初始化为第一个波次
- **AND** 系统 MUST 清空事件波次等待确认状态

#### Scenario: 战斗波次推进到下一批次
- **WHEN** 当前批次所有怪物已死亡
- **THEN** 系统 MUST 检查当前刷怪方案是否还有下一批次
- **AND** 若有下一批次，MUST 推进到下一批次并生成新怪物
- **AND** 若无下一批次，MUST 标记当前战斗波次完成

#### Scenario: 进入事件波次等待确认
- **WHEN** WaveSystem 进入 Chest 或 Shop 类型波次
- **THEN** GameModel MUST 保存当前波次类型、标题、描述和继续文案
- **AND** GameModel MUST 将事件波次等待确认状态设为 true

#### Scenario: 波次推进到下一波次
- **WHEN** 当前波次完成（战斗波次所有批次清完，或事件波次已被玩家确认）
- **THEN** 系统 MUST 清空事件波次等待确认状态
- **AND** 系统 MUST 推进到下一个波次
- **AND** 若无更多波次，MUST 标记关卡完成
