# single-main-ui-entry Specification

## Purpose

定义项目启动后直接进入唯一主界面入口的最小默认流程，确保示例入口、登录流程和示例玩法不再参与默认启动链路。

## Requirements

### Requirement: 项目启动后仅显示单一主界面入口
系统启动并完成热更新初始化后，必须直接进入唯一的主界面入口。该入口必须通过 Navigator.NavigateToAsync 打开 MainMenuScreen，且不得再要求用户先经过独立的入口页、登录页或其他中转流程。

#### Scenario: 启动后直接进入主界面
- **WHEN** 用户启动项目并完成 `GameEntry` 与 `GameLogicEntry` 初始化
- **THEN** 系统必须通过 Navigator.NavigateToAsync("MainMenu", mainViewModel) 打开主界面
- **AND** 屏幕上不得同时存在独立的 `EntryView` 入口窗口

### Requirement: 主界面入口不得依赖示例玩法模块
作为项目最小骨架的主界面入口，必须能够在未注册体力、关卡、战斗、敌人生成或玩法场景模块时正常显示。主界面控制器不得要求这些示例模块存在才可完成初始化。

#### Scenario: 示例玩法模块被移除后仍可打开主界面
- **WHEN** 体力、关卡和玩法相关模块未参与启动注册
- **THEN** 主界面必须仍可成功打开并完成初始化
- **AND** 系统不得因为缺少示例玩法模块而阻断主界面显示

### Requirement: 示例玩法流程不得再出现在默认启动路径中
默认启动路径必须只保留支撑主界面展示所需的最小流程。登录流程、示例战斗流程和其他为框架演示服务的中转流程不得再出现在默认进入路径中。

#### Scenario: 默认流程链被裁剪为主界面最小路径
- **WHEN** 系统初始化流程完成并准备进入首个可交互界面
- **THEN** 后续流程必须只服务于打开主界面入口
- **AND** 默认路径不得切换到示例战斗玩法流程

### Requirement: 被移除的示例入口和玩法代码不得残留活跃引用
当示例入口页、示例玩法流程或相关模块被清理后，仓库中不得保留从启动链、UI 入口链或默认测试链路可达的活跃引用。

#### Scenario: 清理后不存在默认入口到示例逻辑的活跃引用
- **WHEN** 开发者从默认启动链、主界面入口和相关测试入口检查引用关系
- **THEN** 不得再发现指向 `EntryView`、示例玩法流程或其核心模块的活跃默认引用

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
