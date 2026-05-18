## MODIFIED Requirements

### Requirement: 定义 LevelCompleteEvent
系统 SHALL 定义 LevelCompleteEvent 只读结构体，包含 LevelId（int）字段。当所有波次完成时 SHALL 仅在战斗局部事件总线上发布此事件，由 `GameProcedure` 订阅并切回 `MainMenuProcedure`。系统 MUST NOT 在发布 `LevelCompleteEvent` 的同时向全局 `StartLevelRequestedEvent` 发布等价事件，以避免主菜单流程将"关卡完成"误判为"再次启动同一关卡"。

#### Scenario: 关卡完成在本地事件总线发布事件
- **WHEN** 所有关卡波次全部完成
- **THEN** SHALL 发布 LevelCompleteEvent 到 `GameProcedure` 创建的本地事件总线

#### Scenario: 关卡完成不发布 StartLevelRequestedEvent
- **WHEN** 所有关卡波次全部完成
- **THEN** 系统 MUST NOT 在全局事件总线上发布 `StartLevelRequestedEvent`
- **AND** 主菜单流程 MUST NOT 被关卡完成事件直接触发为"开始新关卡"
