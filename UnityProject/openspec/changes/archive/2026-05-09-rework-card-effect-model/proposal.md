## Why

当前 `TbCard` 用单字段 `EffectType` (string) + 单字段 `Value` 表达卡牌效果，运行时 `CardSystem.ApplyEffect` 只处理 `Attack` / `Defend` 两个 case。这套模型无法表达战斗系统设计需要的 5 类卡牌：

- **近战**：单目标 6 伤，1 能量
- **投射**：1 能量，对全部存活怪物分散总伤害 6（2 目标 → 各 3）
- **法术**：1 能量，单/多目标即时 8 伤 + 持续 X 回合每回合 2 点 DoT
- **能量**：恢复出牌资源（当前能量 +2）
- **护盾**：1 能量，自身 +5 护盾

更进一步，已经决定让怪物的攻击/护盾也走卡牌系统（行为对称）——这要求"卡牌效果定义"独立于"谁打"，能被玩家手牌和怪物行动共同复用。当前结构没有任何抽象层支持这一点。

## What Changes

- 重塑 `TbCard` 表结构：移除 `EffectType` / `Value` 字段；新增 `OwnerKind`（Player / Monster / Both）和 `TargetMode`（SingleAuto / SingleManual / All / SplitAcrossAll / Self）
- 新增 `TbCardEffect` 表：一张卡可挂多条效果行，每行 `Kind` + `Value` + `Duration`，`Kind` 覆盖 Damage / Shield / DamageDot / EnergyGain
- 新增运行时能力 `card-effect-runtime`：定义 `IBattleActor` 抽象（HP / Armor / Buff / Energy 的统一接口），以及 `CardEffectExecutor` 纯函数式服务负责执行卡牌效果
- 玩家方新建 `PlayerActor` 适配 `GameModel` 字段为 `IBattleActor`；`MonsterRuntime` 直接实现 `IBattleActor`
- `CardSystem.ApplyEffect` 改为调用 `CardEffectExecutor.Execute`，不再 switch 字符串
- `CardRuntime` 引入 `RemainingDuration` 等运行时字段以支持 DoT 表达
- 配置数据：在 `card.xlsx` 重写 5 张 MVP 基础卡（覆盖 5 类）；在 `card_effect.xlsx` 新建效果行；在 `__tables__.xlsx` 注册新表
- BREAKING：原 spec `basic-card-config` 中"EffectType 必须为 Attack/Defense/EnergyRecover"等约束被废止，迁移到新模型

## Capabilities

### New Capabilities

- `card-effect-runtime`：卡牌效果运行时（IBattleActor + CardEffectExecutor + Buff 列表 + EffectKind / TargetMode / OwnerKind 枚举）

### Modified Capabilities

- `basic-card-config`：字段重做、效果模型重做、MVP 数据从 3 张升到 5 张
- `game-systems`：`CardSystem` 段：ApplyEffect 改为调用 Executor

## Impact

- 受影响代码:
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/CardSystem.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/CardRuntime.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterRuntime.cs`
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameModel.cs`（PlayerActor 包装、Buff 列表）
  - 新文件：`IBattleActor.cs` / `CardEffectExecutor.cs` / `EffectKind.cs` / `TargetMode.cs` / `OwnerKind.cs` / `BuffRuntime.cs` / `PlayerActor.cs`
- 受影响配置:
  - `Configs/GameConfig/Datas/card.xlsx`（结构重做 + 数据重写）
  - `Configs/GameConfig/Datas/card_effect.xlsx`（新表）
  - `Configs/GameConfig/Datas/__tables__.xlsx`（注册 `card_effect.TbCardEffect`）
  - 重新跑 Luban 生成 `GameConfig.card.*`
- 受影响测试:
  - 新增 `CardEffectExecutorTests`（EditMode）覆盖 5 种 EffectKind
  - 现有 `CardSystem` 相关 EditMode 测试需要随之调整
- 不受影响:
  - `MonsterSystem`（Change 2 重塑）
  - `BattleSystem`（除 `InitPlayerAttributes` 由 Change 3 调整外）
  - UI 层 `GameScreen` / `GameViewModel`（Change 4 处理多目标选择 UI）
- 依赖关系：无前置；后续 Change 2/3/4 都依赖本变更
