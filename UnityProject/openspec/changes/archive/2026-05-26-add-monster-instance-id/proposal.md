## Why

每次玩家出牌后，战斗界面上所有存活怪物的位置都会被重新随机洗牌，破坏空间记忆并干扰目标点击。根因是 `MonsterListView` 通过 `ReferenceEquals(monsters, _lastMonstersReference)` 判定"换波"，而 `GameViewModel.SnapshotMonsters` 每次状态同步都会 `new MonsterRuntime[]` 数组——因此每次状态变更都被误判为换波，位置缓存被全量清空。

更深层的问题是：怪物运行时实例缺乏稳定身份标识，view 层只能借助"列表引用"或"列表索引"间接表达身份，这两者都不可靠（前者每次都变，后者在死亡/换波后语义漂移）。

## What Changes

- `MonsterRuntime` 新增稳定的 `InstanceId`（`int`），用于跨快照、跨 view 刷新唯一标识一只怪物实例
- `MonsterSystem` 在 `SpawnBatch` 中为每只新生成的怪物分配自增 `InstanceId`；每场战斗 `Initialize` 时计数器复位
- `MonsterListView` 将位置缓存键从 `monsterIndex (int)` 改为 `InstanceId (int)`，并移除基于 `ReferenceEquals` 的换波判定（`PrunePositions` 按 InstanceId 自动回收死亡/离场怪物的位置即可）
- 测试程序集中既有的 `new MonsterRuntime { ... }` 写法保持兼容（默认 `InstanceId = 0` 不影响纯逻辑测试）；为 `MonsterListView` 新增"出牌后位置稳定"用例

非目标（本变更不做）：
- 不重构 `MonsterDeathEvent`（仍传 index），避免扩散到所有事件订阅方
- 不替换 view 层对 `monsterIndex` 在目标选择回调（`MonsterTargetSelected(handIndex, monsterIndex)`）中的语义，因为该 index 在波内稳定且只在瞬时点击中使用

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-systems`: `MonsterSystem` 在 `SpawnBatch` 时为每只怪物分配全战斗内唯一的 `InstanceId`；`MonsterRuntime` 新增 `InstanceId` 字段
- `gameview-monster-list-view`: `MonsterListView` 按怪物 `InstanceId` 缓存空间位置，确保同一只怪物在波内的位置在每次刷新后保持稳定

## Impact

- **代码**：
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterRuntime.cs`（新增字段）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterSystem.cs`（计数器 + 分配 + Initialize 复位）
  - `Assets/GameScripts/HotFix/GameLogic/UI/Game/Views/MonsterListView.cs`(缓存键替换、删除换波分支)
- **测试**：
  - 现有 EditMode 测试**无需改动**（`InstanceId` 默认 0 对纯逻辑无影响）
  - 新增 `MonsterListViewTests` 用例：连续多次推送 `Monsters.Value`（模拟出牌后 SnapshotMonsters）后，每只存活怪物的 `anchoredPosition` 必须保持不变
- **API / 数据兼容**：无破坏；`MonsterRuntime` 仅新增可选字段
- **依赖 / 框架**：无新增依赖
