## Why

战斗 UI 从 UI Toolkit 迁移到 UGUI 后（commit 6ecee8c），玩家无法通过拖拽卡牌到出牌区域出牌 —— 卡牌跟手都看不见、松手只会回弹，`UseCard` 永远不被调用。`diagnose-card-drag-drop` 在 Unity 实测拿到的日志已经把根因锁死（H1 实锤）：UGUI 适配层 `UguiDragSurface` 在 **Screen Space - Camera** 画布下，用 `RectTransform.GetWorldCorners` 返回的**世界坐标矩形**与 `PointerEventData.position`（**屏幕像素**）直接做 `Rect.Contains` 命中判定，单位不一致 → `DetermineDragMode` 一次都没改变 `_dragMode`，始终为 `Detached`；同源问题让 `UpdateGhostPosition` 的 `rect.position = screenPos` 把 ghost 丢到离相机几百单位的世界坐标，ghost 不可见。本变更修复 UGUI 适配层的坐标空间错配，恢复"拖到出牌区松手 → 出牌"主交互。

## What Changes

- **修复 `UguiDragSurface` 的命中矩形坐标空间**：把 `ToWorldRect`（用 `RectTransform.GetWorldCorners`）改为 `ToScreenRect` —— 取得 4 个世界角后通过 `RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldCorner)` 转换为屏幕像素坐标再 build `Rect`。这样 `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound` 三个 getter 返回的矩形都与 `OnPointerMove(pos)` 的 `pos` 处于同一坐标空间。命名暂保留 `*WorldBound`（避免大范围重命名波及 `IDragSurface` / `MockDragSurface` / 既有测试），但用文档约束明确为"hit-test rect（与 pos 同坐标空间）"。
- **修复 ghost 跟手**：`UguiDragSurface.UpdateGhostPosition(Vector2 pos)` 和 `CreateGhost(int srcIdx, Vector2 pos)` 改用 `RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, pos, canvas.worldCamera, out local)` 把屏幕像素还原到 PreviewLayer 局部坐标后赋给 `rect.anchoredPosition`（而非 `rect.position`）。
- **注入 Canvas 引用**：`UguiDragSurface` 在构造时缓存 `_dropZone.GetComponentInParent<Canvas>()` 与 `canvas.worldCamera`，避免每帧反复 `GetComponentInParent`。
- **`IDragSurface` 文档收紧**：在 `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound` 的 XML doc 上明确"返回矩形 MUST 与 `OnPointerMove(pos)` 入参 `pos` 处于同一坐标空间"。
- **清理诊断脚手架**：移除 `diagnose-card-drag-drop` 引入的 `[CardDrag]` Log.Info 打点（共 7 处）、`IDragSurface.DescribeDropZoneCanvas` 方法、`UguiDragSurface.DescribeDropZoneCanvas` 实现、`MockDragSurface.DescribeDropZoneCanvas` 与 `ConfiguredCanvasDescription` 字段；删除 `openspec/specs/card-drag-diagnostic-logging/` 目录（在归档 `diagnose-card-drag-drop` 时同步发生）。
- **新增 EditMode 测试**：覆盖"屏幕空间命中矩形配置下 `Dragging → OverDropZone → 松手 → CardDroppedOnZone(false)`"主路径，复用 `MockDragSurface`。

## Capabilities

### New Capabilities

- `ugui-drag-surface-screen-space-hittest`：定义 UGUI 适配层 `UguiDragSurface` 在任意 Canvas RenderMode（Overlay / Screen Space - Camera / World Space）下，命中矩形与 ghost 位置都必须与 `PointerEventData.position`（屏幕像素）处于同一坐标空间。

### Modified Capabilities

（无 —— 实测显示状态机契约 `gameview-card-drag-state-machine` 本身正确：它说"在 drop-zone 内 PointerUp 触发 CardDroppedOnZone"，是 UGUI 适配实现把 hit-test 跑在错坐标空间。修措辞反而会污染状态机的语义。坐标空间约束放到新 capability `ugui-drag-surface-screen-space-hittest` 是 implementation contract，与状态机契约正交。）

## Impact

- **代码（生产）**：`Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/UguiDragSurface.cs`、`Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/IDragSurface.cs`（仅 XML doc）
- **代码（清理）**：`Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/CardDragController.cs`（删 `[CardDrag]` 日志）、`Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/Drag/MockDragSurface.cs`（删 `DescribeDropZoneCanvas` / `ConfiguredCanvasDescription`）
- **代码（测试新增）**：`Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/` 下新增屏幕空间命中检测用例
- **`UguiPreviewSurface`**：经审查，其 `GetWorldCorners` 用法是 world→local 工具用法（用同坐标系做坐标变换），不波及；不动
- **依赖关系**：依赖 `diagnose-card-drag-drop` 已实施完成（日志已回收）；本变更归档同时归档 diagnose change，删除其临时 spec 目录
- **运行时行为**：恢复"拖到出牌区松手 → `UseCard(handIdx)`"主路径（含 `SingleManual` 进入目标选择的旁路），同时恢复 ghost 跟手与"拖到手牌区松手 → 重排"次路径
- **风险面**：未影响状态机契约、未影响 `MockDragSurface` 既有断言、未改 Hand/Monster/Phase 任何业务事件流
