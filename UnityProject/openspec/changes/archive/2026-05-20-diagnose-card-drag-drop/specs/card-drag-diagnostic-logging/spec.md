## ADDED Requirements

### Requirement: 卡牌拖拽流程必须在五个关键状态转移点输出诊断日志

为支持 `fix-card-drag-drop-not-played` 定位"拖拽出牌失效"的根因，`CardDragController` SHALL 在以下五个状态转移点各输出一条 `Log.Info` 诊断日志：`OnPointerDown` 入口、`OnPointerMove` 首次越过 `DragThreshold`、`EnterDragging` 入口、`DetermineDragMode` 子态变化、`OnPointerUp` 各 case 入口。日志 SHALL 同步写入，SHALL NOT 改变状态转移规则或公共方法签名。

#### Scenario: PointerDown 进入时输出诊断日志

- **WHEN** `CardDragController.OnPointerDown(handIdx, visualIdx, pointerId, pos)` 被调用且 `Phase == PlayerTurn` 通过早返回检查
- **THEN** SHALL 输出一条 `Log.Info`，包含 `handIdx`、`visualIdx`、`pointerId`、`pos`、当前 `Phase`、当前 `State`
- **AND** 输出 SHALL 在记录 `_pointerStartPos` 与调用 `_surface.CapturePointer` 之间发生，确保所有字段反映入口快照

#### Scenario: PointerMove 首次越过阈值时输出诊断日志

- **WHEN** `CardDragController.OnPointerMove(pointerId, pos)` 中 `Vector2.Distance(pos, _pointerStartPos) > _options.DragThreshold` 首次成立
- **THEN** SHALL 在调用 `EnterDragging(pos)` 之前输出一条 `Log.Info`，包含 `pointerId`、`pos`、`dist`、`DragThreshold`
- **AND** 后续未越阈值的 `OnPointerMove` 调用 SHALL NOT 重复输出（仅首次）

#### Scenario: EnterDragging 进入时输出诊断日志

- **WHEN** `CardDragController.EnterDragging(pointerPos)` 被调用
- **THEN** SHALL 在方法体最早处输出一条 `Log.Info`，包含 `pointerPos`、`_activeVisualIndex`、`_surface.CardCount`

#### Scenario: DetermineDragMode 子态变化时输出坐标证据

- **WHEN** `CardDragController` 内部 `DetermineDragMode(pointerPos)` 返回的 `DragMode` 与上一帧不同
- **THEN** SHALL 输出一条 `Log.Info`，包含以下字段：`pointerPos`、`DropZoneAvailable`、`DropZoneWorldBound` 的 `xMin`/`xMax`/`yMin`/`yMax`、`HandFanWorldBound` 的 `xMin`/`xMax`/`yMin`/`yMax`、Canvas 配置摘要（来自 `IDragSurface.DescribeDropZoneCanvas()`）、新旧 `DragMode`
- **AND** 子态未变化时 SHALL NOT 输出（避免每帧刷屏）

#### Scenario: OnPointerUp 三个分支各自输出诊断日志

- **WHEN** `CardDragController.OnPointerUp(pointerId, pos)` 在 `_state == Dragging` 时分发到 `OverDropZone` / `InsertSlot` / `Detached` 任一 case
- **THEN** SHALL 在进入该 case 时输出一条 `Log.Info`，包含分支名（"OverDropZone" / "InsertSlot" / "Detached"）、`handIdx`、`pos`、即将触发的回调名（如 `CardDroppedOnZone` / `CardDragCancelled`）
- **AND** 未进入 `Dragging` 的"单击"分支 SHALL 输出一条标记 `CardClicked` 的日志

### Requirement: 诊断日志必须使用统一前缀以便检索和一次性清除

所有由本变更引入的诊断 `Log.Info` 输出 SHALL 以字符串 `[CardDrag]` 起始，便于 `fix-card-drag-drop-not-played` 完成验证后用单次 grep 清除。日志正文 SHALL 使用 `field=value` 风格的键值对、字段之间以空格或竖线分隔，便于人眼对照。

#### Scenario: 所有诊断日志以统一前缀开头

- **WHEN** 本变更引入的任意 `Log.Info` 调用执行
- **THEN** 日志文本 SHALL 以 `[CardDrag]` 开头
- **AND** 项目内其他 `Log.Info` 调用 SHALL NOT 因本变更产生新的同前缀输出

#### Scenario: 通过前缀一次性清除所有诊断日志

- **WHEN** `fix-card-drag-drop-not-played` 在其 tasks 中执行"清理诊断日志"步骤
- **THEN** 在 `CardDragController.cs` 中按 `[CardDrag]` 前缀检索并删除全部 `Log.Info` 调用 SHALL 完整移除本变更的痕迹
- **AND** 删除后 `CardDragController.cs` 的行为 SHALL 与本变更实施前一致

### Requirement: IDragSurface 必须提供一次性 Canvas 描述方法

为让 `CardDragController` 在不直接持有 `RectTransform` / `Canvas` 引用的前提下输出 Canvas 配置证据，`IDragSurface` SHALL 暴露一个临时方法 `string DescribeDropZoneCanvas()`，返回当前 `_dropZone` 所在 `Canvas` 的 `renderMode`、`worldCamera` 是否为 null、`EventSystem.pixelDragThreshold` 摘要字符串。此方法 SHALL 在 `fix-card-drag-drop-not-played` 完成验证后由其归档步骤一并删除，SHALL NOT 出现在长期 spec 中。

#### Scenario: 生产 UguiDragSurface 返回真实 Canvas 描述

- **WHEN** `UguiDragSurface.DescribeDropZoneCanvas()` 被调用且 `_dropZone != null`
- **THEN** 返回字符串 SHALL 至少包含 `renderMode={Overlay|Camera|World}`、`worldCamera={null|notnull}` 两个键值
- **AND** 当 `_dropZone == null` 或父级 Canvas 取不到时 SHALL 返回 `"<canvas-unavailable>"` 而非抛异常

#### Scenario: 测试用 mock 可注入任意描述字符串

- **WHEN** EditMode 测试通过 `MockDragSurface` 调用 `CardDragController` 触发 `DetermineDragMode` 子态切换
- **THEN** `MockDragSurface.DescribeDropZoneCanvas()` SHALL 返回测试预先设定的字符串
- **AND** `CardDragController` 输出的 `Log.Info` SHALL 包含该字符串

### Requirement: 本 spec 与 fix 变更归档时一并移除

`card-drag-diagnostic-logging` 是临时诊断 capability，SHALL 在 `fix-card-drag-drop-not-played` 完成验证、删除全部 `[CardDrag]` 日志和 `IDragSurface.DescribeDropZoneCanvas` 方法后由 fix change 的归档步骤删除 `openspec/specs/card-drag-diagnostic-logging/` 目录。SHALL NOT 长期保留在主 specs 中。

#### Scenario: fix-card-drag-drop-not-played 归档时同步移除本 spec

- **WHEN** `fix-card-drag-drop-not-played` 进入归档流程
- **THEN** 其 tasks SHALL 包含"删除 `openspec/specs/card-drag-diagnostic-logging/` 目录"步骤
- **AND** 归档完成后 `openspec/specs/card-drag-diagnostic-logging/` SHALL NOT 存在
- **AND** 与之相关的 `IDragSurface.DescribeDropZoneCanvas` / `[CardDrag]` 诊断日志 SHALL 已在 fix change 中同步清除
