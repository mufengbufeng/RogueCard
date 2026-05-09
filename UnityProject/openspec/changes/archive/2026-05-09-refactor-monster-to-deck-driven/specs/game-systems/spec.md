## MODIFIED Requirements

### Requirement: MonsterSystem 管理怪物行为

MonsterSystem SHALL 在 `Prepare` 阶段调用 `MonsterCardSystem` 与 `MonsterAiBrain` 为每只存活怪物生成本回合 `PendingCards`，在 `MonsterTurn` 阶段按 `PendingCards` 调用 `CardEffectExecutor` 执行行动并清空手牌。MonsterSystem SHALL NOT 自己实现伤害/护盾/DoT 等具体效果（统一由 `CardEffectExecutor` 承担）。

#### Scenario: Prepare 阶段生成怪物意图
- **WHEN** `BattleSystem` 进入 `Prepare` 阶段
- **THEN** MonsterSystem.BeginMonsterPrepare SHALL 对每只 `IsDead == false` 的怪物：
  - 把 `CurrentEnergy` 重置为 `MaxEnergy`
  - 调用 `MonsterAiBrain.SelectIntent(monster, monster.TurnsAlive + 1, scriptedCards)`
  - 将结果写入 `monster.PendingCards`
- **AND** SHALL 在所有怪物处理完毕后由 `BattleSystem` 进入 `PlayerTurn`

#### Scenario: 剧本回合不抽牌
- **WHEN** 当前回合数命中某只怪物的剧本行
- **THEN** `MonsterAiBrain` SHALL 直接产出剧本卡列表
- **AND** MonsterCardSystem SHALL NOT 调用 `Draw`

#### Scenario: 兜底回合真抽牌
- **WHEN** 当前回合数不命中剧本
- **THEN** `MonsterAiBrain` SHALL 先调用 `MonsterCardSystem.Draw(monster, monster.HandLimit)`
- **AND** 在手牌内按 Cost 降序贪心选牌

#### Scenario: MonsterTurn 执行 PendingCards
- **WHEN** `BattleSystem` 进入 `MonsterTurn`，DoT tick 已完成
- **THEN** MonsterSystem.ExecuteTurn SHALL 遍历每只存活怪物的 `PendingCards`
- **AND** 对每张卡 SHALL 调用 `CardEffectExecutor.Execute(card, monster, [playerActor], events)`
- **AND** 在该怪物所有 PendingCards 执行完毕后 SHALL 调用 `MonsterCardSystem.DiscardAllHand(monster)`
- **AND** SHALL 清空 `monster.PendingCards`
- **AND** SHALL 递增 `monster.TurnsAlive`

#### Scenario: 怪物死亡跳过执行
- **WHEN** 进入 `MonsterTurn` 时某只怪物 `IsDead == true`
- **THEN** MonsterSystem SHALL 跳过该怪物的 `ExecuteTurn` 处理
- **AND** SHALL 跳过 `DiscardAllHand` 与 `TurnsAlive++`

### Requirement: BattleSystem 管理战斗阶段和胜负判定

BattleSystem SHALL 管理 `Prepare → PlayerTurn → MonsterTurn → Check` 阶段循环。`Prepare` 阶段 SHALL 恢复玩家能量、触发玩家抽牌、调用 `MonsterSystem.BeginMonsterPrepare`。`MonsterTurn` 阶段 SHALL 在调用 `MonsterSystem.ExecuteTurn` 之前，统一 tick 玩家与所有怪物的 Buffs。`Check` 阶段 SHALL 判断胜负条件。

#### Scenario: Prepare 阶段同时处理玩家与怪物
- **WHEN** 进入 `Prepare` 阶段
- **THEN** SHALL 恢复玩家能量到 `MaxEnergy`、抽玩家牌到 `HandLimit`
- **AND** SHALL 调用 `MonsterSystem.BeginMonsterPrepare`（怪物恢复能量、抽牌或读剧本、生成 PendingCards）
- **AND** SHALL 在两侧准备完毕后切换到 `PlayerTurn`

#### Scenario: MonsterTurn 先 tick Buff 再执行
- **WHEN** 进入 `MonsterTurn` 阶段
- **THEN** SHALL 先 tick 所有 `IBattleActor` 的 Buffs（DoT 扣血、buff RemainingTurns 倒数、归零移除）
- **AND** 然后 SHALL 调用 `MonsterSystem.ExecuteTurn`
- **AND** 在 ExecuteTurn 完成后 SHALL 进入 `Check` 阶段

#### Scenario: DoT tick 杀死玩家立即结算
- **WHEN** DoT tick 导致玩家 `Hp <= 0`
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`
- **AND** SHALL 跳过 `MonsterSystem.ExecuteTurn`

#### Scenario: Check 阶段玩家死亡
- **WHEN** Check 阶段检测到玩家血量 <= 0
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`

#### Scenario: Check 阶段怪物全灭
- **WHEN** Check 阶段检测到当前批次所有怪物血量 <= 0
- **THEN** SHALL 尝试推进到下一批次或下一波次；如果全部完成 SHALL 发布 `BattleEndedEvent(IsVictory=true)`
