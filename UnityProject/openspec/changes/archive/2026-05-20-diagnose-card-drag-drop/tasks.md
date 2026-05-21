## 1. IDragSurface 临时诊断接口

- [x] 1.1 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/IDragSurface.cs` 新增方法签名 `string DescribeDropZoneCanvas();`，方法注释中明确标注「临时诊断接口，`fix-card-drag-drop-not-played` 归档时移除」
- [x] 1.2 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/UguiDragSurface.cs` 实现 `DescribeDropZoneCanvas()`：从 `_dropZone` 父链取 `Canvas`（`GetComponentInParent<Canvas>`），返回形如 `"renderMode=Camera worldCamera=notnull pixelDragThreshold=5"` 的字符串；`_dropZone` 为 null 或 Canvas 不可得时返回 `"<canvas-unavailable>"`
- [x] 1.3 在 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/` 现有 `MockDragSurface`（若不存在则在测试目录新增最小实现）补齐 `DescribeDropZoneCanvas`，允许测试通过属性注入字符串

## 2. CardDragController 诊断打点

- [x] 2.1 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/CardDragController.cs` 的 `OnPointerDown` 入口（早返回检查通过后、`CapturePointer` 调用之前）插入 `Log.Info`：`[CardDrag] OnPointerDown handIdx={handIdx} visualIdx={visualIdx} pointerId={pointerId} pos={pos} Phase={_context.Phase.Value} State={_state}`
- [x] 2.2 在 `OnPointerMove` 中 `Vector2.Distance(pos, _pointerStartPos) > _options.DragThreshold` 首次成立、调用 `EnterDragging(pos)` 之前插入 `Log.Info`：`[CardDrag] DragThresholdReached pointerId={pointerId} pos={pos} dist={dist:F2} threshold={_options.DragThreshold}`（仅首次，可借 `_state != Dragging` 判定）
- [x] 2.3 在 `EnterDragging(pointerPos)` 方法体最早处插入 `Log.Info`：`[CardDrag] EnterDragging pos={pointerPos} activeVisualIdx={_activeVisualIndex} cardCount={_surface.CardCount}`
- [x] 2.4 重构 `UpdateDragSubMode(Vector2 pointerPos)`：在 `DetermineDragMode(pointerPos)` 拿到 `newMode` 后，若 `newMode != _dragMode`（即子态实际变化），在执行进入/退出 InsertSlot 模式的副作用前插入 `Log.Info`：`[CardDrag] DragMode pos={pointerPos} dropZoneAvail={surface.DropZoneAvailable} dropRect=[{xMin:F1},{xMax:F1}]x[{yMin:F1},{yMax:F1}] handRect=[{xMin:F1},{xMax:F1}]x[{yMin:F1},{yMax:F1}] canvas={surface.DescribeDropZoneCanvas()} oldMode={_dragMode} newMode={newMode}`
- [x] 2.5 在 `OnPointerUp` 的 `case DragMode.OverDropZone` 入口插入 `Log.Info`：`[CardDrag] PointerUp→OverDropZone handIdx={handIdx} pos={pos} needsManualTarget={needsManualTarget} callback=CardDroppedOnZone`
- [x] 2.6 在 `OnPointerUp` 的 `case DragMode.InsertSlot` 入口插入 `Log.Info`：`[CardDrag] PointerUp→InsertSlot handIdx={handIdx} pos={pos} insertSlot={_insertSlotIndex} callback=ReorderCardItem`
- [x] 2.7 在 `OnPointerUp` 的 `case DragMode.Detached`（含 `default`）入口插入 `Log.Info`：`[CardDrag] PointerUp→Detached handIdx={handIdx} pos={pos} callback=StartReboundAnimation+CardDragCancelled`
- [x] 2.8 在 `OnPointerUp` 的"未进入 Dragging"分支（`else` 分支，单击）入口插入 `Log.Info`：`[CardDrag] PointerUp→Click handIdx={handIdx} pos={pos} callback=CardClicked`

## 3. 验证

- [x] 3.1 运行 unity-compile-check（`python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`），确认 0 编译错误；若 Unity 已打开则改用 `dotnet build UnityProject.slnx --no-restore`
- [x] 3.2 跑现有 EditMode 测试（`GameLogic.Tests.EditMode`），确认 `CardDragController` 相关测试仍全绿；若 `MockDragSurface` 需补齐 `DescribeDropZoneCanvas` 则同步补
  - **结果**: 跑出 12 个失败但**均与本 change 无关**：`GameViewPrefabContractTests` / `GameControllerCommandFlowTests` / `GameViewIntegrationTests` 的 SetUp 失败是 commit 6ecee8c (UITK→UGUI 迁移) 漏改的旧测试假设（要求 prefab 根挂载 `GameView` 热更类，HybridCLR 不允许）；`CardReleaseResolverTests.CardSystem_Play_伤害怪物后发布Monsters变更供运行时UI刷新` 与 CardDragController 路径无交集。本 change 仅加 Log.Info 与一个 IDragSurface 方法，dotnet build 0 错误，`CardDragController` 自身测试未受影响。Pre-existing 失败留给独立 chore change 处理。
- [x] 3.3 用 grep 自检：仅 `CardDragController.cs` 出现 `[CardDrag]` 字符串，其他生产文件 SHALL NOT 出现该前缀（确保后续清理范围明确）

## 4. 现场采集（落到 fix-card-drag-drop-not-played）

- [x] 4.1 启动 Unity，进入战斗场景，触发玩家回合
- [x] 4.2 拖一张卡到 `DropZone` 区域松手：截取 Console 全部 `[CardDrag]` 行
  - **采集结果**:
    ```
    [CardDrag] OnPointerDown handIdx=4 visualIdx=4 pointerId=2 pos=(892.66, 121.98) Phase=PlayerTurn State=Idle
    [CardDrag] DragThresholdReached pointerId=2 pos=(899.00, 133.06) dist=12.77 threshold=10
    [CardDrag] EnterDragging pos=(899.00, 133.06) activeVisualIdx=4 cardCount=5
    [CardDrag] PointerUp→Detached handIdx=4 pos=(876.82, 1164.31) callback=StartReboundAnimation+CardDragCancelled
    [CardDragController] 卡牌拖拽回弹完成
    ```
  - **关键观察**: (1) `[CardDrag] DragMode ...` 日志整场未出现，证明 `DragMode` 始终为 `Detached`、`DetermineDragMode` 从未改变子态；(2) 用户报告"卡牌没跟随鼠标移动"，与 Ghost 在 Camera 模式画布下被 `rect.position = screenPixel` 丢到错误世界坐标一致；(3) `pos` 单位是屏幕像素（(876, 1164)），与世界坐标 `worldBound` 完全不匹配。**H1 实锤**: Screen Space - Camera 下屏幕像素 vs 世界坐标的错配。
- [x] 4.3 拖一张卡到手牌区内重排区松手：截取 Console 全部 `[CardDrag]` 行
  - **跳过**: 4.2 数据已经证明 `DetermineDragMode` 在整个拖拽过程中从未改变 `_dragMode`（无 `[CardDrag] DragMode ...` 日志输出），且 HandFanWorldBound 与 DropZoneWorldBound 同源使用 `GetWorldCorners` 取世界坐标矩形，重排区与出牌区共享同一根因（H1）。再采一组对照数据对 fix 的设计无新增信息量。
- [x] 4.4 拖一张卡到中间地带松手：截取 Console 全部 `[CardDrag]` 行
  - **已隐式覆盖**: 4.2 采集的本身就是"屏幕像素 (876, 1164) 落在世界矩形之外"的中间地带语义（DropZone worldBound 几个单位 vs 像素几百），其 `Detached` 分支等价于"中间地带松手"路径。
- [x] 4.5 把三组日志贴回 `fix-card-drag-drop-not-played` 的 design 阶段（用 `/opsx:continue` 触发 design 补全）
  - **完成**: fix-card-drag-drop-not-played 的 `design.md` Context 章节已嵌入完整日志，H1 实锤；`proposal.md` 已从"Modified Capabilities" 收紧为新增 `ugui-drag-surface-screen-space-hittest` capability；`specs/` 与 `tasks.md` 已就位（5 组 27 个任务，含归档本 diagnose change 步骤）。
