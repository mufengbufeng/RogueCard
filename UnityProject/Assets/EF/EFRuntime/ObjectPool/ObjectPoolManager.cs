using System;
using System.Collections.Generic;
using EF.Common;

namespace EF.ObjectPool
{
    /// <summary>
    /// 对象池管理器，负责统一创建、更新与销毁各类对象池。
    /// </summary>
    public sealed class ObjectPoolManager : AEFManager, IObjectPoolManager
    {
        private readonly Dictionary<string, IObjectPool> _pools = new();

        /// <summary>
        /// 当前已创建的对象池数量。
        /// </summary>
        public int PoolCount => _pools.Count;

        /// <summary>
        /// 创建一个新的对象池。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="name">对象池名称，同类型可以创建多个不同名称的池。</param>
        /// <param name="factory">默认创建逻辑。</param>
        /// <param name="options">对象池配置。</param>
        /// <param name="onSpawn">对象取出回调。</param>
        /// <param name="onRecycle">对象回收回调。</param>
        /// <param name="onDestroy">对象销毁回调。</param>
        public IObjectPool<T> CreatePool<T>(
            string name,
            Func<T> factory,
            ObjectPoolOptions options = null,
            Action<T> onSpawn = null,
            Action<T> onRecycle = null,
            Action<T> onDestroy = null) where T : class
        {
            string actualName = string.IsNullOrWhiteSpace(name) ? typeof(T).FullName : name;
            string key = BuildPoolKey(typeof(T), actualName);
            if (_pools.ContainsKey(key))
            {
                throw new InvalidOperationException($"对象池 {key} 已存在，禁止重复创建");
            }

            IObjectPool<T> pool = new ObjectPool<T>(actualName, factory, options, onSpawn, onRecycle, onDestroy);
            _pools.Add(key, pool);
            return pool;
        }

        /// <summary>
        /// 获取对象池。
        /// </summary>
        public IObjectPool<T> GetPool<T>(string name) where T : class
        {
            string actualName = string.IsNullOrWhiteSpace(name) ? typeof(T).FullName : name;
            string key = BuildPoolKey(typeof(T), actualName);
            if (!_pools.TryGetValue(key, out IObjectPool pool))
            {
                throw new KeyNotFoundException($"未找到类型 {typeof(T).FullName} 的对象池 {actualName}");
            }

            if (pool is not IObjectPool<T> typedPool)
            {
                throw new InvalidOperationException($"对象池 {actualName} 的类型与请求不匹配");
            }

            return typedPool;
        }

        /// <summary>
        /// 尝试获取对象池。
        /// </summary>
        public bool TryGetPool<T>(string name, out IObjectPool<T> pool) where T : class
        {
            string actualName = string.IsNullOrWhiteSpace(name) ? typeof(T).FullName : name;
            string key = BuildPoolKey(typeof(T), actualName);
            if (_pools.TryGetValue(key, out IObjectPool rawPool) && rawPool is IObjectPool<T> typedPool)
            {
                pool = typedPool;
                return true;
            }

            pool = null;
            return false;
        }

        /// <summary>
        /// 判断指定对象池是否存在。
        /// </summary>
        public bool HasPool<T>(string name) where T : class
        {
            string actualName = string.IsNullOrWhiteSpace(name) ? typeof(T).FullName : name;
            return _pools.ContainsKey(BuildPoolKey(typeof(T), actualName));
        }

        /// <summary>
        /// 销毁对象池。
        /// </summary>
        public bool DestroyPool<T>(string name) where T : class
        {
            string actualName = string.IsNullOrWhiteSpace(name) ? typeof(T).FullName : name;
            string key = BuildPoolKey(typeof(T), actualName);
            if (!_pools.TryGetValue(key, out IObjectPool pool))
            {
                return false;
            }

            pool.Shutdown();
            _pools.Remove(key);
            return true;
        }

        /// <summary>
        /// 手动驱动所有对象池执行释放。
        /// </summary>
        /// <param name="releaseCount">每个对象池需要释放的数量。</param>
        public void ReleaseAll(int releaseCount)
        {
            foreach (IObjectPool pool in _pools.Values)
            {
                pool.Release(releaseCount);
            }
        }

        /// <summary>
        /// 释放所有空闲对象。
        /// </summary>
        public void ReleaseAll()
        {
            foreach (IObjectPool pool in _pools.Values)
            {
                pool.ReleaseAll();
            }
        }

        /// <summary>
        /// 遍历所有对象池。
        /// </summary>
        public IReadOnlyList<IObjectPool> GetAllPools()
        {
            return new List<IObjectPool>(_pools.Values);
        }

        /// <summary>
        /// Unity Update 驱动。
        /// </summary>
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (IObjectPool pool in _pools.Values)
            {
                pool.Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 关闭并释放所有对象池。
        /// </summary>
        public override void Shutdown()
        {
            foreach (IObjectPool pool in _pools.Values)
            {
                pool.Shutdown();
            }

            _pools.Clear();
        }

        private static string BuildPoolKey(Type objectType, string name)
        {
            string poolName = string.IsNullOrWhiteSpace(name) ? objectType.FullName : name;
            return $"{objectType.AssemblyQualifiedName}:{poolName}";
        }
    }
}
