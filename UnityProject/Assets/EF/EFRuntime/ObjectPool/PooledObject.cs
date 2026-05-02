using System;

namespace EF.ObjectPool
{
    /// <summary>
    /// 对象池内部使用的包装类型，用于记录状态。
    /// </summary>
    /// <typeparam name="T">被包装的对象类型。</typeparam>
    internal sealed class PooledObject<T> where T : class
    {
        public PooledObject(T instance, bool spawned, bool locked, float timestamp)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance), "注册到对象池的对象不能为空");
            Locked = locked;
            LastUseTime = timestamp;
            SpawnCount = spawned ? 1 : 0;
            ReferenceCount = spawned ? 1 : 0;
        }

        /// <summary>
        /// 实际的对象实例。
        /// </summary>
        public T Instance { get; }

        /// <summary>
        /// 对象当前是否正在被使用。
        /// </summary>
        public bool InUse => ReferenceCount > 0;

        /// <summary>
        /// 是否锁定，锁定的对象不会被自动释放。
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// 对象被取出的次数，可用于统计。
        /// </summary>
        public int SpawnCount { get; private set; }

        /// <summary>
        /// 当前引用计数，允许多次取出时用于记录。
        /// </summary>
        public int ReferenceCount { get; private set; }

        /// <summary>
        /// 上一次使用的绝对时间。
        /// </summary>
        public float LastUseTime { get; private set; }

        /// <summary>
        /// 标记为已取出。
        /// </summary>
        /// <param name="timestamp">当前时间。</param>
        public void MarkSpawned(float timestamp)
        {
            SpawnCount++;
            ReferenceCount++;
            LastUseTime = timestamp;
        }

        /// <summary>
        /// 标记为已回收。
        /// </summary>
        /// <param name="timestamp">当前时间。</param>
        public void MarkRecycled(float timestamp)
        {
            if (ReferenceCount > 0)
            {
                ReferenceCount--;
            }

            LastUseTime = timestamp;
        }
    }
}
