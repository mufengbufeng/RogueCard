## Context

`MonsterListView` 当前用 `Dictionary<int monsterIndex, Vector2>` 缓存怪物在容器内的随机位置，并以 `ReferenceEquals(monsters, _lastMonstersReference)` 判定是否换波：

```
监听:    _context.Monsters.Changed → Refresh()
判定:    if (!ReferenceEquals(monsters, _lastMonstersReference)) Clear()
缓存:    _positionByIndex[monsterIndex] = randomPos
```

数据源端：

```
GameModel.PropertyChanged("Monsters")
  → GameViewModel.OnModelPropertyChanged
  → Monsters.Value = SnapshotMonsters()   // 每次 new MonsterRuntime[]
```

每次出牌都会让 `GameModel` 推送 `Monsters` 变更（怪物 Hp/Armor/Buff/PendingCards 改动），导致 `SnapshotMonsters` 每次都创建新数组，view 误判为换波，位置被洗。

**关键观察**：`SnapshotMonsters` 拷贝时 **元素引用稳定**（`copy[i] = src[i]` 是引用复制），所以 `MonsterRuntime` 实例在波内是稳定的——只是 view 层缺乏稳定 key 来表达"同一只怪物"。

**生成路径单一**：`MonsterSystem.SpawnBatch` 是唯一生产 `MonsterRuntime` 的运行时路径（其他都在测试代码里），便于集中分配 `InstanceId`。

## Goals / Non-Goals

**Goals:**
- 同一波战斗内，每只怪物的屏幕位置在多次 view 刷新后保持不变，直至该怪物死亡或换波
- `MonsterRuntime` 拥有跨快照稳定的运行时身份标识（`InstanceId`）
- 测试代码无需为 `InstanceId` 显式赋值即可继续工作（即默认值 0 必须能跑通现有用例）

**Non-Goals:**
- 不修改 `MonsterDeathEvent`/`MonsterTargetSelected` 等通过 index 传递怪物身份的事件接口（留待后续"事件身份化"专项）
- 不改 `ReactiveProperty<T>` 的相等比较语义
- 不优化 `SnapshotMonsters` 的"无变化则复用数组"逻辑（不属于本变更目标，且会增加耦合）
- 不引入 GUID/字符串 id；只用 `int` 自增即可满足身份唯一与日志可读性

## Decisions

### 决策 1：身份标识用 `int InstanceId`，由 `MonsterSystem` 分配

**选择**：`MonsterRuntime` 新增 `int InstanceId { get; set; }`，由 `MonsterSystem._nextInstanceId` 自增分配。

**理由**：
- `int` 性能与可读性优于 `Guid`/`string`，对日志/调试更友好
- "自增 id" 在单机回合制场景完全够用；本项目无网络同步、无持久化需求
- 公共 setter（`{ get; set; }`）保持与 `MonsterRuntime` 其他字段一致的极简模式，方便测试代码用对象初始化器构造

**替代方案**：
- `MonsterRuntime` 实例引用直接做 Dictionary key（不加字段）—— 拒绝：把"视图位置稳定"语义隐式绑死到对象生命周期，调试日志/未来事件接口仍需身份字段，迟早要加
- `Guid` —— 拒绝：本场景过度设计，且日志冗长

### 决策 2：计数器作用域 = 每场战斗

**选择**：`MonsterSystem.Initialize`（或等价的战斗初始化入口）将 `_nextInstanceId` 复位为 1（0 保留给"未分配"语义）。

**理由**：
- 每场战斗的怪物 InstanceId 从 1 开始，日志/调试更易读（"本场第 3 只生成的怪物"）
- 跨战斗复用同一计数器会让 id 单调膨胀，无收益
- 0 保留为"默认未分配"哨兵，便于测试代码不显式赋值时与生产代码区分

**替代方案**：
- `MonsterSystem` 单例生命周期内单调递增 —— 拒绝，理由如上
- `static` 全局计数器 —— 拒绝，全局状态破坏可测性

### 决策 3：View 层完全删除"换波判定"分支

**选择**：`MonsterListView.Refresh` 不再判断 `ReferenceEquals`、不再保留 `_lastMonstersReference` 字段；位置缓存键从 `monsterIndex` 改为 `InstanceId`；`PrunePositions` 改为遍历当前快照中所有存活怪物的 `InstanceId`，把缓存中不在该集合内的 key 全部移除。

**理由**：
- 一旦 key 是稳定身份，"换波"和"死亡"在缓存上语义统一：新 InstanceId 自然没有缓存→分配新位置；不再出现的 InstanceId 自然被 prune
- 删掉 `_lastMonstersReference` 字段消除"列表引用变 = 换波"这个错误假设的最后一个落脚点，防止退化

**替代方案**：
- 保留换波判定但改为"集合签名比较" —— 拒绝，PrunePositions 已经足够，无须再加一层判断

### 决策 4：测试兼容策略 = 默认 0 + 不强制 setter

**选择**：现有 `new MonsterRuntime { Hp = ..., MaxHp = ... }` 用法保持原样；仅在新增的 `MonsterListView` 位置稳定性测试里，对 `MonsterRuntime` 显式设置不同的 `InstanceId`。

**理由**：
- 现有纯逻辑测试（BattleSystem / MonsterAiBrain / CardReleaseResolver 等）不关心 view 层位置，`InstanceId = 0` 对它们透明
- 改动面最小，回归风险最低

## Risks / Trade-offs

| 风险 | 缓解 |
|---|---|
| 测试代码里多只 monster `InstanceId` 都为 0，若未来某段生产代码也用 `InstanceId` 做 key 会冲突 | 文档在 `MonsterRuntime.InstanceId` 注释中说明"0 = 未分配，仅测试用"；新增使用方在 PR review 时检查 |
| view 层位置 key 切换后，目标选择回调仍用 `monsterIndex` 传递，存在"两套身份"并存 | 接受；非本变更目标，且 `MonsterTargetSelected` 是瞬时点击事件，使用 index 不会引发位置漂移 |
| `MonsterSystem.Initialize` 复位计数器，但若有测试直接调用 `SpawnBatch` 而绕过 `Initialize`，则 `_nextInstanceId` 可能未复位 | `MonsterSystem` 现有 `Initialize` 已在所有真实战斗入口被调用；为测试可见性，计数器初值设为 1，且字段名直白便于排查 |
| `_positionByIndex` 字段被 view 层私有持有，重命名后若有其它代码反射访问会断 | 全仓 `grep` 确认无外部引用（私有字段不应该有） |

## Migration Plan

无运行时数据迁移。所有改动仅影响 view 渲染缓存与运行时身份分配；战斗逻辑、存档、配置表均不涉及。

部署后行为差异：
- 用户视角：怪物在波内位置稳定，无其它可见变化
- 开发者视角：日志中怪物可通过 `InstanceId` 跨事件追踪

## Open Questions

无。
