## ADDED Requirements

### Requirement: UISystem 基类提供统一生命周期
UISystem 抽象基类 SHALL 提供 OnInitialize 和 OnDispose 两个虚方法，供派生类覆写。InternalInitialize 方法 SHALL 接收 ModelBase 和事件发布器接口，并在调用 OnInitialize 前完成赋值。

#### Scenario: System 初始化成功
- **WHEN** 调用 InternalInitialize(model, eventPublisher) 传入有效的 Model 和事件发布器
- **THEN** Model 和事件发布器属性 SHALL 被正确赋值，OnInitialize SHALL 被调用

#### Scenario: System 初始化参数校验
- **WHEN** 调用 InternalInitialize 传入 null 的 model 或 eventPublisher
- **THEN** SHALL 抛出 ArgumentNullException

### Requirement: UISystem 提供 GetModel 强类型访问
UISystem SHALL 提供 `GetModel<TModel>()` 便利方法，将持有的 ModelBase 转换为强类型 Model 返回。

#### Scenario: 获取强类型 Model
- **WHEN** System 持有 GameModel 实例，调用 GetModel<GameModel>()
- **THEN** SHALL 返回正确的 GameModel 实例

### Requirement: UISystem 提供事件绑定自动清理
UISystem SHALL 通过 ControllerEventBinder 提供事件订阅能力。Dispose 时 SHALL 自动清理所有通过 EventBinder 注册的事件绑定。

#### Scenario: Dispose 清理事件绑定
- **WHEN** System 通过 EventBinder 订阅了事件后调用 Dispose
- **THEN** 所有事件绑定 SHALL 被自动取消订阅，EventBinder SHALL 被释放

### Requirement: UISystem 实现 IDisposable
UISystem SHALL 实现 IDisposable 接口。Dispose SHALL 是幂等的（多次调用安全）。Dispose 后 Model 和事件发布器引用 SHALL 被置为 null。

#### Scenario: 幂等 Dispose
- **WHEN** 对同一个 System 实例多次调用 Dispose
- **THEN** 第一次调用 SHALL 执行清理，后续调用 SHALL 直接返回不抛异常

### Requirement: UIRuntimeContext 提供事件发布器
UIRuntimeContext SHALL 新增 IEventPublisher 类型的属性，用于 UISystem 的事件通信。构造函数 SHALL 接受 IEventPublisher 参数。

#### Scenario: Context 持有事件发布器
- **WHEN** UIManager 创建 UIRuntimeContext 时传入事件发布器
- **THEN** Context.EventPublisher SHALL 返回该发布器实例

### Requirement: UIManager 生命周期时序调整
UIManager.CreateOrReuseInstanceAsync SHALL 按以下顺序调用：
1. Controller.InternalInitialize
2. Controller.InternalPrepareAsync
3. View.InternalInitialize（包含 OnInitialize）
4. View.InternalBindings（包含 OnBindings）
5. View.InternalOpen
6. Controller.InternalEnter

#### Scenario: 时序保证数据就绪
- **WHEN** Controller.OnPrepareAsync 中初始化了 Model 数据
- **THEN** View.OnBindings 执行时 SHALL 能通过 GetModelData 读到有效数据

#### Scenario: 绑定时数据有效
- **WHEN** View.OnBindings 中调用 BindProperty 注册绑定
- **THEN** 绑定 SHALL 立即使用当前 Model 数据触发一次 setter 回调

### Requirement: UIController 移除直接写 View 的能力
UIController 不 SHALL 提供直接设置 View 展示属性的机制。Controller 的职责限定为：获取 Model、获取 View、绑定 View 用户事件到 System 调用、管理 System 生命周期。

#### Scenario: Controller 只做事件转发
- **WHEN** View 触发 OnCardUsed 事件
- **THEN** Controller SHALL 将调用转发到对应 System 的方法，不直接修改 View 状态
