## Why

战斗系统设计要求"角色基础数据由配置驱动"，其中明确规定：

- 生命：基础 100，每升一级 +10
- 能量：基础 3，升级不变
- 卡牌上限：基础 10，升级不变
- 经验：跟随等级表

现有 `TbPlayerLevel` 表只提供了 `BaseEnergy` / `HandLimit` / `ExpToLevelUp`，**完全缺少 HP 字段**。运行时 `BattleSystem.InitPlayerAttributes` 临时使用 `GameModel.DefaultPlayerHp` 这个硬编码常量为玩家初始化生命，违背"角色数据必须由表配置"的原则。

本变更补齐 HP 字段，并把 `InitPlayerAttributes` 重写为完全从 `TbPlayerLevel` 读取数值。

## What Changes

- 在 `TbPlayerLevel` 表中新增 `BaseHp` (int) 字段，每行独立配置该等级的 HP 上限
- MVP 数据按"基础 100 + 升级 +10"规则填入：1 级 100、2 级 110、3 级 120、4 级 130、5 级 140
- `BattleSystem.InitPlayerAttributes` 重写：从 `TbPlayerLevel.GetOrDefault(currentLevel)` 读取所有玩家初始属性
- 移除 `GameModel.DefaultPlayerHp` 常量与硬编码 fallback
- `GameModel.InitBattleAttributes` 签名调整为 `(maxEnergy, handLimit, maxHp)`，并在内部初始化 `PlayerHp = maxHp` / `PlayerMaxHp = maxHp`
- 不引入"经验值 / 等级提升"运行时逻辑（升级与三选一奖励是后续变更的范围）

## Capabilities

### Modified Capabilities

- `player-level-config`：新增 `BaseHp` 字段定义与 5 级数据
- `game-systems`：`BattleSystem` 的 `InitPlayerAttributes` 段从 fallback 常量改为查表

## Impact

- 受影响代码:
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/BattleSystem.cs`（`InitPlayerAttributes` 重写）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameModel.cs`（删除 `DefaultPlayerHp` 常量、扩展 `InitBattleAttributes` 签名）
- 受影响配置:
  - `Configs/GameConfig/Datas/player_level.xlsx`（新增字段 + 5 级数据）
  - 重新跑 Luban 生成
- 受影响测试:
  - `BattleSystemTests` / `GameModelTests` 中涉及初始化的用例需要更新断言
- 不受影响:
  - `CardSystem`、`MonsterSystem`、`CardEffectExecutor`、UI 层
- 依赖关系: 与 Change 1、Change 2 互相独立，可并行；建议串行排在 Change 1 / Change 2 之后以减少 BattleSystem 文件冲突
