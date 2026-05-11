## MODIFIED Requirements

### Requirement: 项目启动后仅显示单一主界面入口
系统启动并完成热更新初始化后 SHALL 直接进入唯一的主界面入口。该入口 MUST 通过 `Navigator.OpenAsync<MainView>(mainViewModel)`（或等价的字符串重载 `Navigator.OpenAsync("MainView", mainViewModel)`）打开 `MainView`，且 SHALL NOT 再要求用户先经过独立的入口页、登录页或其他中转流程。

#### Scenario: 启动后直接进入主界面
- **WHEN** 用户启动项目并完成 `GameEntry` 与 `GameLogicEntry` 初始化
- **THEN** 系统必须通过 `Navigator.OpenAsync<MainView>(mainViewModel)` 打开主界面
- **AND** 屏幕上不得同时存在独立的 `EntryView` 入口窗口

### Requirement: 主界面开始按钮必须发起默认关卡进入请求
主界面 `MainView` MUST 将开始按钮点击转发为 MainViewModel.RequestStart() 命令意图。MainMenuProcedure MUST 订阅此意图事件并执行 `Navigator.OpenAsync<GameView>(gameViewModel)` 切换到局内界面。

#### Scenario: 点击开始按钮后导航到局内界面
- **WHEN** 用户在主界面点击开始按钮
- **THEN** `MainView` SHALL 调用 MainViewModel.RequestStart()
- **AND** MainMenuProcedure SHALL 收到 StartRequested 事件
- **AND** Procedure SHALL 调用 `Navigator.OpenAsync<GameView>(gameViewModel)`
