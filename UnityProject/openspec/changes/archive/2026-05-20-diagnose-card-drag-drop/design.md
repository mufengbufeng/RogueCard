## Context

`commit 6ecee8c` 把战斗 UI 从 UI Toolkit 完整迁移到 UGUI MVC 后，卡牌无法拖到出牌区出牌。explore 阶段对 `CardDragController` / `UguiDragSurface` / `HandFanView` / `CardItemView` / `GameView.prefab` / `Entry.unity` 做了静态分析，得出 3 个候选断点（详见 `fix-card-drag-drop-not-played` proposal 的 H1/H2/H3 假设）。在没有运行时证据前直接动 `UguiDragSurface` 风险很高（H1 的修复要换坐标空间、注入 Canvas 引用、影响 Ghost 位置），因此先做一次精准打点：把状态机走向、坐标取值与 Canvas 配置一次性打到 Console，让 fix change 拿到铁证再下手。

## Goals / Non-Goals

**Goals**

- 在生产代码路径上、单次拖拽操作内，输出足够的证据区分 3 个候选假设：
  - 事件是否到达控制器（PointerDown）
  - `OnDrag` 是否被持续派发（PointerMove）
  - 是否真的进入 `Dragging` 状态（DragThreshold）
  - `DetermineDragMode` 的命中判断是否被坐标空间错配吃掉（最关键）
  - 松手实际走哪条分支（OverDropZone / InsertSlot / Detached）
- 在 `DetermineDragMode` 同时打出 `Canvas.renderMode` 与 `worldCamera` 是否为 null，直接给 fix change 判定"要不要走 `RectTransformUtility` 那条修复路径"。
- 改动可回滚：所有打点集中在单一文件，删除一次性完成。

**Non-Goals**

- 不修复任何业务逻辑、不调整状态转移规则、不动方法签名、不动 prefab、不动 spec 之外的代码。
- 不引入新的 `IDragSurface` 方法或字段（避免日志反向污染抽象层）。
- 不为日志加单元测试（属临时性脚手架，归档时一并清除）。

## Decisions

### 决策 1：日志只加在 `CardDragController.cs`，不渗透到 `UguiDragSurface` / `HandFanView`

- **Why**：fix change 大概率改 `UguiDragSurface`。如果日志散在多处，清理成本变高、`UguiDragSurface` 改动 diff 也会被日志噪音淹没。
- **Trade-off**：`DetermineDragMode` 里要打 Canvas 配置，意味着该方法需要临时通过 `_surface` 反向取 RectTransform —— 但 `IDragSurface` 只暴露 `Rect`，没有暴露 RectTransform/Canvas。
- **解法**：在 `CardDragController.DetermineDragMode` 内通过 `_surface.DropZoneWorldBound` 之外**额外获取的不变信息**（如尺寸量级）即可定性。但要拿到 `Canvas.renderMode` 必须接触 `RectTransform`。
- **结论**：在 `UguiDragSurface` 上**临时新增一个方法** `string DescribeDropZoneCanvas()`，返回字符串形式的"`renderMode=X worldCamera=null/notnull pixelDragThreshold=Y`"，归档时一并删除。日志主体仍在 `CardDragController`，但 surface 多一个一次性诊断接口；这一例外是为换"无需修改任何业务逻辑就能拿到 Canvas 配置"的强证据。

### 决策 2：日志等级用 `Log.Info`，不用 `Log.Debug`

- **Why**：项目 `EF.Debugger` 在 Editor 默认显示 Info；Debug 可能被静默。诊断只跑一次、流程内打点最多 7~8 条/拖，对 Console 压力极小。
- **Trade-off**：Release 构建若禁用 Info 会失效。本变更只在 Editor 跑诊断，可以接受。

### 决策 3：`DetermineDragMode` 单条 multi-line 日志，而不是分散多条

- **Why**：单次状态机推进可能调 `DetermineDragMode` 上百次（每个 OnDrag 帧）。打成单条聚合日志，方便用 Console 的折叠功能；同时人眼对照一行就能定性。
- **Mitigation**：仅在 `DragMode` 实际变化时打印（与上次相同则跳过），避免每帧刷屏。

### 决策 4：日志前缀统一 `[CardDrag]`

- **Why**：fix change 清理时用 grep `[CardDrag]` 即可一次清除，命中精准。
- **Trade-off**：与项目其他 `Log.Info` 前缀风格保持一致（参见现有 `[GameView]`、`[CardDragController]` 等用法）。

### 决策 5：打点点位与字段集

| 点位 | 触发时机 | 字段 |
|---|---|---|
| `OnPointerDown` 入口 | 每次按下 | `handIdx`、`visualIdx`、`pointerId`、`pos`、`Phase`、`State` |
| `OnPointerMove` 入口（仅首次越阈值前） | 拖动累计距离 ≥ DragThreshold 时打一次 | `pointerId`、`pos`、`dist` |
| `EnterDragging` 入口 | 每次进入 Dragging | `pos`、`_activeVisualIndex`、`CardCount` |
| `DetermineDragMode`（仅 mode 变化时） | 子态切换 | `pointerPos`、`DropZoneAvailable`、`DropZoneWorldBound`、`HandFanWorldBound`、`CanvasDescription`（来自 `surface.DescribeDropZoneCanvas()`）、`newMode` |
| `OnPointerUp` 三个 case 入口 | 每次松手 | 分支名 + 关键 callback 名 |

## Risks / Trade-offs

- **Risk**: `DescribeDropZoneCanvas()` 临时方法污染 `IDragSurface` 抽象 → **Mitigation**：归档 fix change 时由 fix change 一并删除该方法和接口签名；spec 里标注"临时接口，归档时移除"。
- **Risk**: 日志在用户实机环境跑不到（仅 Editor 复现） → **Mitigation**：可接受，本变更目的就是 Editor 诊断；用户已能在本地稳定复现。
- **Risk**: 加日志后行为发生时序偏移 → **Mitigation**：`Log.Info` 同步执行、无 IO 等待，对 60FPS 拖拽无可察觉影响；Unity Console 异步刷新。
- **Risk**: 漏掉某个 case 导致定位不全 → **Mitigation**：打点覆盖 H1/H2/H3 三个假设的判定证据；若日志仍不足，diagnose change 可二次迭代加点。

## Migration Plan

1. 实施 tasks.md 中的打点改动。
2. Unity 编辑器跑一局：在 `BattlePanel` 状态下拖一张卡到 `DropZone` 区域松手；同样的操作再拖到手牌区域松手做对照。
3. 截取 Console 日志中所有 `[CardDrag]` 前缀的行，提交到 `fix-card-drag-drop-not-played` 的 design 流程。
4. fix change 完成后，在其 tasks 中包含"清理 `[CardDrag]` 日志 + 移除 `DescribeDropZoneCanvas` 方法"项，diagnose change 归档时 spec 同步删除。

**回滚**：`git revert` 本 change 的实施 commit 即可，无副作用。

## Open Questions

- 是否需要顺手在 `HandFanView.OnCardPointerMove` 也加一条 log 来确认 UGUI 事件层是否走到 HandFanView？当前 design 仅在 `CardDragController` 内打点；若 H2 假设成真（事件未派发），则日志会显示"PointerDown 有，PointerMove 无"，足以推断。如果要进一步定位事件路径，再决定是否扩展（暂列为 open）。
