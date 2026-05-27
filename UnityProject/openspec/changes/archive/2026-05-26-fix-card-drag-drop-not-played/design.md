## Context

`diagnose-card-drag-drop` 在 Unity 实测拿到的现场日志（玩家拖一张卡到出牌区松手）：

```
[CardDrag] OnPointerDown        pos=(892.66, 121.98)   Phase=PlayerTurn State=Idle
[CardDrag] DragThresholdReached pos=(899.00, 133.06)   dist=12.77 threshold=10
[CardDrag] EnterDragging        pos=(899.00, 133.06)   activeVisualIdx=4 cardCount=5
[CardDrag] PointerUp→Detached   pos=(876.82, 1164.31)  callback=StartReboundAnimation+CardDragCancelled
[CardDragController] 卡牌拖拽回弹完成
```

**两个铁证**：

1. **`[CardDrag] DragMode ...` 日志整场未出现** —— 该日志在 `UpdateDragSubMode` 检测到 `newMode != _dragMode` 时才输出。意味着 `DetermineDragMode` 在每一帧 `OnPointerMove` 返回的都是相同值，结合初始 `_dragMode = Detached`，证明 `DropZoneWorldBound.Contains(pointerPos)` 和 `HandFanWorldBound.Contains(pointerPos)` 在整个拖拽过程中**始终返回 false**。
2. **用户报告"卡牌没跟随鼠标移动"** —— `UguiDragSurface.UpdateGhostPosition(Vector2 pos)` 中 `rect.position = pos` 在 Screen Space - Camera 画布下把屏幕像素 `(876, 1164)` 解释为世界坐标，ghost 被丢到 `worldCamera` 远端、`planeDistance=100` 之外，渲染不可见。

**根因**：`UguiDragSurface.ToWorldRect` 使用 `RectTransform.GetWorldCorners` 取出**世界坐标矩形**（在 Camera 模式下值域是 ±几个单位），而 `CardDragController` 拿它去 `Contains(eventData.position)`，`eventData.position` 是**屏幕像素**（值域 0..1920 等）。单位不同 → 命中永远 false → 子态永远停在 `Detached` → 松手走 rebound 分支 → `UseCard` 不调用。

迁移前 UI Toolkit 没踩到这个坑，因为 UITK 的 `worldBound` 自带 panel 内统一坐标语义，与指针事件坐标一致。UGUI 的 `RectTransform.GetWorldCorners` 取的是 3D 世界坐标，与画布渲染模式强耦合 —— **Overlay 下世界坐标恰好等于屏幕像素（重合）**，但 Screen Space - Camera / World Space 下不再重合。`UIRoot` 的 Canvas 配置为 `RenderMode=1` + 显式 `worldCamera=UICamera` + `PlaneDistance=100`，正是 Camera 模式。

## Goals / Non-Goals

**Goals**

- 让 `UguiDragSurface` 在任意 Canvas RenderMode 下，命中矩形与 ghost 位置都与 `PointerEventData.position`（屏幕像素）同坐标空间，恢复"拖到出牌区松手出牌"的主交互
- 保持 `IDragSurface` / `CardDragController` / `MockDragSurface` / 状态机 spec 形状不变，最小化波及面
- 在文档层面把坐标空间约束写清，避免以后再有人凭"WorldBound"字面意思去用 `RectTransform.GetWorldCorners`
- 配合本变更归档 `diagnose-card-drag-drop`，清掉 `[CardDrag]` 临时日志与 `DescribeDropZoneCanvas` 临时接口，让 `IDragSurface` 回到精简形态

**Non-Goals**

- **不重命名** `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound`：改名会波及 `MockDragSurface` 字段、十余个测试断言、状态机 spec 措辞，性价比低；用 XML doc 收紧契约即可
- **不动状态机 spec** `gameview-card-drag-state-machine`：契约层面"在 drop-zone 内 PointerUp 触发 CardDroppedOnZone"本身是对的，问题在适配实现
- **不改 `UguiPreviewSurface`**：审查后其 `GetWorldCorners` 用法是 world→local 的纯坐标变换工具用法（同坐标系内），与本 bug 无关
- **不修复其他 9 个 pre-existing EditMode 测试失败**（`GameViewPrefabContractTests` 等期望 prefab 根挂热更类 `GameView` MonoBehaviour，违反 HybridCLR 约束）：那是独立的 chore，本变更不揽
- **不调整 DragThreshold / 抗抖逻辑**：现有阈值 10px 实测 12.77 即触发，工作正常

## Decisions

### 决策 1：用屏幕空间统一命中坐标，而非世界空间

- **选**：把世界坐标矩形转屏幕像素矩形再 `Contains` 屏幕像素 `pos`
- **不选**：把屏幕像素 `pos` 转世界坐标再 `Contains` 世界矩形
- **Why**：上层 `CardDragController` 收到的 `pos` 来自 `IDragHandler.OnDrag` 的 `PointerEventData.position`（屏幕像素），是事实标准；状态机内部所有比较都基于这个 `pos`；ghost 的最终着位也是屏幕像素到 RectTransform 局部的转换路径。统一到屏幕像素侧改动面最小，也最符合 UGUI 事件系统的自然语义。
- **Trade-off**：屏幕像素值域大（几百到几千），`Rect` 计算的浮点精度低于世界坐标 —— 实测无影响，UI 命中检测精度需求只在像素级。

### 决策 2：在 `UguiDragSurface` 构造时缓存 Canvas 引用 + worldCamera

- **选**：构造时 `_dropZone.GetComponentInParent<Canvas>()`，存为字段，使用时直接读取
- **不选**：每个 getter 调用时 `GetComponentInParent`
- **Why**：`DetermineDragMode` 每帧 OnDrag 调用，反复 `GetComponentInParent` 是 GC 与遍历开销
- **降级处理**：构造时 Canvas 取不到（`_dropZone == null` 或孤立节点）→ 缓存 `null`，命中检测调用 `RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null)`，在 Overlay 兼容模式下退化为旧行为；ghost 位置同理用 `null` camera 走 Overlay 路径
- **Trade-off**：Canvas 引用的 `worldCamera` 是 Canvas 字段、可在运行时由 EF UIManager 改写。生产路径上 UIRoot 创建后 worldCamera 不变，可以静态缓存；若未来需要支持热切相机，再加 `OnCanvasHierarchyChanged` 回调。当前不引入额外机制。

### 决策 3：用 `RectTransformUtility.WorldToScreenPoint` 而非 `Camera.WorldToScreenPoint`

- **选**：`RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldCorner)`
- **不选**：直接调 `canvas.worldCamera.WorldToScreenPoint(worldCorner)`
- **Why**：`RectTransformUtility.WorldToScreenPoint` 在 `canvas.worldCamera == null` 时（Overlay）自动返回 `worldCorner.xy`，无分支；而 `canvas.worldCamera.WorldToScreenPoint` 会 NRE。统一函数减少特例。

### 决策 4：ghost 位置用 `localPosition` + 在 `CreateGhost` 重置 anchor/pivot/rotation

- **选**：`ScreenPointToLocalPointInRectangle(_previewLayer, pos, canvas.worldCamera, out local)` → `rect.localPosition = new Vector3(local.x, local.y, rect.localPosition.z)`；同时在 `CreateGhost` 入口把 ghost 的 `anchorMin=anchorMax=(0.5,0.5)` / `pivot=(0.5,0.5)` / `localRotation=identity`
- **不选**：`rect.anchoredPosition = local`（首版方案，现场验收发现"卡牌跟随但偏移距离"）
- **不选**：`rect.position = ScreenPointToWorldPointInRectangle(...).result`
- **Why（首版的坑）**：`anchoredPosition` 是 RectTransform 的 pivot 相对于其 anchor 的位置。`local`（来自 `ScreenPointToLocalPointInRectangle`）是 pivot 在父级局部空间的位置。当 ghost 继承源卡的 `anchor=(0,1)`/`pivot=(0,1)`（来自 `ApplyFanTransform`）时，anchor 落在 `_previewLayer` 局部坐标的左上角而非中心。此时 `anchoredPosition = local` 把 ghost 的 pivot 摆到了 `（local + anchor_in_parent_local）`，造成可见的恒定偏移；用户报告"看到卡牌但偏移了距离跟随"。
- **Why（现版）**：`localPosition` 直接把 ghost 的 pivot 摆到父级局部空间的 `local` 处，与 anchor 无关、与父级 pivot 无关，鲁棒。配合在 `CreateGhost` 显式把 ghost 的 `pivot` 设为 `(0.5,0.5)`，pivot 落点 = 视觉中心，指针正好压在卡牌中央。`localRotation = identity` 顺手清除手牌扇形遗留的卡牌倾斜角，避免 ghost 跟着扇形角度旋转。
- **Side effect**：`UpdateGhostPosition` 内的 `SetParent(_previewLayer, false)` 和 `sizeDelta` 保留（InsertSlot 切换可能影响父级；尺寸需要显式锁定）。anchor/pivot/rotation 的重置只在 `CreateGhost` 做一次，避免每帧重设。

### 决策 5：不动 `IDragSurface` 形状，仅在 XML doc 注释里加坐标空间约束

- **选**：在 `DropZoneWorldBound` / `HandFanWorldBound` / `GetCardWorldBound` 的 XML doc 上加一行"返回矩形 MUST 与 `OnPointerMove(pos)` 入参 `pos` 同坐标空间（UGUI 实现为屏幕像素）"
- **不选**：把它们重命名为 `DropZoneHitRect` 等
- **Why**：重命名波及 `MockDragSurface` 至少 3 个字段、`CardDragController` 至少 4 处调用、相关测试断言；契约用 doc 也能锁住，新增 `ugui-drag-surface-screen-space-hittest` capability 在 spec 层兜底。最小变动原则。

### 决策 6：新增独立 capability `ugui-drag-surface-screen-space-hittest`

- **选**：spec 层面新增 capability，描述 UGUI 适配的坐标空间契约
- **不选**：把约束塞进现有 `gameview-card-drag-state-machine` spec
- **Why**：状态机 spec 是平台无关的契约（"在 X 内 PointerUp 触发 Y"），不应耦合 UGUI 实现细节；新 capability 专门承载实现契约，未来若再换渲染后端，新后端要遵守同一坐标空间约束，规则集中在一处。

### 决策 7：诊断脚手架的清理放在本变更 tasks，而非 diagnose-change 的归档脚本

- **选**：本变更 tasks 显式包含"删除 `[CardDrag]` 日志、删除 `DescribeDropZoneCanvas`、归档 diagnose-change"三步
- **不选**：让 `/opsx:archive diagnose-card-drag-drop` 自动清代码
- **Why**：openspec archive 不动代码，只移 spec / change 目录。诊断代码的清理本来就是 fix 的范畴（fix 验证通过后再清，避免清掉了又重测发现 fix 不行）；归档 diagnose 在 fix 验证通过之后做，顺序上是 fix → 清诊断代码 → 归档 diagnose。

## Risks / Trade-offs

- **[Risk] Canvas.worldCamera 在运行时被替换** → **Mitigation**：本项目 EF UIManager 创建 UIRoot 后不再换相机；若未来要支持，本变更已经把 Canvas 引用集中在 `UguiDragSurface` 构造时缓存的字段里，加监听点足够清晰
- **[Risk] World Space Canvas 场景未覆盖**（如未来 VR/3D UI）→ **Mitigation**：`RectTransformUtility.RectangleContainsScreenPoint` 在 World Space 下需要 camera 参数才能投影；本变更保证传入 `canvas.worldCamera`，在 Unity 文档定义的所有 RenderMode 下都行为正确
- **[Risk] 屏幕分辨率改变 / DPI 切换时缓存 worldCamera 不变但投影矩阵变化** → **Mitigation**：`WorldToScreenPoint` 每帧重新投影，结果跟随当前投影矩阵；只缓存 Canvas/Camera 引用本身、不缓存投影结果，已自动适应
- **[Risk] ghost 跟手在极端缩放下偏移** → **Mitigation**：`anchoredPosition` 与 `_previewLayer` 的 RectTransform 缩放兼容；若 `_previewLayer` 有非 1 的 scale，`ScreenPointToLocalPointInRectangle` 已自动消去
- **[Risk] EditMode 测试在 `MockDragSurface` 下覆盖不到屏幕空间问题**（mock 不模拟 Camera 投影） → **Mitigation**：`MockDragSurface` 直接配置 Rect 数值，本来就不模拟坐标变换；新增测试用直接配的屏幕像素矩形覆盖"`pos` 在矩形内 → 进入 OverDropZone"的状态机契约。Camera 模式坐标变换的正确性由生产路径手动验收（重跑用户的拖拽测试）

## Migration Plan

1. **修生产代码**（tasks 1.x）：改 `UguiDragSurface.ToWorldRect` / `UpdateGhostPosition` / `CreateGhost`，注入 Canvas 缓存
2. **加 doc 契约**（tasks 1.x）：在 `IDragSurface` 三个 getter 上加 XML doc 约束
3. **加测试**（tasks 2.x）：新增 EditMode 用例覆盖"屏幕像素矩形 + 屏幕像素 pos → OverDropZone → CardDroppedOnZone(false)"
4. **跑编译 + 现有测试**（tasks 3.x）：dotnet build + 重跑 CardDragControllerTests / UguiDragSurfaceTests
5. **现场验收**（tasks 4.x）：Unity 中拖卡到出牌区 → ghost 跟手 → 松手出牌；拖到手牌区 → 重排
6. **清诊断脚手架**（tasks 5.x）：删 `[CardDrag]` 日志、删 `DescribeDropZoneCanvas` 接口与实现、删 `MockDragSurface` 中的诊断字段
7. **归档 diagnose-change**（tasks 5.x）：`/opsx:archive diagnose-card-drag-drop` —— 同步移除 `openspec/specs/card-drag-diagnostic-logging/` 目录

**回滚**：本变更若引入回归，单 commit revert `UguiDragSurface.cs` 即可恢复 H1 实锤前的状态（H1 状态本来就是"不可出牌"，回滚成本 = 重新 broken，不会比当前更糟）。

## Open Questions

无 —— H1 实锤后，方案路径与影响面都已收敛。
