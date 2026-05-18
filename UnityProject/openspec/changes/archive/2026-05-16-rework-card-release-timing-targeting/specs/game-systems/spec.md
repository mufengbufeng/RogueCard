## MODIFIED Requirements

### Requirement: BattleSystem 管理战斗阶段和胜负判定

BattleSystem SHALL 管理 `Prepare → PlayerTurn → MonsterTurn → Check` 阶段循环。**进入战斗前 SHALL 通过 `InitPlayerAttributes` 从 `TbPlayerLevel` 读取玩家当前等级对应的 `BaseHp` / `BaseEnergy` / `HandLimit`，调用 `GameModel.InitBattleAttributes(maxEnergy, handLimit, maxHp)` 完成玩家属性初始化**。`Prepare` 阶段 SHALL 恢复玩家能量、触发玩家抽牌、调用 `MonsterSystem.BeginMonsterPrepare` 让每只怪物按牌组驱动生成 `PendingCards`。`MonsterTurn` 阶段 SHALL 先结算敌人回合开始效果（包含现有 DoT tick 语义），再调用 `MonsterSystem.ExecuteTurn`，之后结算敌人回合结束效果，最后进入 `Check` 阶段判断胜负条件。

> 本 Requirement 是 Change 1 → Change 2 → Change 3 以及本变更的累积态：Change 1 引入 DoT tick；Change 2 引入 BeginMonsterPrepare；Change 3 引入按等级初始化玩家属性；本变更引入 EnemyTurnStart / EnemyTurnEnd 结算点。所有 Scenarios 必须保留。

#### Scenario: 进入战斗时按等级初始化玩家属性
- **WHEN** `BattleSystem.EnterBattle` 被调用
- **THEN** SHALL 读取 `tables.TbPlayerLevel.GetOrDefault(_model.CurrentLevel)`
- **AND** 若返回为 null SHALL 再尝试 `GetOrDefault(1)`
- **AND** 若仍为 null SHALL 抛出 `InvalidOperationException`，错误信息包含"缺少 1 级 PlayerLevel 数据"
- **AND** 取到的等级数据 SHALL 把 `BaseHp / BaseEnergy / HandLimit` 三个字段全部传入 `GameModel.InitBattleAttributes`
- **AND** SHALL NOT 引用任何 `GameModel.DefaultPlayerHp` 之类的硬编码常量

#### Scenario: 1 级玩家进入战斗
- **WHEN** `_model.CurrentLevel == 1` 时进入战斗
- **THEN** `_model.PlayerHp` SHALL 等于 100
- **AND** `_model.PlayerMaxHp` SHALL 等于 100
- **AND** `_model.MaxEnergy` SHALL 等于 3
- **AND** `_model.HandLimit` SHALL 等于 10

#### Scenario: 5 级玩家进入战斗
- **WHEN** `_model.CurrentLevel == 5` 时进入战斗
- **THEN** `_model.PlayerHp` SHALL 等于 140
- **AND** `_model.PlayerMaxHp` SHALL 等于 140

#### Scenario: Prepare 阶段同时处理玩家与怪物
- **WHEN** 进入 `Prepare` 阶段
- **THEN** SHALL 恢复玩家能量到 `MaxEnergy`、抽玩家牌到 `HandLimit`
- **AND** SHALL 调用 `MonsterSystem.BeginMonsterPrepare`（怪物恢复能量、抽牌或读剧本、生成 PendingCards）
- **AND** SHALL 在两侧准备完毕后切换到 `PlayerTurn`

#### Scenario: 回合结束流转包含敌人回合开始和结束结算
- **WHEN** 调用 `BattleSystem.EndTurn()`
- **THEN** SHALL 将阶段切换到 `MonsterTurn`
- **AND** SHALL 先结算所有 `EnemyTurnStart` 效果（包含 DoT 扣血、buff RemainingTurns 倒数、归零移除）
- **AND** SHALL 再执行 `MonsterSystem.ExecuteTurn`
- **AND** SHALL 在怪物行动完成后结算所有 `EnemyTurnEnd` 效果
- **AND** SHALL 在敌人回合结束效果结算完毕后进入 `Check` 阶段

#### Scenario: EnemyTurnStart 杀死玩家立即结算
- **WHEN** EnemyTurnStart 效果导致 `_model.PlayerHp <= 0`
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`
- **AND** SHALL 跳过 `MonsterSystem.ExecuteTurn`
- **AND** SHALL 跳过 EnemyTurnEnd 效果结算

#### Scenario: EnemyTurnStart 杀死怪物发布死亡事件
- **WHEN** EnemyTurnStart 效果导致某只怪物 `Hp <= 0`
- **THEN** SHALL 通过事件总线发布对应 `MonsterDeathEvent`
- **AND** 该怪物在后续 `MonsterSystem.ExecuteTurn` 中 SHALL 被跳过

#### Scenario: EnemyTurnEnd 结算后进入 Check
- **WHEN** EnemyTurnEnd 效果完成结算
- **THEN** SHALL 进入 `Check` 阶段
- **AND** SHALL 由 `Check` 阶段统一处理玩家死亡、怪物全灭、批次推进或战斗胜利

#### Scenario: Check 阶段玩家死亡
- **WHEN** Check 阶段检测到玩家血量 <= 0
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`

#### Scenario: Check 阶段怪物全灭
- **WHEN** Check 阶段检测到当前批次所有怪物血量 <= 0
- **THEN** SHALL 尝试推进到下一批次或下一波次；如果全部完成 SHALL 发布 `BattleEndedEvent(IsVictory=true)`

### Requirement: CardSystem 管理卡牌操作

CardSystem SHALL 处理出牌、抽牌、洗牌。出牌 SHALL 校验阶段和能量，校验通过后 SHALL 扣除能量、移除手牌、加入弃牌堆，并通过卡牌释放调度层按 `CardReleaseKind`、`TargetMode`、`TargetCount` 和效果触发时机解析目标与结算效果；执行完毕 SHALL 发布 `CardPlayedEvent`。弃牌堆非空且牌库空时 SHALL 自动将弃牌堆洗入牌库。CardSystem SHALL NOT 自己实现伤害/护盾/DoT/能量等具体效果。

#### Scenario: 出牌校验通过并通过释放调度层执行效果
- **WHEN** 调用 `CardSystem.Play(handIndex)`，阶段为 `PlayerTurn`，手牌索引有效，能量足够
- **THEN** CardSystem SHALL 扣除能量、移除手牌、加入弃牌堆
- **AND** SHALL 构造 `caster = new PlayerActor(_model)`
- **AND** SHALL 调用卡牌释放调度层处理该卡释放
- **AND** 释放调度层 SHALL 根据 `card.Config.CardReleaseKind`、`card.Config.TargetMode`、`card.Config.TargetCount` 和效果触发时机计算目标并调用 `CardEffectExecutor.Execute`
- **AND** SHALL 发布 `CardPlayedEvent(card.Config.Id)`

#### Scenario: 出牌校验失败
- **WHEN** 调用 `CardSystem.Play(handIndex)`，阶段不是 `PlayerTurn` 或能量不足
- **THEN** SHALL 不扣能量、不移除手牌、不调用释放调度层、不调用 Executor、不发布 `CardPlayedEvent`

#### Scenario: 牌库耗尽自动洗牌
- **WHEN** 抽牌时牌库为空但弃牌堆非空
- **THEN** SHALL 将弃牌堆全部卡牌洗入牌库（Fisher-Yates），然后继续抽牌

#### Scenario: 订阅回合结束事件
- **WHEN** 收到 `TurnEndedEvent`
- **THEN** SHALL 弃掉当前手牌，然后抽牌到手牌上限

#### Scenario: 初始化牌库筛选 OwnerKind
- **WHEN** 调用 `CardSystem.InitDeck`
- **THEN** SHALL 只把 `IsBasic == true` 且 `OwnerKind in {Player, Both}` 的卡加入抽牌堆
- **AND** SHALL 跳过 `OwnerKind == Monster` 的卡
