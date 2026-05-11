## MODIFIED Requirements

### Requirement: GameView 动态怪物项实例化
GameView SHALL 通过 `VisualTreeAsset.CloneTree()` 动态实例化怪物项模板（替代 Instantiate Prefab），并通过 `monsterContainer.Add(item)` 添加到 ScrollView 内容容器中。每个怪物项 SHALL 通过 `item.Q<Label>("name-text")` 设置怪物名称和 HP 显示。

#### Scenario: 根据怪物列表刷新显示
- **WHEN** Model.Monsters 属性变更触发 BindProperty 回调
- **THEN** SHALL 先清除所有已有怪物项（RemoveFromHierarchy），再为每个 Hp > 0 的怪物通过 CloneTree 创建新项
- **AND** 通过 UQuery 查找 Label 设置名称、HP、护甲信息

#### Scenario: 怪物死亡时移除显示项
- **WHEN** 怪物 Hp 降为 0
- **THEN** 对应的怪物显示项 SHALL 通过 RemoveFromHierarchy 从容器中移除

### Requirement: GameView 怪物意图显示
GameView SHALL 在意图 ScrollView 中通过 CloneTree 实例化意图项模板，显示每个怪物的下一回合意图类型（攻击/防御）和数值。

#### Scenario: 显示怪物攻击意图
- **WHEN** 怪物当前意图为 MonsterIntentType.Attack，值为 8
- **THEN** SHALL 在意图项的 Label 中显示 "怪物名: 攻击 8"，文本颜色为红色

#### Scenario: 显示怪物防御意图
- **WHEN** 怪物当前意图为 MonsterIntentType.Defend，值为 5
- **THEN** SHALL 在意图项的 Label 中显示 "怪物名: 防御 5"

### Requirement: GameView 手牌显示
GameView SHALL 在手牌 ScrollView 中通过 CloneTree 实例化卡牌项模板，显示玩家当前手牌的名称和费用。

#### Scenario: 刷新手牌列表
- **WHEN** Model.Hand 属性变更
- **THEN** SHALL 先清除所有已有卡牌项，再为每张手牌通过 CloneTree 创建新项
- **AND** 通过 UQuery 设置卡牌名称和费用

### Requirement: GameView 信息区域显示
GameView SHALL 在信息区域使用 Label 显示当前战斗阶段、能量、血量、护甲等状态。结束回合按钮 SHALL 使用 UI Toolkit 的 Button，交互状态通过 `SetEnabled()` 控制。

#### Scenario: 玩家回合时启用结束回合按钮
- **WHEN** 战斗阶段变为 PlayerTurn
- **THEN** 结束回合按钮 SHALL 被启用（SetEnabled(true)），信息 Label 显示 "你的回合 | 能量:X/Y | 血量:A/B | 护甲:C"

#### Scenario: 非玩家回合时禁用结束回合按钮
- **WHEN** 战斗阶段不是 PlayerTurn
- **THEN** 结束回合按钮 SHALL 被禁用（SetEnabled(false)）

### Requirement: GameView 结束回合和出牌事件
GameView SHALL 通过 RegisterViewCallback 注册结束回合按钮的 ClickEvent 和卡牌项的 ClickEvent，触发对应的 C# 事件（OnEndTurnRequested 和 OnCardUsed）。回调 SHALL 在 UIView 释放时自动清理。

#### Scenario: 点击结束回合按钮
- **WHEN** 玩家点击结束回合按钮
- **THEN** SHALL 触发 OnEndTurnRequested 事件

#### Scenario: 点击手牌
- **WHEN** 玩家点击某张手牌
- **THEN** SHALL 触发 OnCardUsed 事件，参数为该手牌在列表中的索引
