## 1. 配置表结构与数据

- [x] 1.1 在 `Configs/GameConfig/Datas/monster.xlsx` 中给 `TbMonster` 新增 `MaxEnergy` (int) / `HandLimit` (int) 字段
- [x] 1.2 删除 `Configs/GameConfig/Datas/monster_intent.xlsx`（整张表与配套数据）
- [x] 1.3 新建 `Configs/GameConfig/Datas/monster_deck.xlsx`，结构包含 `Id` (int) / `MonsterId` (int#ref=monster.TbMonster) / `Order` (int) / `CardId` (int#ref=card.TbCard) / `Count` (int)
- [x] 1.4 在 `__tables__.xlsx` 移除 `monster.TbMonsterIntent` 注册行；新增 `monster.TbMonsterDeck` 注册行（记录类名 `MonsterDeck`，从 `monster_deck.xlsx` 读取）
- [x] 1.5 在 `card.xlsx` 与 `card_effect.xlsx` 中新增 2 张怪物专属卡：怪物攻击 3（`OwnerKind=Monster`，`Cost=1`，`TargetMode=SingleAuto`，挂 1 条 Damage 3 effect）、怪物护盾 3（`OwnerKind=Monster`，`Cost=1`，`TargetMode=Self`，挂 1 条 Shield 3 effect）
- [x] 1.6 在 `monster.xlsx` 给史莱姆填入 `MaxHp=30` / `MaxEnergy=1` / `HandLimit=1`
- [x] 1.7 在 `monster_deck.xlsx` 给史莱姆写入数据：剧本 3 行（Order=1 攻 / Order=2 盾 / Order=3 攻，每行 Count=1）+ 兜底 2 行（Order=0 攻 Count=2 / Order=0 盾 Count=1）
- [x] 1.8 跑 Luban 生成代码，确认 `GameConfig.monster.MonsterDeck` 类型生成、`MonsterIntent` / `MonsterIntentType` 类型已被移除
- [x] 1.9 在 `Assets/GameScripts/HotFix/GameProto/` 中扫描确认 `MonsterIntent` 类已不存在

## 2. 运行时类型与服务

- [x] 2.1 `MonsterRuntime.cs` 启用 `Hand : List<CardRuntime>` / `DrawPile : List<CardRuntime>` / `DiscardPile : List<CardRuntime>` / `PendingCards : List<Card>` / `TurnsAlive : int` 字段（Change 1 已加 Energy/MaxEnergy/Buffs，本变更补齐其余）
- [x] 2.2 `MonsterRuntime` 移除 `CurrentIntent` / `IntentSequenceIndex` 字段
- [x] 2.3 新建 `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterCardSystem.cs`，方法：`InitDeck(MonsterRuntime)` / `Draw(MonsterRuntime, int count)` / `DiscardAllHand(MonsterRuntime)` / `ShuffleDrawPile(MonsterRuntime)`
- [x] 2.4 `MonsterCardSystem.InitDeck` 读 `TbMonsterDeck`，按 `Order > 0` 与 `Order = 0` 分组：`Order > 0` 的存入怪物的 `ScriptedCards : Dictionary<int, Card>`（键为 Order）；`Order = 0` 的按 `Count` 展开后存入 `DrawPile` 并洗牌
- [x] 2.5 新建 `MonsterAiBrain.cs`，静态方法 `SelectIntent(MonsterRuntime monster, int turnNumber, IList<Card> scriptedCards) → IList<Card>`
- [x] 2.6 `SelectIntent` 实现：`scriptedCards[turnNumber]` 存在 → 返回单元素列表；否则按手牌 Cost 降序贪心，能量足则选入返回，扣临时能量，直到无可出牌

## 3. MonsterSystem 重写

- [x] 3.1 `MonsterSystem.RefreshIntents` 重命名为 `BeginMonsterPrepare`
- [x] 3.2 `BeginMonsterPrepare` 逻辑：对每只存活怪物 `monster.TurnsAlive++`、若有剧本卡则跳过抽牌；否则 `_monsterCardSystem.Draw(monster, monster.HandLimit)`，再调 `MonsterAiBrain.SelectIntent`，结果写入 `monster.PendingCards`
- [x] 3.3 `MonsterSystem.ExecuteTurn` 改为：对每只存活怪物按 `PendingCards` 依次调用 `CardEffectExecutor.Execute(card, monster, [PlayerActor], events)`，然后 `_monsterCardSystem.DiscardAllHand(monster)`，最后清空 `PendingCards`
- [x] 3.4 `MonsterSystem.SpawnBatch` 在创建 `MonsterRuntime` 后立即调用 `_monsterCardSystem.InitDeck(monster)`，并初始化 `CurrentEnergy = MaxEnergy`、`TurnsAlive = 0`

## 4. BattleSystem 接入

- [x] 4.1 `BattleSystem.ExecutePreparePhase` 在恢复玩家能量、抽玩家牌之后，调用 `_monsterSystem.BeginMonsterPrepare`
- [x] 4.2 `BattleSystem.Initialize` 增加 `MonsterCardSystem` 注入（GameProcedure 创建并传入）
- [x] 4.3 准备阶段对每只怪物 `CurrentEnergy = MaxEnergy`（与玩家恢复能量逻辑对称）
- [x] 4.4 `GameProcedure.cs` 创建 `_monsterCardSystem`，与其他 system 一起 Init / Initialize / Dispose

## 5. 测试

- [x] 5.1 新建 `MonsterCardSystemTests.cs`：覆盖 InitDeck（剧本 + 兜底分组）、Draw（不超过 HandLimit）、洗牌、DiscardAllHand
- [x] 5.2 新建 `MonsterAiBrainTests.cs`：覆盖第 1/2/3 回合走剧本路径、第 4 回合走贪心 + 能量约束
- [x] 5.3 调整 `MonsterSystemTests.cs`：删除 IntentType 用例，新增"史莱姆战斗 5 回合脚本"端到端用例
- [x] 5.4 调整 `BattleSystemTests.cs`：Prepare 阶段会触发怪物 BeginMonsterPrepare 的断言
- [x] 5.5 删除原 `monster-intent-config` 相关的所有 EditMode 测试（如有）
- [x] 5.6 跑 unity-compile-check 通过；EditMode 全部测试绿

## 6. 文档与归档

- [x] 6.1 在 `add-card-rogue-core-loop/tasks.md` 中标记本变更已完成
- [x] 6.2 通过 `/opsx:verify` 校验本变更交付（`openspec validate --strict` 通过；EditMode 253/253 全绿）
- [x] 6.3 通过 `/opsx:archive` 归档本变更
- [x] 6.4 在 `/opsx:archive` 完成后，手动执行 `rm -r openspec/specs/monster-intent-config/` 并提交
