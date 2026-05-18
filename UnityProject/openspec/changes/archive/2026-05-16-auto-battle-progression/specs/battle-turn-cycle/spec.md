## ADDED Requirements

### Requirement: 玩家回合内怪物全清后必须自动结束当前回合
系统 MUST 在玩家回合（`BattlePhase.PlayerTurn`）内，当当前批次所有在场怪物的 HP 降至 0 或以下时，自动结束玩家回合并直接进入检查阶段（`BattlePhase.Check`），无需玩家点击"结束回合"按钮。自动推进 MUST 跳过怪物回合（`BattlePhase.MonsterTurn`），不触发敌人回合开始 / 结束的 Buff Tick 与延迟卡牌结算。

#### Scenario: 玩家用一张卡清空当前批次自动进入检查阶段
- **WHEN** 战斗处于 `BattlePhase.PlayerTurn`
- **AND** 玩家成功打出一张卡，效果结算后当前批次所有怪物 HP 都 <= 0
- **THEN** 系统 MUST 在卡牌结算结束后自动进入 `BattlePhase.Check`
- **AND** 系统 MUST NOT 经过 `BattlePhase.MonsterTurn`
- **AND** 系统 MUST NOT 触发敌人回合开始 / 结束的 Buff Tick 或延迟效果结算

#### Scenario: 检查阶段按现有规则推进批次或波次
- **WHEN** 自动结束回合后进入 `BattlePhase.Check`
- **AND** 当前刷怪方案还有下一批次
- **THEN** 系统 MUST 推进到下一批次并回到 `BattlePhase.Prepare`
- **WHEN** 当前刷怪方案没有下一批次
- **THEN** 系统 MUST 发布 `BattleEndedEvent(IsVictory = true)`

#### Scenario: 多卡 AOE 清空只触发一次自动结束
- **WHEN** 玩家打出一张 AOE 卡，结算过程中多只怪物先后死亡
- **THEN** 系统 MUST 在一次出牌结算结束后才检查"是否全清"
- **AND** 系统 MUST 仅触发一次 `SetPhase(Check)`，不得在每只怪物死亡时反复切相位

#### Scenario: 怪物未全清时不触发自动结束
- **WHEN** 战斗处于 `BattlePhase.PlayerTurn`
- **AND** 玩家出牌后仍有至少一只怪物 HP > 0
- **THEN** 系统 MUST 保持在 `BattlePhase.PlayerTurn`
- **AND** 系统 MUST 等待玩家继续操作或手动结束回合
