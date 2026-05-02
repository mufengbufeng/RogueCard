# 全工作树 Review 修复（F1-F6）

## 问题
对完整工作树进行 opence-review 后，发现 6 项问题（从高到低排列）：
- **F1**: `EntityManager.Shutdown()` 使用 `entityGroup.Release()` 只清理池内容，未从 `ObjectPoolManager._pools` 字典中移除键，重新进入游戏时 `CreatePool` 抛 `InvalidOperationException`
- **F2**: `GamePlayProcedure.OnLeave()` 退出玩法时未关闭 `GamePlayView` 窗口，导致 UI 残留
- **F3**: UHub binding 使用 public 字段 + `_` 前缀命名约定是否正确（确认无问题）
- **F4**: `EnemyEntity.ResolveGamePlayModel()` 使用带缓存的 early return，当 Model 在 Procedure 切换时被 Shutdown 后，缓存指向已释放的对象
- **F5**: `GamePlayController` 订阅了 `OnContinueRequested` 但未创建对应处理方法，且 `UnsubscribeMenuController()` 中未退订该事件
- **F6**: `GamePlayModel.AddScore()` 对负数 delta 静默忽略，缺少诊断日志

## 根因

### F1: ObjectPool 泄漏
`EntityGroup.Release()` 内部调用 `ObjectPool<T>.Shutdown()` → `Clear()`，只清空池内容但不从 `ObjectPoolManager._pools` 字典移除键。正确的清理方法是 `ObjectPoolManager.DestroyPool<T>(name)`，它同时调用 `pool.Shutdown()` 并移除字典键。

### F4: Model stale 引用
`ResolveGamePlayModel()` 原实现使用 `if (_gamePlayModel != null) return;` 提前返回，当 `GamePlayModel` 在 Procedure 切换时被 `Shutdown()`（`_initialized = false, _manager = null`），缓存的 `_gamePlayModel` 仍非 null 但已失效。对其调用 `SetValue` 不会崩溃但语义错误。

### F5: 事件订阅不完整
`OnContinueRequested += HandleContinueGame` 已添加到订阅代码中，但 `HandleContinueGame` 方法尚未实现，`UnsubscribeMenuController()` 也只退订了 `OnBackRequested`。

## 修复

### F1 — `EntityManager.cs`
- `Shutdown()`: 将 `entityGroup.Release()` 改为 `_objectPoolManager.DestroyPool<IEntity>(kvp.Key)`
- `RemoveEntityGroup()`: 同样改为 `_objectPoolManager.DestroyPool<IEntity>(name)`

### F2 — `GamePlayProcedure.cs`
- `OnLeave()`: 在模块清理之前添加 `GameLogicEntry.UI.CloseWindowAsync("GamePlayView").Forget()`

### F3 — 无需修改
- UHub binding 通过 `ComponentBinder` 反射扫描 `NonPublic | Public | Instance` 字段，命名约定 `_xxxYyy` → RC key `XxxYyy`。`public` 字段 + `_` 前缀是项目内 UIView 子类的标准用法。

### F4 — `EnemyEntity.cs`
- `ResolveGamePlayModel()` 改为每次重新查询 `ModelManager.TryGetModel(typeof(GamePlayModel))`，返回 `GamePlayModel` 而非 `void`
- `TryAwardKillScore()` 使用返回值而非字段缓存，避免 stale 引用

### F5 — `GamePlayController.cs`
- 新增 `HandleContinueGame()` 方法：调用 `UnsubscribeMenuController()` + 输出日志
- `UnsubscribeMenuController()` 添加 `_gameMenuController.OnContinueRequested -= HandleContinueGame`

### F6 — `GamePlayModel.cs`
- `AddScore()` 在 `delta <= 0` 分支添加 `Log.Warning($"[GamePlayModel] AddScore 收到非正增量 {delta}，已忽略")`

## 关键教训

### ObjectPool 生命周期管理
- `ObjectPool.Shutdown()` / `Clear()` 只清理池内容，不负责从 `ObjectPoolManager` 注册表中移除
- 当需要彻底释放一个对象池（包括注册表键）时，必须调用 `ObjectPoolManager.DestroyPool<T>(name)`
- `Clear()` 是幂等的，对空池安全调用

### Model 引用在跨 Procedure 时可能失效
- `ModelBase.Shutdown()` 将 `_initialized = false`、`_manager = null`，但对象本身仍在内存中
- 持有 Model 引用的代码（如 Entity）在跨生命周期场景中应每次重新查询，而非依赖缓存

### 事件订阅必须成对
- 所有 `+=` 订阅必须有对应的 `-=` 退订
- 当添加新事件订阅时，必须同时检查所有退订路径（`OnExit`、`Dispose`、`UnsubscribeXxx` 等）
