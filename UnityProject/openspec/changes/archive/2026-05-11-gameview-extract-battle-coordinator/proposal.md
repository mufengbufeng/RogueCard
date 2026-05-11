## Why

Change 1 与 change 2 完成后，`GameView` 仍承载 ~400 行：BattlePanel content 加载、`MonsterListView` / `HandFanView` 装配、`SelectingTarget` 跨模块编排（ESC / 空白点击 / 怪物点击）、`end-turn-btn`、`fail-toast`、`RewardPanel` 按钮处理、Region 切换。其中跨模块编排（`SelectingTarget`）涉及 `HandFanView`（保留 ghost）+ `MonsterListView`（高亮 + 临时点击）+ `IHandContext`（最终调 `UseCard(idx, monsterIdx)`）三方协作，散落在 `GameView` 中违反"协调器只协调"原则。

本 change 抽出顶层 `BattlePanelView` 协调器，把战斗子界面的所有装配与跨模块流程从 `GameView` 剥离；同时把"目标选择"与"回合控制 + fail toast"两个独立流程拆成 `TargetSelector` 与 `TurnControlView`。`GameView` 退化为纯顶层路由：常驻区域（`PlayerStatusView`）+ Region 切换 + `RewardPanel` 简单按钮处理。

## What Changes

- **新增** `BattlePanelView` 子模块，作为 BattlePanel content 的顶层协调器：在 `Region.ShowAsync("BattlePanel")` 后实例化，内部装配 `MonsterListView` + `HandFanView` + `TargetSelector` + `TurnControlView` + 共享元素（`preview-layer`、`drop-zone`），订阅 `HandFanView` 三事件并按 `needsManualTarget` 路由
- **新增** `TargetSelector` 子模块，封装 `SelectingTarget` 跨模块编排：`Enter(handIdx)` 给 `MonsterListView` 加目标高亮、注册 ESC / 空白点击 / 怪物点击监听；`MonsterClicked` → `IHandContext.UseCard(handIdx, monsterIdx)` → `Exit`；`Cancelled` → 调 `HandFanView.RequestGhostRebound(handIdx)` 复用回弹动画
- **新增** `TurnControlView` 子模块，封装 `end-turn-btn` + `fail-toast`，订阅 `Phase.Changed` 启用按钮、订阅 `CardPlayFailed` 显示 toast（含版本号"新失败覆盖旧失败"机制）
- **新增** `IBattleContext` / `ITargetContext` / `ITurnContext` 切片接口
- **扩展** `MonsterListView` 暴露 `EnterTargetMode(Action<int> onClick)` / `ExitTargetMode()` API；内部为每只存活怪物添加 `target-selectable.active` 类与临时点击回调
- **扩展** `HandFanView` 暴露 `RequestGhostCleanup()` / `RequestGhostRebound(handIdx)`，复用 change 2 的 ghost 销毁与协同回弹逻辑
- **GameView 瘦身**：保留 `OnSetup` 装配 `PlayerStatusView` + `OnPhaseChanged` 调度 Region 切换 + `BindBattleContent` 实例化 `BattlePanelView` + `BindRewardContent` 注册 `reward-confirm-btn` + `OnDispose` 兜底；删除 `SelectingTarget` 全部字段与方法、`end-turn-btn` / `fail-toast` 全部处理
- **测试** EditMode 用例：`TurnControlView` 阶段→按钮启用、fail toast 版本号机制、`TargetSelector` ESC / 空白 / 怪物点击三路径状态转移

## Capabilities

### New Capabilities

- `gameview-battle-panel-coordinator`: BattlePanel 子界面顶层协调器契约，内部装配子模块、订阅 `HandFanView` 事件、按 `needsManualTarget` 路由到 `TargetSelector` 或直接调 `UseCard`
- `gameview-target-selection-flow`: 出 SingleManual 卡的目标选择跨模块流程（`HandFanView` 保留 ghost → `MonsterListView` 进入高亮态 → 怪物点击 / ESC / 空白点击三选一）契约
- `gameview-turn-control-view`: 结束回合按钮 + 出牌失败 toast 子模块契约（含版本号"新失败覆盖旧失败"）

### Modified Capabilities

- `game-ui-data-binding`: 把 "GameScreen 必须支持 Region 切换 Battle 和 Reward 视图" / 出牌失败 toast / 选目标流程相关要求迁出，改写为 "`GameView` 转为顶层路由、`BattlePanelView` 装配战斗子模块、`TargetSelector` 编排目标选择"
- `gameview-monster-list-view` (来自 change 1): 新增 `EnterTargetMode(Action<int> onClick)` / `ExitTargetMode()` 公开 API
- `gameview-hand-fan-view` (来自 change 2): 新增 `RequestGhostCleanup()` / `RequestGhostRebound(handIdx)` 公开 API

## Impact

- **框架层** 不变
- **游戏层** (`Assets/GameScripts/HotFix/GameLogic/UI/Game/`) — 新增 ~7 个文件：
  - `Context/IBattleContext.cs`、`ITargetContext.cs`、`ITurnContext.cs`
  - `Views/BattlePanelView.cs`、`TargetSelector.cs`、`TurnControlView.cs`
  - `MonsterListView.cs` / `HandFanView.cs` 扩展（追加方法）
  - `GameView.cs` 减少约 300 行（从 ~400 降至约 100）
- **资源** 不变
- **测试** — 新增 `Tests/EditMode/UI/Game/Views/TurnControlViewTests.cs`、`TargetSelectorTests.cs`
- **可选 PlayMode 测试**：出 SingleManual 卡 → 选怪物 happy path（端到端）；本 change 不强制
- **风险** — 中。跨模块协议设计若有遗漏会回归。生命周期需特别注意：`TargetSelector.Cancelled` 时 ghost 所有权要还给 `HandFanView` 以复用回弹动画
