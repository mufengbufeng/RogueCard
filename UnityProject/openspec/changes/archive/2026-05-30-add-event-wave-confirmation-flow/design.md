## Context

关卡波次配置已经区分 Battle / Chest / Shop，但运行时只有战斗波次有停留点。`WaveSystem.StartLevel()` 读取 `LevelWave` 后调用 `StartCurrentWave()`；当前实现中非 Battle 会直接 `AdvanceToNextWave()`，因此配置表里的 Title / Description / ContinueText 对玩家不可见。

局内 UI 已有 BattlePanel / RewardPanel 的第一阶段结构，`GameViewModel` 也有 `RewardSelected` 命令，但现有语义把确认奖励、继续事件波次、关卡完成返回主菜单混在一起。这个设计将确认动作保留为一个 UI 命令，但由 `GameProcedure` 根据 `GameModel` / `GameViewModel` 状态决定后续动作。

## Goals / Non-Goals

### Goals

- 非战斗波次必须展示当前波次文案，并等待玩家点击确认后才推进。
- BattlePanel 与 RewardPanel 的显隐由 ViewModel 状态驱动，避免 View 读取 `GameModel` 内部字段。
- 事件确认与关卡完成确认必须有清晰分支，避免普通事件节点直接回主菜单。
- 第一阶段继续使用 RewardPanel 作为通用事件确认面板，不引入正式奖励或商店系统。

### Non-Goals

- 不实现正式宝箱奖励抽取、商店商品、货币、购买流程。
- 不修改关卡完成事件的本地事件总线边界。
- 不要求新增完整玩法数据，只要求已有配置文案能被运行时展示。

## Decisions

### Event Wave State

`GameModel` 应新增或暴露一组只读 UI 状态，用于描述当前非战斗波次：

- 当前波次类型
- 标题
- 描述
- 继续按钮文案
- 是否正在等待事件确认

命名可随实现贴合现有风格，例如 `CurrentWaveType` / `CurrentWaveTitle` / `CurrentWaveDescription` / `CurrentWaveContinueText` / `IsAwaitingWaveConfirmation`。`GameViewModel` 应把这些状态镜像为 `ReactiveProperty`，并在 `BindModel()` / `SyncAll()` 中同步。

### Wave Progression

`WaveSystem.StartCurrentWave()` 分支：

- Battle：保持现有 `EnterBattleWave(wave)` 行为。
- Chest / Shop：写入事件波次 UI 状态，设置等待确认，不生成怪物，不发布 `BattleEndedEvent`，不自动推进。

`WaveSystem` 提供一个显式确认入口，例如 `ConfirmCurrentWave()`。只有在 `IsAwaitingWaveConfirmation == true` 时确认才会清除等待状态并调用 `AdvanceToNextWave()`；否则确认应为空操作或记录 warning。

### Completion Confirmation

当最后一个波次完成后，`WaveSystem` 仍设置 `IsLevelComplete = true` 并发布 `LevelCompleteEvent`。但 `GameProcedure.OnLevelComplete` 不应在收到事件后立即切回主菜单；它应记录“关卡已完成，等待 UI 确认”。

当 UI 确认命令到达时：

- 若正在等待事件波次确认：调用 `WaveSystem.ConfirmCurrentWave()`。
- Else 若关卡已完成：切回 `MainMenuProcedure`。
- Else：忽略或记录 warning。

这保留 `LevelCompleteEvent` 的流程边界，同时让玩家能看到完成态或最后一个事件节点的确认 UI。

### UI Binding

`GameView` / RewardPanel 应显示 ViewModel 的当前波次标题、描述、继续文案。BattlePanel 在 `Phase` 为战斗相关阶段且未等待事件确认时显示；RewardPanel 在等待事件确认或关卡完成确认时显示。

如当前 RewardPanel 文案只有固定“奖励”语义，应改成中性命名或中性绑定文本；不要求立即重命名 prefab 层级。

## Risks

- 如果 `LevelCompleteEvent` 订阅测试当前假设“收到即切回主菜单”，需要改为“收到后进入完成确认，点击确认后切回”。
- 若 `GameView` 现有 RewardPanel 只绑定奖励按钮，没有标题/描述字段，需要补齐 prefab 引用和 ReferenceCollector 规则。
- 非战斗波次如果配置缺少 `ContinueText`，运行时需要提供合理默认文案，避免按钮空文本。
