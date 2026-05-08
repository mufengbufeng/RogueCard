## MODIFIED Requirements

### Requirement: 项目启动后仅显示单一主界面入口
系统启动并完成热更新初始化后，必须直接进入唯一的主界面入口。该入口必须通过 Navigator.NavigateToAsync 打开 MainMenuScreen，且不得再要求用户先经过独立的入口页、登录页或其他中转流程。

#### Scenario: 启动后直接进入主界面
- **WHEN** 用户启动项目并完成 `GameEntry` 与 `GameLogicEntry` 初始化
- **THEN** 系统必须通过 Navigator.NavigateToAsync("MainMenu", mainViewModel) 打开主界面
- **AND** 屏幕上不得同时存在独立的 `EntryView` 入口窗口

### Requirement: 主界面必须展示默认关卡入口信息
主界面入口 MUST 在默认可交互状态下展示一个可开始的默认关卡信息。默认关卡标识 MUST 从 TbLevel 配置表读取 IsDefault=true 的记录。MainViewModel 的 StatusText、LevelName、LevelDesc 属性 MUST 由 Procedure 在创建 ViewModel 时从配置表填充。

#### Scenario: 打开主界面后显示配置表中的默认关卡
- **WHEN** MainMenuProcedure 创建 MainViewModel 并从 TbLevel 读取默认关卡
- **THEN** MainViewModel.LevelName.Value SHALL 为关卡配置中的 Name
- **AND** MainViewModel.LevelDesc.Value SHALL 为关卡配置中的 Desc
- **AND** MainViewModel.CanStart.Value SHALL 为 true

#### Scenario: 配置表中无默认关卡时回退到安全状态
- **WHEN** TbLevel 中不存在 IsDefault=true 的记录
- **THEN** 系统 MUST 记录警告日志
- **AND** MainViewModel MUST 展示占位提示信息
- **AND** 系统 MUST NOT 因缺少默认关卡配置而阻断主界面显示

### Requirement: 主界面开始按钮必须发起默认关卡进入请求
主界面 MainMenuScreen MUST 将开始按钮点击转发为 MainViewModel.RequestStart() 命令意图。MainMenuProcedure MUST 订阅此意图事件并执行 Navigator.NavigateToAsync("Game", gameViewModel) 切换到局内界面。

#### Scenario: 点击开始按钮后导航到局内界面
- **WHEN** 用户在主界面点击开始按钮
- **THEN** MainMenuScreen SHALL 调用 MainViewModel.RequestStart()
- **AND** MainMenuProcedure SHALL 收到 StartRequested 事件
- **AND** Procedure SHALL 调用 Navigator.NavigateToAsync("Game", gameViewModel)
