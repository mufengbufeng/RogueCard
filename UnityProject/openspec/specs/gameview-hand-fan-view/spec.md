# gameview-hand-fan-view Specification

## Purpose
TBD - created by archiving change gameview-extract-hand-fan-subsystem. Update Purpose after archive.
## Requirements
### Requirement: CardItemView 必须封装单卡视图与事件转发

`CardItemView` SHALL 封装单张 UGUI 手牌卡：从 `HandCardTemplate` 实例化根对象、设置卡名与费用文本、维护 `RectTransform` 和 `CanvasGroup`。SHALL 持有 `HandIndex`（构造时传入，闭包语义，reorder 不变）。SHALL 实现 Unity pointer handler 或等价事件桥接，将 PointerDown / PointerEnter / PointerLeave 转发给上层 `HandFanView`。SHALL 实现 `IDisposable` 解注册回调或销毁运行时对象。

#### Scenario: 渲染卡牌名称与费用

- **WHEN** 用 `CardRuntime { Config = { Name = "突刺", Cost = 1 } }` 构造
- **THEN** 卡名文本 SHALL 为 `"突刺"`
- **AND** 费用文本 SHALL 为 `"1"`

#### Scenario: 悬停视觉切换

- **WHEN** `SetHovering(true)` 被调用
- **THEN** 卡牌项 SHALL 应用悬停视觉状态，例如抬升、描边或缩放

#### Scenario: PointerDown 转发到 HandFanView

- **WHEN** 卡牌项收到 UGUI pointer down
- **THEN** SHALL 触发 `PointerDown` 事件，参数包含自身实例与指针位置/ID

#### Scenario: HandIndex 闭包语义

- **WHEN** 构造时传 `handIndex=2` 后 `HandFanView` 内部 reorder 把该 view 移到位置 0
- **THEN** `CardItemView.HandIndex` SHALL 仍为 2（用于 `UseCard` 调用）

### Requirement: HandFanView 必须用 UGUI 容器刷新扇形手牌

`HandFanView` SHALL 接收手牌容器、drop-zone、preview-layer、手牌模板、`IHandContext` 与 `HandFanLayoutOptions`。当 `Hand` 变化时，HandFanView SHALL 清理旧卡项、实例化新卡项、按 `FanLayoutCalc.ComputeSlot` 应用 `RectTransform.anchoredPosition` 与 `localRotation`，并同步 sibling 顺序。

#### Scenario: 首次刷新生成手牌项

- **WHEN** `Hand.Value` 包含 3 张卡
- **THEN** 手牌容器 SHALL 包含 3 个运行时卡牌项
- **AND** 每个卡牌项 SHALL 使用对应卡牌名称和费用

#### Scenario: 扇形布局应用到 RectTransform

- **WHEN** `FanLayoutCalc.ComputeSlot` 返回 `Left=100`、`Top=20`、`TranslateY=8`、`RotateDegrees=-3`
- **THEN** 对应卡牌项 `RectTransform` SHALL 应用等价的锚点位置和旋转

### Requirement: HandFanView 必须转发 CardDragController 回调

`HandFanView` SHALL 创建 `CardDragController` 并提供 UGUI `IDragSurface` 生产实现。`CardDragController` 发出的 `CardClicked`、`CardDroppedOnZone`、`CardDragCancelled` SHALL 由 HandFanView 转发给上层 BattlePanelView，并保持原有 hand index 语义。

#### Scenario: 卡牌点击触发预览并转发

- **WHEN** `CardDragController` 回调 `CardClicked(1)`
- **THEN** HandFanView SHALL 切换卡牌预览
- **AND** SHALL 触发 `HandFanView.CardClicked(1)`

#### Scenario: 拖到 drop-zone 转发

- **WHEN** `CardDragController` 回调 `CardDroppedOnZone(2, true)`
- **THEN** HandFanView SHALL 触发 `CardDroppedOnZone(2, true)`

### Requirement: HandFanView 必须支持 Dispose 清理 UGUI 运行时项

`HandFanView.Dispose()` SHALL 解绑 `IHandContext.Hand.Changed`，Dispose 拖拽和预览控制器，销毁 ghost、占位卡和所有运行时卡牌项，并清空公开事件。Dispose SHALL 幂等。

#### Scenario: Dispose 后 Hand 变化不刷新

- **WHEN** `HandFanView.Dispose()` 已调用
- **AND** 之后 `Hand.Value` 变化
- **THEN** 手牌容器 SHALL NOT 创建新的运行时卡牌项

#### Scenario: 重复 Dispose 安全

- **WHEN** 同一 `HandFanView` 的 `Dispose()` 被调用两次
- **THEN** 第二次调用 SHALL NOT 抛出异常
