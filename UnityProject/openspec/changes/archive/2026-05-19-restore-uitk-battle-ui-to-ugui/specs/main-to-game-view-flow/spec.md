## MODIFIED Requirements

### Requirement: 进入局内流程时必须关闭主界面并打开局内界面

GameProcedure MUST 创建或获取局内运行时上下文，并调用 `IUIManager.OpenWindowAsync<GameView, GameController>("GameView", UILayer.Normal, ...)` 打开 UGUI `GameView`。MainMenuProcedure 离开时 MUST 关闭 `MainView`。GameProcedure MUST 将 int 类型关卡标识传递给局内 UI 或其 Controller 可读取的上下文。打开的 `GameView` MUST 承载完整局内战斗 UI，而不是仅显示摘要文本和临时命令按钮。

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

#### Scenario: GameView 打开后装配完整战斗 UI
- **WHEN** GameView 作为 UGUI 窗口打开并收到 `GameViewModel`
- **THEN** GameView MUST 初始化玩家状态面板、BattlePanel/RewardPanel 显隐、手牌、怪物、结束回合和奖励确认子视图
- **AND** GameView MUST NOT 仅创建摘要文本或临时按钮作为局内主 UI

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
