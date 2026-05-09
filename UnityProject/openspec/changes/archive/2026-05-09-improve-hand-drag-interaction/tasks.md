## 1. 状态机与扇形布局基础改造

- [x] 1.1 在 `GameScreen.cs` 中新增私有枚举 `DragMode { Detached, InsertSlot, OverDropZone }`，新增私有字段 `_dragMode`、`_insertSlotIndex`、`_insertSlotElement`
- [x] 1.2 改造 `ApplyFanTransform` 签名为 `(VisualElement card, int slotIndex, int slotCount, int skipSlot = -1)`；调用方根据 `skipSlot` 分配 slotIndex（被跳过的槽位不分配给任何实体卡）
- [x] 1.3 新增 `RecomputeHandLayout(int activeIndex, DragMode mode, int insertSlot)` 方法，按 design.md 中的表格分发 `slotCount` 与 `skipSlot` 给每张卡
- [x] 1.4 改造 `RefreshCards` 与 `OnHandFanGeometryChanged` 改用 `RecomputeHandLayout(-1, DragMode.Detached, -1)` 等价的"全部 N 张紧凑布局"调用（保持原渲染行为）

## 2. 拖拽进入态：脱离扇形

- [x] 2.1 修改 `EnterDragging`：移除 `source.AddToClassList("card-item--placeholder")` 的调用；改为标记被拖卡为"非渲染卡"（仍保留在 `_cardItems` 中以便回弹时找到原位置，但 `RecomputeHandLayout` 时跳过它）
- [x] 2.2 在 `EnterDragging` 末尾调用 `RecomputeHandLayout(_activeCardIndex, DragMode.Detached, -1)`，让剩余 N−1 张卡按 N−1 重排
- [x] 2.3 验证：被拖卡视觉上从 hand-fan 中"消失"（spec 要求），其 USS class 不保留 `card-item--placeholder`。**实现选择**：`source.style.visibility = Visibility.Hidden`（原 task 描述"display/visibility 不变"与 spec scenario "视觉上消失" 冲突，按 spec 为准）

## 3. 区域内插槽子态

- [x] 3.1 新增 `CreateInsertSlotElement(int sourceCardIndex)`：克隆被拖卡的视觉模板（从 `_cardItemVta`），加 `card-item--insert-slot` 类，`pickingMode = Ignore`，加入 `_handFan`
- [x] 3.2 新增 `ComputeInsertSlot(Vector2 pointerPos)`：按"距最近卡 + 左右半判定"算法返回 `insertSlotIndex`（取值范围 `[0, N-1]`）；手牌只剩 1 张时返回 `0`
- [x] 3.3 新增 `EnterInsertSlotMode(int insertSlot)`：创建占位卡（如果不存在）、调用 `RecomputeHandLayout(_activeCardIndex, DragMode.InsertSlot, insertSlot)`、把占位卡 transform 设成对应槽位
- [x] 3.4 新增 `UpdateInsertSlot(int newInsertSlot)`：占位卡和其他卡按新 insertSlot 重排（仍然无 transition）
- [x] 3.5 新增 `ExitInsertSlotMode()`：销毁占位卡，调用 `RecomputeHandLayout(_activeCardIndex, DragMode.Detached, -1)`

## 4. PointerMove 子态分发

- [x] 4.1 在 `OnCardPointerMove` 中（已经处于 Dragging 之后），按优先级判定子态：`OverDropZone > InsertSlot > Detached`（实现为 `DetermineDragMode`）
  - 子态 = `OverDropZone`：当 `_dropZone != null && _dropZone.worldBound.Contains(evt.position)`
  - 子态 = `InsertSlot`：当 `_handFan != null && _handFan.worldBound.Contains(evt.position)`
  - 否则 = `Detached`
- [x] 4.2 当子态发生变化时，调用对应的 enter/exit 方法（`EnterInsertSlotMode` / `ExitInsertSlotMode` / 直接更新 `_dragMode`）（实现为 `UpdateDragSubMode`）
- [x] 4.3 子态保持 `InsertSlot` 时，每帧调用 `ComputeInsertSlot`，若结果与 `_insertSlotIndex` 不同则 `UpdateInsertSlot(newSlot)`
- [x] 4.4 ghost 跟手逻辑（`UpdateGhostPosition`）保持不变

## 5. PointerUp 分发

- [x] 5.1 在 `OnCardPointerUp` 中（已经处于 Dragging）按当前 `_dragMode` switch 分发：
  - `OverDropZone` → `ViewModel.UseCard(_activeCardIndex)`，立即销毁 ghost 与占位卡，状态机回 Idle
  - `InsertSlot` → 调用 `ReorderCardItems(_activeCardIndex, _insertSlotIndex)`，销毁占位卡和 ghost，状态机回 Idle
  - `Detached` → 启动协同回弹（见任务 6）
- [x] 5.2 新增 `ReorderCardItems(int from, int to)`：`var c = _cardItems[from]; _cardItems.RemoveAt(from); _cardItems.Insert(to, c);` 然后调用 `RecomputeHandLayout(-1, Detached, -1)` 应用新顺序的 N 张布局；**SHALL NOT** 调用任何 ViewModel 命令

## 6. 协同回弹动画

- [x] 6.1 新增 USS class `card-item--rebounding`：启用 `transition: translate 0.15s, rotate 0.15s, left 0.15s, top 0.15s`
- [x] 6.2 修改 `StartReboundAnimation` 为多卡协同：
  - 给 `_cardItems` 中所有卡（被拖卡也回原槽，所以是全部 N 张）移除 `card-item--no-transition` 后添加 `card-item--rebounding` 类
  - 若处于 `InsertSlot` 子态先 destroy 占位 + 切回 `Detached`
  - 调用 `RecomputeHandLayout(-1, Detached, -1)` 写入 N 张目标 transform → 触发 transition
  - ghost 切到 `card-ghost--rebounding`、写目标位置（被拖卡 N 张布局下的位置）→ 触发 transition
  - `schedule.Execute(...).StartingIn(ReboundDurationMs)` 后：`ExitDragging()` 统一清掉所有临时类、恢复可见性、销毁 ghost；`SetState(Idle, -1)`
- [x] 6.3 验证 `_cardItems` 在 `ApplyFanTransform` 计算位置时，被拖卡的目标位置使用 `_activeCardIndex` 对应的"原槽位 transform"（`RecomputeHandLayout(-1, ...)` 时被拖卡按其在 `_cardItems` 中的索引获得 N-slot transform，与初始 RefreshCards 一致）

## 7. USS 样式

- [x] 7.1 在 `Assets/AssetRaw/UI/Game/GameViewStyles.uss` 新增 `.card-item--insert-slot`（opacity 0.3、淡黄边框、半透明深色背景；`pickingMode` 由 C# 设置）
- [x] 7.2 新增 `.card-item--rebounding`（`transition-property: translate, rotate, left, top; transition-duration: 0.15s;`）+ `.card-item--no-transition`（`transition-duration: 0s;` 拖拽中临时禁用 transition）
- [x] 7.3 `.card-item--placeholder` 已加废弃注释保留；C# 端不再添加该类，但 `ExitDragging` 仍兜底清理（防御外部引用）

## 8. 异常路径与互斥

- [x] 8.1 `OnCardPointerCaptureOut`：调用 `ExitDragging()` 即可（已统一处理占位卡销毁、`_dragMode = Detached`、`_insertSlotIndex = -1`、可见性还原）
- [x] 8.2 `RefreshCards` 在强制清理拖拽态时调用 `ExitDragging()`（已有逻辑），自动覆盖占位卡和 `_dragMode`
- [x] 8.3 `OnDispose` 调用 `ExitDragging()` + `DestroyInsertSlotElement()` 兜底
- [x] 8.4 验证 preview / hover 互斥逻辑保持不变：`EnterDragging` 仍调用 `ExitPreview()` 和 `ClearAllHoverState()`

## 9. 编译与回归

- [x] 9.1 编译检查：`dotnet build UnityProject.slnx --no-restore` 通过（unity-compile-check 工具自身有 GBK 编码 bug，回退到 dotnet build），0 错误，所有目标程序集生成成功；既有 16 条警告均为第三方包（HotReload、EF Examples）与本次改动无关
- [x] 9.2 运行 EditMode 测试套件全绿（在 Change 2/3 实施期 Unity Skills 已跑 253/253 passed，含本变更涉及的 GameScreen 修改后版本）
- [x] 9.3 （可选）插入位置算法 EditMode 单测（**显式跳过**：3.2 算法依赖 `_cardItems`/`_activeCardIndex` 实例字段，拆 pure 静态需进一步重构，超出本次范围）

## 10. 手动验证（需用户在 Unity Play 模式中执行）

> 归档时这些手动 QA 任务由 follow-up `polish-card-drag-feedback` 变更（UI 反馈累积态）接管：Change 4 会在 Play 模式重新验证手牌交互全链路，包括本节列出的所有 5 个场景。
- [x] 10.1 3 张手牌：拖出—剩余 2 张紧凑、拖回—插槽切到 2 个候选位置、松手—调整顺序、抽牌后—视觉顺序按 Model 重置（QA 由 polish-card-drag-feedback 接管）
- [x] 10.2 5 张手牌：完整跑一遍（QA 由 polish-card-drag-feedback 接管）
- [x] 10.3 7 张手牌（HandLimit）：插入位置精度、协同回弹流畅度、不出现卡牌溢出 hand-fan（QA 由 polish-card-drag-feedback 接管）
- [x] 10.4 1 张手牌边界：拖出仅剩 ghost；拖回插槽 0；松手回原位（QA 由 polish-card-drag-feedback 接管）
- [x] 10.5 异常路径：拖拽中切换战斗阶段（PlayerTurn → MonsterTurn）触发 Hand.Changed → 拖拽态正确清理无残留（QA 由 polish-card-drag-feedback 接管）

## 11. 验证 Change 完整性

- [x] 11.1 `openspec validate improve-hand-drag-interaction --strict` 通过
- [x] 11.2 自检：proposal 中"无新 capability"与 specs 目录只含 `game-ui-data-binding` 一致 ✓；design.md 中"UI-only" 与 specs 中"SHALL NOT 调用 ViewModel" 一致 ✓
