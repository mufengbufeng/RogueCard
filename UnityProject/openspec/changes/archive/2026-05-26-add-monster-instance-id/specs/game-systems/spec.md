## ADDED Requirements

### Requirement: MonsterRuntime 必须拥有稳定的 InstanceId

`MonsterRuntime` SHALL 暴露一个 `int InstanceId` 字段，用于在战斗运行期间唯一标识一只怪物实例。该字段语义如下：

- `InstanceId == 0` 表示未分配（仅用于测试代码或未经 `MonsterSystem.SpawnBatch` 生成的实例）
- `InstanceId > 0` 表示已分配，全战斗内唯一
- 一旦分配，InstanceId SHALL 在该怪物存活/死亡的整个生命周期内保持不变

#### Scenario: 默认值为未分配

- **WHEN** 通过对象初始化器构造 `new MonsterRuntime { Hp = 30, MaxHp = 30 }` 而未显式设置 `InstanceId`
- **THEN** `monster.InstanceId` SHALL 等于 `0`

#### Scenario: 分配后不再变化

- **WHEN** `MonsterRuntime` 已被 `MonsterSystem.SpawnBatch` 分配 `InstanceId = 5`
- **AND** 该怪物随后受到伤害、添加 Buff、或被标记为死亡（`Hp = 0`）
- **THEN** `monster.InstanceId` SHALL 仍为 `5`

### Requirement: MonsterSystem.SpawnBatch 必须为每只新怪物分配唯一 InstanceId

`MonsterSystem` SHALL 维护一个战斗内自增 `int` 计数器；`SpawnBatch` 为每一只新生成的 `MonsterRuntime` 分配 `InstanceId = 计数器++`，初值为 `1`。`MonsterSystem` SHALL 暴露 `ResetForNewBattle()` 方法在每场战斗开始时将该计数器复位为 `1`；`BattleSystem.EnterBattle` SHALL 在调用 `SpawnBatch` 之前调用 `ResetForNewBattle()`。

#### Scenario: 同一批次内多只怪物 InstanceId 互不相同

- **WHEN** 调用 `SpawnBatch` 生成 `Count = 3` 的同种怪物
- **THEN** 生成的 3 只 `MonsterRuntime` 的 `InstanceId` 集合 SHALL 为 `{1, 2, 3}`

#### Scenario: 多次 SpawnBatch 在同一场战斗内单调递增

- **WHEN** 首次 `SpawnBatch` 生成 2 只怪物
- **AND** 后续再次 `SpawnBatch` 生成 2 只怪物
- **THEN** 第二次生成的怪物 `InstanceId` SHALL 为 `{3, 4}`

#### Scenario: 新战斗复位计数器

- **WHEN** 第一场战斗结束（已分配 `InstanceId` 至 7）
- **AND** 调用 `MonsterSystem.ResetForNewBattle()` 启动新一场战斗
- **AND** 在新战斗中首次 `SpawnBatch` 生成 1 只怪物
- **THEN** 该怪物 `InstanceId` SHALL 为 `1`
