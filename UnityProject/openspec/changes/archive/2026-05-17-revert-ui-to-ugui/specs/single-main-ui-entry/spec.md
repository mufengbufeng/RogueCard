## MODIFIED Requirements

### Requirement: 项目启动后仅显示单一主界面入口
系统启动并完成热更新初始化后 SHALL 直接进入唯一的主界面入口。该入口 MUST 通过 `IUIManager.OpenWindowAsync<MainView, MainController>("MainView", UILayer.Normal, ...)` 打开 UGUI `MainView`，且 SHALL NOT 再要求用户先经过独立的入口页、登录页或其他中转流程。

#### Scenario: 启动后直接进入主界面
- **WHEN** 用户启动项目并完成 `GameEntry` 与 `GameLogicEntry` 初始化
- **THEN** 系统 MUST 通过 `IUIManager.OpenWindowAsync<MainView, MainController>("MainView", UILayer.Normal, ...)` 打开主界面
- **AND** 屏幕上 MUST NOT 同时存在独立的 `EntryView` 入口窗口

### Requirement: 主界面开始按钮必须发起默认关卡进入请求
主界面 `MainView` MUST 将 UGUI 开始按钮点击转发为主界面进入关卡请求。`MainController` SHALL 将按钮事件发布为默认关卡进入意图，`MainMenuProcedure` SHALL 承接该意图并通过流程状态机切换到 `GameProcedure`。

#### Scenario: 点击开始按钮后切换到局内流程
- **WHEN** 用户在主界面点击 UGUI 开始按钮
- **THEN** `MainView` SHALL 发出开始游戏事件
- **AND** `MainController` SHALL 发布默认关卡进入请求或调用等价命令入口
- **AND** `MainMenuProcedure` SHALL 通过流程状态机切换到 `GameProcedure`
- **AND** `GameProcedure` SHALL 负责打开 `GameView`
