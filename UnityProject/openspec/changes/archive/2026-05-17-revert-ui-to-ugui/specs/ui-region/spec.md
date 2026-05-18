## REMOVED Requirements

### Requirement: Region 必须支持动态加载 UXML 子模板

**Reason**: 运行时 UI 不再使用 VisualElement 插槽或 UXML 子模板。

**Migration**: UGUI 子区域 SHALL 使用 Prefab 内已有 GameObject、Panel、CanvasGroup 或动态实例化的 UGUI Prefab 切换。

### Requirement: Region 必须支持清空内容

**Reason**: UITK Region 类型被移除。

**Migration**: UGUI 子区域清理 SHALL 由对应 `UIView`、`UIController` 或子面板协调器负责，通常通过 `SetActive(false)`、销毁子对象或回收到对象池实现。

### Requirement: Region 必须暴露当前内容供 UQuery

**Reason**: UQuery 与 VisualElement 不再用于运行时 UI。

**Migration**: UGUI 子面板 SHALL 暴露强类型组件引用、Transform 根节点或 ReferenceCollector key 供代码访问。

### Requirement: Screen Region routing must use UI route semantics

**Reason**: Screen-owned Region routing 被移除。

**Migration**: GameView 或 GameController 仍 SHALL 使用 UI 路由语义区分战斗面板和奖励面板，但实现方式为 UGUI Panel/子对象切换。

### Requirement: Screen-owned Region content coordinators must be disposed when leaving their route

**Reason**: Screen-owned UITK Region coordinator 不再存在。

**Migration**: UGUI 子面板协调器若订阅事件，离开对应 UI 路由时 SHALL 释放订阅或 Dispose。

### Requirement: Screen phase subscriptions must be released on disposal

**Reason**: Screen 生命周期被 UGUI UIView/UIController 生命周期替代。

**Migration**: View 或 Controller 对阶段变化、模型事件或本地事件总线的订阅 SHALL 在 `OnClose`、`OnExit` 或 `OnRelease` 中取消。

## ADDED Requirements

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
