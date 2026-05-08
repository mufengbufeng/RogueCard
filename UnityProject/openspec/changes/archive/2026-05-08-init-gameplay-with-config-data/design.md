## Context

当前主界面点击"开始游戏"后，GameProcedure 仅打开空的 GameView 界面，无任何业务逻辑。已有 Luban 配置表（TbLevel、TbLevelWave、TbBattleWaveSpawn、TbBattleWaveSpawnBatch、TbMonster、TbCard、TbPlayerLevel）和 ConfigSystem 加载器均已生成但从未接入运行时。StartLevelRequestedEvent 使用 string 类型 LevelId，与 Luban 表 int 主键不一致。GameView.prefab 已有 MonsterRoot、CardScrollRect、TipsScrollRect、UseRect 四个 UI 区域，GamePlay 目录下有 CardItem、MonsterItem、TipsItem 三个子项预制体，但 C# 代码均未引用。

这是一个纯 2D 卡牌 Roguelike 游戏，所有游戏逻辑直接在 UI 层实现，不使用 3D 场景或 GameObject 实体。

## Goals / Non-Goals

**Goals:**

- 将 ConfigSystem 接入 GameLogicEntry，全局暴露 Tables 供游戏逻辑访问
- 将 StartLevelRequestedEvent.LevelId 和 MainModel.DefaultLevelId 从 string 改为 int
- 从 TbLevel 配置读取默认关卡信息替代硬编码常量
- 新增 TbMonsterIntent 配置表，支持权重随机和序列循环混合模式的怪物意图
- 新增 GameModel 局内数据模型，管理关卡/波次/批次/能量/手牌运行时状态
- 实现 GameController 核心逻辑：关卡上下文构建、波次推进、战斗回合循环
- 实现 GameView UI 联动：根据运行时状态动态实例化怪物/卡牌/意图 UI 子项
- 在 GameProcedure.OnEnter() 中将 levelId 传递给 GameController 构建关卡上下文

**Non-Goals:**

- 不实现卡牌效果结算系统（伤害计算、buff/debuff 应用）
- 不实现宝箱波次和商店波次的具体玩法
- 不实现怪物 AI 决策树或复杂行为模式
- 不实现存档、关卡选择、多关卡解锁
- 不实现拖拽出牌的交互手势，第一版用点击使用
- 不实现动画、特效、音效
- 不修改 GameView.prefab 或子项预制体的结构

## Decisions

### 1. ConfigSystem 在 GameLogicEntry.Init() 中初始化

ConfigSystem 作为全局服务在 Init() 中创建，通过静态属性暴露 Tables。所有游戏逻辑通过 GameLogicEntry.Config 访问配置数据。

- 选择原因：配置表是全局基础数据，应在任何游戏逻辑之前就绪。Init() 是热更新代码最早执行点，保证后续所有流程都能访问。
- 替代方案：在 GameProcedure.OnEnter() 中延迟加载。该方式更轻量，但每次进入局内都要判断是否已加载，且主界面展示关卡信息时也需要配置数据。

### 2. StartLevelRequestedEvent.LevelId 从 string 改为 int

事件和 MainModel 中的关卡标识统一使用 int 类型，与 Luban 表主键对齐。

- 选择原因：Luban 所有表主键为 int，保持类型一致可避免运行时转换。IsDefault 查找也可以直接遍历 TbLevel.DataList。
- 替代方案：保持 string 类型，运行时 int.Parse 转换。该方式兼容性更好但增加转换代码，且容易产生格式错误。

### 3. 怪物意图使用 TbMonsterIntent 独立表，Weight + Order 混合模式

新增 TbMonsterIntent 表，每行记录一个怪物的某种意图。运行时根据字段值决定选择策略：Order > 0 且 Weight == 0 时按 Order 循环（Boss 模式），Weight > 0 且 Order == 0 时按权重随机（普通怪模式）。两者都有值时 Order 循环优先。

- 选择原因：一张表同时覆盖两种模式，无需区分怪物类型。Boss 可以精心设计行为序列，普通怪用随机增加不确定性。
- 替代方案：只用权重随机或只用序列循环。单一模式无法同时满足 Boss 可控和普通怪随机的需求。

### 4. GameModel 作为 ModelBase 管理局内运行时状态

GameModel 注册到 ModelManager，持有当前关卡配置、波次列表、当前波次/批次索引、当前能量、手牌列表、怪物运行时列表等状态。GameController 通过 Model 读写状态，GameView 通过 ModelData 只读访问。

- 选择原因：遵循 EF MVC 模式，Model 负责数据、Controller 负责逻辑、View 负责展示。Model 的响应式绑定可以驱动 View 自动刷新。
- 替代方案：直接在 GameController 中用字段管理状态。该方式更快但不符合框架约定，也难以利用 ModelManager 的生命周期管理。

### 5. 战斗回合循环用简单状态枚举驱动

使用 BattlePhase 枚举（Prepare/PlayerTurn/MonsterTurn/Check）管理回合阶段。GameController 在 Update 或通过事件驱动阶段转换。第一版不做 FSM，用 switch 分支足够。

- 选择原因：回合制游戏的阶段是固定顺序，不需要 FSM 的灵活性。简单枚举更直观，后续需要时可以迁移到 IFsmManager。
- 替代方案：使用 IFsmManager 创建战斗 FSM。该方式更正式但当前阶段过重，增加不必要的复杂度。

### 6. UI 子项通过资源加载 + 实例化方式创建

GameView 在 OnOpen 时通过 ResourceManager 加载 GamePlay_CardItem、GamePlay_MonsterItem、GamePlay_TipsItem 预制体，根据运行时数据实例化对应数量到 MonsterRoot/CardScrollRect/TipsScrollRect 的 Content 区域。

- 选择原因：已有预制体都带 ReferenceCollector，实例化后可以直接获取子组件引用来设置数据。
- 替代方案：使用 EF 的 EntityManager 和对象池。该方式更规范但增加接入成本，第一版先用直接实例化。

## Risks / Trade-offs

- [风险] ConfigSystem 在 Init() 同步加载所有配置表可能导致启动卡顿 → [缓解] Luban bytes 文件通常很小，同步加载耗时可接受。若后续表数据量增大，可改为异步加载。
- [风险] StartLevelRequestedEvent 类型变更是 BREAKING 改动，所有订阅方和测试都需更新 → [缓解] 当前只有 MainMenuProcedure 订阅该事件，影响范围可控。
- [风险] GameModel 承载过多状态可能变得臃肿 → [缓解] 第一版只包含核心战斗状态，后续可以拆分为 LevelContext、BattleState 等子模型。
- [风险] 怪物意图的 Weight + Order 混合模式可能产生歧义 → [缓解] 文档明确优先级规则（Order 循环 > Weight 随机），运行时严格按规则选择。
- [风险] 直接实例化 UI 子项不使用对象池，频繁创建销毁可能产生 GC → [缓解] 第一版怪物和手牌数量有限（通常 < 10），GC 压力可接受。后续接入对象池优化。
