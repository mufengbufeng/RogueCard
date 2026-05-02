using System;
using System.Collections.Generic;
using EF.Common;

namespace EF.ObjectPool
{
    /// <summary>
    /// 定义对象池管理器提供的能力。
    /// </summary>
    public interface IObjectPoolManager : IEFManager
    {
        /// <summary>
        /// 当前已创建的对象池数量。
        /// </summary>
        int PoolCount { get; }

        /// <summary>
        /// 创建一个新的对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <param name="factory">默认创建逻辑。</param>
        /// <param name="options">对象池配置。</param>
        /// <param name="onSpawn">对象取出回调。</param>
        /// <param name="onRecycle">对象回收回调。</param>
        /// <param name="onDestroy">对象销毁回调。</param>
        IObjectPool<T> CreatePool<T>(
            string name,
            Func<T> factory,
            ObjectPoolOptions options = null,
            Action<T> onSpawn = null,
            Action<T> onRecycle = null,
            Action<T> onDestroy = null) where T : class;

        /// <summary>
        /// 获取对象池，如未找到将抛出异常。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        IObjectPool<T> GetPool<T>(string name) where T : class;

        /// <summary>
        /// 尝试获取对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <param name="pool">输出对象池引用。</param>
        bool TryGetPool<T>(string name, out IObjectPool<T> pool) where T : class;

        /// <summary>
        /// 判断指定对象池是否存在。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        bool HasPool<T>(string name) where T : class;

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        bool DestroyPool<T>(string name) where T : class;

        /// <summary>
        /// 手动驱动所有对象池执行释放。
        /// </summary>
        /// <param name="releaseCount">每个对象池需要释放的数量。</param>
        void ReleaseAll(int releaseCount);

        /// <summary>
        /// 释放所有空闲对象。
        /// </summary>
        void ReleaseAll();

        /// <summary>
        /// 遍历所有对象池。
        /// </summary>
        IReadOnlyList<IObjectPool> GetAllPools();
    }
}
