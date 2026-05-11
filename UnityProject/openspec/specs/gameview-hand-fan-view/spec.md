# gameview-hand-fan-view Specification

## Purpose
TBD - created by archiving change gameview-extract-hand-fan-subsystem. Update Purpose after archive.
## Requirements
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
