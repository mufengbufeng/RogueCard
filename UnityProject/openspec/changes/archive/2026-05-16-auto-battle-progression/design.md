## Context

`BattleSystem` 以 `BattlePhase` 状态机驱动战斗循环：`Prepare → PlayerTurn → MonsterTurn → Check`。`Check` 阶段已经包含"批次全清推进 / 失败结算"逻辑，但只在 `MonsterTurn` 结束后才会进入。换句话说，玩家回合内即使用一张大牌秒杀了当前批次所有怪物，循环仍卡在 `PlayerTurn`，必须等玩家点"结束回合"才走 `MonsterTurn → Check`。

`WaveSystem.AdvanceToNextWave` 在所有波次完成时会发布 `LevelCompleteEvent`，但紧接着又向全局 `GameLogicEntry.Event.StartLevelRequestedEvent` 推了一次相同 `LevelId`。`StartLevelRequestedEvent` 的语义是"主菜单请求开始关卡"，被 `MainMenuProcedure` 订阅来触发流程切换；在 `WaveSystem` 里复用它实际上是一个早期占位实现，会让"关卡完成"被误解为"再开一局相同关卡"。当前 `GameProcedure` 没有任何订阅 `LevelCompleteEvent` 的代码，关卡完成后界面卡在 `GameView`。

涉及的关键文件：
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/BattleSystem.cs` —— `SetPhase`、`ExecuteCheckPhase`、`OnActorDied` 是切入点。
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/WaveSystem.cs` —— `AdvanceToNextWave` 中残留的 `StartLevelRequestedEvent.Publish`。
- `Assets/GameScripts/HotFix/GameLogic/Procedure/Game/GameProcedure.cs` —— `EnterAsync` 与 `Cleanup` 是订阅 / 取订的窗口；`procedureOwner.ChangeState<MainMenuProcedure>(procedureOwner)` 是切回主菜单的标准方式。
- `LocalEventBus` 在 `GameProcedure` 内创建，所有 System 共享；`LevelCompleteEvent` 可以直接在该总线上订阅。

约束：
- 自动结束回合不能引入异步等待——`BattleSystem` 现在是同步状态机，引入 UniTask 会让现有测试和 `MonsterSystem.ExecuteTurn` 时序复杂化。
- 不能在卡牌效果"半路"上递归切相位：`OnActorDied` 是 `IBattleEventSink` 回调，由卡牌效果或 Buff 结算调用，在效果还未走完时切相位会破坏调用栈一致性。
- `GameProcedure.Cleanup` 必须保证多次调用安全（`OnLeave` / `OnDestroy` 都会调用），新增订阅要在 Cleanup 中取消。

## Goals / Non-Goals

**Goals:**
- 玩家回合内当前批次怪物被清空后，`BattleSystem` 同步自动推进到下一阶段，不需要 UI 触发。
- 关卡完成（`LevelCompleteEvent`）后 `GameProcedure` 自动 `ChangeState<MainMenuProcedure>`，关闭 `GameView` 回到 `MainView`。
- 移除 `WaveSystem` 中复用 `StartLevelRequestedEvent` 的临时实现，让"关卡完成"语义清晰。
- 保留现有 `BattleSystem` 的同步状态机风格与测试结构，新增逻辑可被 EditMode 测试覆盖。

**Non-Goals:**
- 不引入"关卡完成动画 / 结算面板 / 延迟返回"等 UI 表现层流程，只保证逻辑路径打通；UI 表现可以在后续单独变更追加。
- 不调整 `BattlePhase` 枚举或新增阶段——清空后的推进继续走现有 `Check` 阶段。
- 不改动主菜单流程对 `StartLevelRequestedEvent` 的订阅。
- 不修改 Luban 配置或事件总线基础设施。

## Decisions

### 决策 1：玩家回合内清空检测放在 `BattleSystem.OnActorDied` 之后的"延迟入口"

**做法：** 在 `BattleSystem` 新增私有方法 `TryAutoEndPlayerTurnIfCleared()`，由"卡牌一次完整结算结束"或"Buff Tick 结束"作为延迟触发点统一调用一次。具体接入位置：
- `CardSystem.Play` 现有调用链最终在结算完所有效果后会回到 `BattleSystem` 检查（或通过 `CardPlayedEvent`）。为了不依赖 `CardSystem` 内部实现，最稳妥的做法是让 `BattleSystem` 订阅本地 `CardPlayedEvent`，在回调里调用 `TryAutoEndPlayerTurnIfCleared()`。
- `BattleSystem` 自身的 `TickBuffs` / `ResolveEnemyTurnStartEffects` / `ResolveEnemyTurnEndEffects` 已经在内部，可以在这些方法尾部直接调用一次（仅当 `Phase == PlayerTurn` 才生效）。

**`TryAutoEndPlayerTurnIfCleared` 行为：**
1. 仅当 `_model.Phase == BattlePhase.PlayerTurn` 才工作；其他阶段（特别是已经在 `MonsterTurn` / `Check`）直接返回。
2. 调用与 `ExecuteCheckPhase` 相同的"批次全清"判定：`_model.Monsters.All(m => m.Hp <= 0)`。
3. 若全清，**直接 `SetPhase(BattlePhase.Check)`**，跳过 `MonsterTurn`——因为已经没有怪物可以执行行动，进 `Check` 会自动推进到下一批次 / 下一波次或发布 `BattleEndedEvent(true)`。

**为什么不直接调用 `EndTurn()`：** `EndTurn` 会走 `MonsterTurn`，触发 `ResolveEnemyTurnStartEffects`（敌人回合开始 Buff / 延迟卡牌结算）。在玩家清空后再让敌人 Buff 触发一次，会带来"玩家明明清场了但自己又被 DoT 又烫一刀"的反直觉表现。直接进 `Check` 与"清场即胜利"心智模型一致。

**为什么不在 `OnActorDied` 里直接切相位：** `OnActorDied` 由卡牌效果在结算过程中调用，可能在一个伤害循环里依次触发多次（AOE 卡）；如果在第一次触发就 `SetPhase(Check)`，后续效果还在执行就会引起状态机错乱。改成订阅 `CardPlayedEvent`（发布在 `CardSystem.Play` 末尾）就保证了"一次出牌的所有效果都结算完才检查"。

**备选方案：**
- *A. 在 `CardSystem.Play` 末尾直接调 `BattleSystem`：* 需要 `CardSystem` 持有 `BattleSystem` 引用，反向破坏现有依赖方向（目前是 `BattleSystem.Initialize(cardSystem)`）。订阅 `CardPlayedEvent` 更解耦。
- *B. 在 `GameProcedure` 监听 `MonsterDeathEvent` 并调用 `_battleSystem.EndTurn()`：* 越层调度、且按上面分析 `EndTurn` 不是想要的语义。

### 决策 2：`GameProcedure` 订阅本地 `LevelCompleteEvent` 自动 `ChangeState<MainMenuProcedure>`

**做法：**
- 在 `GameProcedure.EnterAsync` 创建 `_localEventBus` 后立即订阅 `LevelCompleteEvent`：`_localEventBus.GetChannel<LevelCompleteEvent>().Subscribe(OnLevelComplete)`。
- 回调 `OnLevelComplete(LevelCompleteEvent evt)` 中调用 `ChangeState<MainMenuProcedure>(procedureOwner)`。`procedureOwner` 需要在 `OnEnter` 中作为字段保存，或通过现有 `ProcedureBase` 暴露的能力获取（参考 `MainMenuProcedure` 的实现，使用相同模式）。
- `Cleanup` 中新增 `Unsubscribe(OnLevelComplete)`，保证多次清理安全。

**为什么走本地总线而不是全局 hub：** `LevelCompleteEvent` 当前已在本地总线发布，订阅方与发布方都在同一个流程的生命周期内，本地订阅最直接；全局 hub 主要服务跨流程通信，目前没有其它流程需要监听关卡完成。

**为什么用 `ChangeState` 而不是 Navigator 直接 `Navigate("Main")`：** `GameView` 与 `MainView` 是不同 Procedure 拥有的资源；如果只让 Navigator 切界面，`GameProcedure` 的 System / 订阅就不会被清理，导致泄漏。走流程切换会自动调用 `OnLeave → Cleanup`，进入 `MainMenuProcedure.OnEnter` 中已有的 `Navigator.OpenAsync<MainView>` 流程。

### 决策 3：`WaveSystem.AdvanceToNextWave` 移除 `StartLevelRequestedEvent.Publish`

**做法：** 删除 `globalHub.StartLevelRequestedEvent.Publish(new StartLevelRequestedEvent(_levelId, ""))` 这一行；只保留对本地总线发布 `LevelCompleteEvent`。

**理由：** `StartLevelRequestedEvent` 语义是"开始一关"，复用它会让 `MainMenuProcedure` 错把"完成"当成"再来一次"。这是早期为了让流程切换"动起来"的临时手段，现在 `GameProcedure` 自己订阅 `LevelCompleteEvent` 后就不再需要。

**风险与缓解：** 如果之后某个外部消费者依赖了这条全局事件（目前 grep 结果显示只在 `MainMenuProcedure` 订阅，且语义错误），需要在 PR 描述中明确该清理。在测试中加一条"关卡完成不发布 `StartLevelRequestedEvent`"的负向断言可以保护回归。

## Risks / Trade-offs

- **风险：自动结束回合与某些"敌人回合开始"延迟卡牌效果耦合** → 通过统一走 `SetPhase(Check)`（跳过 `MonsterTurn`）规避；同时在 EditMode 中加一条"玩家清空后不会执行敌人 Buff Tick"的测试以锁定行为。
- **风险：玩家"清场瞬间 ChangeState"导致 GameView 资源在动画中被销毁，出现一帧黑屏 / 卡顿** → 本次只承担流程切换正确性的责任；如果出现观感问题，后续在 `GameProcedure` 内加一个短延迟（UniTask.DelayFrame）或战斗结束面板再切流程，不影响当前 spec 行为。
- **权衡：未引入"结算面板"的关卡完成体验比较突兀** → 优先满足"自动闭环"的功能需求；UI 表现层流程作为后续单独变更追加。
- **风险：`BattleSystem` 订阅 `CardPlayedEvent` 后需要在 `Dispose` 中正确取消** → 在 `BattleSystem.Init` 中保存 handler，`Dispose` 中调用 `Unsubscribe`；测试中加一条"Dispose 后不再响应事件"的断言。
