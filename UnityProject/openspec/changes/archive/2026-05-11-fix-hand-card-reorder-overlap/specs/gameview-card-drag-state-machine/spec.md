## MODIFIED Requirements

### Requirement: CardDragController 必须维护 3 子态拖拽模式

`CardDragController` 在 `Dragging` 态中 SHALL 维护 `DragMode` 枚举：

- `Detached` —— 中间地带，剩余卡按 N-1 紧凑布局
- `InsertSlot` —— 鼠标在 `hand-fan` 内，留出空槽 + 半透明占位卡，剩余卡按 N 槽排但跳过空槽
- `OverDropZone` —— 鼠标在 `drop-zone` 内，剩余卡按 N-1 紧凑（与 `Detached` 同布局，但松手会出牌）

子态切换优先级 SHALL 为 `OverDropZone > InsertSlot > Detached`。

#### Scenario: 鼠标移入 hand-fan 进入 InsertSlot

- **WHEN** `Dragging.Detached` 态鼠标移入 `hand-fan` worldBound
- **THEN** 子态 SHALL 转为 `InsertSlot`
- **AND** `IDragSurface.CreateInsertSlot(activeIdx)` SHALL 被调用
- **AND** 剩余卡 SHALL 按 N 槽布局重排但跳过 `insertSlotIndex`

#### Scenario: 鼠标移入 drop-zone 进入 OverDropZone

- **WHEN** `Dragging.InsertSlot` 态鼠标移入 `drop-zone` worldBound
- **THEN** 子态 SHALL 转为 `OverDropZone`
- **AND** `IDragSurface.DestroyInsertSlot()` SHALL 被调用
- **AND** 剩余卡 SHALL 按 N-1 紧凑布局重排

#### Scenario: 鼠标移出 hand-fan 与 drop-zone 进入 Detached

- **WHEN** `Dragging.InsertSlot` 态鼠标移出 `hand-fan` 但未进入 `drop-zone`
- **THEN** 子态 SHALL 转为 `Detached`
- **AND** `IDragSurface.DestroyInsertSlot()` SHALL 被调用

#### Scenario: InsertSlot 内移动更新 insertSlot

- **WHEN** `Dragging.InsertSlot` 态鼠标在 `hand-fan` 内移动且 `ComputeInsertSlot` 输出从 1 变为 2
- **THEN** `IDragSurface.ApplyInsertSlotTransform` SHALL 用新 slot 重排占位卡
- **AND** 其他卡 SHALL 重新分配槽位（跳过新 insertSlot）

#### Scenario: 在 hand-fan 内松手提交视觉换位

- **WHEN** `Dragging.InsertSlot` 态在 `hand-fan` 内松手
- **THEN** `IDragSurface.ReorderCardItem(activeVisualIdx, insertSlotIdx)` SHALL 被调用
- **AND** `CardDragController` SHALL 在换位提交后按新的视觉顺序对全部 N 张卡应用最终扇形布局
- **AND** 每张可见卡 SHALL 占用唯一的最终槽位，SHALL NOT 与相邻卡共享同一 `left/top/translate/rotate` 结果
- **AND** ghost 与占位卡 SHALL 销毁
- **AND** 所有卡 SHALL 还原 `opacity` / `pickingMode` / inline transitionDuration
- **AND** 状态 SHALL 转回 `Idle`

#### Scenario: 在原槽位松手仍然应用最终布局

- **WHEN** `Dragging.InsertSlot` 态在 `hand-fan` 内松手且 `insertSlotIdx == activeVisualIdx`
- **THEN** `CardDragController` SHALL 仍按 N 张卡应用最终扇形布局
- **AND** 被拖卡 SHALL 恢复可见且位于自己的最终槽位
- **AND** 状态 SHALL 转回 `Idle`

#### Scenario: 换位前释放被拖卡 pointer capture

- **WHEN** `Dragging.InsertSlot` 态在 `hand-fan` 内松手
- **THEN** `CardDragController` SHALL 在改变视觉列表顺序前释放被拖卡的 pointer capture
- **AND** 释放 pointer capture 的 card index SHALL 指向松手前的 active visual card
