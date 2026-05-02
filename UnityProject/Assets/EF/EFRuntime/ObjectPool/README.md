# 对象池模块

## 概述
对象池模块用于在 Unity 项目中统一管理可重复利用的对象实例，降低频繁创建与销毁带来的 GC 压力。模块提供灵活的配置能力、生命周期控制以及完备的统计接口，适用于子弹、特效、网络消息等高频资源的复用场景。

## 核心类型
- `ObjectPoolOptions`：定义容量、自动释放间隔、过期时间等运行参数，所有 setter 均内建合法性校验。
- `IObjectPool` / `IObjectPool<T>`：对象池的抽象接口，提供取用、回收、注册、锁定等操作。
- `ObjectPool<T>`：泛型对象池实现，支持多引用（AllowMultiSpawn）、按需回收、到期释放与预热能力。
- `PooledObject<T>`：内部包装类型，记录引用计数、最后使用时间与锁定状态，保证对象状态准确。
- `ObjectPoolManager`：继承自 `AEFManager`，负责统一创建、查询、遍历与销毁各对象池，并在 `Update`/`Shutdown` 中驱动所有池的生命周期。

## 快速上手
```csharp
using EF.ObjectPool;

// 初始化管理器（通常由框架统一创建）
ObjectPoolManager poolManager = new ObjectPoolManager();

// 创建对象池
var bulletPool = poolManager.CreatePool(
    name: "BulletPool",
    factory: () => new Bullet(),
    options: new ObjectPoolOptions
    {
        AllowMultiSpawn = false,
        Capacity = 200,
        ExpireTime = 30f,
        AutoReleaseInterval = 5f
    },
    onSpawn: bullet => bullet.ResetState(),
    onRecycle: bullet => bullet.Deactivate(),
    onDestroy: bullet => bullet.Dispose());

// 取用与回收
Bullet bulletInstance = bulletPool.Spawn();
// ... 使用 bulletInstance ...
bulletPool.Recycle(bulletInstance);

// Unity Update 中驱动（框架会自动调用）
poolManager.Update(Time.deltaTime, Time.unscaledDeltaTime);
```

## 生命周期与维护
- 调用 `ObjectPoolManager.Update` 会驱动所有池执行自动释放与统计刷新，应由框架在每帧统一触发。
- 当游戏退出或模块卸载时，调用 `ObjectPoolManager.Shutdown` 以确保池内对象全部释放。
- `ReleaseAll()` 与 `ReleaseAll(int releaseCount)` 可在加载场景或内存紧张时主动触发批量回收。
- 对于需要保留的对象，可通过 `IObjectPool<T>.SetLocked(instance, true)` 暂时锁定，避免被自动释放。

## 配置说明
- `AllowMultiSpawn`：允许对象被多次取出（引用计数模式）。启用后需确保对象逻辑支持重复持有。
- `AutoRelease`：是否启用自动回收空闲对象，默认开启。
- `Capacity`：容量上限，超过后会优先释放最久未使用的空闲对象。
- `ExpireTime`：对象空闲超过该值（秒）后会在自动释放时被回收，设置为 0 可关闭过期判断。
- `AutoReleaseInterval`：自动释放检测的时间间隔（秒）。

## 调试建议
- 通过 `IObjectPool.TotalCount`、`AvailableCount`、`SpawnedCount` 可实时掌握池内资源分布。
- 在编辑器调试时，可使用 `ObjectPoolManager.GetAllPools()` 获取快照并输出到日志。
- 若出现对象无法回收的情况，优先检查是否启用了多引用模式或对象被锁定。
