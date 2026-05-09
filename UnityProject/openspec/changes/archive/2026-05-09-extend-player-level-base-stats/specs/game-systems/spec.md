## MODIFIED Requirements

### Requirement: BattleSystem 管理战斗阶段和胜负判定

BattleSystem SHALL 管理 `Prepare → PlayerTurn → MonsterTurn → Check` 阶段循环。**进入战斗前 SHALL 通过 `InitPlayerAttributes` 从 `TbPlayerLevel` 读取玩家当前等级对应的 `BaseHp` / `BaseEnergy` / `HandLimit`，调用 `GameModel.InitBattleAttributes(maxEnergy, handLimit, maxHp)` 完成玩家属性初始化**。`Prepare` 阶段 SHALL 恢复玩家能量、触发玩家抽牌、调用 `MonsterSystem.BeginMonsterPrepare` 让每只怪物按牌组驱动生成 `PendingCards`。`MonsterTurn` 阶段 SHALL 在调用 `MonsterSystem.ExecuteTurn` 之前，统一 tick 玩家与所有怪物的 Buffs（处理 DoT 扣血、buff 倒计时、归零移除）。`Check` 阶段 SHALL 判断胜负条件。

> 本 Requirement 是 Change 1 → Change 2 → Change 3 三次 MODIFIED 的累积态：Change 1 引入 DoT tick；Change 2 引入 BeginMonsterPrepare；Change 3 引入按等级初始化玩家属性。三者必须保留全部 Scenarios。

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

#### Scenario: 回合结束流转包含 DoT tick
- **WHEN** 调用 `BattleSystem.EndTurn()`
- **THEN** SHALL 将阶段切换到 `MonsterTurn`
- **AND** SHALL 先 tick 所有 `IBattleActor` 的 Buffs（DoT 扣血、buff RemainingTurns 倒数、归零移除）
- **AND** SHALL 再执行 `MonsterSystem.ExecuteTurn`
- **AND** SHALL 在执行完后进入 `Check` 阶段

#### Scenario: DoT tick 杀死玩家立即结算
- **WHEN** DoT tick 导致 `_model.PlayerHp <= 0`
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`
- **AND** SHALL 跳过 `MonsterSystem.ExecuteTurn`

#### Scenario: DoT tick 杀死怪物发布死亡事件
- **WHEN** DoT tick 导致某只怪物 `Hp <= 0`
- **THEN** SHALL 通过事件总线发布对应 `MonsterDeathEvent`
- **AND** 该怪物在后续 `MonsterSystem.ExecuteTurn` 中 SHALL 被跳过

#### Scenario: Check 阶段玩家死亡
- **WHEN** Check 阶段检测到玩家血量 <= 0
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`

#### Scenario: Check 阶段怪物全灭
- **WHEN** Check 阶段检测到当前批次所有怪物血量 <= 0
- **THEN** SHALL 尝试推进到下一批次或下一波次；如果全部完成 SHALL 发布 `BattleEndedEvent(IsVictory=true)`
