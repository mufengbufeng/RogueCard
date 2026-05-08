## 1. 框架层基础设施

- [x] 1.1 在 EFRuntime/Event 中定义 IEventPublisher 接口（Subscribe/Unsubscribe/Publish 泛型方法），解耦框架层对 HotFix EventHub 的直接依赖
- [x] 1.2 创建 UISystem 抽象基类（EFRuntime/UI/UISystem.cs）：持有 ModelBase 和 IEventPublisher，提供 OnInitialize/OnDispose 生命周期，集成 ControllerEventBinder 自动清理，实现 IDisposable
- [x] 1.3 扩展 UIRuntimeContext：新增 IEventPublisher 属性，构造函数接受 IEventPublisher 参数
- [x] 1.4 修改 UIManager.CreateOrReuseInstanceAsync 调整生命周期时序为：Controller.Initialize → Controller.PrepareAsync → View.Initialize → View.Bindings → View.Open → Controller.Enter

## 2. 局内事件定义

- [x] 2.1 在 HotFix/GameLogic/Event/ 中创建 BattleEvents.cs，定义 CardPlayedEvent、TurnEndedEvent、MonsterDeathEvent、BattleEndedEvent、LevelCompleteEvent 五个只读结构体

## 3. Game System 实现

- [x] 3.1 实现 CardSystem：出牌校验与执行、卡牌效果应用、抽牌/洗牌逻辑（Fisher-Yates）、订阅 TurnEndedEvent 触发弃牌和抽牌
- [x] 3.2 实现 MonsterSystem：怪物意图刷新（序列循环和权重随机两种模式）、执行怪物回合行动（攻击扣护甲再扣血、防御加护甲）、怪物死亡发布 MonsterDeathEvent
- [x] 3.3 实现 BattleSystem：Prepare/PlayerTurn/MonsterTurn/Check 阶段循环、能量恢复、回合结束流转、胜负判定、批次推进判断
- [x] 3.4 实现 WaveSystem：关卡数据加载、波次按 Order 排序推进、战斗波次进入、非战斗波次跳过、关卡完成发布 LevelCompleteEvent

## 4. Controller 和 View 改造

- [x] 4.1 精简 GameController：OnInitialize 获取 View/Model 引用，OnPrepareAsync 创建四个 System 实例，OnEnter 绑定 View 事件到 System 方法（纯转发），OnExit 释放 System
- [x] 4.2 改造 GameView：移除被 Controller 直接赋值的公开属性（MonsterRuntimes、HandRuntimes 等），在 OnBindings 中通过 BindProperty 绑定 Model 只读数据接口，移除 RefreshDisplay 手动调用

## 5. 编译验证与测试

- [x] 5.1 运行编译检查确保 EFRuntime 和 HotFix 层无编译错误
- [x] 5.2 更新现有 EditMode 测试适配新生命周期时序
- [x] 5.3 为 UISystem 基类编写 EditMode 单元测试（初始化、Dispose 幂等性、事件清理）
- [x] 5.4 为 CardSystem 编写 EditMode 单元测试（出牌校验、抽牌洗牌、牌库耗尽自动洗牌）
