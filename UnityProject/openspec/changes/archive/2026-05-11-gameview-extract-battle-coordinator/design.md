## Context

三步重构的最后一步。Change 1 完成"叶子"（PlayerStatus / MonsterList），change 2 完成"重型叶子"（HandFan 子系统含状态机和测试），本 change 完成"协调器"——把战斗子界面的所有装配与跨模块流程从 `GameView` 剥离到 `BattlePanelView`。

`GameView` 在本 change 后仅 ~100 行，职责清晰：

```
GameView (Screen<GameViewModel>, ~100 行)
  ├─ OnSetup() → 装 PlayerStatusView (常驻)
  ├─ OnPhaseChanged() → Region.ShowAsync(BattlePanel | RewardPanel)
  ├─ BindBattleContent() → new BattlePanelView(content, viewModel)
  ├─ BindRewardContent() → reward-confirm-btn → vm.SelectReward()
  └─ OnDispose() → Dispose 全部子模块
```

`BattlePanelView` 是战斗子界面的真正"内部协调器"：

```
BattlePanelView (~150 行)
  ├─ 持有 monster-container / hand-fan / drop-zone / preview-layer / end-turn-btn / fail-toast
  ├─ 实例化 MonsterListView (子)
  ├─ 实例化 HandFanView (子, 共享 preview-layer)
  ├─ 实例化 TargetSelector (子, 跨模块编排)
  ├─ 实例化 TurnControlView (子)
  ├─ 订阅 HandFanView 三事件 → 路由到 TargetSelector 或 IHandContext.UseCard
  └─ Dispose 时按反序释放所有子模块
```

跨模块协议的核心是 `SelectingTarget`：拖拽 SingleManual 卡到 drop-zone 松手后，需要协调 `HandFanView`（保留 ghost）、`MonsterListView`（进入高亮态）、`TargetSelector`（监听点击 / ESC / 空白）、`IHandContext`（最终命令）。本 change 把这套编排从 `GameView` 私有方法升格为 `TargetSelector` 类，明确各方接口。

## Goals / Non-Goals

**Goals:**

- 把 `BindBattleContent` 中的子模块装配迁到 `BattlePanelView`
- 抽出 `TargetSelector`，明确"出 SingleManual 卡 → 选目标"流程的跨模块协议
- 抽出 `TurnControlView`，把 `end-turn-btn` 启用控制与 `fail-toast` 显示版本号机制独立
- `GameView` 退化为顶层路由，只负责 Region 切换与 RewardPanel
- 保留所有可观察行为：fail toast 中文映射（`InsufficientEnergy → "能量不足"` 等）、toast 1.2s 自动隐藏、`SelectingTarget` 中 `Phase != PlayerTurn` 强制取消、ESC / 空白点击取消、ghost 在选目标时悬浮在 drop-zone 上

**Non-Goals:**

- 不改 `RewardPanel` 现有行为
- 不引入嵌套 `Region`（`BattlePanelView` 仍由 `GameView._mainRegion` 加载）
- 不重构 `Region` API
- 不抽 `OnPhaseChanged` 中的 `MapPhaseToRegion` 函数（留在 `GameView` 即可）
- 不引入异步 / 延迟 / 队列机制处理 `SelectingTarget`，保持同步

## Decisions

### 决策 1：BattlePanelView 持有共享元素引用

`BattlePanelView` SHALL 持有 `preview-layer`、`drop-zone`、`monster-container`、`hand-fan`、`end-turn-btn`、`fail-toast` 共 6 个共享 / 子区域元素引用。子模块按需通过构造参数获得引用（不持久持有 BattlePanelView 反向引用）。

```csharp
class BattlePanelView : IDisposable
{
    public BattlePanelView(VisualElement content, IBattleContext ctx,
                           VisualTreeAsset monsterItemTpl, VisualTreeAsset cardItemTpl)
    {
        _previewLayer = content.Q("preview-layer");
        _dropZone = content.Q("drop-zone");
        _monsterContainer = content.Q("monster-container");
        _handFan = content.Q("hand-fan");
        _endTurnBtn = content.Q<Button>("end-turn-btn");
        _failToast = content.Q<Label>("fail-toast");

        _monsterListView = new MonsterListView(_monsterContainer, ctx, monsterItemTpl);
        _handFanView = new HandFanView(_handFan, _dropZone, _previewLayer, ctx, cardItemTpl, _options);
        _turnControlView = new TurnControlView(_endTurnBtn, _failToast, ctx);
        _targetSelector = new TargetSelector(_monsterListView, _handFanView, ctx);

        _handFanView.CardClicked += OnCardClicked;
        _handFanView.CardDroppedOnZone += OnCardDroppedOnZone;
        _handFanView.CardDragCancelled += OnCardDragCancelled;
    }

    void OnCardDroppedOnZone(int handIdx, bool needsManualTarget)
    {
        if (needsManualTarget) _targetSelector.Enter(handIdx);
        else ctx.UseCard(handIdx);
    }
}
```

**为什么 BattlePanelView 持有 6 个 VisualElement 而不是仅持有子模块？** Region 切换时整个 content 会重建，这 6 个元素只在 BattlePanelView 生命周期内有效。统一查询 + 构造参数注入比每个子模块自己 `Q<>()` 更省事且不重复。

**为什么不让 `IBattleContext` 暴露 VisualElement？** Context 应只暴露 ViewModel 切片，不掺 UI 引用。UI 引用属于"装配信息"，应通过构造参数。

### 决策 2：TargetSelector 跨模块协议

`TargetSelector` 是本 change 的设计核心。明确三方协议：

```
TargetSelector.Enter(handIdx)
  ├─ _state = Active
  ├─ _selectedHandIdx = handIdx
  ├─ _monsterListView.EnterTargetMode(OnMonsterClicked)
  ├─ 注册 ESC 监听 (TrickleDown 到 GameView root)
  ├─ 注册空白点击监听 (TrickleDown 到 GameView root)
  └─ 注：ghost 仍由 HandFanView 持有，无需迁移所有权

OnMonsterClicked(monsterIdx)
  ├─ _ctx.UseCard(_selectedHandIdx, monsterIdx)
  └─ ExitInternal(reason: confirmed)  // ghost 由 HandFanView 自然清理 (因为 Hand 变化触发 RefreshCards)

OnEscape() / OnBackdropClicked()
  └─ ExitInternal(reason: cancelled)

ExitInternal(reason)
  ├─ _monsterListView.ExitTargetMode()
  ├─ 解除 ESC + 空白监听
  ├─ if (reason == cancelled) _handFanView.RequestGhostRebound(_selectedHandIdx)
  ├─ if (reason == confirmed) _handFanView.RequestGhostCleanup()
  └─ _state = Idle
```

**关键设计：ghost 不在 TargetSelector 中迁移所有权。** 现有代码把 `_dragGhost` 转给 `_selectingTargetGhost` 字段，本 change 简化为：ghost 一直由 `HandFanView` 持有，`TargetSelector` 只负责"该不该清理 / 该不该回弹"，通过 `RequestGhostCleanup` / `RequestGhostRebound` 两个方法表达意图。

**`HandFanView.RequestGhostRebound(handIdx)` 实现：** 复用 change 2 中 `CardDragController.StartReboundAnimation` 的协同回弹逻辑——立即销毁 ghost、其他卡 transition 0.15s 回到 N 张布局、被拖卡 opacity 立即恢复。但本调用并非源自 PointerUp，需要通过 `IDragSurface` 暴露一个 `BeginExternalRebound(handIdx)` 入口或直接在 `HandFanView` 中实现路径（具体由 change 2 末尾遗留的接口决定，本 change 复用即可）。

**Phase 中途变化的强制取消：** `BattlePanelView` 订阅 `IBattleContext.Phase.Changed`，若 `_targetSelector.IsActive && phase != PlayerTurn`，调 `_targetSelector.Cancel()`。

### 决策 3：TurnControlView——版本号 fail toast 机制保留

`fail-toast` 的"新失败覆盖旧失败"机制（现有代码用 `_failToastVersion` 字段 + `schedule.Execute(...).StartingIn(1200)` 中检查版本号）SHALL 完整保留：

```csharp
class TurnControlView : IDisposable {
    long _toastVersion;

    void OnCardPlayFailed(string reason) {
        var text = MapReasonToZh(reason);
        _failToast.text = text;
        _failToast.AddToClassList("fail-toast--visible");

        long ver = ++_toastVersion;
        _failToast.schedule.Execute(() => {
            if (ver == _toastVersion && _failToast != null) {
                _failToast.RemoveFromClassList("fail-toast--visible");
            }
        }).StartingIn(1200);
    }

    static string MapReasonToZh(string reason) => reason switch {
        "InsufficientEnergy" => "能量不足",
        "NotPlayerTurn" => "现在不是你的回合",
        "InvalidTarget" => "无效目标",
        "InvalidHandIndex" => "卡牌索引错误",
        _ => "出牌失败",
    };
}
```

**测试可验证：** 连续两次失败间隔 < 1.2s，第二次后等 1.5s，断言 toast 已隐藏（说明版本号生效，第一次的回调被"新失败"覆盖）。

### 决策 4：MonsterListView 扩展 API（向后兼容）

`MonsterListView` 在 change 1 已存在，本 change 追加两个方法（不破坏 change 1 已有 API）：

```csharp
public void EnterTargetMode(Action<int> onMonsterClick) {
    _onTargetClick = onMonsterClick;
    _targetClickHandlers.Clear();
    for (int i = 0; i < _monsterItems.Count; i++) {
        var monster = _ctx.Monsters.Value[i];
        if (monster == null || monster.IsDead) continue;
        var item = _monsterItems[i];
        item.Root.AddToClassList("target-selectable");
        item.Root.AddToClassList("active");
        int captured = i;
        EventCallback<ClickEvent> handler = e => { onMonsterClick?.Invoke(captured); e.StopPropagation(); };
        _targetClickHandlers.Add(handler);
        item.Root.RegisterCallback(handler);
    }
}

public void ExitTargetMode() {
    for (int i = 0; i < _monsterItems.Count; i++) {
        var item = _monsterItems[i];
        item.Root.RemoveFromClassList("target-selectable");
        item.Root.RemoveFromClassList("active");
        if (i < _targetClickHandlers.Count) {
            item.Root.UnregisterCallback(_targetClickHandlers[i]);
        }
    }
    _targetClickHandlers.Clear();
    _onTargetClick = null;
}
```

**Refresh 重建怪物项时**：若处于 target 模式，需要重新对新存活怪物加类与回调（防止刷新后高亮丢失）。本 change 加入此保护。

### 决策 5：IBattleContext / ITargetContext / ITurnContext 切片设计

```csharp
public interface IBattleContext :
    IPlayerStatusContext,    // PlayerStatusView 仍通过 GameView 装配, 但 BattlePanelView 也用到 Phase
    IMonsterListContext,
    IHandContext,
    ITurnContext { }

public interface ITargetContext {
    void UseCardOnMonster(int handIdx, int monsterIdx);
    ReactiveProperty<BattlePhase> Phase { get; }   // 用于 Phase 中途变化强制取消
}

public interface ITurnContext {
    ReactiveProperty<BattlePhase> Phase { get; }
    void EndTurn();
    event Action<string> CardPlayFailed;
}
```

`IBattleContext` 是 union 接口（继承多个），方便 `BattlePanelView` 一次拿全。`ITargetContext` 与 `ITurnContext` 是子模块自己的窄切片。

`UseCardOnMonster(handIdx, monsterIdx)` 是 `IHandContext.UseCard(handIdx, monsterIdx)` 的语义命名（`ITargetContext` 不暴露 `UseCard(handIdx)` 单参数版本，避免误用）。

### 决策 6：GameView 协调器最终形态

```csharp
public class GameView : Screen<GameViewModel> {
    private PlayerStatusView _playerStatusView;
    private BattlePanelView _battlePanelView;
    private Button _rewardConfirmBtn;

    private Region _mainRegion;
    private BattlePhase _activeRegionPhase = BattlePhase.Idle;

    protected override void OnSetup() {
        var slot = this.Q("main-region");
        if (slot == null) return;
        _mainRegion = new Region(slot, GameLogicEntry.Resource);
        _playerStatusView = new PlayerStatusView(this, ViewModel);
        ViewModel.Phase.Changed += OnPhaseChanged;
    }

    public override void OnShow() {
        OnPhaseChanged(ViewModel.Phase.Value);
    }

    async void OnPhaseChanged(BattlePhase phase) {
        var target = MapPhaseToRegion(phase);
        if (target == _activeRegionPhase || _mainRegion == null) return;
        _activeRegionPhase = target;
        try {
            switch (target) {
                case BattlePhase.PlayerTurn:
                    await _mainRegion.ShowAsync("BattlePanel");
                    BindBattleContent();
                    break;
                case BattlePhase.Reward:
                    await _mainRegion.ShowAsync("RewardPanel");
                    BindRewardContent();
                    break;
            }
        } catch (Exception e) { Log.Error(...); }
    }

    void BindBattleContent() {
        var content = _mainRegion.CurrentContent;
        if (content == null) return;
        _battlePanelView?.Dispose();
        _battlePanelView = new BattlePanelView(content, ViewModel, _monsterItemVta, _cardItemVta);
    }

    void BindRewardContent() {
        var content = _mainRegion.CurrentContent;
        if (content == null) return;
        _rewardConfirmBtn = content.Q<Button>("reward-confirm-btn");
        _rewardConfirmBtn?.RegisterCallback<ClickEvent>(_ => ViewModel.SelectReward());
    }

    public override void OnDispose() {
        _playerStatusView?.Dispose();
        _battlePanelView?.Dispose();
        _mainRegion?.Clear();
        base.OnDispose();
    }

    static BattlePhase MapPhaseToRegion(BattlePhase phase) => phase switch { ... };
}
```

体积约 100 行。

## Risks / Trade-offs

- **风险**：`TargetSelector` ESC / 空白点击监听注册到 `GameView` root 上（`TrickleDown`），若 `BattlePanelView` 被 Dispose 时未解除监听，会泄漏。
  → 缓解：`BattlePanelView.Dispose()` 调 `_targetSelector.Dispose()`，后者解绑全部监听并清空字段。

- **风险**：`MonsterListView.EnterTargetMode` 期间 `Monsters.Changed` 触发刷新（如怪物死亡）→ 怪物项重建后 `target-selectable` 类丢失。
  → 缓解：`MonsterListView` 内部维护 `_targetModeActive` flag 与 `_currentTargetClickHandler`，刷新后若 flag 为 true，重新对存活怪物应用类与回调。

- **风险**：`HandFanView.RequestGhostRebound(handIdx)` 在非拖拽态被调用（如外部错误调用）会破坏内部状态。
  → 缓解：`HandFanView` 内部检查若当前状态非 `SelectingTarget`（或类似 marker）则忽略；同时记录日志。`TargetSelector` 仅在 `Enter` 后才会调用此方法。

- **权衡**：`IBattleContext` 多继承接口看起来"重"，但替代方案是构造 `BattlePanelView` 时传 4 个 context 参数，可读性差。union 接口方便。

- **权衡**：`fail-toast` 留在 `BattlePanel.uxml`（不放到 `GameUxml.uxml` 常驻区域），意味着 BattlePanel 切走时 fail-toast 也消失。当前 `RewardPanel` 不会出牌，无 fail toast 需求，可接受。

## Migration Plan

1. 引入 `IBattleContext` / `ITargetContext` / `ITurnContext`，让 `GameViewModel` 实现并提供 `UseCardOnMonster(idx, m)` 转发方法
2. 引入 `TurnControlView`，迁移 `end-turn-btn` `SetEnabled` + `OnCardPlayFailed` 全部逻辑（含版本号机制 + 中文映射）；配套 `TurnControlViewTests`
3. 扩展 `MonsterListView` 加 `EnterTargetMode` / `ExitTargetMode` 方法 + 刷新时保护
4. 扩展 `HandFanView` 加 `RequestGhostCleanup` / `RequestGhostRebound(handIdx)` 方法（可能需要给 change 2 引入的 `CardDragController` 加 `BeginExternalRebound` 入口）
5. 引入 `TargetSelector`，迁移 `EnterSelectingTarget` / `OnMonsterTargetClicked` / `CancelSelectingTarget` / `ExitSelectingTarget` / `ClearSelectingTargetMonsterHighlights` / `UnregisterSelectingTargetCallbacks` 全部逻辑
6. 引入 `BattlePanelView`，迁移 `BindBattleContent` 全部装配逻辑、`OnCardPlayFailed` 转发到 `TurnControlView`、订阅 `HandFanView` 三事件、订阅 `Phase.Changed` 强制取消 `TargetSelector`
7. `GameView` 瘦身：删除 `_handFanView` / `_monsterListView` / `_endTurnBtn` / `_failToast` / 全部 `SelectingTarget` 字段与方法 / `OnCardPlayFailed`；`BindBattleContent` 改为只 `new BattlePanelView(...)`
8. 跑测试 + 手动验证全部战斗流程

## Open Questions

- `BattlePanelView` 是否值得让 `RewardPanelView` 也跟进抽出？目前不抽（一个按钮，过度设计）。但如果 RewardPanel 后续加奖励列表、卡牌选择 UI 等，再单独 OpenSpec change 抽出 `RewardPanelView`。
- PlayMode 端到端测试是否本 change 必做？提案中标"可选"，避免阻塞。后续可单独 change 引入 `out-singlemanual-card-end-to-end` 测试。
