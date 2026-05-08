## Why

当前点击主界面"开始游戏"按钮后，GameProcedure 只打开了空的 GameView 界面，没有读取任何配置数据，也没有关卡运行时上下文。已有的 Luban 配置表（TbLevel、TbLevelWave、TbBattleWaveSpawn、TbBattleWaveSpawnBatch、TbMonster、TbCard、TbPlayerLevel）和 ConfigSystem 加载器均已就绪但从未接入运行时。需要将配置数据接入游戏流程，在进入 GamePlay 时根据配置表构建关卡上下文、展示怪物和手牌，并驱动战斗回合循环。

## What Changes

- **BREAKING** `StartLevelRequestedEvent.LevelId` 从 `string` 改为 `int`，与 Luban 表主键类型对齐
- **BREAKING** `MainModel.DefaultLevelId` 从 `string` 改为 `int`，从 TbLevel 配置读取而非硬编码常量
- 在 `GameLogicEntry.Init()` 中初始化并注册 `ConfigSystem`，暴露 `Tables` 供全局访问
- 新增 `GameModel` 局内数据模型，管理当前关卡、波次、批次、能量、手牌等运行时状态
- 实现 `GameController` 核心逻辑：关卡上下文构建、波次推进、战斗回合循环（准备→玩家回合→怪物回合→检查）
- 实现 `GameView` UI 联动：根据运行时状态动态实例化怪物/卡牌/意图 UI 子项
- 新增 `TbMonsterIntent` 配置表，怪物意图支持权重随机和序列循环混合模式
- 新增 `MonsterIntentType` 枚举（Attack/Defend/Buff/Debuff）

## Capabilities

### New Capabilities

- `config-system-integration`: ConfigSystem 在 GameLogicEntry 中初始化并注册，全局暴露 Tables 访问能力
- `game-runtime-context`: 局内运行时数据模型，管理关卡/波次/批次推进状态和玩家战斗属性
- `battle-turn-cycle`: 战斗回合循环驱动（准备阶段刷新意图→玩家回合出牌→怪物回合执行意图→检查胜负）
- `game-ui-data-binding`: GameView/GameController 根据运行时上下文动态展示怪物、手牌、意图
- `monster-intent-config`: 怪物意图配置表 TbMonsterIntent，支持权重随机和序列循环混合模式

### Modified Capabilities

- `main-to-game-view-flow`: LevelId 从 string 改为 int，MainModel 从配置表读取默认关卡信息
- `single-main-ui-entry`: MainModel 的 DefaultLevelId 类型变更影响展示逻辑

## Impact

- **运行时代码**: GameLogicEntry.cs、GameProcedure.cs、GameController.cs、GameView.cs、MainModel.cs、MainController.cs、MainMenuProcedure.cs、StartLevelRequestedEvent.cs
- **新增代码**: GameModel.cs（局内数据模型）、TbMonsterIntent 相关 Luban 生成代码
- **配置数据**: 新增 TbMonsterIntent Luban 表源文件和生成 bytes
- **依赖**: ConfigSystem（已存在于 GameProto）、EF 框架的 ModelManager、UIManager、EventHub
- **测试**: MainControllerTests、MainMenuToGameProcedureTests 需要跟随 LevelId 类型变更更新
