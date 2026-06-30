using System;
using System.Collections.Generic;

namespace EF.ObjectPool
{
    /// <summary>
    /// 泛型对象池实现。
    /// </summary>
    /// <typeparam name="T">池内对象类型。</typeparam>
    public sealed class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Dictionary<T, PooledObject<T>> _all = new();
        private readonly Queue<PooledObject<T>> _available = new();
        private readonly List<PooledObject<T>> _candidates = new();
        private readonly Func<T> _defaultFactory;
        private readonly Action<T> _onSpawn;
        private readonly Action<T> _onRecycle;
        private readonly Action<T> _onDestroy;
        private readonly ObjectPoolOptions _options;

        private float _time;
        private float _autoReleaseElapsed;

        /// <summary>
        /// 创建对象池。
        /// </summary>
        /// <param name="name">对象池名称。</param>
        /// <param name="factory">默认创建逻辑。</param>
        /// <param name="options">运行配置，为空时使用默认配置。</param>
        /// <param name="onSpawn">对象被取出时的回调。</param>
        /// <param name="onRecycle">对象回收时的回调。</param>
        /// <param name="onDestroy">对象最终释放时的回调。</param>
        public ObjectPool(
            string name,
            Func<T> factory,
            ObjectPoolOptions options = null,
            Action<T> onSpawn = null,
            Action<T> onRecycle = null,
            Action<T> onDestroy = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("对象池名称不能为空", nameof(name));
            }

            Name = name;
            _defaultFactory = factory;
            _onSpawn = onSpawn;
            _onRecycle = onRecycle;
            _onDestroy = onDestroy;
            _options = options?.Clone() ?? new ObjectPoolOptions();
        }

        /// <summary>
        /// 对象池名称，用于调试和区分同类型对象池。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 池内对象的运行时类型。
        /// </summary>
        public Type ObjectType => typeof(T);

        /// <summary>
        /// 池中已注册对象总数，包含使用中与空闲对象。
        /// </summary>
        public int TotalCount => _all.Count;

        /// <summary>
        /// 当前未被取用的对象数量。
        /// </summary>
        public int AvailableCount
        {
            get
            {
                int count = 0;
                foreach (PooledObject<T> entry in _all.Values)
                {
                    if (!entry.InUse)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 当前已取出且尚未完全回收的对象数量。
        /// </summary>
        public int SpawnedCount => TotalCount - AvailableCount;

        /// <summary>
        /// 是否允许同一对象被重复取出并通过引用计数回收。
        /// </summary>
        public bool AllowMultiSpawn => _options.AllowMultiSpawn;

        /// <summary>
        /// 对象池当前使用的运行配置。
        /// </summary>
        public ObjectPoolOptions Options => _options;

        /// <summary>
        /// 使用默认工厂从池中取出对象，空闲不足时创建新对象。
        /// </summary>
        /// <returns>取出的对象实例。</returns>
        public T Spawn()
        {
            if (_defaultFactory == null)
            {
                throw new InvalidOperationException($"对象池 {Name} 未提供默认创建逻辑");
            }

            return Spawn(_defaultFactory);
        }

        /// <summary>
        /// 从池中取出对象，空闲不足时使用指定工厂创建新对象。
        /// </summary>
        /// <param name="factory">用于创建新对象的工厂方法。</param>
        /// <returns>取出的对象实例。</returns>
        public T Spawn(Func<T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory), "创建对象的工厂方法不能为空");
            }

            PooledObject<T> entry = GetReusableEntry();
            if (entry == null)
            {
                T instance = factory();
                if (instance == null)
                {
                    throw new InvalidOperationException("对象工厂返回了空引用");
                }

                entry = new PooledObject<T>(instance, true, false, _time);
                _all.Add(instance, entry);
            }
            else
            {
                entry.MarkSpawned(_time);
            }

            _onSpawn?.Invoke(entry.Instance);
            EnsureCapacity();
            return entry.Instance;
        }

        /// <summary>
        /// 回收已注册对象，无法回收时抛出异常。
        /// </summary>
        /// <param name="instance">需要回收的对象实例。</param>
        public void Recycle(T instance)
        {
            if (!TryRecycleInternal(instance, true))
            {
                throw new InvalidOperationException($"对象 {instance} 不属于对象池 {Name} 或已被回收");
            }
        }

        /// <summary>
        /// 尝试回收已注册对象，失败时返回 false。
        /// </summary>
        /// <param name="instance">需要回收的对象实例。</param>
        /// <returns>回收成功返回 true，否则返回 false。</returns>
        public bool TryRecycle(T instance)
        {
            return TryRecycleInternal(instance, false);
        }

        /// <summary>
        /// 判断对象是否已注册到当前对象池。
        /// </summary>
        /// <param name="instance">待检查的对象实例。</param>
        /// <returns>对象已注册返回 true，否则返回 false。</returns>
        public bool Contains(T instance)
        {
            return instance != null && _all.ContainsKey(instance);
        }

        /// <summary>
        /// 向对象池注册外部对象，并指定初始取用与锁定状态。
        /// </summary>
        /// <param name="instance">需要注册的对象实例。</param>
        /// <param name="spawned">是否初始视为已取出。</param>
        /// <param name="locked">是否锁定，锁定对象不会被自动释放。</param>
        public void Register(T instance, bool spawned = false, bool locked = false)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance), "注册到对象池的对象不能为空");
            }

            if (_all.ContainsKey(instance))
            {
                throw new InvalidOperationException($"对象 {instance} 已重复注册到对象池 {Name}");
            }

            PooledObject<T> entry = new(instance, spawned, locked, _time);
            _all.Add(instance, entry);

            if (!spawned)
            {
                _available.Enqueue(entry);
            }
            else
            {
                _onSpawn?.Invoke(instance);
            }

            EnsureCapacity();
        }

        /// <summary>
        /// 设置已注册对象的锁定状态，锁定对象不会被自动释放。
        /// </summary>
        /// <param name="instance">目标对象实例。</param>
        /// <param name="locked">是否锁定该对象。</param>
        public void SetLocked(T instance, bool locked)
        {
            if (!_all.TryGetValue(instance, out PooledObject<T> entry))
            {
                throw new InvalidOperationException($"对象 {instance} 未注册到对象池 {Name}");
            }

            entry.Locked = locked;
        }

        /// <summary>
        /// 按条件释放空闲且未锁定的对象。
        /// </summary>
        /// <param name="predicate">释放筛选条件，返回 true 表示可以释放。</param>
        public void Release(Func<T, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate), "释放筛选条件不能为空");
            }

            _candidates.Clear();
            foreach (PooledObject<T> entry in _all.Values)
            {
                if (!entry.InUse && !entry.Locked && predicate(entry.Instance))
                {
                    _candidates.Add(entry);
                }
            }

            ReleaseCandidates();
        }

        /// <summary>
        /// 使用默认工厂预创建指定数量的空闲对象。
        /// </summary>
        /// <param name="count">需要预创建的对象数量。</param>
        public void Prewarm(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (_defaultFactory == null)
            {
                throw new InvalidOperationException($"对象池 {Name} 未提供默认创建逻辑，无法预热");
            }

            for (int i = 0; i < count; i++)
            {
                T instance = _defaultFactory();
                if (instance == null)
                {
                    throw new InvalidOperationException("对象工厂返回了空引用");
                }

                PooledObject<T> entry = new(instance, false, false, _time);
                _all.Add(instance, entry);
                _available.Enqueue(entry);
            }

            EnsureCapacity();
        }

        /// <summary>
        /// 推进对象池时间，并按配置自动释放过期的空闲对象。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒），当前实现未使用。</param>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            _time += elapseSeconds;
            if (!_options.AutoRelease || _all.Count == 0)
            {
                return;
            }

            _autoReleaseElapsed += elapseSeconds;
            if (_autoReleaseElapsed < _options.AutoReleaseInterval)
            {
                return;
            }

            _autoReleaseElapsed = 0f;

            if (_options.ExpireTime <= 0f)
            {
                return;
            }

            _candidates.Clear();
            foreach (PooledObject<T> entry in _all.Values)
            {
                if (!entry.InUse && !entry.Locked && _time - entry.LastUseTime >= _options.ExpireTime)
                {
                    _candidates.Add(entry);
                }
            }

            ReleaseCandidates();
        }

        /// <summary>
        /// 按最近使用时间优先释放指定数量的空闲对象。
        /// </summary>
        /// <param name="releaseCount">最多需要释放的对象数量。</param>
        public void Release(int releaseCount)
        {
            if (releaseCount <= 0)
            {
                return;
            }

            _candidates.Clear();
            foreach (PooledObject<T> entry in _all.Values)
            {
                if (!entry.InUse && !entry.Locked)
                {
                    _candidates.Add(entry);
                }
            }

            if (_candidates.Count == 0)
            {
                return;
            }

            _candidates.Sort((left, right) => left.LastUseTime.CompareTo(right.LastUseTime));

            if (releaseCount < _candidates.Count)
            {
                _candidates.RemoveRange(releaseCount, _candidates.Count - releaseCount);
            }

            ReleaseCandidates();
        }

        /// <summary>
        /// 释放所有空闲且未锁定的对象。
        /// </summary>
        public void ReleaseAll()
        {
            _candidates.Clear();
            foreach (PooledObject<T> entry in _all.Values)
            {
                if (!entry.InUse && !entry.Locked)
                {
                    _candidates.Add(entry);
                }
            }

            ReleaseCandidates();
        }

        /// <summary>
        /// 清空对象池并销毁全部对象，包含正在使用的对象。
        /// </summary>
        public void Clear()
        {
            foreach (PooledObject<T> entry in _all.Values)
            {
                _onDestroy?.Invoke(entry.Instance);
            }

            _all.Clear();
            _available.Clear();
            _candidates.Clear();
        }

        /// <summary>
        /// 关闭对象池并清空全部对象。
        /// </summary>
        public void Shutdown()
        {
            Clear();
        }

        private bool TryRecycleInternal(T instance, bool throwOnFail)
        {
            if (instance == null)
            {
                if (throwOnFail)
                {
                    throw new ArgumentNullException(nameof(instance), "回收的对象不能为空");
                }

                return false;
            }

            if (!_all.TryGetValue(instance, out PooledObject<T> entry))
            {
                if (throwOnFail)
                {
                    throw new InvalidOperationException($"对象 {instance} 未注册到对象池 {Name}");
                }

                return false;
            }

            if (entry.ReferenceCount == 0)
            {
                if (throwOnFail)
                {
                    throw new InvalidOperationException($"对象 {instance} 已经被回收，无法重复回收");
                }

                return false;
            }

            entry.MarkRecycled(_time);
            _onRecycle?.Invoke(instance);
            if (!entry.InUse)
            {
                _available.Enqueue(entry);
            }

            return true;
        }

        private PooledObject<T> GetReusableEntry()
        {
            while (_available.Count > 0)
            {
                PooledObject<T> entry = _available.Dequeue();
                if (!_all.ContainsKey(entry.Instance))
                {
                    continue;
                }

                if (!AllowMultiSpawn && entry.InUse)
                {
                    continue;
                }

                return entry;
            }

            if (!AllowMultiSpawn)
            {
                foreach (PooledObject<T> entry in _all.Values)
                {
                    if (!entry.InUse)
                    {
                        return entry;
                    }
                }
            }
            else
            {
                foreach (PooledObject<T> entry in _all.Values)
                {
                    return entry;
                }
            }

            return null;
        }

        private void EnsureCapacity()
        {
            if (_options.Capacity == int.MaxValue || _all.Count <= _options.Capacity)
            {
                return;
            }

            int needRelease = _all.Count - _options.Capacity;
            Release(needRelease);
        }

        private void ReleaseCandidates()
        {
            if (_candidates.Count == 0)
            {
                return;
            }

            foreach (PooledObject<T> entry in _candidates)
            {
                if (!_all.Remove(entry.Instance))
                {
                    continue;
                }

                _onDestroy?.Invoke(entry.Instance);
            }

            _candidates.Clear();
            CleanAvailableQueue();
        }

        private void CleanAvailableQueue()
        {
            if (_available.Count == 0)
            {
                return;
            }

            PooledObject<T>[] snapshot = _available.ToArray();
            _available.Clear();
            foreach (PooledObject<T> entry in snapshot)
            {
                if (_all.TryGetValue(entry.Instance, out PooledObject<T> current) && !current.InUse)
                {
                    _available.Enqueue(current);
                }
            }
        }
    }
}
