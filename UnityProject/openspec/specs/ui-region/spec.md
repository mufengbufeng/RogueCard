# ui-region Specification

## Purpose

定义 Region 组件，支持 Screen 内部动态切换子区域内容（如 Battle → Reward）。Region 持有 VisualElement 插槽引用，按需 CloneTree 加载 UXML 子模板到插槽中，由 Screen 通过 ReactiveProperty 驱动切换时机。
## Requirements
### Requirement: UGUI GameView 必须支持战斗和奖励子面板切换

UGUI `GameView` SHALL 能根据局内阶段显示战斗子面板或奖励子面板。切换实现 SHALL 基于 UGUI GameObject/Panel/CanvasGroup，而不是 `Region.ShowAsync` 或 UXML 加载。

#### Scenario: 战斗阶段显示战斗面板
- **WHEN** 局内阶段为 Prepare、PlayerTurn、MonsterTurn 或 Check
- **THEN** `GameView` SHALL 显示战斗面板
- **AND** 奖励面板 SHALL 不处于可交互显示状态

#### Scenario: 奖励阶段显示奖励面板
- **WHEN** 局内阶段为 Reward
- **THEN** `GameView` SHALL 显示奖励面板
- **AND** 战斗面板的输入事件 SHALL 不再触发出牌或结束回合命令

