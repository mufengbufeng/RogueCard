## Context

Change 1 完成后，`GameView` 仍承载 ~1100 行手牌相关逻辑。本 change 抽出整个手牌子系统，是三步重构中的最大块。手牌交互的复杂度集中在三处：

1. **几何 / 布局**：扇形 transform 公式、`ComputeInsertSlot` 命中、`worldBound` 与 `RecomputeHandLayout` 协同
2. **状态机**：`CardInteractionState`（4 态）+ `DragMode`（3 子态）+ `SelectingTarget` 子态，进出态各有 enter/exit 副作用，含 ghost 销毁、占位卡处理、`opacity` / `pickingMode` 还原、USS transition baseline 维护
3. **指针捕获**：`PointerCapture` 中途丢失（`PointerCaptureOutEvent`）的兜底路径

本 change 通过"接口边界 + 纯函数 + mock 测试"分解这三块，显著提升可测性。

## Goals / Non-Goals

**Goals:**

- 把扇形布局公式抽成纯函数 + 配置对象，覆盖 EditMode 单元测试
- 把拖拽状态机抽成可 mock UI 副作用的控制器，覆盖关键状态转移用例
- 保留现有可观察行为 100% 一致：`DragThreshold = 10f`、`MaxCardSpacing = 120f`、`RotatePerStep = 3°`、`ReboundDurationMs = 160`、`opacity 0` 不用 `visibility Hidden`、ghost 在中间地带松手立即销毁、占位卡使用 inline `transitionDuration = 0` 等所有手感细节
- 让 `SelectingTarget` 流程的"入口事件"清晰化（`CardDroppedOnZone` 携带 `needsManualTarget` 标志）
- 子模块自管 `Dispose`（沿用 change 1 约定）

**Non-Goals:**

- 不抽 `SelectingTarget` 跨模块编排（留给 change 3）
- 不抽 `BattlePanelView` 协调器（留给 change 3）
- 不改任何手感参数与动画时序
- 不引入新的 UXML / USS 文件（`CardItem.uxml` 不变）
- 不重构 `Region` 或 `_mainRegion` 切换逻辑

## Decisions

### 决策 1：状态机组织——保留 enum + 每态一组方法（折中方案）

不引入显式 State Pattern（每态一个类）。保留两个 enum：

```csharp
enum CardInteractionState { Idle, Hovering, Previewing, Dragging, SelectingTarget }
enum DragMode { Detached, InsertSlot, OverDropZone }
```

但代码组织从"长方法 + switch 分散"改为"每态有 enter/exit/handlePointerMove/handlePointerUp 的命名方法簇"：

```csharp
class CardDragController {
    State _state;
    DragMode _dragMode;

    // ── PointerDown 入口 ──
    void OnPointerDown(int handIdx, Vector2 pos) { ... }

    // ── Idle / Hovering 是 default 态, 无 enter/exit ──

    // ── Dragging 态 ──
    void EnterDragging(int cardIdx, Vector2 pos) { ... }
    void OnDraggingPointerMove(Vector2 pos) { ... }
    void OnDraggingPointerUp(Vector2 pos) { ... }
    void ExitDragging() { ... }

    // ── DragMode 子态切换 ──
    void EnterInsertSlotMode(int slot) { ... }
    void UpdateInsertSlot(int slot) { ... }
    void ExitInsertSlotMode() { ... }
    DragMode DetermineDragMode(Vector2 pos) { ... }
    void UpdateDragSubMode(Vector2 pos) { ... }

    // ── 兜底 ──
    void OnPointerCaptureOut() { ... }
}
```

**为什么不用 State Pattern？** 5 态 × 3 子态 = 15 个潜在类，对一个尚可阅读的状态机过度设计。命名方法簇已能让"每态做了什么"一目了然。

**为什么不全部内联在 `HandFanView`？** 状态机本身已 ~400 行，与 `CardItemView` 渲染、`FanLayoutCalc` 调用混合时阅读成本仍高。独立类边界清晰。

**`SelectingTarget` 留在哪个层？** `CardDragController` 内部不知道 `SelectingTarget`——只在 `Dragging.OverDropZone` 松手时通过 `IDragHostCallbacks.CardDroppedOnZone(handIdx, needsManualTarget)` 抛事件给 `HandFanView`，`HandFanView` 再向 `BattlePanelView`（change 3）转发。`needsManualTarget` 由 `CardDragController` 通过 `IHandContext` 查 `Hand[handIdx].Config.TargetMode == SingleManual` 计算。

### 决策 2：IDragSurface 接口边界——封装一切 UI 副作用

```csharp
public interface IDragSurface
{
    // 卡牌集合操作
    int CardCount { get; }
    Rect GetCardWorldBound(int cardIdx);
    void ApplyFanTransform(int cardIdx, FanSlotAssignment slot);
    void SetCardOpacity(int cardIdx, float opacity);
    void SetCardPickingMode(int cardIdx, bool pickable);
    void SetCardTransitionDuration(int cardIdx, float seconds);
    void ReorderCardItem(int from, int to);
    void SyncSiblingOrder();

    // Drop zone & hand fan 几何
    Rect DropZoneWorldBound { get; }
    Rect HandFanWorldBound { get; }
    bool DropZoneAvailable { get; }
    void SetDropZoneActive(bool active);

    // Ghost / 占位卡
    void CreateGhost(int sourceCardIdx, Vector2 pos);
    void UpdateGhostPosition(Vector2 pos);
    void DestroyGhost();
    void CreateInsertSlot(int sourceCardIdx);
    void DestroyInsertSlot();
    void ApplyInsertSlotTransform(FanSlotAssignment slot);

    // 调度 (用于 ReboundDurationMs 延迟回弹)
    void Schedule(Action action, long delayMs);

    // PointerCapture
    void CapturePointer(int cardIdx, int pointerId);
    void ReleasePointer(int cardIdx, int pointerId);

    // 上层回调
    IDragHostCallbacks Callbacks { get; }
}

public interface IDragHostCallbacks
{
    void CardDroppedOnZone(int handIdx, bool needsManualTarget);
    void CardDragCancelled(int handIdx);  // 中间地带松手 / capture out
    void CardClicked(int handIdx);          // 不进入 Dragging 的 PointerUp (位移 ≤ DragThreshold)
}
```

**为什么暴露这么多方法？** 拖拽状态机确实操作大量 UI 副作用。把它们集中在 `IDragSurface` 中比让 controller 直接持有 N 个 `VisualElement` 更好——测试可一次实现 fake 验证调用序列。

**为什么 `ApplyFanTransform` 接 `FanSlotAssignment` 而不接 left/top/translate/rotate？** 让 `FanLayoutCalc` 输出强类型对象，`IDragSurface` 实现负责映射到 inline style。这样状态机不直接操作 style 字段，测试也只断言 slot 数据正确。

```csharp
public readonly struct FanSlotAssignment {
    public float Left;
    public float Top;
    public float TranslateY;
    public float RotateDegrees;
}
```

### 决策 3：FanLayoutCalc + HandFanLayoutOptions——纯函数 + 配置对象

```csharp
public sealed class HandFanLayoutOptions {
    public float DragThreshold = 10f;
    public float MaxCardSpacing = 120f;
    public float RotatePerStep = 3f;
    public float TranslateYCoeff = 3.5f;
    public float CardWidth = 150f;
    public float CardHeight = 230f;
    public float HandFanBottomPadding = 20f;
    public long ReboundDurationMs = 160;
}

public static class FanLayoutCalc {
    public static FanSlotAssignment ComputeSlot(
        int slotIdx, int slotCount,
        float fanWidth, float fanHeight,
        HandFanLayoutOptions opts);

    public static int ComputeInsertSlot(
        Vector2 pointerPos,
        IReadOnlyList<Rect> otherCardWorldBounds,  // 不包含被拖卡
        int activeIdxInVisualOrder);
}
```

**为什么静态？** 纯函数，无状态，最易测。

**为什么 options 是字段不是常量？** 测试可注入不同参数验证边界（如 `MaxCardSpacing = 0` 时所有卡叠在一起）；生产环境 `HandFanView` 传一个 `static readonly HandFanLayoutOptions Default`。

**测试用例样本：**

```csharp
[Test]
public void ComputeSlot_FiveCards_CenterCardAtZeroOffset()
{
    var slot = FanLayoutCalc.ComputeSlot(2, 5, 800f, 280f, new());
    Assert.AreEqual(0f, slot.RotateDegrees, 0.001f);   // center
    Assert.AreEqual(0f, slot.TranslateY, 0.001f);
}

[Test]
public void ComputeInsertSlot_PointerInLeftHalfOfNearest_InsertsBefore()
{
    var bounds = new[] { Rect(100, 0, 50, 100), Rect(200, 0, 50, 100), Rect(300, 0, 50, 100) };
    int slot = FanLayoutCalc.ComputeInsertSlot(new(210f, 50f), bounds, activeIdxInVisualOrder: -1);
    Assert.AreEqual(1, slot);  // pointer 在 200 卡左半 → 在它之前插入
}
```

### 决策 4：CardItemView——薄封装，事件抛给 HandFanView

```csharp
public sealed class CardItemView : IDisposable {
    public VisualElement Root { get; }
    public int HandIndex { get; }   // closure 捕获，reorder 不变

    // 渲染
    public void SetCardData(CardRuntime card);
    public void SetHovering(bool hovering);

    // 事件转发 (HandFanView 订阅)
    public event Action<CardItemView, PointerDownEvent> PointerDown;
    public event Action<CardItemView> PointerEnter;
    public event Action<CardItemView> PointerLeave;

    public void Dispose();
}
```

**为什么不让 `CardItemView` 自己处理拖拽？** 拖拽涉及与其他卡协同（占位卡、reorder、ghost 等），跨实例状态——天然属于上层。`CardItemView` 只负责"我自己"。

**`HandIndex` vs `VisualOrderIndex`：** `CardItemView.HandIndex` 是 closure 捕获的"它在 ViewModel.Hand 中的索引"，`reorder` 后不变（用于 `UseCard(handIdx)`）；视觉位置由 `HandFanView._cardItems` 列表中的位置表示，`reorder` 后会变。这两个语义在 `CardDragController` 内部要严格区分（与现有代码一致）。

### 决策 5：preview-layer 共享元素的归属

`preview-layer` 在 BattlePanel.uxml 中是共享浮层：

- ghost 创建在此（`CardDragController` 用）
- 预览克隆卡也创建在此（`CardPreviewController` 用）
- change 3 的 `TargetSelector` 也会用此层挂临时元素

本 change 中 `HandFanView` 持有 `_previewLayer` 引用，并以**构造参数**形式分别传给 `CardDragController` 与 `CardPreviewController`（包装在 `IDragSurface` / `IPreviewSurface` 中）。change 3 引入 `BattlePanelView` 时，`HandFanView` 改为从 `BattlePanelView` 接收引用，无需改 controller 层接口。

### 决策 6：HandFanView 的几何响应

保持现有 `OnHandFanGeometryChanged` 行为：

- `_handFan.RegisterCallback<GeometryChangedEvent>(...)` 在构造时注册
- 几何变化时按当前状态机状态分派：
  - `Dragging` 状态 → `CardDragController.OnGeometryChanged()` → 重新 `ApplyFanTransform` 全部卡（按当前 `_dragMode`）
  - 其他状态 → `RecomputeHandLayout(activeIdx=-1)` 全部按 N 张紧凑布局
- `Dispose` 时取消注册

`HandFanView` 持有 `_handFanGeometryHandler` 委托引用以便对称解绑（修复现有代码中"内容切换后旧引用泄漏"的同类问题）。

### 决策 7：拖拽状态机回归保护——5 条手动验证路径

由于本 change 风险最高，验收清单必须明确以下路径：

1. **预览路径**：单击 → 放大克隆 → 单击同卡退出 → 单击别卡切换
2. **悬停路径**：鼠标进入卡 → `card-item--hovering` 类 → 移出 → 类移除
3. **拖到 drop-zone 出 AutoTarget 卡**：拖拽 → drop-zone 高亮 → 松手 → ghost 销毁、`UseCard(idx, -1)` 调用、卡从手牌列表中移除
4. **拖到 drop-zone 出 SingleManual 卡**：拖拽 → 松手 → 进入 `SelectingTarget`（GameView 中保留，行为不变）→ 点击怪物 / ESC / 空白
5. **中间地带松手回弹**：拖出 hand-fan 但未到 drop-zone → 松手 → ghost 立即销毁、其他卡协同回弹到 N 张布局、被拖卡 `opacity` 立即恢复
6. **InsertSlot 顺序调整**：拖拽时鼠标移到 hand-fan 内某位置 → 占位卡出现 → 移动 → 占位卡跟随 → 松手 → `_cardItems` 顺序调整、其他卡平滑回到新位置

## Risks / Trade-offs

- **风险（高）**：USS transition baseline。占位卡 / 其他卡的 `transitionDuration` 通过 inline style 而非 USS class 切换（现有代码已踩坑并备注）。重构时若错把 inline style 改成 class toggling，回弹动画首帧会出现 rotate 错乱。
  → 缓解：`IDragSurface.SetCardTransitionDuration(idx, seconds)` 实现严格沿用现有 `SetCardTransitionDuration` 内部代码（`new StyleList<TimeValue>(...)`）；测试用 fake 记录调用序列即可，不验证真实 transition。

- **风险（高）**：`opacity 0` + `pickingMode Ignore` 替代 `visibility Hidden`。改动这两行对 ghost 销毁时序非常敏感。
  → 缓解：spec 明确写出"被拖卡 SHALL 通过 `opacity = 0` + `pickingMode = Ignore` 标记不可见，SHALL NOT 用 `visibility = Hidden`"；EditMode 测试用 fake `IDragSurface` 记录调用并断言。

- **风险（中）**：`PointerCapture` 中途丢失。当前 `OnCardPointerCaptureOut` 是兜底，状态机重构时容易漏掉。
  → 缓解：spec 明确写一个 scenario "拖拽中 PointerCapture 丢失 SHALL 强制重置到 Idle 并清理 ghost / 占位卡 / 解事件"；mock 测试覆盖。

- **风险（中）**：`reorder` 后 `_activeCardIndex`（视觉）与 `_activeHandIndex`（hand）语义分离。当前代码已修，重构时容易合并出 bug。
  → 缓解：`CardDragController` 内部明确两个字段，方法注释每处用哪个；`CardItemView.HandIndex` 是 closure 捕获、`HandFanView._cardItems.IndexOf(view)` 是视觉位置。

- **权衡**：`IDragSurface` 接口面较宽（~15 个方法）。
  → 接受。拖拽状态机本质就操作这么多 UI 元素；接口宽但每个方法语义清楚，比让 controller 直接持有 7 个 `VisualElement` + N 个 helper 委托好。

## Migration Plan

1. 引入 `IHandContext` 切片，`GameViewModel` 实现
2. 引入 `HandFanLayoutOptions` + `FanLayoutCalc.ComputeSlot`，配套 `FanLayoutCalcTests`，先单独验证布局公式
3. 引入 `FanLayoutCalc.ComputeInsertSlot`，配套测试
4. 引入 `IDragSurface` + `IDragHostCallbacks` + `FanSlotAssignment`，写一个 stub `DragSurface` 包装现有 `GameView` 的字段（仅迁移，不改逻辑）
5. 引入 `CardDragController`，把 `EnterDragging` / `ExitDragging` / `OnCardPointerMove` / `OnCardPointerUp` / `OnCardPointerCaptureOut` / `UpdateDragSubMode` / 子态进出方法搬过去；通过 `IDragSurface` 间接操作 UI；`HandFanView` 暂未引入，`GameView` 直接持有 `CardDragController` + 实现 `IDragSurface`
6. 引入 `CardItemView`，迁移单卡 hover / PointerDown 转发；`GameView` 用 `List<CardItemView>` 替代 `List<VisualElement>`
7. 引入 `IPreviewSurface` + `CardPreviewController`，迁移 `EnterPreview` / `ExitPreview` / `TogglePreview`
8. 引入 `HandFanView`，从 `GameView` 中接管：模板加载、`RefreshCards`、`RecomputeHandLayout`、`SyncSiblingOrder`、几何回调注册、`IDragSurface` 实现、`CardDragController` / `CardPreviewController` 装配；对外暴露事件 `CardDroppedOnZone(handIdx, needsManualTarget)` / `CardDragCancelled(handIdx)` / `CardClicked(handIdx)`
9. `GameView` 缩减：`BindBattleContent` 中实例化 `HandFanView`，订阅其事件（`CardDroppedOnZone(needsManualTarget=true)` 进入 `EnterSelectingTarget`，否则直接 `ViewModel.UseCard(handIdx)`；`CardClicked` 不调命令）；`OnDispose` 调 `_handFanView?.Dispose()`
10. 跑测试 + 手动验证 5 条路径
11. 删除 `GameView` 中已迁走的字段、方法、enum、常量

## Open Questions

- `IDragSurface.Schedule(Action, long delay)` 的实现：当前 `GameView.schedule.Execute(...).StartingIn(ms)` 是 `VisualElement.schedule`。`HandFanView` 持有 `_handFan` 元素，调 `_handFan.schedule.Execute(...)`。测试用 fake 同步记录回调，立即触发即可。
- `CardDragController` 是否暴露 `_state` 字段供 `HandFanView` 查询？目前看不需要——`HandFanView` 通过 `IDragHostCallbacks` 事件被动响应即可。若 change 3 的 `TargetSelector` 需要查询"拖拽是否仍在进行"，再加 `IsDragging` 只读属性。
