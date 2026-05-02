using System;
using System.Collections.Generic;
using EF.Common;

namespace EF.Fsm
{
    /// <summary>
    /// FSM 管理器，实现创建、查询与生命周期托管。
    /// </summary>
    public sealed class FsmManager : AEFManager, IFsmManager
    {
        private readonly Dictionary<FsmKey, IFsmInternal> _fsms = new();
        private readonly List<IFsmInternal> _updateBuffer = new();

        public int Count => _fsms.Count;

        public bool HasFsm(Type ownerType, string name = "")
        {
            FsmKey key = CreateKey(ownerType, name);
            return _fsms.ContainsKey(key);
        }

        public bool HasFsm<TOwner>(string name = "") where TOwner : class
        {
            return HasFsm(typeof(TOwner), name);
        }

        public IFsm GetFsm(Type ownerType, string name = "")
        {
            FsmKey key = CreateKey(ownerType, name);
            return _fsms.TryGetValue(key, out IFsmInternal fsm) ? fsm : null;
        }

        public IFsm<TOwner> GetFsm<TOwner>(string name = "") where TOwner : class
        {
            return (IFsm<TOwner>)GetFsm(typeof(TOwner), name);
        }

        public IFsm<TOwner> CreateFsm<TOwner>(string name, TOwner owner, IEnumerable<FsmState<TOwner>> states) where TOwner : class
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner), "状态机宿主不能为空。");
            }

            if (states == null)
            {
                throw new ArgumentNullException(nameof(states), "状态集合不能为空。");
            }

            List<FsmState<TOwner>> stateList = states as List<FsmState<TOwner>> ?? new List<FsmState<TOwner>>(states);
            if (stateList.Count == 0)
            {
                throw new ArgumentException("状态集合不能为空。", nameof(states));
            }

            FsmKey key = CreateKey(typeof(TOwner), name);
            if (_fsms.ContainsKey(key))
            {
                throw new InvalidOperationException($"已存在宿主 {typeof(TOwner).FullName}，名称 {key.Name} 的状态机。");
            }

            Fsm<TOwner> fsm = new Fsm<TOwner>(key.Name, owner, stateList);
            _fsms.Add(key, fsm);
            return fsm;
        }

        public IFsm<TOwner> CreateFsm<TOwner>(string name, TOwner owner, params FsmState<TOwner>[] states) where TOwner : class
        {
            return CreateFsm(name, owner, (IEnumerable<FsmState<TOwner>>)states);
        }

        public bool DestroyFsm(Type ownerType, string name = "")
        {
            FsmKey key = CreateKey(ownerType, name);
            if (!_fsms.TryGetValue(key, out IFsmInternal fsm))
            {
                return false;
            }

            fsm.InternalShutdown();
            _fsms.Remove(key);
            return true;
        }

        public bool DestroyFsm<TOwner>(string name = "") where TOwner : class
        {
            return DestroyFsm(typeof(TOwner), name);
        }

        public bool DestroyFsm(IFsm fsm)
        {
            if (fsm == null)
            {
                return false;
            }

            if (fsm is not IFsmInternal internalFsm)
            {
                return false;
            }

            FsmKey key = CreateKey(fsm.OwnerType, fsm.Name);
            if (!_fsms.TryGetValue(key, out IFsmInternal stored) || !ReferenceEquals(stored, internalFsm))
            {
                return false;
            }

            internalFsm.InternalShutdown();
            _fsms.Remove(key);
            return true;
        }

        public void GetAllFsm(List<IFsm> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results), "结果列表不能为空。");
            }

            results.Clear();
            foreach (IFsmInternal fsm in _fsms.Values)
            {
                results.Add(fsm);
            }
        }

        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            _updateBuffer.Clear();
            _updateBuffer.AddRange(_fsms.Values);

            foreach (IFsmInternal fsm in _updateBuffer)
            {
                fsm.InternalUpdate(elapseSeconds, realElapseSeconds);
            }
        }

        public override void Shutdown()
        {
            _updateBuffer.Clear();
            _updateBuffer.AddRange(_fsms.Values);

            foreach (IFsmInternal fsm in _updateBuffer)
            {
                fsm.InternalShutdown();
            }

            _fsms.Clear();
            _updateBuffer.Clear();
        }

        private static FsmKey CreateKey(Type ownerType, string name)
        {
            if (ownerType == null)
            {
                throw new ArgumentNullException(nameof(ownerType), "宿主类型不能为空。");
            }

            string normalizedName = name ?? string.Empty;
            return new FsmKey(ownerType, normalizedName);
        }

        private readonly struct FsmKey : IEquatable<FsmKey>
        {
            public FsmKey(Type ownerType, string name)
            {
                OwnerType = ownerType;
                Name = name;
            }

            public Type OwnerType { get; }

            public string Name { get; }

            public bool Equals(FsmKey other)
            {
                return OwnerType == other.OwnerType && string.Equals(Name, other.Name, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is FsmKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(OwnerType, Name);
            }
        }
    }
}
