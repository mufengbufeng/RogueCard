## MODIFIED Requirements

### Requirement: GameScreen 必须通过 ReactiveProperty 驱动所有 UI 更新

`GameView`（前称 GameScreen）SHALL 在 `OnSetup()` 中装配子模块（`PlayerStatusView`、`MonsterListView` 等）；子模块 SHALL 通过自身切片接口订阅 `GameViewModel` 的 `ReactiveProperty.Changed` 事件。任何 UI 更新 SHALL 由 ViewModel 属性变化驱动。`GameView` 与子模块 SHALL NOT 直接访问 `Model` 或 `Config`（`MonsterItemView` 渲染意图时读 `TbCardEffect` 是唯一例外，仅用于查询 effect 表）。

#### Scenario: 怪物列表变化时刷新 UI

- **WHEN** `ViewModel.Monsters.Value` 被设置为新的怪物列表
- **THEN** `MonsterListView` SHALL 收到 `Changed` 回调
- **AND** SHALL 清空旧怪物元素并重新实例化怪物子项

#### Scenario: 手牌列表变化时刷新 UI

- **WHEN** `ViewModel.Hand.Value` 被设置为新的手牌列表
- **THEN** 手牌子模块 SHALL 收到 `Changed` 回调
- **AND** SHALL 刷新手牌区域

#### Scenario: 玩家 HP 变化时状态面板刷新

- **WHEN** `ViewModel.PlayerHp.Value` 变化
- **THEN** `PlayerStatusView` SHALL 更新 HP 进度条与文本

### Requirement: GameView 必须按子模块切片接口装配子视图

`GameView` SHALL 在 `OnSetup()` 中实例化常驻区域子模块 `PlayerStatusView`，传入 `IPlayerStatusContext` 切片（由 `GameViewModel` 实现）。`GameView` SHALL 在 `BindBattleContent()` 中实例化战斗子模块（含 `MonsterListView`），传入 `IMonsterListContext` 切片，并在 `Region` 切换或 `OnDispose` 时调用各子模块的 `Dispose()`。`GameView` SHALL NOT 直接 `Q<>()` 已迁移到子模块的元素（`info-text`、`hp-bar-fill`、`hp-text`、`armor-text`、`energy-bar-fill`、`energy-text`、`player-buff-bar`、`monster-container`）。

#### Scenario: 子模块在 OnSetup 中实例化

- **WHEN** `GameView.OnSetup()` 执行完成
- **THEN** `PlayerStatusView` SHALL 已被构造并完成首次渲染

#### Scenario: 战斗子模块随 Region 切换重建

- **WHEN** `Region` 从其他面板切换到 `BattlePanel`
- **THEN** 旧 `MonsterListView`（若存在）SHALL 被 `Dispose`
- **AND** 新 `MonsterListView` SHALL 绑定到新 BattlePanel content 并触发首次渲染

#### Scenario: GameView 不再直接查询常驻区域元素

- **WHEN** 阅读 `GameView` 类源码
- **THEN** SHALL NOT 包含 `Q<Label>("info-text")`、`Q("hp-bar-fill")`、`Q<Label>("hp-text")` 等已迁出元素的查询

## REMOVED Requirements

### Requirement: GameView 信息区域显示

**Reason**: 行为已迁移到 `gameview-player-status-view` capability，由 `PlayerStatusView` 子模块承担。
**Migration**: 见 `gameview-player-status-view/spec.md` 中 "PlayerStatusView 必须渲染 HP / 护甲 / 能量进度条与文本" 与 "PlayerStatusView 必须渲染战斗阶段文本" 两个 requirement。

### Requirement: GameView 动态怪物项实例化

**Reason**: 行为已迁移到 `gameview-monster-list-view` capability，由 `MonsterListView` 子模块承担。
**Migration**: 见 `gameview-monster-list-view/spec.md` 中 "MonsterListView 必须基于 Monsters 列表全量重建怪物项" 与 "MonsterItemView 必须封装单只怪物视图渲染" 两个 requirement。

### Requirement: GameView 怪物意图显示

**Reason**: 行为已迁移到 `gameview-monster-list-view` capability，由 `MonsterItemView` 承担，新格式按 `TbCardEffect` 表多 effect 渲染（已不限于"攻击/防御"二分）。
**Migration**: 见 `gameview-monster-list-view/spec.md` 中 "MonsterItemView 必须按 PendingCards 渲染意图" requirement。
