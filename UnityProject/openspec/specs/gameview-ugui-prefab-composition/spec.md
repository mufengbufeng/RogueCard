# gameview-ugui-prefab-composition Specification

## Purpose

定义局内 `GameView.prefab` 的 UGUI Prefab 结构与 `ReferenceCollector` 绑定约束。规定 Prefab 必须直接承载完整战斗 UI（玩家状态、战斗面板、奖励面板、手牌、怪物、目标选择、结束回合、奖励确认等），运行时不再依赖 UITK UXML/USS 或临时摘要 UI 兜底。

## Requirements

### Requirement: GameView Prefab 必须声明完整局内 UGUI 绑定

`Assets/AssetRaw/UI/Game/GameView.prefab` SHALL 在根对象上挂载 `GameView`、`Canvas`、`CanvasScaler`、`GraphicRaycaster` 与 `ReferenceCollector`。`ReferenceCollector` SHALL 至少包含以下 key：`BgImage`、`BattlePanel`、`RewardPanel`、`PlayerStatusPanel`、`InfoText`、`PlayerHpFill`、`PlayerHpText`、`PlayerArmorText`、`PlayerEnergyFill`、`PlayerEnergyText`、`PlayerBuffBar`、`MonsterRect`、`CardSc`、`DropZone`、`PreviewLayer`、`EndBtn`、`FailToast`、`RewardConfirmBtn`、`HandCardTemplate`、`MonsterItemTemplate`、`BuffIconTemplate`、`IntentIconTemplate`。

#### Scenario: Prefab 引用完整

- **WHEN** Unity 加载 `Assets/AssetRaw/UI/Game/GameView.prefab`
- **THEN** Prefab 根对象 SHALL 存在 `ReferenceCollector`
- **AND** `ReferenceCollector` SHALL 能通过所有必需 key 取到非空对象或组件

#### Scenario: 运行时不依赖动态兜底 UI

- **WHEN** `GameView.OnOpen` 执行
- **THEN** `GameView` SHALL 使用 Prefab 引用初始化子视图
- **AND** SHALL NOT 为正式战斗 UI 临时创建 `PhaseTextRuntime`、`HandCommandPanelRuntime` 或 `MonsterCommandPanelRuntime`

### Requirement: GameView Prefab 必须包含可显隐的战斗与奖励面板

`GameView.prefab` SHALL 包含 `BattlePanel` 与 `RewardPanel` 两个 UGUI 面板。`BattlePanel` SHALL 承载玩家战斗交互区域；`RewardPanel` SHALL 承载关卡完成奖励确认区域。面板显隐 SHALL 通过 `GameViewModel.Phase` 驱动，不能通过加载 UXML/Region 资源实现。

#### Scenario: 战斗阶段显示 BattlePanel

- **WHEN** `GameViewModel.Phase.Value` 为 `Prepare`、`PlayerTurn`、`MonsterTurn` 或 `Check`
- **THEN** `BattlePanel` SHALL 处于 active 状态
- **AND** `RewardPanel` SHALL 处于 inactive 状态

#### Scenario: 奖励阶段显示 RewardPanel

- **WHEN** `GameViewModel.Phase.Value` 为 `Reward`
- **THEN** `RewardPanel` SHALL 处于 active 状态
- **AND** `BattlePanel` SHALL 处于 inactive 状态

### Requirement: GameView Prefab 模板必须默认隐藏且可实例化

手牌卡、怪物项、Buff 图标、意图图标模板 SHALL 作为 Prefab 内隐藏模板或子 Prefab 引用提供。模板在运行时列表渲染前 SHALL 保持 inactive，不参与射线命中；实例化出的运行时项 SHALL 挂到对应容器并设置 active。

#### Scenario: 手牌模板实例化

- **WHEN** 手牌列表刷新且 `HandCardTemplate` 存在
- **THEN** 系统 SHALL 为每张手牌实例化一个卡牌项
- **AND** 原始 `HandCardTemplate` SHALL 保持 inactive

#### Scenario: 怪物模板实例化

- **WHEN** 怪物列表刷新且 `MonsterItemTemplate` 存在
- **THEN** 系统 SHALL 为每只存活怪物实例化一个怪物项
- **AND** 原始 `MonsterItemTemplate` SHALL 保持 inactive
