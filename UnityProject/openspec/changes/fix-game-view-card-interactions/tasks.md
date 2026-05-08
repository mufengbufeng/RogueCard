## 1. UXML 与 USS 结构调整

- [x] 1.1 修改 `Assets/AssetRaw/UI/Game/BattlePanel.uxml`：删除 `<ui:ScrollView name="card-scroll">`，替换为 `<ui:VisualElement name="hand-fan" class="hand-fan">` 与 `<ui:VisualElement name="preview-layer" class="preview-layer" picking-mode="Ignore">`
- [x] 1.2 修改 `Assets/AssetRaw/UI/Game/GameViewStyles.uss`：在 `.card-area` 之后新增 `.hand-fan { position: relative; flex-grow: 1; }` 与 `.preview-layer { position: absolute; left:0; right:0; top:0; bottom:0; }` 容器样式
- [x] 1.3 修改 `.card-item`：去掉 `margin: 6px 10px`，改为 `position: absolute; transform-origin: 50% 100%; transition-property: translate, rotate, scale, opacity; transition-duration: 0.15s;`
- [x] 1.4 新增 `.card-item--placeholder { opacity: 0.3; }` 类用于拖拽占位
- [x] 1.5 新增 `.card-item--preview { scale: 1.6; }` 类（克隆预览卡用），并配套 `.card-ghost` 增加 `transition-property: left, top; transition-duration: 0.15s` 用于回弹动画
- [x] 1.6 调整 `.card-item:hover`：保留视觉高亮，新增 `.card-item--hovering { translate: 0 -20px; scale: 1.05; }` 类供 C# 控制
- [x] 1.7 在 Unity Editor 中确认 UXML 与 USS 编辑无报错（语法、引用路径正确）

## 2. GameScreen 字段与状态机重构

- [ ] 2.1 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameScreen.cs` 中将字段 `_cardScroll: ScrollView` 替换为 `_handFan: VisualElement`，并新增 `_previewLayer: VisualElement`
- [ ] 2.2 新增私有枚举 `CardInteractionState { Idle, Hovering, Previewing, Dragging }` 与字段 `_state`、`_activeCardIndex`、`_pointerStartPos`、`_dragGhost`、`_previewClone`、`_capturedPointerId`
- [ ] 2.3 删除原 `_isDragging`、`_dragCardIndex` 散落字段；统一通过 `_state` 与 `_activeCardIndex` 表达
- [ ] 2.4 在 `BindBattleContent` 中改为查询 `hand-fan` 与 `preview-layer`，更新失败时打印明确错误日志
- [ ] 2.5 新增 `SetState(CardInteractionState newState, int cardIndex)` 单一状态切换入口，负责进出态时的副作用清理与触发
- [ ] 2.6 新增常量 `DragThreshold = 10f`、`HoverLift = 20f`、`PreviewScale = 1.6f`、`MaxCardSpacing = 120f`、`RotatePerStep = 3f`、`TranslateYCoeff = 3.5f` 集中管理参数

## 3. 扇形布局与渲染

- [ ] 3.1 重写 `RefreshCards`：清空旧卡 → 按 `ViewModel.Hand.Value` 数量计算每张卡 transform → 写入 inline style（`style.left`、`style.translate`、`style.rotate`）
- [ ] 3.2 实现私有方法 `ApplyFanTransform(VisualElement card, int index, int total)`：按设计文档公式计算 `offset / rotateZ / translateY / left` 并写入 inline style；hand-fan 宽度通过 `_handFan.resolvedStyle.width` 取得（resolvedStyle 未就绪时退化为 `_handFan.layout.width` 或常量）
- [ ] 3.3 处理 hand-fan 初始 layout 未就绪的情况：在 `OnSetup` 或 `BindBattleContent` 中给 `_handFan` 注册 `GeometryChangedEvent`，几何变更后重排扇形
- [ ] 3.4 注册卡牌的 `PointerDownEvent`、`PointerEnterEvent`、`PointerLeaveEvent` 回调
- [ ] 3.5 验证 5 张手牌呈现轻微弧形扇形（中央 0°、最外侧 ±6°、中央最高、两端略下沉）

## 4. 拖拽交互（CapturePointer + 阈值）

- [ ] 4.1 实现 `OnCardPointerDown(PointerDownEvent evt, int cardIndex, VisualElement source)`：记录 `_pointerStartPos = evt.position`、调用 `source.CapturePointer(evt.pointerId)`、保存 `_capturedPointerId`，注册 `PointerMoveEvent / PointerUpEvent / PointerCaptureOutEvent` 到 `source`，但**不立即**进入 Dragging 态
- [ ] 4.2 实现 `OnCardPointerMove`：若 `_state != Dragging` 且 `Vector2.Distance(evt.position, _pointerStartPos) > DragThreshold` 则 `SetState(Dragging, cardIndex)`；若已在 Dragging 态则更新 ghost 位置
- [ ] 4.3 实现 `EnterDragging`：克隆卡牌为 ghost VisualElement 加入 `this`（Screen 根），原卡加 `card-item--placeholder` 类，drop-zone 加 `active` 类，强制清除预览态
- [ ] 4.4 实现 `UpdateGhostPosition(Vector2 position)`：将 ghost 的 `style.left = position.x - cardWidth/2`、`style.top = position.y - cardHeight/2`
- [ ] 4.5 实现 `OnCardPointerUp`：若 `_state == Dragging` 且指针在 `_dropZone.worldBound` 内 → 销毁 ghost、调用 `ViewModel.UseCard(_activeCardIndex)`、`SetState(Idle, -1)`；若 `_state == Dragging` 且不在 drop-zone 内 → 触发回弹（4.6）；若 `_state != Dragging` → 视为单击 → 切换预览
- [ ] 4.6 实现拖拽回弹：写入 ghost 的 `style.left/top` 为原卡 `worldBound.center` 对应坐标 → schedule.Execute 在 transition-duration 后销毁 ghost、移除 placeholder 类、`SetState(Idle, -1)`
- [ ] 4.7 注册 `PointerCaptureOutEvent` 兜底：若中途丢失 capture 立即 `SetState(Idle, -1)` 并清理状态
- [ ] 4.8 在 `OnCardPointerUp` 与回弹完成时调用 `source.ReleasePointer(_capturedPointerId)`，并 Unregister `PointerMoveEvent / PointerUpEvent / PointerCaptureOutEvent`

## 5. 点击预览

- [ ] 5.1 实现 `EnterPreview(int cardIndex)`：克隆当前卡牌（CardItem.uxml.CloneTree() + 填充 name/cost）→ 加 `card-item--preview` 类 → 加入 `_previewLayer` → 设 `pointer-events: none` (`pickingMode = PickingMode.Ignore`) → 计算位置：原卡 `worldBound` 上方 1.6× 高度处
- [ ] 5.2 实现 `ExitPreview`：销毁 `_previewClone`，置 null
- [ ] 5.3 单击切换逻辑：`OnCardPointerUp` 单击分支判断 `_state == Previewing && _activeCardIndex == cardIndex` → ExitPreview + `SetState(Idle, -1)`；否则 ExitPreview（如果有）→ EnterPreview(cardIndex) + `SetState(Previewing, cardIndex)`
- [ ] 5.4 在 `EnterDragging` 中无条件调用 `ExitPreview` 确保互斥
- [ ] 5.5 在 `RefreshCards`（手牌变更）时若处于预览态则强制 `SetState(Idle, -1)` 防止悬空克隆卡

## 6. 悬停抬升

- [ ] 6.1 实现 `OnCardPointerEnter(PointerEnterEvent evt, int cardIndex, VisualElement source)`：仅在 `_state == Idle` 时给 source 加 `card-item--hovering` 类
- [ ] 6.2 实现 `OnCardPointerLeave`：移除 `card-item--hovering` 类
- [ ] 6.3 在 `EnterDragging` / `EnterPreview` 时清除所有卡牌的 `card-item--hovering` 类，避免残留抬升

## 7. 兼容、清理与日志

- [ ] 7.1 删除原 `OnDragMove`、`OnDragEnd`、`CreateDragGhost`、`UpdateGhostPosition` 中已被替换的旧实现，保留新版统一命名
- [ ] 7.2 在 `OnDispose` 中确保：取消所有 capture（若仍持有）、销毁 ghost / preview clone、清空 hand-fan 与 preview-layer 子节点
- [ ] 7.3 关键状态切换处加 `Log.Info` / `Log.Warning`：`SetState`、capture 丢失、回弹完成
- [ ] 7.4 确认 `GameScreen.cs` 不再引用 `ScrollView` 类型与 `card-scroll` 字符串

## 8. 编译与验证

- [ ] 8.1 运行 `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` 确保 GameLogic 程序集无编译错误
- [ ] 8.2 在 Unity Editor 打开场景进入 GameView，验证 Bug #1：点击卡牌不再让界面上移
- [ ] 8.3 验证 Bug #2：手牌横向扇形排列，最多 5–7 张时不溢出
- [ ] 8.4 验证 Bug #3：卡牌呈现轻微弧形（中央 0°、两端 ±6°、外侧略下沉）
- [ ] 8.5 验证 Bug #4：按住卡牌拖动，ghost 实时跟随鼠标
- [ ] 8.6 验证新增交互：单击卡牌正上方出现 1.6× 放大预览、再次单击关闭、单击其它卡切换、悬停抬升 20px、拖到 drop-zone 出牌、拖到 drop-zone 外回弹
- [ ] 8.7 运行 `openspec validate fix-game-view-card-interactions --strict` 通过

## 9. 归档准备

- [ ] 9.1 提交所有修改并撰写中文 commit message（描述变更内容和原因）
- [ ] 9.2 通过 `/opsx:verify fix-game-view-card-interactions` 校验实现与 spec 一致
- [ ] 9.3 通过 `/opsx:archive fix-game-view-card-interactions` 归档变更
