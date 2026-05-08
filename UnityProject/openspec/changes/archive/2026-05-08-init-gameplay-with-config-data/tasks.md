## 1. ConfigSystem 接入与类型对齐

- [x] 1.1 在 GameLogicEntry.Init() 中初始化 ConfigSystem，添加 Config 静态属性暴露 Tables
- [x] 1.2 StartLevelRequestedEvent.LevelId 从 string 改为 int，更新构造函数
- [x] 1.3 MainModel.DefaultLevelId 从 string 改为 int，从 TbLevel 配置读取 IsDefault=true 的记录替代硬编码常量
- [x] 1.4 MainController 跟随 LevelId 类型变更，更新 HandleStartGame 方法
- [x] 1.5 MainMenuProcedure 跟随 LevelId 类型变更，确认事件订阅和处理逻辑正确
- [x] 1.6 GameProcedure.OnEnter() 接收并传递 int 类型关卡标识给 GameController

## 2. TbMonsterIntent 配置表

- [x] 2.1 在 Luban 配置源数据目录创建 TbMonsterIntent 表定义，包含 Id、MonsterId、Order、IntentType 枚举、Value、Weight 字段
- [x] 2.2 创建 MonsterIntentType 枚举（Attack=1, Defend=2），预留 Buff=3、Debuff=4
- [x] 2.3 运行 Luban 代码生成，生成 TbMonsterIntent.cs 和 MonsterIntent.cs 到 GameProto
- [x] 2.4 更新 Tables.cs 生成代码，确认 TbMonsterIntent 已注册
- [x] 2.5 填写示例怪物意图数据（至少一个 Boss 序列和一个普通怪权重随机）

## 3. GameModel 局内数据模型

- [x] 3.1 创建 GameModel.cs，继承 ModelBase，包含关卡配置引用、波次列表、当前波次/批次索引
- [x] 3.2 GameModel 添加玩家战斗属性：当前能量、最大能量、手牌上限、手牌列表、弃牌堆
- [x] 3.3 GameModel 添加怪物运行时列表，每个实例包含配置引用、血量、护甲、当前意图
- [x] 3.4 GameModel 实现 IGameModelData 只读接口
- [x] 3.5 在 GameLogicEntry.InitializeModels() 中注册 GameModel

## 4. GameController 战斗核心逻辑

- [x] 4.1 GameController.OnEnter() 根据 int 关卡标识从 TbLevel 构建关卡运行时上下文
- [x] 4.2 实现 BattlePhase 枚举（Prepare/PlayerTurn/MonsterTurn/Check）和阶段切换
- [x] 4.3 实现准备阶段：刷新怪物意图、恢复能量、抽牌到手牌上限
- [x] 4.4 实现怪物意图生成：Order 序列循环和 Weight 权重随机混合选取逻辑
- [x] 4.5 实现玩家回合：点击手牌使用卡牌，扣除能量，触发效果
- [x] 4.6 实现怪物回合：按意图类型执行 Attack 扣血 / Defend 加护甲
- [x] 4.7 实现检查阶段：判断怪物全灭推进批次/波次，判断玩家死亡结束游戏
- [x] 4.8 实现波次推进：当前波次完成后推进到下一波次，最后波次完成标记关卡完成

## 5. GameView UI 联动

- [x] 5.1 GameView 绑定 MonsterRoot、CardScrollRect、TipsScrollRect、UseRect 引用（通过 ReferenceCollector）
- [x] 5.2 实现怪物展示：根据怪物运行时列表实例化 GamePlay_MonsterItem 到 MonsterRoot
- [x] 5.3 实现意图展示：根据怪物意图实例化 GamePlay_TipsItem 到 TipsScrollRect Content
- [x] 5.4 实现手牌展示：根据手牌列表实例化 GamePlay_CardItem 到 CardScrollRect Content
- [x] 5.5 实现卡牌点击交互：绑定 CardItem 点击事件通知 GameController 使用卡牌
- [x] 5.6 实现结束回合按钮或交互，通知 GameController 进入怪物回合

## 6. 测试与验证

- [x] 6.1 更新 MainControllerTests 跟随 LevelId 类型变更
- [x] 6.2 更新 MainMenuToGameProcedureTests 跟随 LevelId 类型变更
- [x] 6.3 运行编译检查确认无编译错误
- [x] 6.4 在 Unity 编辑器中验证完整流程：主界面 → 点击开始 → GameView 展示怪物/手牌/意图
