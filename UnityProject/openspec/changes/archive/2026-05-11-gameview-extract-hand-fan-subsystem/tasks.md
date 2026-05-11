## 1. 切片接口与 ViewModel 实现

- [x] 1.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Context/IHandContext.cs`，暴露 `Hand`、`Phase` 两个 `ReactiveProperty<>`、`UseCard(int handIdx, int targetIdx = -1)` 方法、`event Action<string> CardPlayFailed`
- [x] 1.2 让 `GameViewModel` 显式实现 `IHandContext`（追加 `: IHandContext`，无新字段）
- [x] 1.3 编译验证

## 2. 布局纯函数 + 配置对象

- [x] 2.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Layout/HandFanLayoutOptions.cs`，含 8 个字段与默认值（与 `GameView` 现有常量一致）
- [x] 2.2 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Layout/FanSlotAssignment.cs`（readonly struct，含 `Left`、`Top`、`TranslateY`、`RotateDegrees`）
- [x] 2.3 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Layout/FanLayoutCalc.cs`（静态类），实现 `ComputeSlot(slotIdx, slotCount, fanWidth, fanHeight, options) -> FanSlotAssignment`
- [x] 2.4 实现 `ComputeInsertSlot(pointerPos, IReadOnlyList<Rect> otherCardWorldBounds, IReadOnlyList<int> otherCardVisualIndices)`（"距最近卡 + 左/右半"算法 + Clamp 边界）
- [x] 2.5 新增 `Tests/EditMode/Game/UI/Layout/FanLayoutCalcTests.cs`：覆盖 5 张卡中心 offset、对称性、`MaxCardSpacing` 截断、`slotCount=1`、`Top` 不为负、`ComputeInsertSlot` 左右半、单卡返回 0、越界 Clamp
- [x] 2.6 EditMode 测试编译通过（运行交给 Unity Test Runner）

## 3. IDragSurface + IDragHostCallbacks 接口

- [x] 3.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Drag/IDragSurface.cs`：含 ~20 个 UI 副作用方法
- [x] 3.2 新增 `IDragHostCallbacks.cs`：`CardClicked(handIdx)` / `CardDroppedOnZone(handIdx, needsManualTarget)` / `CardDragCancelled(handIdx)`
- [x] 3.3 新增 `Tests/EditMode/Game/UI/Drag/MockDragSurface.cs`（fake，记录调用序列、暴露断言辅助方法）+ `CapturingDragHostCallbacks`

## 4. CardDragController 实现

- [x] 4.1 新增 `Drag/CardDragController.cs`，构造参数 `(IDragSurface surface, IHandContext context, HandFanLayoutOptions options)`
- [x] 4.2 维护字段：`_state`、`_dragMode`、`_activeVisualIndex`、`_activeHandIndex`、`_insertSlotIndex`、`_pointerStartPos`、`_capturedPointerId`、`_disposed`
- [x] 4.3 实现 `OnPointerDown(handIdx, visualIdx, pointerId, pos)`：仅 `Phase == PlayerTurn` 才接受
- [x] 4.4 实现 `OnPointerMove(pointerId, pos)`：阈值检查 + UpdateGhostPosition + UpdateDragSubMode
- [x] 4.5 实现 `OnPointerUp(pointerId, pos)`：四路径分发（Click / OverDropZone+Auto / OverDropZone+Manual / InsertSlot / Detached）
- [x] 4.6 实现 `OnPointerCaptureOut`：强制 `ExitDragging` + `Callbacks.CardDragCancelled`
- [x] 4.7 实现 `EnterDragging` / `ExitDragging` / `EnterInsertSlotMode` / `UpdateInsertSlot` / `ExitInsertSlotMode` / `DetermineDragMode` / `UpdateDragSubMode`
- [x] 4.8 实现 `StartReboundAnimation`：DestroyGhost + 退出 InsertSlot + 被拖卡 opacity 恢复 + 其他卡 transition 0.15s + Schedule(ExitDragging)
- [x] 4.9 实现 `OnGeometryChanged()`
- [x] 4.10 实现 `Dispose()`

## 5. CardDragController 测试

- [x] 5.1 新增 `Tests/EditMode/Game/UI/Drag/CardDragControllerTests.cs`
- [x] 5.2 用例：PointerDown + 阈值内 PointerUp → `CardClicked` 调用
- [x] 5.3 用例：PointerDown + 超阈值 PointerMove → 进入 Dragging + ghost + opacity 0
- [x] 5.4 用例：Dragging 拖到 drop-zone（AutoTarget）→ `CardDroppedOnZone(idx, false)` + ghost 销毁
- [x] 5.5 用例：Dragging 拖到 drop-zone（SingleManual）→ `CardDroppedOnZone(idx, true)` + ghost 保留
- [x] 5.6 用例：Dragging InsertSlot 内松手 → `ReorderCardItem` 调用
- [x] 5.7 用例：Dragging Detached 松手 → `StartReboundAnimation` + Schedule + FlushScheduled 后 ExitDragging
- [x] 5.8 用例：拖拽中 `OnPointerCaptureOut` → ghost 销毁 + 状态归 Idle + `CardDragCancelled`
- [x] 5.9 用例：reorder 后 `_activeHandIndex` 不变（SingleManual 卡）
- [x] 5.10 用例：状态机 SHALL 用 `SetCardOpacity(idx, 0)` 而非 visibility
- [x] 5.11 用例：状态机 SHALL 用 `SetCardTransitionDuration(idx, 0)` 而非 USS 类切换

## 6. CardItemView

- [x] 6.1 新增 `Views/CardItemView.cs`，构造参数 `(VisualElement clonedRoot, int handIndex, CardRuntime card)`
- [x] 6.2 在构造中查找 Q `card-name` / `card-cost` Label 并设置文本
- [x] 6.3 注册 `PointerDownEvent` / `PointerEnterEvent` / `PointerLeaveEvent` 转发到 `event PointerDown / PointerEnter / PointerLeave`
- [x] 6.4 实现 `SetHovering(bool)` 切换 `card-item--hovering` 类
- [x] 6.5 实现 `Dispose()` 解注册回调，幂等

## 7. IPreviewSurface + CardPreviewController

- [x] 7.1 新增 `Drag/IPreviewSurface.cs`：5 个方法
- [x] 7.2 新增 `Drag/CardPreviewController.cs`，构造参数 `(IPreviewSurface surface, HandFanLayoutOptions options)`
- [x] 7.3 实现 `TogglePreview(handIdx, source)`：用引用判断同卡
- [x] 7.4 实现 `EnterPreview` / `ExitPreview`，使用 `IPreviewSurface` 计算坐标 + 添加 `card-item--preview` 类
- [x] 7.5 实现 `Dispose()`：调 `ExitPreview` 清理残留，幂等
- [x] 7.6 新增 `Tests/EditMode/Game/UI/Drag/CardPreviewControllerTests.cs`：同卡 Toggle 退出、别卡切换、reorder 后引用比较仍生效、Dispose 销毁残留

## 8. HandFanView 装配

- [x] 8.1 新增 `Views/HandFanView.cs`，构造参数 `(handFan, dropZone, previewLayer, context, cardItemTemplate, options)`
- [x] 8.2 内部嵌套类 `DragSurfaceImpl` + `PreviewSurfaceImpl` 实现两个接口的生产版本
- [x] 8.3 装配 `CardDragController` + `CardPreviewController`，通过嵌套类 `DragHostCallbacksImpl` 转发
- [x] 8.4 订阅 `context.Hand.Changed` → `RefreshCards()`
- [x] 8.5 实现 `RecomputeFanLayout` 在 CardDragController 内部（HandFanView 构造时调 ApplyInitialFanLayout）
- [x] 8.6 实现 `SyncSiblingOrderInternal`：按 _cardItems 顺序 BringToFront + 占位卡最上
- [x] 8.7 注册 `_handFan` 的 `GeometryChangedEvent`，转发 `_dragController.OnGeometryChanged()`
- [x] 8.8 暴露 `event CardClicked` / `CardDroppedOnZone` / `CardDragCancelled`
- [x] 8.9 实现 `Dispose()`：解 Hand 订阅、解 Geometry 回调、Dispose 子控制器、清空 _cardItems、幂等
- [x] 8.10 提供 `RequestGhostCleanup()` / `RequestGhostRebound(int)` 公开方法（change 3 完整接入）

## 9. GameView 协调器瘦身

- [x] 9.1 在 `BindBattleContent` 中实例化 `HandFanView`，订阅三个事件（CardClicked / CardDroppedOnZone / CardDragCancelled）
- [x] 9.2 删除 `GameView` 中已迁走的字段（_handFan / _previewLayer / _cardItems / _dragGhost / _previewClone / 状态机字段等）
- [x] 9.3 删除已迁走的方法（RefreshCards / ApplyFanTransform / 拖拽 / 预览 / 悬停 / 状态机方法等）
- [x] 9.4 删除常量（DragThreshold / MaxCardSpacing / RotatePerStep / TranslateYCoeff / CardWidth / CardHeight / HandFanBottomPadding / ReboundDurationMs）
- [x] 9.5 删除 enum `CardInteractionState` / `DragMode`（迁到 `Drag/CardInteractionState.cs` / `DragMode.cs` 公开类型）
- [x] 9.6 保留：SelectingTarget 相关字段与方法（适配为通过 HandFanView.RequestGhostRebound/Cleanup 触发 ghost 处理）、子模块装配、_endTurnBtn / _failToast / _rewardConfirmBtn、OnPhaseChanged Region 切换、OnCardPlayFailed
- [x] 9.7 验证 `GameView.cs` 行数从 ~1349 降至 446（任务目标 ~400，达成）

## 10. 验收

- [x] 10.1 编译检查通过：`dotnet build UnityProject.slnx --no-restore` 0 error 0 warning
- [ ] 10.2 EditMode 测试全绿（待 Unity Test Runner 手动触发）
- [x] 10.3 `openspec validate gameview-extract-hand-fan-subsystem` 通过
- [ ] 10.4 手动验证 5 条拖拽路径：
  - 路径 A：单击预览（Toggle 同卡退出 / Toggle 别卡切换 / 单击空白不响应）
  - 路径 B：悬停抬升（Idle 态有 hover 类，Dragging / Previewing 态无）
  - 路径 C：拖到 drop-zone 出 AutoTarget 卡（ghost 销毁 + 卡从手牌移除 + 能量消耗）
  - 路径 D：拖到 drop-zone 出 SingleManual 卡（保留 ghost 进入 SelectingTarget，点击怪物 / ESC / 空白）
  - 路径 E：中间地带松手回弹（ghost 立即销毁 + 其他卡协同回到 N 张布局）
  - 路径 F：InsertSlot 顺序调整（拖到 hand-fan 内不同位置 → 占位卡跟随 → 松手后 _cardItems 顺序调整）
- [ ] 10.5 手动验证拖拽中 `Phase` 变为 `MonsterTurn`（外部强制结束回合）→ `HandFanView` 强制退出拖拽态、ghost / 占位卡清理、无悬挂订阅
- [ ] 10.6 手动验证窗口尺寸调整（resize）→ 扇形布局重新计算正确

## 已知不完美之处（change 3 处理）

- `HandFanView.RequestGhostRebound(int)` 当前简化为 `DestroyGhost`，未实现 spec 中的"协同回弹动画 + 0.15s transition + 状态归 Idle"完整流程。原因：协同回弹需要 `CardDragController` 在 `_state == Idle` 时也能复用 `StartReboundAnimation` 内部钩子，而 controller 内部状态机要求严格 `Dragging` 态才能进入回弹。完整接入留给 change 3 引入 `BeginExternalRebound(handIdx)` 内部 API。
- `HandFanView.RefreshCards` 中拖拽态强制退出当前用 `OnPointerCaptureOut(-1)` sentinel 触发，未来 change 3 引入 `CardDragController.ForceCancel()` 公开方法替代。
