## ADDED Requirements

### Requirement: Game scene provides in-game presentation container
系统 SHALL 提供名为 `Game` 的 Unity 场景作为局内运行场景，并在场景内提供局内相机、舞台挂载根节点和打开局内 UI 的入口。

#### Scenario: Enter Game scene
- **WHEN** 开发者或后续流程加载 `Game` 场景
- **THEN** 场景中 MUST 存在可承载战斗舞台和局内 UI 的基础对象结构

#### Scenario: Scene remains content-agnostic
- **WHEN** 不同关卡或波次进入 `Game` 场景
- **THEN** 场景 MUST 不依赖硬编码的具体怪物、卡牌或关卡数据才能打开

### Requirement: GameView provides player operation UI skeleton
系统 SHALL 提供 `GameView` 局内 UI 窗口，用于承载玩家状态、手牌区、拖拽层、目标线层、回合按钮和弹窗根节点。

#### Scenario: Open GameView
- **WHEN** `GameView` 被 UI 系统打开
- **THEN** 玩家状态区、手牌区、拖拽层、目标线层、回合按钮和弹窗根节点 MUST 可见或可被运行时脚本定位

#### Scenario: GameView follows MVC boundaries
- **WHEN** `GameView` 展示局内信息
- **THEN** View MUST 只负责表现和只读数据访问，写入操作 MUST 通过对应 Controller 或后续局内输入接口发起

### Requirement: Battle stage uses 2D world-space presentation
系统 SHALL 提供战斗舞台预制体结构，用 2D 世界空间承载背景、怪物槽位、世界特效和前景表现。

#### Scenario: Instantiate battle stage
- **WHEN** 战斗舞台预制体被放入 `Game` 场景或运行时实例化
- **THEN** 舞台 MUST 提供 `BackgroundRoot`、`MonsterSlotRoot`、`WorldFxRoot` 和 `ForegroundRoot` 等固定表现根节点

#### Scenario: Separate world and UI concerns
- **WHEN** 局内同时显示背景、怪物和玩家手牌
- **THEN** 背景与怪物 MUST 位于 2D 世界空间，玩家手牌和操作按钮 MUST 位于 UI 层

### Requirement: Background supports layered forward transition
系统 SHALL 提供至少远景、中景、近景三类背景层，并支持在前进或波次切换时播放短暂滚动过渡。

#### Scenario: Play forward transition
- **WHEN** 后续流程触发局内前进表现
- **THEN** 背景层 MUST 能以不同速度滚动以表达前进感

#### Scenario: No real map movement required
- **WHEN** 背景滚动过渡播放完成
- **THEN** 系统 MUST 能在不移动玩家实体、不引入真实地图行走逻辑的情况下进入下一表现状态

### Requirement: Monster slots provide 3x3 presentation anchors
系统 SHALL 在战斗舞台中提供 3x3 怪物槽位表现锚点，并通过位置、缩放和排序表现前排、中排、后排层级。

#### Scenario: Locate monster slot anchors
- **WHEN** 后续怪物阵型系统需要把怪物显示到 3x3 阵型
- **THEN** 舞台 MUST 提供 9 个可定位的怪物槽位锚点

#### Scenario: Represent depth ordering
- **WHEN** 前排、中排和后排怪物同时显示
- **THEN** 前排怪物 MUST 在视觉上更靠前，且排序规则 MUST 支持遮挡后排怪物

#### Scenario: Provide interaction anchors
- **WHEN** 后续目标选择、血条或意图表现需要绑定怪物
- **THEN** 怪物表现对象 MUST 提供可供 UI 或特效定位的锚点
