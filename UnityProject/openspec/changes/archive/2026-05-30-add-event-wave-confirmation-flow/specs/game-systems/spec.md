## MODIFIED Requirements

### Requirement: WaveSystem 管理关卡和波次推进
WaveSystem SHALL 从配置表加载关卡数据，按 Order 排序波次，按序推进波次。波次类型为 Battle 时 SHALL 调用 BattleSystem 初始化战斗。波次类型为 Chest 或 Shop 时 SHALL 进入事件波次等待确认状态，向 UI 暴露当前波次展示文案，并且 SHALL NOT 自动推进到下一波次。所有波次完成时 SHALL 发布 LevelCompleteEvent。

#### Scenario: 启动关卡
- **WHEN** 调用 WaveSystem.StartLevel(levelId) 传入有效关卡标识
- **THEN** SHALL 从配置表加载关卡数据，按 Order 排序波次，进入第一个波次

#### Scenario: 战斗波次进入
- **WHEN** 当前波次类型为 Battle 且有有效 PayloadId
- **THEN** SHALL 加载刷怪方案，调用 BattleSystem 初始化战斗
- **AND** SHALL NOT 设置事件波次等待确认状态

#### Scenario: 非战斗波次等待确认
- **WHEN** 当前波次类型为 Chest 或 Shop
- **THEN** SHALL 将当前波次标题、描述和继续文案写入局内运行时状态
- **AND** SHALL 设置事件波次等待确认状态
- **AND** SHALL NOT 调用 BattleSystem 初始化战斗
- **AND** SHALL NOT 自动推进到下一波次

#### Scenario: 确认非战斗波次后推进
- **WHEN** 当前处于事件波次等待确认状态
- **AND** 玩家通过局内 UI 发出确认命令
- **THEN** WaveSystem SHALL 清除事件波次等待确认状态
- **AND** SHALL 推进到下一波次

#### Scenario: 关卡完成
- **WHEN** 最后一个波次完成
- **THEN** SHALL 发布 LevelCompleteEvent
