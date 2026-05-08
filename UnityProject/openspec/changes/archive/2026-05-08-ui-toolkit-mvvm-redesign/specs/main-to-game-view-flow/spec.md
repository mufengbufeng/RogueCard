## MODIFIED Requirements

### Requirement: 主菜单必须承接默认关卡进入请求并切换到局内流程
系统 MUST 在 MainMenuProcedure 处于激活状态时承接 MainViewModel.RequestStart() 发出的进入请求（携带 int 类型关卡标识），并通过流程状态机切换到局内流程。

#### Scenario: 点击开始游戏后切换到局内流程
- **WHEN** MainMenuProcedure 订阅了 MainViewModel.StartRequested 事件
- **AND** 用户点击开始按钮触发 MainViewModel.RequestStart()
- **THEN** MainMenuProcedure MUST 接收到事件
- **AND** 系统 MUST 从主菜单流程切换到局内流程

#### Scenario: 流程切换不重新启动流程状态机
- **WHEN** 默认关卡进入请求在流程状态机已经启动后被处理
- **THEN** 系统 MUST 使用流程状态切换进入局内流程
- **AND** 系统 MUST NOT 调用只能用于首次启动的流程启动接口

### Requirement: 进入局内流程时必须关闭主界面并打开局内界面
GameProcedure MUST 创建 GameViewModel 并调用 Navigator.NavigateToAsync("Game", gameViewModel) 打开 GameScreen。Navigator 自动关闭当前 MainMenuScreen。GameProcedure MUST 将 int 类型关卡标识传递给 GameViewModel。

#### Scenario: 局内流程进入时打开 GameScreen 并传递关卡标识
- **WHEN** 系统进入局内流程
- **THEN** GameProcedure MUST 创建 GameViewModel 并设置 LevelId
- **AND** GameProcedure MUST 调用 Navigator.NavigateToAsync("Game", gameViewModel)
- **AND** Navigator MUST 自动关闭 MainMenuScreen

#### Scenario: GameScreen 作为 MVVM Screen 打开
- **WHEN** Navigator 处理 NavigateToAsync("Game", ...)
- **THEN** MUST 加载 GameScreen 的 UXML
- **AND** MUST 创建 GameScreen 实例并添加到 Shell.ScreenLayer
- **AND** MUST 调用 Setup(gameViewModel) 和 OnShow()

### Requirement: GameProcedure 必须承接 Controller 职责创建 System 并订阅 ViewModel 命令
GameProcedure SHALL 在进入时创建 GameViewModel、CardSystem、MonsterSystem、BattleSystem、WaveSystem。GameProcedure SHALL 订阅 GameViewModel 的命令意图事件（CardUsed、EndTurnRequested）并转发到对应 System 方法。

#### Scenario: GameProcedure 创建 System 并初始化
- **WHEN** GameProcedure.OnEnter 被调用
- **THEN** GameProcedure SHALL 创建 CardSystem、MonsterSystem、BattleSystem、WaveSystem
- **AND** GameProcedure SHALL 调用各 System 的 Init 方法
- **AND** GameProcedure SHALL 调用 WaveSystem.StartLevel(levelId)

#### Scenario: GameProcedure 订阅 ViewModel 命令转发到 System
- **WHEN** GameViewModel.CardUsed 事件触发
- **THEN** GameProcedure SHALL 调用 CardSystem.Play(handIndex)
- **WHEN** GameViewModel.EndTurnRequested 事件触发
- **THEN** GameProcedure SHALL 调用 BattleSystem.EndTurn()

#### Scenario: GameProcedure 退出时清理
- **WHEN** GameProcedure.OnLeave 被调用
- **THEN** GameProcedure SHALL 取消订阅 ViewModel 的所有命令事件
- **AND** GameProcedure SHALL Dispose 所有 System
