# 解决方案：敌人生成系统 + 子弹系统

## 问题
飞机大战需要完整的敌人生成、行为控制和子弹发射系统。敌人需要从屏幕上方随机位置生成、向下移动、在指定位置停留并周期性攻击、最终离开屏幕后回收。同时需要通用子弹系统供玩家和敌人共用。

## 根因
1. **Entity 系统类型缺失**：`EntityManager.CreateEntityInstance()` 始终创建 `DefaultEntity` 而非 `EnemyEntity`，导致敌人无法执行自定义行为逻辑。
2. **生成位置固定**：`_enemyPoint` 场景引用未传递给生成器模块，所有敌人在同一位置出现。
3. **集合修改异常**：`EntityManager.Update()` 在 foreach 循环中，`EnemyEntity.CheckBoundary()` 调用 `HideEntity()` 修改了 `_entities` 字典，导致 "Collection was modified during enumeration" 崩溃。
4. **缺少子弹系统**：敌人攻击只有日志输出，无实际弹幕。

## 解决方案

### Entity 工厂模式
- 在 `EntityGroupOptions` 添加 `Func<IEntity> EntityFactory` 属性
- `EntityManager.CreateEntityInstance()` 优先调用 `EntityFactory`，允许每个实体组使用不同的实体子类

### 安全的实体更新循环
- `EntityManager.Update()` 先将所有实体快照到 `_entityUpdateQueue`，再逐个处理
- 处理时检查 `_entities.ContainsKey()`，跳过已被移除的实体

### 敌人行为系统
- `EnemyEntity` 继承 `EntityBase`，实现 Moving → Staying → Attacking → Moving 状态机
- `EnemySpawnerModule` 管理生成间隔、最大数量、随机位置（基于 spawn area Transform + radius）
- `EnemyBehaviorData` 作为 `userData` 传递移动速度、停留位置、攻击间隔等参数

### 通用子弹系统
- `BulletModule` 使用 `IObjectPoolManager.CreatePool<GameObject>()` 管理子弹生命周期
- `BulletData` 携带位置、方向、速度、所有者标签（"Player"/"Enemy"）
- 基于摄像机视口的边界检测自动回收出界子弹

### 集成方式
- `GamePlayProcedure.OnEnter` 中注册 `EnemySpawnerModule` 和 `BulletModule` 到 `GamePlayScope`
- `GamePlayProcedure.OnLeave` 通过 `ShutdownScope` 统一清理

## 已知遗留问题（Review 发现）
- **EntityManager 内存泄漏**：`ShowEntityAsync` 从不复用 GameObject，`LoadEntityAssetAsync` 丢弃 `AssetHandle`
- **子弹初始化时序**：`BulletModule.Initialize()` 使用 fire-and-forget，敌人可能在子弹预制体加载完成前就开始攻击
- **async void 风险**：`EnemySpawnerModule.SpawnEnemy()` 和 `GamePlayProcedure.OnEnter` 使用 `async void`，异常不会被捕获

这些问题建议作为后续独立变更来修复。

## 关键设计模式总结

| 模式 | 适用场景 | 示例 |
|------|---------|------|
| Entity + EntityFactory | 需要生命周期管理和 Update 驱动的复杂对象 | 敌人 |
| ObjectPool + Module | 高频创建/销毁的简单对象 | 子弹、背景 |
| BehaviorData as userData | 传递配置给实体实例 | EnemyBehaviorData |
| Scope-based cleanup | 场景级资源统一清理 | GamePlayScope = 1001 |

## 结果
- 敌人从屏幕上方随机位置生成，向下移动、在指定高度停留、周期性发射子弹
- 离开屏幕后自动回收，生成器恢复生成
- 流程切换时所有敌人和子弹被正确清理
