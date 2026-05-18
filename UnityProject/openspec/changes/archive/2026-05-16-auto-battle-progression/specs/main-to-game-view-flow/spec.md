## ADDED Requirements

### Requirement: 关卡完成时 GameProcedure 必须自动切回主菜单流程
系统 MUST 在 `GameProcedure` 处于激活状态时订阅本地 `LevelCompleteEvent`，收到事件后自动通过流程状态机切换回 `MainMenuProcedure`，由 `MainMenuProcedure` 重新打开 `MainView`。`GameProcedure` MUST 在切换前正确取消所有 ViewModel 与本地事件总线的订阅，并 Dispose 所有 System 与本地事件总线。

#### Scenario: 收到 LevelCompleteEvent 后切回 MainMenuProcedure
- **WHEN** `GameProcedure` 已激活并完成 `EnterAsync`
- **AND** `WaveSystem` 在本地事件总线上发布 `LevelCompleteEvent`
- **THEN** `GameProcedure` MUST 调用流程状态机切换到 `MainMenuProcedure`
- **AND** `GameProcedure.OnLeave` MUST 被触发并执行 `Cleanup`

#### Scenario: Cleanup 必须取消 LevelCompleteEvent 订阅
- **WHEN** `GameProcedure.Cleanup` 被调用（来自正常切换、关卡完成或 shutdown）
- **THEN** 系统 MUST 调用 `LocalEventBus.GetChannel<LevelCompleteEvent>().Unsubscribe(...)` 取消订阅
- **AND** 系统 MUST 调用 `Cleanup` 后即使再次发布 `LevelCompleteEvent` 也不会触发已销毁流程的回调

#### Scenario: 切回主菜单后 MainView 重新打开
- **WHEN** 流程切换到 `MainMenuProcedure`
- **THEN** `MainMenuProcedure.OnEnter` 的现有流程 MUST 重新打开 `MainView`
- **AND** Navigator MUST 自动关闭仍在 ScreenLayer 的 `GameView`
