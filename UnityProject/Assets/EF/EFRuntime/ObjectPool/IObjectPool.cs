using System;

namespace EF.ObjectPool
{
    /// <summary>
    /// 对象池的非泛型抽象接口。
    /// </summary>
    public interface IObjectPool
    {
        /// <summary>
        /// 对象池名称，便于调试和定位。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 池内对象的类型。
        /// </summary>
        Type ObjectType { get; }

        /// <summary>
        /// 当前池内对象总数（包含使用中与空闲）。
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// 当前空闲对象数量。
        /// </summary>
        int AvailableCount { get; }

        /// <summary>
        /// 当前正在使用的对象数量。
        /// </summary>
        int SpawnedCount { get; }

        /// <summary>
        /// 是否允许对象被多次取出。
        /// </summary>
        bool AllowMultiSpawn { get; }

        /// <summary>
        /// 对象池运行配置的只读视图。
        /// </summary>
        ObjectPoolOptions Options { get; }

        /// <summary>
        /// 在 Unity Update 中驱动对象池逻辑。
        /// </summary>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 主动释放若干个空闲对象。
        /// </summary>
        /// <param name="releaseCount">需要释放的对象数量。</param>
        void Release(int releaseCount);

        /// <summary>
        /// 释放所有空闲对象。
        /// </summary>
        void ReleaseAll();

        /// <summary>
        /// 清空对象池，释放全部对象（包含使用中对象）。
        /// </summary>
        void Clear();

        /// <summary>
        /// 关闭对象池。
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 泛型对象池接口，提供类型安全的取用与回收操作。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    public interface IObjectPool<T> : IObjectPool where T : class
    {
        /// <summary>
        /// 从池中取出一个对象，如无空闲对象则自动创建。
        /// </summary>
        T Spawn();

        /// <summary>
        /// 使用自定义工厂取出一个对象，适用于临时替换默认创建逻辑。
        /// </summary>
        /// <param name="factory">自定义创建委托。</param>
        T Spawn(Func<T> factory);

        /// <summary>
        /// 回收一个对象。
        /// </summary>
        /// <param name="instance">需要回收的对象。</param>
        void Recycle(T instance);

        /// <summary>
        /// 尝试回收对象，不会抛出异常而是返回是否成功。
        /// </summary>
        bool TryRecycle(T instance);

        /// <summary>
        /// 判断对象是否已被注册到池中。
        /// </summary>
        bool Contains(T instance);

        /// <summary>
        /// 向对象池注册一个对象，可选设定其初始状态。
        /// </summary>
        /// <param name="instance">需要注册的对象。</param>
        /// <param name="spawned">是否视为正在使用。</param>
        /// <param name="locked">是否锁定，锁定对象不会被自动释放。</param>
        void Register(T instance, bool spawned = false, bool locked = false);

        /// <summary>
        /// 设置对象的锁定状态。
        /// </summary>
        /// <param name="instance">目标对象。</param>
        /// <param name="locked">锁定状态。</param>
        void SetLocked(T instance, bool locked);

        /// <summary>
        /// 根据筛选规则释放对象。
        /// </summary>
        /// <param name="predicate">返回 true 表示该对象可以被释放。</param>
        void Release(Func<T, bool> predicate);

        /// <summary>
        /// 预创建指定数量的对象。
        /// </summary>
        /// <param name="count">需要预热的数量。</param>
        void Prewarm(int count);
    }
}
