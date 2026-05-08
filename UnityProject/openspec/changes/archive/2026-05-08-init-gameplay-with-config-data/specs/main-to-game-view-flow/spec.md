## MODIFIED Requirements

### Requirement: 主菜单必须承接默认关卡进入请求并切换到局内流程
系统 MUST 在主菜单流程处于激活状态时承接主界面发出的默认关卡进入请求（携带 int 类型关卡标识），并通过流程状态机切换到局内流程。流程切换 MUST 使用运行中的流程状态机切换能力，不得通过重新启动流程管理器实现。

#### Scenario: 点击开始游戏后切换到局内流程
- **WHEN** 用户在主界面点击开始游戏按钮
- **THEN** 主菜单流程 MUST 接收到携带 int 类型关卡标识的进入请求
- **AND** 系统 MUST 从主菜单流程切换到局内流程

#### Scenario: 流程切换不重新启动流程状态机
- **WHEN** 默认关卡进入请求在流程状态机已经启动后被处理
- **THEN** 系统 MUST 使用流程状态切换进入局内流程
- **AND** 系统 MUST NOT 调用只能用于首次启动的流程启动接口来进入局内流程

### Requirement: 进入局内流程时必须关闭主界面并打开局内界面
系统 MUST 在离开主菜单流程时关闭 MainView，并在进入局内流程时打开 GameView。MainView 与 GameView MUST NOT 在正常进入局内路径中同时作为活动主窗口显示。GameProcedure MUST 将 int 类型关卡标识传递给 GameController 用于构建关卡运行时上下文。

#### Scenario: 主菜单流程离开时关闭 MainView
- **WHEN** 系统从主菜单流程切换到局内流程
- **THEN** 主菜单流程 MUST 请求关闭 MainView
- **AND** 主界面 MUST 不再作为正常活动主窗口显示

#### Scenario: 局内流程进入时打开 GameView 并传递关卡标识
- **WHEN** 系统进入局内流程
- **THEN** 系统 MUST 使用 EF MVC UI 打开 GameView
- **AND** GameProcedure MUST 将 int 类型关卡标识作为 userData 传递
- **AND** GameController MUST 使用关卡标识从 TbLevel 构建关卡运行时上下文

### Requirement: GameView 必须满足最小 EF MVC UI 契约
局内界面 MUST 提供可被 EF UI 系统实例化和初始化的 GameView 与 GameController 类型。接入配置数据后，GameController MUST 在 OnEnter 中根据接收的关卡标识构建完整的关卡运行时上下文。

#### Scenario: GameView 作为 EF 窗口打开
- **WHEN** 局内流程请求打开 GameView
- **THEN** EF UI 系统 MUST 能解析或动态添加 GameView 组件
- **AND** EF UI 系统 MUST 能创建并初始化 GameController

#### Scenario: GameController 根据关卡标识构建运行时上下文
- **WHEN** GameController.OnEnter 接收到有效的 int 类型关卡标识
- **THEN** GameController MUST 从 TbLevel 查找关卡配置
- **AND** GameController MUST 构建包含波次列表的运行时上下文
- **AND** 若关卡标识无效，MUST 记录错误日志并保持 GameView 可打开状态
