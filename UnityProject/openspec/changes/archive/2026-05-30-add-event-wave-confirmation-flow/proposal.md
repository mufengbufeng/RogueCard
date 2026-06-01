## Why

`WaveSystem` 目前只对 `WaveType.Battle` 进入真实战斗；非战斗波次会立即调用 `AdvanceToNextWave()`。这让宝箱、商店等事件节点在运行时没有可见停顿，玩家也无法阅读当前节点文案或主动确认继续。

同时，`GameProcedure` 已经把 `GameViewModel.RewardSelected` 作为“奖励确认后回主菜单”的命令来处理。这个命令语义太窄：同一个确认动作既需要承接事件波次继续，也需要在关卡完成时回主菜单。若不拆清楚运行时状态，后续接入奖励、商店和结算表现会继续挤在一个临时命令里。

## What Changes

- `WaveSystem` 在遇到非战斗波次时不再自动推进；它应进入一个等待玩家确认的事件波次状态。
- `GameModel` / `GameViewModel` 暴露当前波次展示数据和“是否等待事件确认”的状态，供 `GameView` 切换 BattlePanel / RewardPanel 等 UI。
- `GameProcedure` 将 UI 确认命令路由为：事件波次确认时推进到下一波次；关卡完成确认时切回主菜单。
- `GameView` 的 RewardPanel 暂作为第一阶段通用事件确认面板，展示当前波次标题、描述和继续文案。
- 保留 `LevelCompleteEvent` 作为“所有波次完成”的本地事件；是否立即返回主菜单由确认状态决定，避免非战斗节点和结算节点瞬间跳走。

## Impact

- Affected specs:
  - `level-wave-config`
  - `game-systems`
  - `game-runtime-context`
  - `game-ui-data-binding`
  - `main-to-game-view-flow`
- Affected code areas:
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/WaveSystem.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameModel.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameViewModel.cs`
  - `Assets/GameScripts/HotFix/GameLogic/Procedure/Game/GameProcedure.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameView.cs` and RewardPanel binding classes
  - EditMode tests around wave progression and GameProcedure command flow
