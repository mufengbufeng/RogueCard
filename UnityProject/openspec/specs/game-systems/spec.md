# game-systems Specification

## Purpose

定义核心游戏系统的职责，包括 WaveSystem（关卡和波次推进）、BattleSystem（战斗阶段管理）、CardSystem（卡牌操作）和 MonsterSystem（怪物行为管理）。
## Requirements
### Requirement: WaveSystem 管理关卡和波次推进
WaveSystem SHALL 从配置表加载关卡数据，按 Order 排序波次，按序推进波次。波次类型为 Battle 时 SHALL 调用 BattleSystem 初始化战斗。所有波次完成时 SHALL 发布 LevelCompleteEvent。

#### Scenario: 启动关卡
- **WHEN** 调用 WaveSystem.StartLevel(levelId) 传入有效关卡标识
- **THEN** SHALL 从配置表加载关卡数据，按 Order 排序波次，进入第一个波次

#### Scenario: 战斗波次进入
- **WHEN** 当前波次类型为 Battle 且有有效 PayloadId
- **THEN** SHALL 加载刷怪方案，调用 BattleSystem 初始化战斗

#### Scenario: 非战斗波次跳过
- **WHEN** 当前波次类型不是 Battle
- **THEN** SHALL 自动推进到下一波次

#### Scenario: 关卡完成
- **WHEN** 最后一个波次完成
- **THEN** SHALL 发布 LevelCompleteEvent

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

### Requirement: CardSystem 管理卡牌操作

CardSystem SHALL 处理出牌、抽牌、洗牌。出牌 SHALL 校验阶段和能量，校验通过后 SHALL 调用 `CardEffectExecutor.Execute` 并把 caster 设为 `PlayerActor(_model)`、targets 按 `card.TargetMode` 计算后传入；执行完毕 SHALL 发布 `CardPlayedEvent`。弃牌堆非空且牌库空时 SHALL 自动将弃牌堆洗入牌库。CardSystem SHALL NOT 自己实现伤害/护盾/DoT/能量等具体效果。

#### Scenario: 出牌校验通过并通过 Executor 执行效果
- **WHEN** 调用 `CardSystem.Play(handIndex)`，阶段为 `PlayerTurn`，手牌索引有效，能量足够
- **THEN** CardSystem SHALL 扣除能量、移除手牌、加入弃牌堆
- **AND** SHALL 构造 `caster = new PlayerActor(_model)`
- **AND** SHALL 根据 `card.Config.TargetMode` 计算 `targets`：`SingleAuto`/`SingleManual` 取一个怪物（Manual 来自 UI 选择）、`All`/`SplitAcrossAll` 取全部存活怪物、`Self` 取 caster
- **AND** SHALL 调用 `CardEffectExecutor.Execute(card.Config, caster, targets, events)`
- **AND** SHALL 发布 `CardPlayedEvent(card.Config.Id)`

#### Scenario: 出牌校验失败
- **WHEN** 调用 `CardSystem.Play(handIndex)`，阶段不是 `PlayerTurn` 或能量不足
- **THEN** SHALL 不扣能量、不移除手牌、不调用 Executor、不发布 `CardPlayedEvent`

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

