## MODIFIED Requirements

### Requirement: GameProcedure 必须承接 Controller 职责创建 System 并订阅 ViewModel 命令
GameProcedure SHALL 在进入时创建 GameModel、CardSystem、MonsterSystem、BattleSystem、WaveSystem。GameProcedure SHALL 订阅由 UGUI `GameView` / `GameController` 转发的命令意图（CardUsed、EndTurnRequested、RewardSelected 或等价确认命令）并转发到对应 System 方法或流程分支。

#### Scenario: GameProcedure 创建 System 并初始化
- **WHEN** GameProcedure.OnEnter 被调用
- **THEN** GameProcedure SHALL 创建 CardSystem、MonsterSystem、BattleSystem、WaveSystem
- **AND** GameProcedure SHALL 调用各 System 的 Init 方法
- **AND** GameProcedure SHALL 调用 WaveSystem.StartLevel(levelId)

#### Scenario: GameProcedure 订阅 UI 命令转发到 System
- **WHEN** UGUI 局内 UI 发出使用卡牌命令
- **THEN** GameProcedure SHALL 调用 CardSystem.Play(handIndex, targetIndex)
- **WHEN** UGUI 局内 UI 发出结束回合命令
- **THEN** GameProcedure SHALL 调用 BattleSystem.EndTurn()

#### Scenario: GameProcedure 转发事件波次确认命令
- **WHEN** UGUI 局内 UI 发出 RewardSelected 或等价确认命令
- **AND** 当前局内状态处于事件波次等待确认
- **THEN** GameProcedure SHALL 调用 WaveSystem 的事件波次确认入口
- **AND** SHALL NOT 切换到 MainMenuProcedure

#### Scenario: GameProcedure 退出时清理
- **WHEN** GameProcedure.OnLeave 被调用
- **THEN** GameProcedure SHALL 取消订阅所有 UI 命令事件
- **AND** GameProcedure SHALL Dispose 所有 System
- **AND** GameProcedure SHALL 调用 `IUIManager.CloseWindowAsync("GameView")`

### Requirement: 关卡完成时 GameProcedure 必须等待确认后切回主菜单流程
系统 MUST 在 `GameProcedure` 处于激活状态时订阅本地 `LevelCompleteEvent`。收到事件后，GameProcedure MUST 记录关卡完成等待确认状态，并在 UGUI 局内 UI 后续发出确认命令时通过流程状态机切换回 `MainMenuProcedure`，由 `MainMenuProcedure` 重新打开 UGUI `MainView`。`GameProcedure` MUST 在切换前正确取消所有 UI 命令与本地事件总线的订阅，并 Dispose 所有 System 与本地事件总线。

#### Scenario: 收到 LevelCompleteEvent 后等待 UI 确认
- **WHEN** `GameProcedure` 已激活并完成 `EnterAsync`
- **AND** `WaveSystem` 在本地事件总线上发布 `LevelCompleteEvent`
- **THEN** `GameProcedure` MUST 记录关卡完成等待确认状态
- **AND** MUST NOT 立即调用流程状态机切换到 `MainMenuProcedure`

#### Scenario: 关卡完成确认后切回 MainMenuProcedure
- **WHEN** `GameProcedure` 已记录关卡完成等待确认状态
- **AND** UGUI 局内 UI 发出 RewardSelected 或等价确认命令
- **AND** 当前局内状态不处于事件波次等待确认
- **THEN** `GameProcedure` MUST 调用流程状态机切换到 `MainMenuProcedure`
- **AND** `GameProcedure.OnLeave` MUST 被触发并执行 `Cleanup`

#### Scenario: Cleanup 必须取消 LevelCompleteEvent 订阅
- **WHEN** `GameProcedure.Cleanup` 被调用（来自正常切换、关卡完成或 shutdown）
- **THEN** 系统 MUST 调用 `LocalEventBus.GetChannel<LevelCompleteEvent>().Unsubscribe(...)` 取消订阅
- **AND** 系统 MUST 调用 `Cleanup` 后即使再次发布 `LevelCompleteEvent` 也不会触发已销毁流程的回调

#### Scenario: 切回主菜单后 MainView 重新打开
- **WHEN** 流程切换到 `MainMenuProcedure`
- **THEN** `MainMenuProcedure.OnEnter` 的现有流程 MUST 重新打开 UGUI `MainView`
- **AND** `GameProcedure.OnLeave` MUST 关闭 `GameView`
