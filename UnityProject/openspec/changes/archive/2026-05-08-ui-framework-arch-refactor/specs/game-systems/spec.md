## ADDED Requirements

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
BattleSystem SHALL 管理 Prepare → PlayerTurn → MonsterTurn → Check 阶段循环。Prepare 阶段 SHALL 恢复能量、触发抽牌。Check 阶段 SHALL 判断胜负条件。

#### Scenario: 回合结束流转
- **WHEN** 调用 BattleSystem.EndTurn()
- **THEN** SHALL 将阶段切换到 MonsterTurn，执行怪物行动，然后进入 Check 阶段

#### Scenario: Check 阶段玩家死亡
- **WHEN** Check 阶段检测到玩家血量 <= 0
- **THEN** SHALL 标记玩家死亡，发布 BattleEndedEvent(IsVictory=false)

#### Scenario: Check 阶段怪物全灭
- **WHEN** Check 阶段检测到当前批次所有怪物血量 <= 0
- **THEN** SHALL 尝试推进到下一批次或下一波次；如果全部完成 SHALL 发布 BattleEndedEvent(IsVictory=true)

#### Scenario: Prepare 阶段恢复
- **WHEN** 进入 Prepare 阶段
- **THEN** SHALL 恢复玩家能量至上限，刷新怪物意图，触发抽牌到手牌上限

### Requirement: CardSystem 管理卡牌操作
CardSystem SHALL 处理出牌、抽牌、洗牌。出牌 SHALL 校验阶段和能量，执行效果后发布 CardPlayedEvent。弃牌堆非空且牌库空时 SHALL 自动将弃牌堆洗入牌库。

#### Scenario: 出牌校验通过
- **WHEN** 调用 CardSystem.Play(handIndex)，阶段为 PlayerTurn，手牌索引有效，能量足够
- **THEN** SHALL 扣除能量、移除手牌、执行卡牌效果、发布 CardPlayedEvent

#### Scenario: 出牌校验失败
- **WHEN** 调用 CardSystem.Play(handIndex)，阶段不是 PlayerTurn 或能量不足
- **THEN** SHALL 不执行任何操作

#### Scenario: 牌库耗尽自动洗牌
- **WHEN** 抽牌时牌库为空但弃牌堆非空
- **THEN** SHALL 将弃牌堆全部卡牌洗入牌库（Fisher-Yates），然后继续抽牌

#### Scenario: 订阅回合结束事件
- **WHEN** 收到 TurnEndedEvent
- **THEN** SHALL 弃掉当前手牌，然后抽牌到手牌上限

### Requirement: MonsterSystem 管理怪物行为
MonsterSystem SHALL 刷新怪物意图、执行怪物回合行动。意图刷新 SHALL 按 Order > 0 且 Weight == 0 的序列循环模式和 Weight > 0 的权重随机模式区分。

#### Scenario: 序列意图（Boss 模式）
- **WHEN** 怪物有 Order > 0 且 Weight == 0 的意图配置
- **THEN** SHALL 按 Order 顺序循环选取意图

#### Scenario: 权重随机意图
- **WHEN** 怪物有 Weight > 0 且 Order == 0 的意图配置
- **THEN** SHALL 按权重随机选取意图

#### Scenario: 怪物攻击
- **WHEN** 怪物意图类型为 Attack
- **THEN** SHALL 对玩家造成伤害，先扣除玩家护甲再扣除血量

#### Scenario: 怪物防御
- **WHEN** 怪物意图类型为 Defend
- **THEN** SHALL 增加怪物自身护甲

#### Scenario: 怪物死亡发布事件
- **WHEN** 怪物血量因伤害降至 0 或以下
- **THEN** SHALL 发布 MonsterDeathEvent
