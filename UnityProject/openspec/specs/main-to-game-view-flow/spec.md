# main-to-game-view-flow Specification

## Purpose

定义主菜单开始游戏后进入局内 UI 骨架的最小流程，确保主菜单请求由流程状态机承接并打开 `GameView`。

## Requirements

### Requirement: 主菜单必须承接默认关卡进入请求并切换到局内流程
系统 MUST 在主菜单流程处于激活状态时承接主界面发出的默认关卡进入请求，并通过流程状态机切换到局内流程。流程切换 MUST 使用运行中的流程状态机切换能力，不得通过重新启动流程管理器实现。

#### Scenario: 点击开始游戏后切换到局内流程
- **WHEN** 用户在主界面点击开始游戏按钮
- **THEN** 主菜单流程 MUST 接收到默认关卡进入请求
- **AND** 系统 MUST 从主菜单流程切换到局内流程

#### Scenario: 流程切换不重新启动流程状态机
- **WHEN** 默认关卡进入请求在流程状态机已经启动后被处理
- **THEN** 系统 MUST 使用流程状态切换进入局内流程
- **AND** 系统 MUST NOT 调用只能用于首次启动的流程启动接口来进入局内流程

### Requirement: 进入局内流程时必须关闭主界面并打开局内界面
系统 MUST 在离开主菜单流程时关闭 `MainView`，并在进入局内流程时打开 `GameView`。`MainView` 与 `GameView` MUST NOT 在正常进入局内路径中同时作为活动主窗口显示。

#### Scenario: 主菜单流程离开时关闭 MainView
- **WHEN** 系统从主菜单流程切换到局内流程
- **THEN** 主菜单流程 MUST 请求关闭 `MainView`
- **AND** 主界面 MUST 不再作为正常活动主窗口显示

#### Scenario: 局内流程进入时打开 GameView
- **WHEN** 系统进入局内流程
- **THEN** 系统 MUST 使用 EF MVC UI 打开 `GameView`
- **AND** `GameView` MUST 使用 `Assets/AssetRaw/UI/Game/GameView.prefab` 对应的资源地址加载

### Requirement: GameView 必须满足最小 EF MVC UI 契约
局内界面 MUST 提供可被 EF UI 系统实例化和初始化的 `GameView` 与 `GameController` 类型。该最小契约只要求局内窗口能够成功打开，不要求接入战斗数据、卡牌输入或波次推进。

#### Scenario: GameView 作为 EF 窗口打开
- **WHEN** 局内流程请求打开 `GameView`
- **THEN** EF UI 系统 MUST 能解析或动态添加 `GameView` 组件
- **AND** EF UI 系统 MUST 能创建并初始化 `GameController`

#### Scenario: 缺少战斗数据时仍可打开局内 UI
- **WHEN** 战斗规则、波次推进和卡牌数据同步尚未实现
- **THEN** `GameView` MUST 仍可作为最小局内窗口打开
- **AND** 系统 MUST NOT 因缺少完整战斗运行时上下文而阻断窗口打开
