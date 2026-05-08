# ui-system Specification

## Purpose

定义新 MVVM UI 框架下 System 的初始化契约。System 不再依赖 UISystem 基类或 UIRuntimeContext，改为通过构造函数或 Init 方法直接接收 GameModel 和 IEventPublisher，由 Procedure 管理生命周期。

## Requirements

### Requirement: System 必须通过 Init 方法接收 ViewModel 和事件发布器
System SHALL 提供 Init 方法（或构造函数），接收 GameModel 和 IEventPublisher 作为参数。System SHALL NOT 依赖 UIRuntimeContext 或 UISystem 基类。

#### Scenario: System 通过 Init 接收依赖
- **WHEN** Procedure 创建 System 并调用 Init(gameModel, eventPublisher)
- **THEN** System SHALL 持有有效的 GameModel 和 IEventPublisher 引用
- **AND** System SHALL 能正常执行业务逻辑

#### Scenario: System 不依赖 UI 框架类型
- **WHEN** 检查 System 的代码
- **THEN** System SHALL NOT 引用 UIView、UIController、UIRuntimeContext 或任何 EF.UI 命名空间类型
