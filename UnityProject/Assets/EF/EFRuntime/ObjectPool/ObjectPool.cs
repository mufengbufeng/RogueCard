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

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public Type ObjectType => typeof(T);

        /// <inheritdoc />
        public int TotalCount => _all.Count;

        /// <inheritdoc />
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

        /// <inheritdoc />
        public int SpawnedCount => TotalCount - AvailableCount;

        /// <inheritdoc />
        public bool AllowMultiSpawn => _options.AllowMultiSpawn;

        /// <inheritdoc />
        public ObjectPoolOptions Options => _options;

        /// <inheritdoc />
        public T Spawn()
        {
            if (_defaultFactory == null)
            {
                throw new InvalidOperationException($"对象池 {Name} 未提供默认创建逻辑");
            }

            return Spawn(_defaultFactory);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void Recycle(T instance)
        {
            if (!TryRecycleInternal(instance, true))
            {
                throw new InvalidOperationException($"对象 {instance} 不属于对象池 {Name} 或已被回收");
            }
        }

        /// <inheritdoc />
        public bool TryRecycle(T instance)
        {
            return TryRecycleInternal(instance, false);
        }

        /// <inheritdoc />
        public bool Contains(T instance)
        {
            return instance != null && _all.ContainsKey(instance);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void SetLocked(T instance, bool locked)
        {
            if (!_all.TryGetValue(instance, out PooledObject<T> entry))
            {
                throw new InvalidOperationException($"对象 {instance} 未注册到对象池 {Name}");
            }

            entry.Locked = locked;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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
