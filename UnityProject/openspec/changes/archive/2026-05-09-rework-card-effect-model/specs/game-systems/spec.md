## MODIFIED Requirements

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

### Requirement: BattleSystem 管理战斗阶段和胜负判定

BattleSystem SHALL 管理 `Prepare → PlayerTurn → MonsterTurn → Check` 阶段循环。`Prepare` 阶段 SHALL 恢复能量、触发抽牌。**`MonsterTurn` 阶段 SHALL 在调用 `MonsterSystem.ExecuteTurn` 之前，统一 tick 玩家与所有怪物的 Buffs（处理 DoT 扣血和 Buff 倒计时）**。`Check` 阶段 SHALL 判断胜负条件。

#### Scenario: 回合结束流转包含 DoT tick
- **WHEN** 调用 `BattleSystem.EndTurn()`
- **THEN** SHALL 将阶段切换到 `MonsterTurn`
- **AND** SHALL 先 tick 所有 `IBattleActor` 的 Buffs（DoT 扣血、buff RemainingTurns 倒数）
- **AND** SHALL 再执行 `MonsterSystem.ExecuteTurn`
- **AND** SHALL 在执行完后进入 `Check` 阶段

#### Scenario: DoT tick 杀死玩家立即结算
- **WHEN** DoT tick 导致 `_model.PlayerHp <= 0`
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`
- **AND** SHALL 跳过 `MonsterSystem.ExecuteTurn`

#### Scenario: Check 阶段玩家死亡
- **WHEN** Check 阶段检测到玩家血量 <= 0
- **THEN** SHALL 标记玩家死亡，发布 `BattleEndedEvent(IsVictory=false)`

#### Scenario: Check 阶段怪物全灭
- **WHEN** Check 阶段检测到当前批次所有怪物血量 <= 0
- **THEN** SHALL 尝试推进到下一批次或下一波次；如果全部完成 SHALL 发布 `BattleEndedEvent(IsVictory=true)`

#### Scenario: Prepare 阶段恢复
- **WHEN** 进入 `Prepare` 阶段
- **THEN** SHALL 恢复玩家能量至上限、刷新怪物意图（Change 2 起改为牌组驱动）、触发抽牌到手牌上限
