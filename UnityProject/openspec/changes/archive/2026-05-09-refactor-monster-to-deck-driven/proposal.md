## Why

Change 1 已经把卡牌效果模型与执行器抽象出来（`CardEffectExecutor` + `IBattleActor`）。本变更把怪物的攻击/护盾行动也接到这套统一管线上，把 `IntentType` 枚举驱动的旧模型彻底替换为"行为对称的牌组驱动"模型——怪物拥有自己的能量池、抽牌堆、手牌、弃牌堆和 AI 选牌脑，每回合走完整的 Prepare → AI 选牌 → 显示意图 → MonsterTurn 出牌流程。

用户最终决策已经明确：**行为对称（怪物有真手牌+能量+抽牌）+ 路线 2（剧本前置 + AI 兜底）**。前 X 回合按 `Order` 序列硬编码（不抽牌），之后从抽牌堆抽到手牌、AI 在能量约束下选牌。

## What Changes

- 卸载现有 `monster-intent-config` capability：删除 `TbMonsterIntent` 表 / `MonsterIntentType` 枚举 / 相关 spec 全部替换
- 新增 `monster-deck-config` capability：`TbMonsterDeck` 表，字段 `Id` / `MonsterId` / `Order` / `CardId` / `Count`
  - `Order > 0`：第 N 回合的剧本卡（不参与抽牌堆构造）
  - `Order = 0`：进抽牌堆，`Count` 决定该卡的份数
- `TbMonster` 表新增 `MaxEnergy` (int) / `HandLimit` (int) 字段；每只怪物独立配置
- `MonsterRuntime` 在 Change 1 已实现 `IBattleActor` 基础上，启用 `Hand / DrawPile / DiscardPile / CurrentEnergy` 字段
- 新增 `MonsterCardSystem`：负责怪物的初始化牌堆、抽牌、弃牌、洗牌
- 新增 `MonsterAiBrain`：`SelectIntent(monster, currentTurn)` 决策函数
  - 当前回合 N 在剧本范围内 → 直接返回 `Order = N` 的剧本卡（不抽牌）
  - 否则 → 从手牌中按"能量约束 + 优先 Cost 高"选 0~N 张牌
- 新增 `Buff` 列表 tick 已经在 Change 1 的 BattleSystem 接入；本变更只确保怪物 Buff 也参与 tick
- `MonsterSystem.RefreshIntents` 重命名为 `BeginMonsterPrepare`，负责调用 MonsterCardSystem.Draw 与 MonsterAiBrain.SelectIntent
- `MonsterSystem.ExecuteTurn` 改为按 Prepare 选定的 `PendingCards` 调用 `CardEffectExecutor.Execute`
- `MonsterTurn` 末尾对每只怪物执行 `DiscardAllHand`
- BREAKING：`MonsterIntent.IntentType` / `MonsterIntent.Value` / 相关 EditMode 测试全部废止；`monster-intent-config` capability 整体被替换为 `monster-deck-config`

## Capabilities

### New Capabilities

- `monster-deck-config`：怪物牌组配置（TbMonsterDeck 剧本+权重池混合结构、TbMonster 新增能量/手牌字段）

### Modified Capabilities

- `game-systems`：`MonsterSystem` 段重写；`BattleSystem` 段 `Prepare` 子流程包含怪物抽牌+AI 选牌

### Removed Capabilities

- `monster-intent-config`：整体被 `monster-deck-config` 替换

## Impact

- 受影响代码:
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterSystem.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterRuntime.cs`（启用 Hand/DrawPile/DiscardPile/CurrentEnergy）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/BattleSystem.cs`（Prepare 阶段调用 MonsterCardSystem）
  - 新文件：`MonsterCardSystem.cs` / `MonsterAiBrain.cs`
  - `GameProcedure.cs`（注册 MonsterCardSystem 实例）
- 受影响配置:
  - `Configs/GameConfig/Datas/monster.xlsx`（新增 MaxEnergy / HandLimit 字段）
  - `Configs/GameConfig/Datas/monster_deck.xlsx`（新表）
  - `Configs/GameConfig/Datas/monster_intent.xlsx`（删除文件）
  - `Configs/GameConfig/Datas/__tables__.xlsx`（移除 monster.TbMonsterIntent，新增 monster.TbMonsterDeck）
  - 重新跑 Luban 生成
- 受影响测试:
  - 新增 `MonsterCardSystemTests`、`MonsterAiBrainTests`（EditMode）
  - 现有 `MonsterSystemTests` / `BattleSystemTests` 调整
- 受影响数据:
  - 史莱姆（最低等级怪）配置：`MaxEnergy=1` / `HandLimit=1`，`TbMonsterDeck` 写 3 行剧本（攻 / 盾 / 攻）+ 2 行兜底（攻 ×2 / 盾 ×1）
  - `card.xlsx` 增加 2 张怪物专属卡（`OwnerKind=Monster`）：怪物攻击 3、怪物护盾 3
- 不受影响:
  - `CardSystem`（玩家方）、`CardEffectExecutor`、`IBattleActor`、`PlayerActor`
  - UI 层 `GameScreen`（Change 4 处理意图渲染）
- 依赖关系：本变更前置 = Change 1；后续 Change 4 依赖本变更产出的 PendingCard 信息渲染意图
