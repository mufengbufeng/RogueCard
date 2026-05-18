## Why

当前战斗流程要求玩家在打完所有怪物后仍需手动点击"结束回合"才能推进波次/批次；关卡全部完成后玩家也停留在 GameView，没有回到主菜单的路径。这两个手动步骤打断了"清场 → 推进"的体验闭环，也让"关卡完成"显得没有归宿。本次需求要求把这两个推进动作自动化，让战斗节奏与关卡生命周期形成闭环。

## What Changes

- 玩家回合内当前批次所有怪物被清空时，`BattleSystem` MUST 自动结束当前玩家回合并进入推进判定（无需玩家点击"结束回合"按钮）。
- 关卡所有波次完成（`LevelCompleteEvent` 发布）时，`GameProcedure` MUST 自动从局内流程切回 `MainMenuProcedure`，关闭 `GameView` 并回到主界面。
- `WaveSystem.AdvanceToNextWave` 中残留的、用 `StartLevelRequestedEvent` 在全局事件总线上"伪装"关卡完成的临时实现 MUST 移除；关卡完成只通过 `LevelCompleteEvent` 暴露，避免主菜单流程误把它当成新一轮启动请求。

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `battle-turn-cycle`: 新增"玩家回合内怪物清空自动结束回合"的需求条款，描述清空触发与跳过手动结束回合的语义。
- `main-to-game-view-flow`: 新增"关卡完成自动返回主菜单"的需求条款，描述 `GameProcedure` 订阅 `LevelCompleteEvent` 并切回 `MainMenuProcedure` 的行为。
- `battle-events`: 微调 `LevelCompleteEvent` 的发布契约，移除全局 `StartLevelRequestedEvent` 的复用，明确只发布 `LevelCompleteEvent`。

## Impact

- 代码：`Assets/GameScripts/HotFix/GameLogic/UI/Game/BattleSystem.cs`（出牌或 Buff 结算导致最后一只怪物死亡后的自动推进逻辑）；`Assets/GameScripts/HotFix/GameLogic/UI/Game/WaveSystem.cs`（移除 `StartLevelRequestedEvent` 误用）；`Assets/GameScripts/HotFix/GameLogic/Procedure/Game/GameProcedure.cs`（订阅 `LevelCompleteEvent` 并切回主菜单）。
- 测试：`GameLogic.Tests.EditMode` 下新增 `BattleSystem` 自动结束回合的单元测试；`WaveSystem` 关卡完成事件契约调整对应测试需要更新或新增。
- UI：`TurnControlView` / `GameView` 行为不变，但实际触发结束回合的来源从用户点击扩展到 `BattleSystem` 自动推进。
- 流程：`GameProcedure` 退出路径多了一种来源（关卡完成自动返回），需要确保资源/订阅清理顺序在此路径下依然正确。
- 依赖：无新增第三方依赖；不涉及 Luban 配置变更。
