## Context

EasyFramework 的 UI 系统采用 MVC 架构（UIController / UIView / ModelBase），通过 UIManager 管理窗口生命周期。当前问题集中在 UIController 身上：它同时承担了 UI 协调、游戏逻辑、数据同步三重职责。以 GameController 为例，543 行代码中包含了战斗阶段管理、卡牌效果计算、怪物 AI、抽牌洗牌、波次推进等纯游戏逻辑，这些逻辑与 UI 展示无关却无法独立存在。

现有基础设施：
- `EventChannel<T>`：零 GC 事件系统，支持同步 Publish 和异步 Enqueue。
- `ModelBase` + `INotifyPropertyChanged`：Model 变更通知，已有属性绑定基础。
- `UIPropertyBinding<TSource, TValue>`：View 通过 BindProperty 观察源属性变更。
- `ModelBase.GetViewType()` / `GetViewInstance()`：Model 暴露只读数据接口的机制已就绪。
- `ControllerEventBinder`：自动清理事件订阅。

## Goals / Non-Goals

**Goals:**
- 将游戏逻辑从 Controller 中剥离到独立的 System 层，使 System 可独立测试。
- System 之间通过 EventChannel 事件通信，解耦逻辑模块。
- View 通过 BindProperty 响应式绑定 Model 只读数据接口，消除手动 RefreshView。
- 调整 UIManager 生命周期时序，确保 View 绑定时数据已就绪。
- Controller 精简为薄协调器（~70 行），只做 View 事件到 System 方法的转发。

**Non-Goals:**
- 不重构 MainController/MainView（91 行，问题不大，后续迁移即可）。
- 不改变 ModelManager 的注册/获取机制。
- 不引入外部响应式框架（如 UniRx），复用现有 BindProperty 机制。
- 不改变 EventChannel 的实现。
- 不做全局 ECS 架构改造。

## Decisions

### D1: 新增 UISystem 抽象基类

**选择**: 在 EFRuntime/UI 中新增 `UISystem` 抽象基类，持有 Model 读写权限和 EventHub 引用。

**替代方案**: 无独立基类，System 就是普通类。但这意味着每次都要手动传递 Model 和 EventHub，且没有统一的 Dispose 模式。

**理由**: 基类提供统一的生命周期管理（Initialize/Dispose）和事件自动清理（复用 ControllerEventBinder），减少样板代码。

### D2: System 直接读写 Model

**选择**: System 通过构造函数接收 Model 实例，直接调用 Model 的写方法。

**替代方案**: System 只发事件，Controller 负责写 Model（CQRS 风格）。增加了间接层但更解耦。

**理由**: Controller 已经足够薄，不需要再当"写 Model 的传话筒"。System 是被授权的逻辑执行者。

### D3: System 作为 Controller 的局部对象

**选择**: System 在 Controller.OnPrepareAsync 中创建，在 Controller.OnExit/OnDispose 中销毁。

**替代方案**: System 注册到全局 SystemManager，跨 UI 共享。

**理由**: 局内 System（BattleSystem 等）只在一局游戏中存在。生命周期与 Controller 绑定更自然。全局共享适合持久系统，但不适合局内战斗逻辑。

### D4: 局内事件用 Controller 级别的局部 EventHub，关键事件透传全局

**选择**: System 间通过 UIRuntimeContext 提供的局部 EventHub 通信。关键事件（关卡完成、玩家死亡）额外发布到全局 EventHub。

**替代方案 A**: 所有事件走全局 EventHub。问题：局内事件（抽牌、怪物攻击）暴露给外部 UI，增加耦合。

**替代方案 B**: 全部局部。问题：关卡完成后无法通知 Procedure 层。

**理由**: 局内事件是战斗内部细节，不应污染全局。但"游戏结束"是跨 UI 关注点，需要穿透。

### D5: 生命周期时序调整

**选择**:
```
Controller.Initialize → Controller.PrepareAsync → View.Initialize → View.Bindings → View.Open → Controller.Enter
```

**替代方案**: 保持现有顺序，在 OnBindings 中延迟绑定（OnEnter 时手动注册）。

**理由**: 从根本上修正时序，让 BindProperty 在注册时就能读到有效数据。现有 OnBindings 注册后数据为空的"绑定了个寂寞"问题彻底消除。

### D6: View 数据源限制

**选择**: View 通过 `GetModelData<TData>()` 获取 Model 暴露的只读数据接口（`GetViewType()`/`GetViewInstance()` 机制），用 BindProperty 绑定。Controller 不再直接写 View 属性。

**替代方案**: View 完全不接触 Model，Controller 在 OnEnter 中手动注册所有绑定。更严格的隔离但更冗长。

**理由**: 现有 ModelBase 的只读视图接口机制已经提供了安全的数据暴露方式。View 通过只读接口绑定，天然不能写 Model。充分利用已有基础设施。

## Risks / Trade-offs

**[BREAKING] 生命周期时序变更** → 所有现有 Controller/View 的初始化逻辑可能依赖旧行为（OnBindings 在 OnEnter 之前执行）。迁移时需逐个审查。先只迁移 GameController，验证方案可行后再推广。

**System 间事件顺序不确定** → EventChannel.Publish 是同步调用所有 handler，但 handler 注册顺序影响执行顺序。需要约定 System 初始化顺序，或用 Enqueue 延迟处理避免循环依赖。

**EventHub 引用的传递** → UIRuntimeContext 需要持有 EventHub 引用。但 EventHub 当前在 HotFix 层（GameLogicEntry.Event），而 UIRuntimeContext 在 EFRuntime 层。需要定义一个接口（如 IEventPublisher）让框架层不依赖具体实现。

**局部 EventHub 的实现** → 需要 EventHub 支持创建子作用域或实例化。如果 EventHub 是单例，需要在 Controller 级别创建独立的 EventChannel 实例用于局部通信。
