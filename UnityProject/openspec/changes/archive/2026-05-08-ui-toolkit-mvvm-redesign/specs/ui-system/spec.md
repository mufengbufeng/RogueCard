## REMOVED Requirements

### Requirement: UISystem 基类提供统一生命周期
**Reason**: System 初始化不再通过 UIRuntimeContext 注入，改为直接接收 ViewModel 和 IEventPublisher
**Migration**: System 构造函数或 Init 方法直接接收所需依赖，不再依赖 UISystem 基类

### Requirement: UISystem 提供 GetModel 强类型访问
**Reason**: System 直接持有 ViewModel 引用而非 ModelBase，不需要 GetModel<TModel>() 便利方法
**Migration**: System 通过构造函数或 Init 方法接收 ViewModel

### Requirement: UISystem 提供事件绑定自动清理
**Reason**: ControllerEventBinder 随 UIController 一起移除，System 的事件清理由 Procedure 管理
**Migration**: Procedure 在退出时负责清理 System 的事件订阅

### Requirement: UIRuntimeContext 提供事件发布器
**Reason**: UIRuntimeContext 随 UIManager 一起移除
**Migration**: System 通过构造函数接收 IEventPublisher

### Requirement: UIManager 生命周期时序调整
**Reason**: UIManager 被 Navigator + Shell 替代，生命周期由 Navigator 驱动
**Migration**: Navigator.NavigateToAsync 内部按 LoadContent → Setup → OnShow 顺序调用

### Requirement: UIController 移除直接写 View 的能力
**Reason**: UIController 整体移除，Screen 直接绑定 ViewModel
**Migration**: Screen.OnSetup() 中订阅 ReactiveProperty.Changed 更新 VisualElement

## ADDED Requirements

### Requirement: System 必须通过 Init 方法接收 ViewModel 和事件发布器
System SHALL 提供 Init 方法（或构造函数），接收 GameModel 和 IEventPublisher 作为参数。System SHALL NOT 依赖 UIRuntimeContext 或 UISystem 基类。

#### Scenario: System 通过 Init 接收依赖
- **WHEN** Procedure 创建 System 并调用 Init(gameModel, eventPublisher)
- **THEN** System SHALL 持有有效的 GameModel 和 IEventPublisher 引用
- **AND** System SHALL 能正常执行业务逻辑

#### Scenario: System 不依赖 UI 框架类型
- **WHEN** 检查 System 的代码
- **THEN** System SHALL NOT 引用 UIView、UIController、UIRuntimeContext 或任何 EF.UI 命名空间类型
