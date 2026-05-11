## 1. 切片接口

- [x] 1.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Context/ITurnContext.cs`：`Phase` 只读 ReactiveProperty、`EndTurn()` 命令、`event Action<string> CardPlayFailed`
- [x] 1.2 新增 `ITargetContext.cs`：`Phase`、`UseCardOnMonster(int handIdx, int monsterIdx)`
- [x] 1.3 新增 `IBattleContext.cs`：继承 `IPlayerStatusContext, IMonsterListContext, IHandContext, ITurnContext, ITargetContext`
- [x] 1.4 让 `GameViewModel` 实现 `IBattleContext`（含子接口）；新增 `UseCardOnMonster(int, int)` 方法转发到 `UseCard(int, int)`
- [x] 1.5 编译验证

## 2. TurnControlView

- [x] 2.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/TurnControlView.cs`，构造参数 `(Button endTurnBtn, Label failToast, ITurnContext context)`
- [x] 2.2 注册 `endTurnBtn.ClickEvent` 转发 `context.EndTurn()`
- [x] 2.3 订阅 `Phase.Changed` 控制 `endTurnBtn.SetEnabled(phase == PlayerTurn)`，构造时立即同步一次
- [x] 2.4 订阅 `CardPlayFailed`：实现中文映射 + `_toastVersion` 自增 + `failToast.AddToClassList("fail-toast--visible")` + `failToast.schedule.Execute` 检查版本号 1200ms 后 `RemoveFromClassList`
- [x] 2.5 实现 `Dispose()`：解绑全部、自增 `_toastVersion`（让残留 schedule 检查不通过）、字段置空、幂等

## 3. TurnControlView 测试

- [x] 3.1 新增 `Tests/EditMode/Game/UI/Views/TurnControlViewTests.cs`
- [x] 3.2 新增 `Fakes/FakeTurnContext.cs` 实现 `ITurnContext`
- [x] 3.3 用例：`Phase=PlayerTurn` → `endTurnBtn.enabledSelf == true`；`Phase=MonsterTurn` → `false`；切换覆盖
- [x] 3.4 用例：点击按钮 → `EndTurn` 调用计数 +1（用 ClickEvent.GetPooled + SendEvent）
- [x] 3.5 用例：`CardPlayFailed("InsufficientEnergy")` → `failToast.text == "能量不足"` + 含 `fail-toast--visible` 类
- [x] 3.6 用例：未知 reason → `"出牌失败"` + `NotPlayerTurn → "现在不是你的回合"`
- [ ] 3.7 用例（同步等价）：连续两次失败 + 模拟 schedule 触发 → 第二次的版本号生效——defer 到手动验证（`schedule.Execute` 在 EditMode 不易精确触发，标注在 task 10.5）
- [x] 3.8 用例：`Dispose` 后 `Phase` 变化不再操作 `endTurnBtn` + `Dispose` 后 `CardPlayFailed` 不再修改 toast + 重复 `Dispose` 安全

## 4. MonsterListView 扩展

- [x] 4.1 在 `MonsterListView` 加私有字段 `bool _targetModeActive`、`Action<int> _onTargetClick`、`List<EventCallback<ClickEvent>> _targetClickHandlers`
- [x] 4.2 实现公开 `EnterTargetMode(Action<int> onMonsterClick)`：缓存 callback、对每只存活怪物 `MonsterItemView.Root` 加 `target-selectable.active` 类与临时点击回调
- [x] 4.3 实现公开 `ExitTargetMode()`：清类、解临时回调、清缓存
- [x] 4.4 修改 `Refresh()`：重建后若 `_targetModeActive == true`，重新对存活怪物应用类与回调
- [x] 4.5 在 `Dispose()` 中调 `ExitTargetMode()` 兜底

## 5. HandFanView 扩展

- [x] 5.1 `HandFanView.RequestGhostCleanup()`：转调 `_dragController.RequestGhostCleanup()`，幂等
- [x] 5.2 `HandFanView.RequestGhostRebound(handIdx)`：在 _cardItems 中查 visualIdx → 转调 `_dragController.BeginExternalRebound(visualIdx)`
- [x] 5.3 `CardDragController.BeginExternalRebound(visualIdx)` 实现：暂时切到 `Dragging.Detached` + 复用 `StartReboundAnimation` 内部方法
- [x] 5.4 非预期状态调用时通过 `Log.Warning` 记录，幂等返回

## 6. TargetSelector

- [x] 6.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/TargetSelector.cs`，构造参数 `(VisualElement rootElement, IMonsterTargetSurface monsterList, IHandGhostSurface handFan, ITargetContext context)` —— 用接口替代具体类便于测试 mock；MonsterListView/HandFanView 分别实现 `IMonsterTargetSurface` / `IHandGhostSurface`
- [x] 6.2 维护 `_state` (Idle / Active)、`_selectedHandIdx`、`_keyHandler` (EventCallback<KeyDownEvent>)、`_backdropHandler` (EventCallback<PointerDownEvent>)
- [x] 6.3 实现 `Enter(int handIdx)`：迁移现 `GameView.EnterSelectingTarget` 全部逻辑
- [x] 6.4 实现 `OnMonsterClicked(int monsterIdx)`：调 `_context.UseCardOnMonster(_selectedHandIdx, monsterIdx)` + `ExitInternal(confirmed)`
- [x] 6.5 实现 `Cancel()`：外部调用入口，调 `ExitInternal(cancelled)`
- [x] 6.6 实现 `ExitInternal(reason)`：调 `_monsterList.ExitTargetMode()`、解 ESC + 空白监听、`reason==confirmed` → `_handFan.RequestGhostCleanup()`，`reason==cancelled` → `_handFan.RequestGhostRebound(_selectedHandIdx)`、状态归 Idle
- [x] 6.7 实现 `IsActive` 只读属性
- [x] 6.8 实现 `Dispose()`：若 IsActive 调 `Cancel()`、字段置空、幂等

## 7. TargetSelector 测试

- [x] 7.1 新增 `Tests/EditMode/Game/UI/Views/TargetSelectorTests.cs`
- [x] 7.2 新增 `FakeTargetContext`、`FakeMonsterTargetSurface`、`FakeHandGhostSurface`
- [x] 7.3 用例：Enter 后 `_monsterList.EnterTargetMode` 调用 + `IsActive=true`
- [x] 7.4 用例：怪物点击 → `UseCardOnMonster(handIdx, monsterIdx)` 调用 + `IsActive=false` + ghost cleanup
- [x] 7.5 用例：Cancel 调用 → `ghost rebound(handIdx)` + `ExitTargetMode`
- [x] 7.6 用例：Active 时 Dispose 等价于 Cancel + ESC 取消 + 重复 Enter 忽略 + 重复 Dispose 安全

## 8. BattlePanelView

- [x] 8.1 新增 `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/BattlePanelView.cs`，构造参数 `(VisualElement content, IBattleContext context, VisualTreeAsset monsterItemTpl, VisualTreeAsset cardItemTpl, HandFanLayoutOptions options)`
- [x] 8.2 在构造中查询 6 个共享元素，记录到字段
- [x] 8.3 装配 `MonsterListView` / `HandFanView` / `TurnControlView` / `TargetSelector`（按设计 §1 顺序）
- [x] 8.4 订阅 `_handFanView.CardDroppedOnZone += OnCardDroppedOnZone`，路由到 `_targetSelector.Enter(idx)` 或 `_context.UseCard(idx)`
- [x] 8.5 订阅 `_handFanView.CardClicked` / `CardDragCancelled`：本视图无操作（注释标明原因）
- [x] 8.6 订阅 `_context.Phase.Changed`：若 `_targetSelector.IsActive && phase != PlayerTurn` → `_targetSelector.Cancel()`（用 `((ITurnContext)_context).Phase` 消解多接口二义性）
- [x] 8.7 实现 `Dispose()`：按反序释放（TargetSelector → TurnControlView → HandFanView → MonsterListView），解绑事件，幂等

## 9. GameView 协调器最终瘦身

- [x] 9.1 在 `OnSetup` 中保留 `Region` 创建、`PlayerStatusView` 装配、`Phase.Changed` 订阅；删除 `Hand.Changed` / `Monsters.Changed` / `CardPlayFailed` 等订阅（已迁出）
- [x] 9.2 在 `BindBattleContent` 中删除全部子模块装配代码，改为 `_battlePanelView?.Dispose(); _battlePanelView = new BattlePanelView(content, ViewModel, _monsterItemVta, _cardItemVta, _handFanLayoutOptions);`
- [x] 9.3 删除 `GameView` 中所有 `SelectingTarget` 相关：字段 (`_selectingTargetCardIndex`、`_selectingTargetGhost`、`_monsterClickHandlers`、`_selectingTargetKeyHandler`、`_selectingTargetBackdropHandler`)、方法 (`EnterSelectingTarget`、`OnMonsterTargetClicked`、`CancelSelectingTarget`、`ExitSelectingTarget`、`IsSameOrAncestor`、`ClearSelectingTargetMonsterHighlights`、`UnregisterSelectingTargetCallbacks`)
- [x] 9.4 删除 `_endTurnBtn` / `_failToast` / `_failToastVersion` / `OnCardPlayFailed` 全部移到 TurnControlView
- [x] 9.5 简化 `OnPhaseChanged`：仅保留 Region 切换逻辑（`endTurnBtn.SetEnabled` 由 TurnControlView 自己处理）
- [x] 9.6 简化 `OnDispose`：调 `_playerStatusView?.Dispose()` + `_battlePanelView?.Dispose()` + `_mainRegion?.Clear()`
- [x] 9.7 验证 `GameView.cs` 行数从 ~446 降至 155（任务目标 ~100；BindRewardContent + LoadItemTemplates + 模板加载辅助方法占额外 ~50 行，可接受）

## 10. 验收

- [x] 10.1 编译检查通过：`dotnet build UnityProject.slnx --no-restore` 0 error 0 warning
- [ ] 10.2 EditMode 测试全绿（待 Unity Test Runner 手动触发；含 change 1 / change 2 既有用例 + 本 change 新增 TurnControlView 9 个 + TargetSelector 7 个 = 共 16 个新用例）
- [x] 10.3 `openspec validate gameview-extract-battle-coordinator` 通过
- [ ] 10.4 手动验证完整战斗流程：
  - 路径 G：进入战斗 → 玩家回合（end-turn 按钮启用、HP/能量正常、怪物意图正常）→ 出 AutoTarget 卡（消能量、卡移除、伤害正常应用）→ 出 SingleManual 卡（保留 ghost、怪物高亮、点击怪物结算）→ ESC 取消选目标（ghost 协同回弹、怪物高亮取消、能量未消耗）→ 空白点击取消选目标（同上）→ 出错卡（fail-toast 显示中文，1.2s 自动消失）
  - 路径 H：玩家回合点 end-turn → 怪物回合（按钮禁用，无法操作手牌）→ 怪物攻击 → 玩家回合（按钮重新启用）
  - 路径 I：连续两次出错卡（间隔 < 1.2s）→ toast 始终显示新失败文本，从最后一次起 1.2s 后消失（验证版本号机制）
  - 路径 J：进入选目标后立即触发怪物回合（外部强制 Phase 切换）→ 选目标自动取消，ghost 回弹，无悬挂监听
- [ ] 10.5 手动验证 Region 切到 RewardPanel：BattlePanelView 被 Dispose 无异常；切回 BattlePanel 子模块全部重建正常
- [ ] 10.6 手动验证 Editor 关闭 / 场景切换 → `GameView.OnDispose` 释放全部子模块无 NullReferenceException
- [x] 10.7 `GameView.cs` 行数最终 155 行（协调器形态，目标 ~100，实际包含 BindRewardContent + LoadItemTemplates 共 6 个方法）；新增 13 个文件（3 切片接口 + 4 子模块视图 + 2 surface 接口 + 4 新建子视图）分布合理
