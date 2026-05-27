## 1. UguiDragSurface 修正屏幕空间命中与 ghost 跟手

- [x] 1.1 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/UguiDragSurface.cs` 类体内新增私有字段 `private readonly Canvas _canvas;` 并在构造函数末尾赋值 `_canvas = _dropZone != null ? _dropZone.GetComponentInParent<Canvas>() : null;`（两个构造重载都覆盖）
- [x] 1.2 把 `private static Rect ToWorldRect(RectTransform rectTransform)` 重写为 `private Rect ToScreenRect(RectTransform rectTransform)`：先 `GetWorldCorners` 取四角，再用 `RectTransformUtility.WorldToScreenPoint(_canvas != null ? _canvas.worldCamera : null, worldCorner)` 把每个 corner 转为屏幕像素 `Vector2`，最终用 bottomLeft / topRight 构造 `Rect`。`rectTransform == null` 时仍返回 `Rect.zero`。从 `static` 改为实例方法以便访问 `_canvas`。
- [x] 1.3 把 `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound` 三个 getter 的实现改为调用 `ToScreenRect`（getter 名字保持不变，仅实现切换）
- [x] 1.4 改写 `UpdateGhostPosition(Vector2 pos)`：保留 `SetParent(_previewLayer, false)` 与 `sizeDelta = new Vector2(CardWidth, CardHeight)`；把 `rect.position = pos` 替换为 `if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, pos, _canvas != null ? _canvas.worldCamera : null, out Vector2 local)) rect.anchoredPosition = local;`
- [x] 1.5 检视 `CreateGhost(int sourceCardIdx, Vector2 pos)`：当前末尾调用 `UpdateGhostPosition(pos)`，1.4 已修，无需重复改动。确认其他路径不再直接写 `_ghost.GetComponent<RectTransform>().position = ...`
- [x] 1.6 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/IDragSurface.cs` 中给 `GetCardWorldBound` / `DropZoneWorldBound` / `HandFanWorldBound` 三个 getter 的 XML 文档补一行：「返回矩形 MUST 与 `CardDragController.OnPointerMove` 入参 `pos` 同坐标空间；UGUI 实现层为屏幕像素，不可直接使用 `RectTransform.GetWorldCorners`。」

## 2. 测试

- [x] 2.1 跑 `dotnet build UnityProject.slnx --no-restore`，确认 0 错误
- [x] 2.2 在 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/Drag/CardDragControllerTests.cs`（若已存在）或新建测试文件中加用例 `Drag_PointerOverDropZoneRect_TransitionsToOverDropZoneAndCallsCardDroppedOnZone`：用 `MockDragSurface` 配置 `ConfiguredDropZoneBound = new Rect(800, 1000, 200, 200)`、`ConfiguredHandFanBound = new Rect(0, 0, 1920, 400)`、`ConfiguredCardCount = 1`、`CardBounds[0] = new Rect(900, 100, 150, 230)`；PointerDown→PointerMove(屏幕像素在 dropRect 内)→PointerUp，断言 `CapturingDragHostCallbacks.CardDroppedOnZoneLog` 包含 `(0, false)`。
- [x] 2.3 加用例 `Drag_PointerOverHandFanRect_TransitionsToInsertSlotAndReorders`：类似配置但 PointerMove 落在 `ConfiguredHandFanBound` 内但 `ConfiguredDropZoneBound` 外，PointerUp 后断言 `ReorderCallLog` 非空。
- [x] 2.4 跑 Unity Test Runner EditMode（用户在 Test Runner UI 跑，或启 Unity Skills 由 `/unity-skills` 自动），确认 `CardDragControllerTests` 全绿；pre-existing 失败（`GameViewPrefabContractTests` / `GameControllerCommandFlowTests` / `GameViewIntegrationTests` / `CardReleaseResolverTests.CardSystem_Play_伤害怪物后发布Monsters变更供运行时UI刷新`）依然存在但**不在本变更范围**
  - **结果**: 用户运行 EditMode 测试输出 12 个失败，**全部命中 diagnose-card-drag-drop task 3.2 已记录的 pre-existing 失败集**（11 个 `SetUp` 报"GameView.prefab 根对象必须挂载 GameView" 来自 `GameViewPrefabContractTests` / `GameControllerCommandFlowTests` / `GameViewIntegrationTests`，HybridCLR 不允许把热更类挂在 prefab 上；1 个 `CardReleaseResolverTests.CardSystem_Play_伤害怪物后发布Monsters变更供运行时UI刷新` 与 drag 路径无关）。本次新增的 `Drag_PointerOverDropZoneRect_TransitionsToOverDropZoneAndCallsCardDroppedOnZone` 与 `Drag_PointerOverHandFanRect_TransitionsToInsertSlotAndReorders` **未在失败列表中**，视为通过。

## 3. 现场验收

- [x] 3.1 在 Unity 中打开战斗场景，触发玩家回合
- [x] 3.2 拖一张卡到 DropZone 区域：观察 ghost 是否实时跟手；松手是否进入出牌流程（手牌减少 / 怪物状态变化 / Console 出现 `[CardDrag] DragMode ... newMode=OverDropZone` 与 `PointerUp→OverDropZone callback=CardDroppedOnZone` 日志，证明状态机正确推进）
- [x] 3.3 拖一张卡到手牌区内不同槽位：观察占位卡是否出现；松手后手牌视觉顺序是否更新
- [x] 3.4 拖一张卡到中间地带：观察松手是否触发回弹动画并恢复原槽位
- [x] 3.5 若任一验收失败，回到 task 1.x 排查原因，必要时在 design.md 的 Open Questions 补记并迭代
  - **迭代记录**: 首版用 `anchoredPosition = local` 在现场验收时出现"卡牌可见但偏移跟随"。回到 task 1.4/1.5：`CreateGhost` 入口显式把 ghost 的 `anchorMin=anchorMax=(0.5,0.5)`、`pivot=(0.5,0.5)`、`localRotation=identity`；`UpdateGhostPosition` 改用 `rect.localPosition = new Vector3(local.x, local.y, z)`。`design.md` 决策 4 与 `spec.md` 已同步更新。再次现场验收通过。

## 4. 清理诊断脚手架

- [x] 4.1 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/CardDragController.cs` 中按 `[CardDrag]` 前缀检索并删除全部 `Log.Info` 调用（共 7 处：OnPointerDown / DragThresholdReached / EnterDragging / DragMode / PointerUp→OverDropZone / PointerUp→InsertSlot / PointerUp→Detached / PointerUp→Click —— 注意是 8 处）
- [x] 4.2 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/IDragSurface.cs` 中删除 `string DescribeDropZoneCanvas();` 方法签名与对应 XML 注释（包括「── 诊断（临时）──」分区注释）
- [x] 4.3 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/UguiDragSurface.cs` 中删除 `public string DescribeDropZoneCanvas()` 实现
- [x] 4.4 在 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/Drag/MockDragSurface.cs` 中删除 `public string ConfiguredCanvasDescription = "<mock-canvas>";` 字段与 `public string DescribeDropZoneCanvas() => ConfiguredCanvasDescription;` 方法
- [x] 4.5 再跑 `dotnet build UnityProject.slnx --no-restore` 确认 0 错误（接口签名删除后所有实现都同步移除）
- [x] 4.6 用 grep `[CardDrag]` / `DescribeDropZoneCanvas` 在整个 `Assets/GameScripts/HotFix/` 自检，确认 0 命中

## 5. 归档 diagnose-card-drag-drop

- [x] 5.1 确认 `diagnose-card-drag-drop` 的 4.5 任务可标完成（本 change 的 design 已基于其日志生成）
- [x] 5.2 在 `openspec/changes/diagnose-card-drag-drop/tasks.md` 把 4.5 改成 `- [x] 4.5 ... (日志已落入 fix-card-drag-drop-not-played 的 design.md Context 章节)`
- [x] 5.3 用 `openspec archive diagnose-card-drag-drop` 命令归档诊断变更，同步 `openspec/specs/card-drag-diagnostic-logging/` 目录移除（archive 命令应自动处理；若 archive 不动 spec，则手动 `rm -r openspec/specs/card-drag-diagnostic-logging/`）
- [x] 5.4 用 grep `card-drag-diagnostic-logging` 在 `openspec/` 自检，确认无残留引用
