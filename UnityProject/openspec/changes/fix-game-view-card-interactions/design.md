## Context

`GameView` 是 RogueCard 局内核心战斗界面，由 `GameScreen.cs`（继承 `Screen<GameViewModel>`，本质是 `VisualElement`）驱动 UI Toolkit 的 UXML/USS 渲染。手牌区位于 `BattlePanel.uxml` 的 `card-area` 内，当前实现把手牌挂在 `<ui:ScrollView name="card-scroll">` 的 `contentContainer` 下。

经过排查，四个体验缺陷的根因高度耦合：

1. **ScrollView 默认 Vertical 模式** + 内容溢出 → 点击卡牌触发内置 touch panning，造成"界面上移"。
2. **`flex-direction: row` 写在 ScrollView 自身而非 contentContainer** → 卡牌实际竖排。
3. **`RefreshCards` 仅 `content.Add(item)` 平铺** → 没有按 index 计算 transform，缺少扇形交错。
4. **`OnCardPointerDown` 缺 `CapturePointer`** → 指针离开原卡后 `PointerMoveEvent` 不再派发到 GameScreen，ghost 停止跟随；ScrollView 也会与拖拽抢夺 pointer。

约束：
- 手牌上限较小（一般 ≤7 张），不需要滚动条。
- UI Toolkit 的 pointer event 命中链是 trickle-down/bubble-up，不会无条件冒泡到 root，必须 `CapturePointer` 才能在拖拽中持续接收 move 事件。
- USS 不支持 JS 风格的 nth-child 索引计算，扇形参数必须由 C# 在运行时按手牌数量动态写入 inline style。
- `GameScreen` 已 `position: absolute; left/top=0`，与 panel root 同坐标系，`evt.position` 可直接作为 ghost 的 panel 局部坐标。

## Goals / Non-Goals

**Goals:**
- 一次性消除 4 个交互 bug
- 实现轻微弧形扇形布局（参考杀戮尖塔），在视觉上突出手牌质感
- 区分点击 vs 拖拽，让玩家既能预览卡牌、也能直接拖拽出牌
- 拖拽体验顺滑：ghost 跟随、原卡占位、释放回弹
- 改动严格限定在 UI 层（UXML + USS + GameScreen.cs），不动战斗逻辑/数据/配置

**Non-Goals:**
- 不改动 `CardSystem` / `GameViewModel` / `GameModel` 任何 API
- 不引入指定怪物作为拖拽目标 — 仍统一拖到 `drop-zone` 才出牌
- 不实现卡牌滚动 — 手牌固定容量、扇形可容纳
- 不引入 DOTween / 其它动画库 — 用 USS `transition` 即可
- 不调整 `info-bar` / `player-status` / `monster-area` 等其它区域的布局
- 不改 `RewardPanel` 内的任何交互

## Decisions

### Decision 1：去掉 ScrollView，用绝对定位的扇形容器

**选择**：将 `BattlePanel.uxml` 中的 `<ui:ScrollView name="card-scroll">` 替换为 `<ui:VisualElement name="hand-fan" class="hand-fan">`，且每张卡牌 `position: absolute`。

**理由**：
- 同时根治 Bug #1（无 ScrollView 即无内置滚动）和 Bug #2（直接控制 contentContainer 不再被 ScrollView 默认布局覆盖）。
- 扇形布局需要每张卡的 `left/top/rotate` 都不一样，绝对定位是最自然的方式。
- USS 的 `transform-origin` 可以让 rotate 围绕底边中心，扇形效果更自然。

**替代方案**：
- (A) 保留 ScrollView 切水平模式 — 拒绝。仍需要禁用其内置滚动且无法摆脱 contentContainer 的隐式布局，复杂度高于直接换容器。
- (B) 用 `flex-direction: row` 普通容器 + `margin-left: -20px` 制造层叠 — 拒绝。无法实现扇形旋转角度，只能做平移层叠，视觉效果差。

### Decision 2：放大预览用克隆卡，挂在独立 preview-layer

**选择**：放大态时**克隆**被预览的卡牌，加到 `preview-layer`（覆盖在 `hand-fan` 上的 absolute 容器）；原卡保留在扇形里位置不变。

**理由**：
- 扇形里所有卡都是 `position: absolute`，对原卡做 scale 会让它越界覆盖相邻卡牌、且 transform-origin 与扇形冲突。
- 用克隆卡就能完全独立控制预览的位置（卡牌正上方）和大小（1.6×），不破坏扇形。
- 预览层 `pointer-events: none` 确保不抢拖拽事件。

**替代方案**：
- 直接把原卡 scale 1.6 + 抬升 — 拒绝，理由如上。
- 全屏中央放大 — 用户已选择 (a) 卡牌正上方放大，舍弃。

### Decision 3：CapturePointer + 10px 位移阈值区分点击 vs 拖拽

**选择**：`OnPointerDown` 立即 `target.CapturePointer(evt.pointerId)` 并记录起始位置。`OnPointerMove` 中先判断是否越过 10px 阈值，越过才进入拖拽态、创建 ghost、显示 drop-zone。`OnPointerUp` 时若仍未进入拖拽态则视为点击 → 切换预览态。

**理由**：
- `CapturePointer` 是 UI Toolkit 官方拖拽机制，确保 move/up 事件无条件派发到 capture 元素，根治 Bug #4。
- 位移阈值天然兼容点击（手会有微小抖动，纯比较 down/up 位置不可靠）。
- 10px 是 UI Toolkit 与多数 UI 框架的常见经验值，对手指点击和鼠标点击都安全。

**替代方案**：
- 时间阈值（按住 200ms 进入拖拽）— 拒绝。卡牌游戏拖拽要求即时反馈，等待感差。
- 双击预览 / 单击拖拽 — 拒绝。增加学习成本，与"点击放大"的需求不符。

### Decision 4：扇形布局算法

**公式**（n = 手牌数，i = 当前索引，center = (n-1)/2）：

```
offset      = i - center
rotateZ     = offset * 3°
translateY  = offset² * 3.5px        // 抛物线，外侧下沉
left        = handFanCenterX 
            + offset * cardSpacing 
            - cardWidth/2
            // cardSpacing = min(120, (handFanWidth - cardWidth) / max(1, n-1))
            // 手牌少时按 120px 间隔，多时自动压缩
transformOrigin = 50% 100%           // 底部中心
```

**理由**：
- `offset² * 3.5` 给出"中间高、两边低"的轻微弧形，参数小于杀戮尖塔但保留辨识度。
- `cardSpacing` 自适应宽度，避免手牌增多时溢出 hand-fan 容器。
- 所有数值集中在常量，便于美术后续调整。

### Decision 5：状态机集中管理交互态

**选择**：在 `GameScreen` 内引入 `CardInteractionState` 枚举（`Idle / Hovering / Previewing / Dragging`），每次状态切换走单一入口（`SetState(state, cardIndex)`），由该入口负责创建/销毁 ghost、克隆/移除预览卡、更新 USS 类。

**理由**：
- 当前散落的 `_isDragging` / `_dragCardIndex` / `_dragGhost` 已经难以维护；新增预览/悬停后会进一步发散。
- 状态机让"拖拽强制取消预览"等互斥规则有单一执行点，不会遗漏。
- 单一入口便于后续加日志、加单元测试。

**互斥规则**：
- `Idle` ↔ `Hovering` ↔ `Previewing`（点击切换）
- 任意态 + PointerDown 越阈值 → `Dragging`
- `Dragging` 强制清掉预览克隆卡和 hover 抬升

### Decision 6：拖拽回弹与原卡占位

**选择**：
- 进入拖拽态时给原卡加 `card-item--placeholder` 类（`opacity: 0.3`），保持位置不动。
- 释放在 drop-zone 外时，让 ghost 通过 USS `transition` 平滑过渡到原卡 `worldBound` 中心，过渡完成后销毁 ghost、移除 placeholder 类。
- 释放在 drop-zone 内时立即销毁 ghost 并调用 `ViewModel.UseCard(index)`，不做回弹动画。

**理由**：
- 原卡留位避免扇形重排（n 减 1 → 重新计算所有卡的 transform → 视觉抖动）。
- 回弹动画用 USS `transition-property: left, top; transition-duration: 0.15s` 即可，无需额外动画系统。
- 出牌后 `ViewModel.Hand` 会变更触发 `RefreshCards`，扇形自然过渡到新数量。

### Decision 7：drop-zone 仅在拖拽态显示

**选择**：保持 `.drop-zone { display: none }` 默认，进入拖拽态时给 drop-zone 加 `active` 类（已有的 `.drop-zone.active { display: flex; animation: ... }`），离开拖拽态移除该类。

**理由**：现状代码已经按此设计，只需要确保新状态机在 `Dragging` 进出时正确加/去除 `active` 类。

## Risks / Trade-offs

- **[风险] 手牌数量超过预设区域宽度** → 自适应 `cardSpacing` 在 n 很大时会让卡牌严重重叠；当前 `HandLimit` 在 5–7 之间，120px 间隔下手牌区 ~960px 完全够用；若后续 HandLimit 显著上调需要重新设计。
- **[风险] CapturePointer 与 hover 事件冲突** → 在 capture 期间 `PointerEnter/Leave` 会派发到 capture 目标而非真实命中元素；通过状态机在 `Dragging` 态屏蔽 hover 处理可规避。
- **[风险] USS transition 的回弹起点是当前 left/top，需要在销毁前一帧确保 ghost 还在鼠标位置** → 在 `OnPointerUp` 释放时先把 ghost 的 left/top 写为目标值再触发 transition，避免起点跳变。
- **[风险] 扇形旋转后卡牌的 hover 命中区域**变成旋转后的 OBB 而非 AABB → UI Toolkit 内部已按 transform 处理 pointer 命中，无需额外处理；如出现命中异常再考虑给卡牌外层包一个未旋转的 wrapper。
- **[Trade-off] 预览克隆卡是新建 VisualElement** → 每次切换预览都创建/销毁；考虑到预览卡只有一张、操作频率低，性能不是问题。如未来预览态频繁切换可改为常驻一个 `display: none` 的克隆卡复用。
- **[Trade-off] 不引入动画库** → 回弹/抬升靠 USS transition，曲线只能 ease-in/out，比 DOTween 的弹性曲线略平淡；但避免引入新依赖、保持构建轻量。

## Migration Plan

1. 改 UXML：替换 `card-scroll` 为 `hand-fan` 与 `preview-layer`。
2. 改 USS：新增扇形/预览/拖拽/hover 类，保留原 `.card-item` 基础样式但去掉 `margin`（改用 absolute 定位）。
3. 改 `GameScreen.cs`：
   - 替换字段 `_cardScroll: ScrollView` → `_handFan: VisualElement`，新增 `_previewLayer`
   - 新增 `CardInteractionState` 枚举与 `SetState` 入口
   - `RefreshCards` 计算扇形 transform 并写 inline style；保留 placeholder 卡的 transform，不重写
   - `OnCardPointerDown` 改为：CapturePointer + 记录起点，不立即创建 ghost
   - 新增 `OnCardPointerMove` 处理阈值判断和 ghost 跟随
   - 新增 `OnCardPointerEnter/Leave` 处理 hover
   - 新增 `EnterPreview / ExitPreview / EnterDragging / ExitDragging` 内部方法
4. 编译检查：通过 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py`。
5. 手动验证：在 Unity Editor 中进入 `GameView`，用鼠标依次验证 4 个 bug 全部消除 + 新增预览/悬停体验。
6. 通过 `/opsx:archive` 归档。

回滚策略：所有改动集中在 3 个文件 + 1 个 spec，git revert 单个 commit 即可恢复。

## Open Questions

- 是否需要给放大预览加底色 / 描边以与扇形原卡区分？当前方案是同样视觉，仅放大 1.6×；若美术希望更"高亮"可后续在 `.card-item--preview` 类里加 box-shadow。
- 拖拽出牌成功后，是否要在 `drop-zone` 内做命中确认动画（一闪 / 抖动）？当前不做，留给后续抛光。
