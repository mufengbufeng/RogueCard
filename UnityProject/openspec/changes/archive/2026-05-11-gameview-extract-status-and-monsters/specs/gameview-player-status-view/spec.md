## ADDED Requirements

### Requirement: PlayerStatusView 必须订阅切片接口而非完整 ViewModel

`PlayerStatusView` SHALL 通过构造函数接收一个实现 `IPlayerStatusContext` 的对象，SHALL NOT 直接引用 `GameViewModel` 或 `GameModel`。SHALL NOT 访问 `Hand`、`CardPlayFailed`、`UseCard` 等手牌交互相关字段或事件。

#### Scenario: 通过切片接口构造视图

- **WHEN** 调用 `new PlayerStatusView(rootElement, context)` 且 `context` 仅实现 `IPlayerStatusContext`
- **THEN** 视图 SHALL 成功构造并完成元素查询、事件订阅、首次刷新

#### Scenario: 测试用 fake 上下文

- **WHEN** 测试代码以 `FakePlayerStatusContext`（不是 `GameViewModel`）构造 `PlayerStatusView`
- **THEN** 视图 SHALL 与生产环境表现一致，SHALL NOT 因缺失 `Hand` / `Monsters` 字段而失败

### Requirement: PlayerStatusView 必须渲染 HP / 护甲 / 能量进度条与文本

`PlayerStatusView` SHALL 在 `IPlayerStatusContext.PlayerHp` / `PlayerMaxHp` / `PlayerArmor` / `Energy` / `MaxEnergy` 任一变化时刷新对应 UI 元素。HP 进度条宽度 SHALL 按百分比设置（`width = PlayerHp / PlayerMaxHp × 100%`）；能量进度条同理。HP 文本 SHALL 为 `"{hp}/{maxHp}"` 格式；能量文本同理；护甲文本 SHALL 在 `PlayerArmor > 0` 时显示数值，否则显示 `"0"`。

#### Scenario: HP 变化时进度条按百分比刷新

- **WHEN** `PlayerHp.Value` 从 40 变为 30 且 `PlayerMaxHp.Value` 为 100
- **THEN** `hp-bar-fill` 元素的 `style.width` SHALL 设置为 `30%`
- **AND** `hp-text` 元素的 `text` SHALL 设置为 `"30/100"`

#### Scenario: 护甲为 0 时显示零

- **WHEN** `PlayerArmor.Value` 为 0
- **THEN** `armor-text` 元素的 `text` SHALL 设置为 `"0"`

#### Scenario: 护甲大于 0 时显示数值

- **WHEN** `PlayerArmor.Value` 为 5
- **THEN** `armor-text` 元素的 `text` SHALL 设置为 `"5"`

#### Scenario: PlayerMaxHp 为 0 时不更新进度条宽度

- **WHEN** `PlayerMaxHp.Value` 为 0
- **THEN** `hp-bar-fill.style.width` SHALL NOT 被设置（避免除零，保持 USS 默认值）

### Requirement: PlayerStatusView 必须渲染战斗阶段文本

`PlayerStatusView` SHALL 在 `IPlayerStatusContext.Phase` / `IsLevelComplete` / `IsPlayerDead` 任一变化时刷新 `info-text` 元素。优先级 SHALL 为：`IsLevelComplete=true` → `"关卡完成！"`；`IsPlayerDead=true` → `"玩家死亡"`；否则按 `Phase` 映射中文标签。

#### Scenario: 阶段映射

- **WHEN** `Phase.Value` 变为 `BattlePhase.PlayerTurn` 且 `IsLevelComplete=false`、`IsPlayerDead=false`
- **THEN** `info-text` SHALL 显示 `"你的回合"`

#### Scenario: 关卡完成优先

- **WHEN** `IsLevelComplete.Value` 为 `true` 且 `Phase.Value` 为 `BattlePhase.MonsterTurn`
- **THEN** `info-text` SHALL 显示 `"关卡完成！"`

#### Scenario: 玩家死亡次优

- **WHEN** `IsPlayerDead.Value` 为 `true` 且 `IsLevelComplete.Value` 为 `false`
- **THEN** `info-text` SHALL 显示 `"玩家死亡"`

### Requirement: PlayerStatusView 必须渲染玩家 Buff 状态条

`PlayerStatusView` SHALL 在 `IPlayerStatusContext.PlayerBuffs` 变化时清空 `player-buff-bar` 容器并按列表顺序为每条非空 `BuffRuntime` 添加一个 `Label`，元素 SHALL 应用 CSS 类 `buff-icon`，对 `EffectKind.DamageDot` 类型的 buff 额外应用 `buff-icon-dot` 类。Label 文本 SHALL 为 `"{Value}×{RemainingTurns}"` 格式。

#### Scenario: 空 buff 列表清空容器

- **WHEN** `PlayerBuffs.Value` 为空数组
- **THEN** `player-buff-bar` 的子元素数 SHALL 为 0

#### Scenario: DoT buff 渲染

- **WHEN** `PlayerBuffs.Value` 包含一条 `BuffRuntime { Kind=DamageDot, Value=4, RemainingTurns=2 }`
- **THEN** `player-buff-bar` SHALL 包含一个 Label
- **AND** 该 Label SHALL 应用 `buff-icon` 与 `buff-icon-dot` 类
- **AND** 该 Label 的 `text` SHALL 为 `"4×2"`

### Requirement: PlayerStatusView 必须支持显式 Dispose 释放订阅

`PlayerStatusView` SHALL 实现 `IDisposable`（或具备等价的 `Dispose()` 方法）。`Dispose()` SHALL 解绑构造期向 `IPlayerStatusContext` 各 `ReactiveProperty.Changed` 注册的所有委托，SHALL NOT 抛出异常，SHALL 是幂等的（多次调用安全）。

#### Scenario: Dispose 后 ViewModel 变化不再触发刷新

- **WHEN** 已构造 `PlayerStatusView` 并调用 `view.Dispose()`
- **AND** 之后 `context.PlayerHp.Value` 被修改
- **THEN** `hp-bar-fill` 元素 SHALL NOT 被更新

#### Scenario: 重复 Dispose 安全

- **WHEN** 同一个 `PlayerStatusView` 实例的 `Dispose()` 被调用两次
- **THEN** 第二次调用 SHALL NOT 抛出异常
