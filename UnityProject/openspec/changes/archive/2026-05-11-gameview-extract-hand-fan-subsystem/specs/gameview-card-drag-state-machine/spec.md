## ADDED Requirements

### Requirement: CardDragController 必须通过 IDragSurface 间接操作 UI

`CardDragController` SHALL 通过 `IDragSurface` 接口执行所有 UI 副作用（创建 ghost、应用 transform、设置 opacity / pickingMode、捕获指针、调度延迟、查询 worldBound 等）。SHALL NOT 直接持有任何 `VisualElement` 引用。SHALL NOT 直接调用 `UQuery`。

#### Scenario: 测试用 mock IDragSurface

- **WHEN** 测试用 `MockDragSurface` 构造 `CardDragController` 并执行 PointerDown / PointerMove / PointerUp 序列
- **THEN** 状态机 SHALL 与生产环境一致地推进，所有 UI 副作用 SHALL 通过 `MockDragSurface` 记录的调用序列可断言

### Requirement: CardDragController 必须维护 4 态状态机

`CardDragController` SHALL 维护一个 `CardInteractionState` 枚举字段，取值为 `Idle` / `Hovering` / `Previewing` / `Dragging` / `SelectingTarget`。状态转移 SHALL 遵循下列规则：

- `Idle` + `OnPointerDown` → 记录 `_pointerStartPos` + `CapturePointer`，停留在 `Idle`
- `Idle/Hovering` + `OnPointerMove`（位移 > `options.DragThreshold`）→ 转 `Dragging`，调 `EnterDragging`
- `Dragging` + `OnPointerUp`（在 `drop-zone` 内）→ `ExitDragging` + 上层回调 `CardDroppedOnZone(handIdx, needsManualTarget)`，转 `Idle`
- `Dragging` + `OnPointerUp`（在 `hand-fan` 内）→ `ReorderCardItem(from=activeVisualIdx, to=insertSlot)` + `ExitDragging`，转 `Idle`
- `Dragging` + `OnPointerUp`（中间地带）→ `StartReboundAnimation`（ghost 立即销毁、其他卡 transition 0.15s 回到 N 张）+ 上层回调 `CardDragCancelled` + 延迟 `options.ReboundDurationMs` 后 `ExitDragging`，转 `Idle`
- `Idle/Hovering` + `OnPointerUp`（位移 ≤ `DragThreshold`）→ 上层回调 `CardClicked(handIdx)`，停留在 `Idle`（预览由 `CardPreviewController` 处理）
- 任何态 + `OnPointerCaptureOut` → 强制 `ExitDragging`（若在 `Dragging`）+ 转 `Idle`
- `SelectingTarget` 不由本控制器进入；由 `BattlePanelView`（change 3）或当前 `GameView` 通过 `IDragHostCallbacks.CardDroppedOnZone(needsManualTarget=true)` 接管

#### Scenario: PointerDown 阈值内 PointerUp 触发 CardClicked

- **WHEN** PointerDown 后位移仅 5px（≤ DragThreshold=10）即 PointerUp
- **THEN** `IDragHostCallbacks.CardClicked(handIdx)` SHALL 被调用
- **AND** 状态 SHALL 保持 `Idle`，SHALL NOT 进入 `Dragging`

#### Scenario: PointerMove 位移超阈值进入 Dragging

- **WHEN** PointerDown 后某次 PointerMove 位移 12px（> DragThreshold=10）
- **THEN** 状态 SHALL 转为 `Dragging`
- **AND** `IDragSurface.CreateGhost(activeIdx, pos)` SHALL 被调用
- **AND** `IDragSurface.SetCardOpacity(activeIdx, 0)` SHALL 被调用
- **AND** `IDragSurface.SetCardPickingMode(activeIdx, false)` SHALL 被调用

#### Scenario: 拖到 drop-zone 松手 (AutoTarget) 触发 ExitDragging + Callback

- **WHEN** `Dragging` 态在 `drop-zone` worldBound 内 PointerUp 且 `Hand[idx].Config.TargetMode != SingleManual`
- **THEN** `IDragSurface.DestroyGhost()` SHALL 被调用
- **AND** `IDragSurface.SetCardOpacity(idx, opacity 恢复)` SHALL 被调用
- **AND** `IDragHostCallbacks.CardDroppedOnZone(handIdx, false)` SHALL 被调用
- **AND** 状态 SHALL 转回 `Idle`

#### Scenario: 拖到 drop-zone 松手 (SingleManual) 保留 ghost

- **WHEN** `Dragging` 态在 `drop-zone` 内 PointerUp 且 `Hand[idx].Config.TargetMode == SingleManual`
- **THEN** `IDragSurface.DestroyGhost()` SHALL NOT 被调用
- **AND** `IDragHostCallbacks.CardDroppedOnZone(handIdx, true)` SHALL 被调用
- **AND** ghost 所有权转移到上层（由 `TargetSelector` 在 change 3 处理）

#### Scenario: PointerCaptureOut 中途丢失强制重置

- **WHEN** `Dragging` 态收到 `PointerCaptureOut`（如系统抢走指针）
- **THEN** `IDragSurface.DestroyGhost()` SHALL 被调用
- **AND** `IDragSurface.DestroyInsertSlot()` SHALL 被调用（若存在）
- **AND** 所有卡 SHALL 还原 `opacity` / `pickingMode` / inline transitionDuration
- **AND** 状态 SHALL 转回 `Idle`
- **AND** `IDragHostCallbacks.CardDragCancelled(handIdx)` SHALL 被调用

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

#### Scenario: 在 hand-fan 内松手 ReorderCardItem

- **WHEN** `Dragging.InsertSlot` 态在 `hand-fan` 内松手
- **THEN** `IDragSurface.ReorderCardItem(activeVisualIdx, insertSlotIdx)` SHALL 被调用
- **AND** ghost 与占位卡 SHALL 销毁
- **AND** 状态 SHALL 转回 `Idle`

### Requirement: CardDragController 必须区分 ActiveVisualIndex 与 ActiveHandIndex

`CardDragController` SHALL 同时维护两个索引：

- `_activeHandIndex` —— 闭包语义，`Hand[idx]` 中的位置，`reorder` 后不变；用于 `UseCard(handIdx)` 与 `Hand[idx].Config.TargetMode` 查询
- `_activeVisualIndex` —— 视觉位置，`_cardItems.IndexOf(activeView)`，`reorder` 后会变；用于 `ApplyFanTransform`、`InsertSlot` 计算等 UI 操作

#### Scenario: reorder 后 handIndex 不变

- **WHEN** `Dragging.InsertSlot` 态拖拽过程中视觉位置从 2 移到 0（reorder）
- **THEN** `_activeHandIndex` SHALL 保持构造时的 hand 位置不变
- **AND** `_activeVisualIndex` SHALL 更新为 0

#### Scenario: 命令调用使用 handIndex

- **WHEN** `Dragging.OverDropZone` 松手触发 `CardDroppedOnZone`
- **THEN** 回调参数 `handIdx` SHALL 取自 `_activeHandIndex`
- **AND** SHALL NOT 取自 `_activeVisualIndex`

### Requirement: CardDragController 必须使用 inline transitionDuration 控制动画

`CardDragController`（通过 `IDragSurface.SetCardTransitionDuration`）SHALL 用 inline `transitionDuration` 控制其他卡的过渡动画，SHALL NOT 切换 USS 类（如 `card-item--no-transition`）来达到同样效果。原因：USS 类切换 + inline style 同帧写入会让 transition baseline 失效，导致回弹动画首帧 rotate 错乱。

#### Scenario: 进入 Dragging 时其他卡 transitionDuration=0

- **WHEN** 状态从 `Idle` 转为 `Dragging`
- **THEN** SHALL 对所有卡调 `IDragSurface.SetCardTransitionDuration(idx, 0f)`
- **AND** SHALL NOT 调用任何 USS 类切换 API（如 `AddToClassList("card-item--no-transition")`）

#### Scenario: 中间地带松手回弹时 transitionDuration=0.15s

- **WHEN** `Dragging.Detached` 松手触发 `StartReboundAnimation`
- **THEN** SHALL 对所有卡调 `IDragSurface.SetCardTransitionDuration(idx, 0.15f)`
- **AND** 在 `options.ReboundDurationMs` 后调 `ExitDragging`

### Requirement: CardDragController 必须使用 opacity 0 而非 visibility Hidden 隐藏被拖卡

被拖卡 SHALL 通过 `IDragSurface.SetCardOpacity(idx, 0)` + `IDragSurface.SetCardPickingMode(idx, false)` 隐藏，SHALL NOT 通过 `style.visibility = Hidden`。原因：`visibility` 切换会触发 layout 重算，`opacity` 不会，回弹时被拖卡 fade-in 与 ghost 销毁可同步。

#### Scenario: 进入 Dragging 时被拖卡 opacity=0

- **WHEN** `EnterDragging`
- **THEN** SHALL 对被拖卡调 `IDragSurface.SetCardOpacity(idx, 0)`
- **AND** SHALL NOT 调用任何 visibility 相关 API

#### Scenario: 退出 Dragging 时被拖卡 opacity 恢复

- **WHEN** `ExitDragging`（任意路径）
- **THEN** SHALL 对所有卡调 `IDragSurface.SetCardOpacity(idx, ResetToUssDefault)`（实现层可用 `StyleKeyword.Null`）
