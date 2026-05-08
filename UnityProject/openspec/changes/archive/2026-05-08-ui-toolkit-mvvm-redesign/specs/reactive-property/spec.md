## ADDED Requirements

### Requirement: ReactiveProperty 必须在值变化时通知监听者
ReactiveProperty<T> SHALL 存储 T 类型值，当 Value 被设置为与当前值不同的新值时 SHALL 触发 Changed 事件。值未变化时 SHALL NOT 触发事件。

#### Scenario: 值变化时触发 Changed
- **WHEN** ReactiveProperty<int> 当前值为 5，设置 Value = 10
- **THEN** Changed 事件 SHALL 被触发一次，参数为 10

#### Scenario: 值未变化时不触发 Changed
- **WHEN** ReactiveProperty<int> 当前值为 5，设置 Value = 5
- **THEN** Changed 事件 SHALL NOT 被触发

#### Scenario: 初始值通过构造函数设置
- **WHEN** 创建 ReactiveProperty<string> 初始值为 "hello"
- **THEN** Value SHALL 返回 "hello"

### Requirement: ReactiveProperty 必须支持清理所有监听者
ReactivePropertyBase SHALL 提供 ClearListeners 方法，调用后 SHALL 将所有 Changed 委托置为 null。ReactiveProperty<T> SHALL 继承此方法。

#### Scenario: ClearListeners 后不再触发回调
- **WHEN** 订阅了 Changed 事件后调用 ClearListeners()
- **AND** 随后设置新的 Value
- **THEN** Changed 事件 SHALL NOT 被触发

### Requirement: ViewModelBase 必须自动追踪所有 ReactiveProperty
ViewModelBase SHALL 提供 Prop<T>(initialValue) 工厂方法，每次调用 SHALL 创建 ReactiveProperty<T> 实例并将其加入内部追踪列表。ViewModelBase.Dispose() SHALL 遍历追踪列表并调用每个属性的 ClearListeners()。

#### Scenario: Prop 工厂方法创建并追踪属性
- **WHEN** ViewModel 子类通过 Prop<string>("初始值") 创建属性
- **THEN** 返回的 ReactiveProperty<string> 的 Value SHALL 为 "初始值"
- **AND** 该属性 SHALL 被加入 ViewModelBase 的追踪列表

#### Scenario: Dispose 清理所有追踪属性的监听者
- **WHEN** ViewModel 创建了 3 个 ReactiveProperty 并被 Screen 订阅了 Changed
- **AND** 调用 ViewModel.Dispose()
- **THEN** 所有 3 个 ReactiveProperty 的 Changed SHALL 被置为 null
- **AND** Screen 端不再收到任何回调

#### Scenario: Dispose 是幂等的
- **WHEN** 对同一个 ViewModel 实例多次调用 Dispose()
- **THEN** 第一次调用 SHALL 清理所有属性，后续调用 SHALL 不抛异常
