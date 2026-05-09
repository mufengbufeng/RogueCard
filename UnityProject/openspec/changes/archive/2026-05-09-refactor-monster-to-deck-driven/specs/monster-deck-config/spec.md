## ADDED Requirements

### Requirement: 怪物配置表必须扩展能量与手牌字段

`TbMonster` MUST 新增 `MaxEnergy` (int) 与 `HandLimit` (int) 字段，使每只怪物的能量池上限和手牌上限独立可配。

#### Scenario: TbMonster 字段扩展
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\monster.xlsx`
- **THEN** `TbMonster` 表结构 MUST 包含 `MaxEnergy` (int) 字段
- **AND** `TbMonster` 表结构 MUST 包含 `HandLimit` (int) 字段
- **AND** 测试怪物记录（史莱姆）MUST 填入 `MaxEnergy = 1` 与 `HandLimit = 1`

### Requirement: 怪物牌组必须支持剧本前置 + 兜底权重池混合模式

系统 MUST 在配置数据目录中提供 `TbMonsterDeck` 表，使怪物的每回合行为同时由"剧本回合（Order > 0）"与"兜底牌堆（Order = 0）"驱动。一只怪物 MAY 同时拥有零条到多条剧本行与零条到多条兜底行。

#### Scenario: 创建 TbMonsterDeck 表结构
- **WHEN** 检查 `D:\UnityGame\Self\RogueCard\Configs\GameConfig\Datas\monster_deck.xlsx`
- **THEN** 系统 MUST 存在用于生成 `TbMonsterDeck` 的表结构
- **AND** 该结构 MUST 包含 `Id` (int) / `MonsterId` (int#ref=monster.TbMonster) / `Order` (int) / `CardId` (int#ref=card.TbCard) / `Count` (int) 字段

#### Scenario: 注册到 Luban 表清单
- **WHEN** 检查 `__tables__.xlsx`
- **THEN** 系统 MUST 不再存在 `monster.TbMonsterIntent` 注册
- **AND** 系统 MUST 存在 `monster.TbMonsterDeck` 注册，记录类名 `MonsterDeck`
- **AND** 该注册 MUST 从 `monster_deck.xlsx` 读取

### Requirement: 剧本行与兜底行必须有明确的字段约定

`TbMonsterDeck` 表的每行 MUST 满足以下约定：

- 剧本行：`Order > 0`，`Count = 1`，描述"该怪物第 Order 回合必出 CardId 这张卡"
- 兜底行：`Order = 0`，`Count >= 1`，描述"该怪物的兜底抽牌堆中此卡放 Count 份"

#### Scenario: 剧本行约定
- **WHEN** 一行的 `Order > 0`
- **THEN** `Count` MUST 等于 1（剧本回合每回合只出一张卡）
- **AND** 不同行 `Order` 值 MUST 在同一只怪物中互不相同

#### Scenario: 兜底行约定
- **WHEN** 一行的 `Order = 0`
- **THEN** `Count` MUST 大于等于 1
- **AND** 该行 MUST 进入怪物的初始抽牌堆，按 `Count` 复制为相应份数

### Requirement: 怪物牌组的引用卡牌必须使用怪物可用的 OwnerKind

`TbMonsterDeck` 引用的 `CardId` 对应的 `TbCard` 记录 MUST 满足 `OwnerKind in {Monster, Both}`。

#### Scenario: 校验 OwnerKind
- **WHEN** 检查任意一行 `TbMonsterDeck` 的 `CardId`
- **THEN** 对应的 `TbCard` 记录 `OwnerKind` MUST 不是 `Player`

### Requirement: MVP 史莱姆牌组必须配置剧本 3 回合 + 兜底 2 张

为联调最低等级怪物战斗，`TbMonsterDeck` MUST 为史莱姆（最低等级怪物）至少提供以下配置：

- 3 行剧本（Order = 1 攻 / Order = 2 盾 / Order = 3 攻，每行 Count = 1）
- 2 行兜底（Order = 0 攻 Count = 2 / Order = 0 盾 Count = 1）

#### Scenario: 史莱姆数据完整性
- **WHEN** 检查 `monster_deck.xlsx` 中 MonsterId 等于史莱姆 Id 的所有行
- **THEN** MUST 至少存在 3 行 `Order > 0` 的剧本行（覆盖第 1、2、3 回合）
- **AND** MUST 至少存在 2 行 `Order = 0` 的兜底行
- **AND** 所有引用的 `CardId` MUST 是 `OwnerKind in {Monster, Both}` 的卡

### Requirement: 怪物 AI 决策必须按剧本优先 / 兜底次之的顺序生成本回合行动

运行时 `MonsterAiBrain.SelectIntent` MUST 按以下顺序生成怪物本回合的 `PendingCards`：

1. 若 `monster.TurnsAlive + 1 == N` 存在 `Order = N` 的剧本行 → `PendingCards = [那张卡]`，**不抽牌**、**不进行能量校验**
2. 否则 → 调用 `MonsterCardSystem.Draw` 抽到 `HandLimit` → 在手牌中按 `Cost` 降序贪心：能量够则选入 `PendingCards` 并扣临时能量，能量不够则跳过该卡，直到无卡可出

#### Scenario: 剧本回合直接产出意图
- **WHEN** 怪物的 `TurnsAlive + 1` 等于某行剧本的 `Order`
- **THEN** `MonsterAiBrain` MUST 把该行 `CardId` 对应的 `Card` 加入 `PendingCards`
- **AND** MUST NOT 调用 `MonsterCardSystem.Draw`
- **AND** MUST NOT 校验该卡的 `Cost` 与怪物 `CurrentEnergy`

#### Scenario: 兜底回合走真抽牌 + 贪心
- **WHEN** 怪物的 `TurnsAlive + 1` 不等于任何剧本行的 `Order`
- **THEN** `MonsterAiBrain` MUST 先调用 `MonsterCardSystem.Draw(monster, monster.HandLimit)`
- **AND** 在抽完牌后按 `Cost` 降序遍历手牌
- **AND** 对每张牌检查 `card.Cost <= 临时剩余能量`，满足则选入 `PendingCards` 并 `临时剩余能量 -= card.Cost`
- **AND** 在没有可出的牌时停止

#### Scenario: 兜底抽牌堆耗尽时洗弃牌堆
- **WHEN** `MonsterCardSystem.Draw` 在抽牌堆为空时
- **THEN** MUST 把弃牌堆全部洗回抽牌堆（Fisher-Yates）
- **AND** MUST 继续抽牌直到达到 HandLimit 或两堆都耗尽

#### Scenario: MonsterTurn 结束后弃光手牌
- **WHEN** `MonsterSystem.ExecuteTurn` 完成对一只怪物的所有 `PendingCards` 执行
- **THEN** MUST 调用 `MonsterCardSystem.DiscardAllHand(monster)`
- **AND** MUST 清空 `monster.PendingCards`
