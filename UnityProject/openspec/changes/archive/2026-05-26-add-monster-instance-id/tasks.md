## 1. MonsterRuntime 新增 InstanceId

- [x] 1.1 在 `Assets/GameScripts/HotFix/GameLogic/UI/Game/MonsterRuntime.cs` 中新增 `public int InstanceId { get; set; }`，并补函数级 XML 注释（说明 `0 = 未分配，仅测试`，`>0 = MonsterSystem 分配的全战斗内唯一标识`）

## 2. MonsterSystem 分配 InstanceId

- [x] 2.1 在 `MonsterSystem` 中新增 `private int _nextInstanceId = 1;` 字段，附中文注释
- [x] 2.2 在 `MonsterSystem` 中新增 `ResetForNewBattle()` 等价方法，由 `BattleSystem.EnterBattle` 在 `SpawnBatch` 前调用，将 `_nextInstanceId` 复位为 `1`（同步更新 spec 场景描述）
- [x] 2.3 在 `MonsterSystem.SpawnBatch` 内 `new MonsterRuntime { ... }` 对象初始化器里增加 `InstanceId = _nextInstanceId++`
- [x] 2.4 全仓 `grep` 确认没有其它生产代码路径 `new MonsterRuntime`（测试代码忽略）

## 3. MonsterListView 切换按 InstanceId 的位置缓存

- [x] 3.1 在 `MonsterListView` 中将 `_positionByIndex` 重命名为 `_positionByInstanceId`，类型保持 `Dictionary<int, Vector2>`，注释更新为"按怪物 InstanceId 缓存"
- [x] 3.2 删除 `_lastMonstersReference` 字段及 `Refresh` 中 `if (!ReferenceEquals(monsters, _lastMonstersReference)) { _positionByIndex.Clear(); ... }` 整段换波判定
- [x] 3.3 改写 `EnsurePositionFor`：参数从 `int monsterIndex` 改为 `MonsterRuntime monster`（或直接传 `int instanceId`），按 `monster.InstanceId` 读取/写入缓存
- [x] 3.4 改写 `PrunePositions`：根据当前快照中"存活且 `InstanceId > 0`"怪物集合，移除缓存中其它 key（含 `InstanceId == 0` 或不在当前列表的）
- [x] 3.5 在 `Refresh` 的循环中将原 `EnsurePositionFor(monsterIndex, ...)` 调用切换到新签名，并保留 `placedAliveIndex` 用于兜底网格定位
- [x] 3.6 `Dispose` 中 `_positionByIndex.Clear()` 同步改名为 `_positionByInstanceId.Clear()`

## 4. EditMode 测试：怪物位置稳定性

- [x] 4.1 在 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/MonsterListViewTests.cs` 新增用例 `Positions_StayStable_AfterMonstersValueReassigned`：
  - 构造 3 只显式带 `InstanceId = 1/2/3` 的 `MonsterRuntime`
  - 记录首次 Refresh 后每只怪物对应 `MonsterItemView.Root.GetComponent<RectTransform>().anchoredPosition`
  - 将 `Monsters.Value` 重新赋值为 `new[] { A, B, C }`（新数组，同元素引用）
  - 断言每只怪物的 `anchoredPosition` 与首次一致
- [x] 4.2 新增用例 `Positions_DroppedForDeadMonsters_OthersUnchanged`：
  - 同样 3 只怪物 → 首次 Refresh
  - 把 `B.Hp = 0` 后再次推送 `Monsters.Value`
  - 断言 `A`、`C` 位置不变，`B` 不再被渲染
- [x] 4.3 新增用例 `Positions_AssignedFresh_OnNewWave`：
  - 首波 InstanceId `{1, 2}` 渲染后
  - 推送 `Monsters.Value = new[] { newM4, newM5 }`（`InstanceId = 4, 5`）
  - 断言 `newM4`、`newM5` 获得新位置，缓存中不再含 `1`、`2`

## 5. EditMode 测试：MonsterSystem InstanceId 分配

- [x] 5.1 新建 `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/MonsterSystemInstanceIdTests.cs` —— 通过 internal `AssignNextInstanceId()` 直接验证计数器（避开 SpawnBatch 的 `GameLogicEntry.Config` 静态依赖，与 `MonsterSystemFlowTests` 现有测试规约一致）：
  - `AssignNextInstanceId_StartsAtOne_AndIncrementsMonotonically`：首次起从 1 起递增
  - `AssignNextInstanceId_ContinuesAcrossMultipleCallSites`：模拟多次 SpawnBatch 计数器持续递增 → `{1,2}` 与 `{3,4}`
  - `ResetForNewBattle_RestartsCounterAtOne`：分配 7 次后 `ResetForNewBattle` → 下次分配回到 1

## 6. 验证

- [x] 6.1 编译通过：Unity-Skills 服务未启用，回退 `dotnet build UnityProject.slnx` —— 0 errors，24 warnings（皆为既有 GameView 字段未赋值/HotReload 包警告，与本变更无关）
- [x] 6.2 EditMode 测试运行通过：`MonsterListViewTests`、`MonsterSystemFlowTests`（或新增的 InstanceId 测试类）
- [x] 6.3 在 Unity Editor 中手动验证：进入战斗 → 连续出多张牌 → 每只存活怪物的屏幕位置保持不变
- [x] 6.4 手动验证：怪物死亡后剩余怪物位置不变；切换下一波后怪物重新随机分布
- [x] 6.5 运行 `openspec validate add-monster-instance-id --strict` 通过校验
