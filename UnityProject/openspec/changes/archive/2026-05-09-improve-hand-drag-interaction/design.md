## Context

`GameScreen.cs` 当前的手牌交互状态机包含 `Idle / Hovering / Previewing / Dragging` 四态，由 `OnCardPointerDown/Move/Up` 驱动。拖拽态下：
- 原卡保留在 `_handFan` 内，加 `card-item--placeholder` 类（opacity 0.3）作为视觉占位；
- ghost 在 `_previewLayer` 上跟随指针；
- 松手在 drop-zone 外通过 USS transition 回弹原卡位置销毁 ghost。

约束与现状：
- 扇形布局参数集中在 `ApplyFanTransform(card, index, total)`，假设 `total` 是当前手牌数 N，且每张卡的目标位置由 `index` 决定。
- 手牌渲染源头是 `ViewModel.Hand.Changed` → `RefreshCards`，会清掉 `_cardItems` 重建。
- `CardSystem.Play` / `Draw` 通过 `_model.SetHand(...)` 修改手牌列表，必然触发 `Hand.Changed`。
- `_cardItems` 列表里的卡牌当前 closure 持有 `index = handIndex`，`OnCardPointerUp` 在 drop-zone 内时直接 `ViewModel.UseCard(idx)`，`idx` 即 closure 捕获的初始位置。

需求 1+2+3 一并交付，单 change，仅改 GameScreen + USS，不动 GameLogic / ViewModel / Model。

## Goals / Non-Goals

**Goals:**
- 拖拽进入态：被拖卡完全脱离扇形，剩余 N−1 张卡按 N−1 重排
- 拖拽中三态切换（每帧按指针位置判断）：DraggingDetached / DraggingInsertSlot / DraggingOverDropZone
- 区域内插槽：鼠标在 hand-fan worldBound 内 → 显示 `card-item--insert-slot` 半透明占位 + 其他卡按 N 槽留空重排
- 松手分发：drop-zone → UseCard；hand-fan → 重排 `_cardItems`；其他 → ghost 与其他卡协同回弹 N 张布局
- 拖拽中其他卡的 transform 变更无 transition；松手回弹时所有相关卡临时启用 0.15s transition

**Non-Goals:**
- **不**新增 `ViewModel.MoveCardInHand` 或类似命令；调整顺序仅落在 UI 层
- **不**修改 `GameModel.Hand` 列表顺序；`Hand.Changed` 的渲染路径不变（一次抽/出牌后视觉重排会被重置）
- **不**动 `BattlePanel.uxml` 的层级结构（drop-zone / preview-layer / hand-fan 仍保持现有兄弟关系）
- **不**重构 preview / hover 状态机（保持现有互斥规则）
- **不**支持多指拖拽 / 多选拖拽

## Decisions

### Decision 1: 拖拽子态用内部枚举 `DragMode`，主状态机仍用现有 `CardInteractionState`
将 `Dragging` 这一主态保持，新增私有枚举 `DragMode { Detached, InsertSlot, OverDropZone }`，仅在 `_state == Dragging` 时有效。

**Why X over Y：**
- 替代方案：把 `CardInteractionState` 拆成 `DraggingDetached/DraggingInsertSlot/DraggingOverDropZone` 三个值。但其他态判定（`if (_state == CardInteractionState.Dragging)`）会变成多分支，对现有代码（`RefreshCards`、`OnCardPointerEnter` 等多处）造成不必要的散弹改动。
- 选用内部 `DragMode` 枚举，把"是否拖拽"和"拖拽到哪"解耦：现有判断只看 `_state`，子态切换只动 `_dragMode` 字段，更聚焦。

### Decision 2: `ApplyFanTransform` 增加 `skipSlot` 参数，引入"虚拟槽位"
新签名：
```
ApplyFanTransform(VisualElement card, int slotIndex, int slotCount, int skipSlot = -1)
```
- `slotCount`：扇形布局参考的总槽位数（拖拽态时 = N，否则 = 实际渲染数）
- `skipSlot`：要"跳过"的槽位索引，`-1` 表示不跳过；其他值表示该槽位是空的（用于占位卡或被拖出的卡）
- 实际写入 transform 时，根据 `slotIndex` 和 `slotCount` 计算（与原逻辑一致），`skipSlot` 仅影响调用方如何分配 slotIndex

新增 `RecomputeHandLayout(int activeIndex, DragMode mode, int insertSlot)`：
| mode | slotCount | skipSlot | 说明 |
|---|---|---|---|
| `Idle / Detached / OverDropZone` | N−1 | −1 | 剩余 N−1 张紧凑排（被拖卡不参与） |
| `InsertSlot` | N | `insertSlot` | 剩余 N−1 张让出第 `insertSlot` 槽，占位卡放在该槽 |

**Why X over Y：**
- 替代方案 A：直接传 `(currentIndex, currentCount)`，把"哪个 slotIndex"算好再传。但占位卡和"空槽"概念不一致——占位卡本身需要占第 `insertSlot` 槽，剩余卡需要"假装第 k 位被占"。`skipSlot` 让两边逻辑统一。
- 替代方案 B：提供两个独立函数 `ApplyFanTransformDetached / ApplyFanTransformInsertSlot`。但扇形参数计算公式完全一样，仅"用哪个槽"不同，复用更清晰。

### Decision 3: 区域判定每帧实时求值，不缓存
`OnCardPointerMove` 每帧根据 `evt.position` + `_dropZone.worldBound` + `_handFan.worldBound` 直接判定 `DragMode`，命中变化时调用 `RecomputeHandLayout`。

**Why：**
- worldBound 求值是 O(1)，每帧重算开销可忽略
- 缓存 mode 状态会引入"切换边界什么时候触发 layout 重建"的额外复杂度，反而增加 bug 面
- 优先级：`OverDropZone > InsertSlot > Detached`（drop-zone 在视觉上覆盖在 hand-fan 上层时也以 drop-zone 为准）

### Decision 4: 插入位置算法 = 鼠标距最近卡 + 左右半判定
```
对剩余 N−1 张卡（按当前 _cardItems 顺序、跳过 activeIndex）：
  找出 worldBound.center 距 pointer.x 最近的一张
  若 pointer.x < 该卡 center.x → 插入到它之前
  否则 → 插入到它之后
若 N−1 == 0（手牌只有 1 张）→ insertSlot = 0
```

**Why X over Y：**
- 替代方案 A：枚举 N 个候选插入点（间隙），取距鼠标最近的。在边缘（最左 / 最右）更精准。
- 替代方案 B（选用）：最近卡 + 左右半。代码更短、手牌数 ≤ 7 时差异不可见、心智模型更直接。
- 实际游戏手牌上限为 `HandLimit`（当前看 GameModel 应在 5–7 范围），方案 B 足够。

### Decision 5: 松手回弹的协同动画方式 = 临时启用 USS class
在 `_handFan` 的卡牌（即 `_cardItems` 中除被拖卡外的所有卡）默认 inline transform 切换无 transition。松手回弹时：
1. 给参与回弹的卡牌加 `card-item--rebounding` 类，该类启用 `transition: translate 0.15s, rotate 0.15s, left 0.15s, top 0.15s`
2. `RecomputeHandLayout(activeIndex=-1, mode=Idle, insertSlot=-1)` 把所有卡按 N 张布局写入 transform → 触发 transition
3. ghost 同样切到 `card-ghost--rebounding`（已存在）走 transition
4. `schedule.Execute(...).StartingIn(160ms)` 后移除 `card-item--rebounding`、销毁 ghost、`SetState(Idle)`

**Why X over Y：**
- 替代方案：用 `experimental.animation` API 编程式动画。可控但代码量大、与现有 USS 路径不一致
- 选用 USS class toggle：与现有 `card-ghost--rebounding` 模式一致，最小心智负担

### Decision 6: `_cardItems` 重排 = 列表 reorder + 重新分配 closure 的 handIndex
松手在 hand-fan 内时：
1. `var dragged = _cardItems[_activeCardIndex]; _cardItems.RemoveAt(_activeCardIndex); _cardItems.Insert(insertSlot, dragged);`
2. 重新调用 `RecomputeHandLayout(-1, Idle, -1)` 应用新顺序的扇形 transform
3. **不**重新注册 PointerDown/Enter/Leave 回调；现有 closure 中的 `index` 是初始 handIndex，对应 ViewModel.Hand 中的真实卡，UseCard 时仍用此 index 即可正确出牌

**Why：**
- 关键：closure 中的 `index` ≠ `_cardItems` 列表中的位置，它是 ViewModel.Hand 中的索引。视觉重排不改变它，UseCard 仍能找到正确的 CardRuntime
- 唯一需要小心：`OnCardPointerDown` 里也用 `_activeCardIndex` 作为状态量，这个 `_activeCardIndex` 在 reorder 后应该指 `_cardItems` 中的新位置。但 reorder 发生在 PointerUp，下一次 PointerDown 会重置 `_activeCardIndex`，无需特殊处理

### Decision 7: 占位卡（insert-slot）的视觉实现
新建一张专用 VisualElement `_insertSlotElement`，进入 `InsertSlot` 子态时创建并 `Add` 到 `_handFan`，加 `card-item--insert-slot` 类（USS：opacity 0.3、border-color 偏淡），离开此子态时移除。

**Why：**
- 不复用 `card-item--placeholder` 类——语义不同：旧 placeholder 是"被拖卡自己"的半透明，新 insert-slot 是"将插入此处"的虚影
- 占位卡和被拖卡内容相同（同一张 card 的克隆），但 `pickingMode = Ignore` 不抢点击

### Decision 8: A 方案副作用的明示位置
"调整顺序后下一次 Hand.Changed 触发，视觉顺序按 Model 重置"——这是 A 方案的预期行为。在 spec 里以独立 Scenario 显式声明，避免后续被当成 bug 修复。

## Risks / Trade-offs

- [拖拽中其他卡 transition 默认开启会跟手卡顿] → 默认 `.card-item` 不写 transition，`card-item--rebounding` 类才启用 transition，松手时间窗内才生效
- [插入算法在边缘不够精准] → 验证阶段如果手牌满（HandLimit 7）时玩家投诉边缘判定，可后续改算法 A（无 spec 变更）
- [reorder 后下一次 Hand.Changed 立即重置] → 在 spec 中明示，作为 A 方案的已知约束。如未来要持久化（B 方案），需要新建另一个 change 引入 `ViewModel.MoveCardInHand`
- [_handFan 与 _dropZone worldBound 重叠时的优先级] → 当前 BattlePanel.uxml 中两者无重叠，但代码层面坚持 `OverDropZone > InsertSlot > Detached` 的判定优先级，未来 UXML 改动也安全
- [测试覆盖弱] → 状态判定和 _handFan worldBound 强耦合，难做 EditMode 单测。插入位置算法（最近卡 + 左右半判定）可拆出 pure helper，给 EditMode 加最小覆盖
- [与 migrate-ui-to-uitoolkit 平行变更] → 两者都触及 GameScreen / BattlePanel，开始 apply 前需 git fetch、确认 migrate 剩余 task 是否合并；如果冲突就在 worktree 隔离实现

## Migration Plan

无运行时数据迁移（纯 UI 行为变更）。代码层迁移：
1. 旧 `card-item--placeholder` 类的 USS 仍保留以防未捕获到的引用，但 GameScreen 不再添加该类。如确认无其他使用，apply 阶段可一并删除。
2. `ApplyFanTransform` 签名变化是内部方法，无外部调用方，直接改造即可。

## Open Questions

- 是否需要为"调整顺序"加一个反馈音效或轻微动画（如插槽出现时的弹性）？默认不加，简化范围；apply 后由 PM/设计层决定。
- 手牌只剩 1 张时，是否允许"区域内拖拽调整"？算法上已退化为 `insertSlot = 0`，视觉等同 detach + 立刻插回。代码不需要特殊处理，但 spec 应明确（在 Scenario 中加一条边界覆盖）。
