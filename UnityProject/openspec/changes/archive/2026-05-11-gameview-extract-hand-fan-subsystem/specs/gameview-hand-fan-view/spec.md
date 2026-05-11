## ADDED Requirements

### Requirement: HandFanView 必须订阅 IHandContext 切片接口

`HandFanView` SHALL 通过构造函数接收 `IHandContext` 切片实例，SHALL NOT 直接引用 `GameViewModel` 的非手牌字段。`IHandContext` SHALL 暴露：`Hand`、`Phase` 两个 `ReactiveProperty<>`；`UseCard(int handIdx, int targetIdx = -1)` 命令；`CardPlayFailed` 事件。

#### Scenario: 通过切片构造

- **WHEN** `new HandFanView(handFan, dropZone, previewLayer, context, cardItemTemplate, options)`
- **THEN** SHALL 成功构造，订阅 `context.Hand.Changed`，触发首次 `RefreshCards()`

### Requirement: HandFanView 必须按 Hand 列表全量重建 CardItemView

`HandFanView` SHALL 在 `IHandContext.Hand.Value` 变化时清空 `_cardItems` 列表中所有 `CardItemView`（调用各自 `Dispose()`）、清空 `hand-fan` 容器，并按列表顺序为每张 `CardRuntime` 创建一个 `CardItemView`，加入 `hand-fan` 容器与 `_cardItems` 列表，最后调 `FanLayoutCalc.ComputeSlot` 与 `IDragSurface.ApplyFanTransform` 应用 N 张紧凑布局，并 `SyncSiblingOrder()`。

#### Scenario: 手牌从 5 张变为 4 张时整体重建

- **WHEN** `Hand.Value` 从 5 张变为 4 张
- **THEN** `hand-fan` 子元素数 SHALL 为 4（不含占位卡 / ghost）
- **AND** 所有 `CardItemView` SHALL 重新计算 transform

#### Scenario: 手牌变化时清掉残留交互态

- **WHEN** 当前处于 `Dragging` 状态且 `Hand.Value` 被外部修改
- **THEN** `HandFanView` SHALL 强制 `CardDragController` 退出拖拽（销毁 ghost、占位卡、还原 opacity / pickingMode）
- **AND** 重新构建后 SHALL 处于 `Idle` 状态

#### Scenario: 预览态在手牌变化时退出

- **WHEN** 当前处于 `Previewing` 状态且 `Hand.Value` 被修改
- **THEN** `CardPreviewController` SHALL 退出预览（销毁克隆卡）

### Requirement: HandFanView 必须维护 sibling 顺序与列表顺序一致

`HandFanView` SHALL 在 `_cardItems` 列表顺序变化时（如拖拽 reorder）调用 `SyncSiblingOrder()`：按列表顺序对每个 `CardItemView.Root` 依次 `BringToFront()`，最终 sibling 顺序与列表顺序一致（c0 在底、c[N-1] 在顶）。占位卡（若存在）SHALL 始终在最上。

#### Scenario: reorder 后 sibling 顺序同步

- **WHEN** 拖拽 reorder 后 `_cardItems` 顺序变化
- **THEN** `hand-fan` 内 sibling 顺序 SHALL 与 `_cardItems` 列表顺序一致
- **AND** 若有占位卡，SHALL 在最上层

#### Scenario: 频繁 PointerMove 期间 SHALL NOT 频繁 BringToFront

- **WHEN** 拖拽中 `PointerMoveEvent` 持续触发 `RecomputeHandLayout`
- **THEN** SHALL NOT 在每次 `RecomputeHandLayout` 中调用 `SyncSiblingOrder`
- **AND** `SyncSiblingOrder` SHALL 仅在 `RefreshCards` 末尾、`ReorderCardItem` 末尾调用

### Requirement: HandFanView 必须响应 hand-fan 几何变化

`HandFanView` SHALL 注册 `_handFan` 的 `GeometryChangedEvent` 回调，回调 SHALL 在拖拽中按当前 `DragMode` 重排（`CardDragController.OnGeometryChanged()`），非拖拽中按 N 张紧凑布局重排。`HandFanView` SHALL 持有委托引用以便对称解绑，SHALL 在 `Dispose` 中解绑。

#### Scenario: 容器尺寸变化时重排

- **WHEN** `hand-fan` 的 `resolvedStyle.width` 从 800 变为 1000
- **THEN** 所有卡 SHALL 重新 `ApplyFanTransform`

#### Scenario: 拖拽中尺寸变化按当前子态重排

- **WHEN** 当前 `DragMode = InsertSlot` 且 `hand-fan` 几何变化
- **THEN** 重排 SHALL 仍按 `InsertSlot` 子态（保留空槽 + 占位卡位置）

### Requirement: HandFanView 必须暴露上层事件

`HandFanView` SHALL 暴露三个事件供 `BattlePanelView`（或当前 `GameView`）订阅：

- `event Action<int> CardClicked` —— 单击（位移 ≤ `DragThreshold`）某卡的 PointerUp 时触发
- `event Action<int, bool> CardDroppedOnZone` —— 在 drop-zone 内松手；`bool needsManualTarget` 取自 `Hand[handIdx].Config.TargetMode == SingleManual`
- `event Action<int> CardDragCancelled` —— 中间地带松手或 `PointerCaptureOut` 中途丢失时触发

#### Scenario: AutoTarget 卡拖到 drop-zone 触发 CardDroppedOnZone(needsManualTarget=false)

- **WHEN** 拖拽一张 `TargetMode != SingleManual` 的卡并在 `drop-zone` 内松手
- **THEN** `CardDroppedOnZone` SHALL 触发，参数为 `(handIdx, false)`

#### Scenario: SingleManual 卡拖到 drop-zone 触发 needsManualTarget=true

- **WHEN** 拖拽一张 `TargetMode == SingleManual` 的卡并在 `drop-zone` 内松手
- **THEN** `CardDroppedOnZone` SHALL 触发，参数为 `(handIdx, true)`
- **AND** ghost SHALL 保留在屏幕上（由上层 `TargetSelector` 接管）

#### Scenario: 中间地带松手触发 CardDragCancelled

- **WHEN** 拖拽时既不在 `hand-fan` 内也不在 `drop-zone` 内松手
- **THEN** `CardDragCancelled` SHALL 触发
- **AND** ghost SHALL 立即销毁
- **AND** 其他卡 SHALL 协同回弹到 N 张布局

### Requirement: HandFanView 必须支持显式 Dispose

`HandFanView` SHALL 实现 `IDisposable`，`Dispose()` SHALL：

- 解绑 `IHandContext.Hand.Changed`
- 解绑 `_handFan` 的 `GeometryChangedEvent`
- 调 `CardDragController.Dispose()` 与 `CardPreviewController.Dispose()`
- 释放所有 `CardItemView`
- 清空内部列表与字段引用
- 幂等

#### Scenario: Dispose 后 Hand 变化不再触发刷新

- **WHEN** `view.Dispose()` 后 `context.Hand.Value` 被修改
- **THEN** `_cardItems` SHALL NOT 增减

## ADDED Requirements

### Requirement: CardItemView 必须封装单卡视图与事件转发

`CardItemView` SHALL 封装单张卡的 UI：从 `CardItem.uxml` `CloneTree()` 出 `.card-item` 内层 `VisualElement`、设置 `card-name` / `card-cost` 文本。SHALL 持有 `HandIndex`（构造时传入，闭包语义，reorder 不变）。SHALL 注册 `PointerDownEvent` / `PointerEnterEvent` / `PointerLeaveEvent` 回调并转发给上层 `HandFanView`。SHALL 实现 `IDisposable` 解注册回调。

#### Scenario: 渲染卡牌名称与费用

- **WHEN** 用 `CardRuntime { Config = { Name = "突刺", Cost = 1 } }` 构造
- **THEN** `card-name` Label `text` SHALL 为 `"突刺"`
- **AND** `card-cost` Label `text` SHALL 为 `"1"`

#### Scenario: 悬停类切换

- **WHEN** `SetHovering(true)` 被调用
- **THEN** Root `VisualElement` SHALL 应用 `card-item--hovering` 类

#### Scenario: PointerDown 转发到 HandFanView

- **WHEN** 卡 Root 收到 `PointerDownEvent`
- **THEN** SHALL 触发 `PointerDown` 事件，参数包含自身实例与 `PointerDownEvent`

#### Scenario: HandIndex 闭包语义

- **WHEN** 构造时传 `handIndex=2` 后 `HandFanView` 内部 reorder 把该 view 移到位置 0
- **THEN** `CardItemView.HandIndex` SHALL 仍为 2（用于 `UseCard` 调用）
