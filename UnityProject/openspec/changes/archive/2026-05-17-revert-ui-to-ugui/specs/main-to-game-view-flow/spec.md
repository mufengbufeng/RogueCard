## MODIFIED Requirements

### Requirement: 主菜单必须承接默认关卡进入请求并切换到局内流程
系统 MUST 在 MainMenuProcedure 处于激活状态时承接 UGUI 主界面开始按钮发出的进入请求（携带 int 类型关卡标识），并通过流程状态机切换到局内流程。

#### Scenario: 点击开始游戏后切换到局内流程
- **WHEN** MainMenuProcedure 订阅了默认关卡进入请求
- **AND** 用户点击 UGUI `MainView` 的开始按钮
- **THEN** MainMenuProcedure MUST 接收到进入请求
- **AND** 系统 MUST 从主菜单流程切换到局内流程

#### Scenario: 流程切换不重新启动流程状态机
- **WHEN** 默认关卡进入请求在流程状态机已经启动后被处理
- **THEN** 系统 MUST 使用流程状态切换进入局内流程
- **AND** 系统 MUST NOT 调用只能用于首次启动的流程启动接口

### Requirement: 进入局内流程时必须关闭主界面并打开局内界面
GameProcedure MUST 创建或获取局内运行时上下文，并调用 `IUIManager.OpenWindowAsync<GameView, GameController>("GameView", UILayer.Normal, ...)` 打开 UGUI `GameView`。MainMenuProcedure 离开时 MUST 关闭 `MainView`。GameProcedure MUST 将 int 类型关卡标识传递给局内 UI 或其 Controller 可读取的上下文。

#### Scenario: 局内流程进入时打开 GameView 并传递关卡标识
- **WHEN** 系统进入局内流程
- **THEN** GameProcedure MUST 设置待进入关卡标识
- **AND** GameProcedure MUST 调用 `IUIManager.OpenWindowAsync<GameView, GameController>("GameView", UILayer.Normal, ...)`
- **AND** MainMenuProcedure.OnLeave MUST 调用 `IUIManager.CloseWindowAsync("MainView")`

#### Scenario: GameView 作为 UGUI 窗口打开
- **WHEN** UIManager 处理 `OpenWindowAsync<GameView, GameController>("GameView", ...)`
- **THEN** MUST 加载 GameView Prefab
- **AND** MUST 创建或获取 GameView MonoBehaviour
- **AND** MUST 初始化 GameController 并调用窗口生命周期

### Requirement: GameProcedure 必须承接 Controller 职责创建 System 并订阅 ViewModel 命令
GameProcedure SHALL 在进入时创建 GameModel、CardSystem、MonsterSystem、BattleSystem、WaveSystem。GameProcedure SHALL 订阅由 UGUI `GameView` / `GameController` 转发的命令意图（CardUsed、EndTurnRequested）并转发到对应 System 方法。

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

#### Scenario: GameProcedure 退出时清理
- **WHEN** GameProcedure.OnLeave 被调用
- **THEN** GameProcedure SHALL 取消订阅所有 UI 命令事件
- **AND** GameProcedure SHALL Dispose 所有 System
- **AND** GameProcedure SHALL 调用 `IUIManager.CloseWindowAsync("GameView")`

### Requirement: 关卡完成时 GameProcedure 必须自动切回主菜单流程
系统 MUST 在 `GameProcedure` 处于激活状态时订阅本地 `LevelCompleteEvent`，收到事件后自动通过流程状态机切换回 `MainMenuProcedure`，由 `MainMenuProcedure` 重新打开 UGUI `MainView`。`GameProcedure` MUST 在切换前正确取消所有 UI 命令与本地事件总线的订阅，并 Dispose 所有 System 与本地事件总线。

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
- **THEN** `MainMenuProcedure.OnEnter` 的现有流程 MUST 重新打开 UGUI `MainView`
- **AND** `GameProcedure.OnLeave` MUST 关闭 `GameView`
