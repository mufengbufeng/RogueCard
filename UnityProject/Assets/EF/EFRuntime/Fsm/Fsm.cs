using System;
using System.Collections.Generic;

namespace EF.Fsm
{
    /// <summary>
    /// 有限状态机实现，负责管理状态生命周期与共享数据。
    /// </summary>
    /// <typeparam name="TOwner">宿主类型。</typeparam>
    internal sealed class Fsm<TOwner> : IFsm<TOwner>, IFsmInternal where TOwner : class
    {
        private readonly string _name;
        private readonly TOwner _owner;
        private readonly Dictionary<Type, FsmState<TOwner>> _states;
        private readonly FsmDataCollection _dataCollection = new();

        private bool _isRunning;
        private bool _isDestroyed;
        private FsmState<TOwner> _currentState;
        private float _currentStateTime;

        /// <summary>
        /// 初始化状态机实例。
        /// </summary>
        public Fsm(string name, TOwner owner, IEnumerable<FsmState<TOwner>> states)
        {
            _name = name ?? string.Empty;
            _owner = owner ?? throw new ArgumentNullException(nameof(owner), "状态机宿主不能为空。");

            if (states == null)
            {
                throw new ArgumentNullException(nameof(states), "状态集合不能为空。");
            }

            _states = new Dictionary<Type, FsmState<TOwner>>();

            foreach (FsmState<TOwner> state in states)
            {
                if (state == null)
                {
                    throw new ArgumentException("状态集合中存在空引用。", nameof(states));
                }

                Type stateType = state.GetType();
                if (_states.ContainsKey(stateType))
                {
                    throw new InvalidOperationException($"状态 {stateType.FullName} 已经注册，不能重复添加。");
                }

                _states.Add(stateType, state);
            }

            foreach (FsmState<TOwner> state in _states.Values)
            {
                state.OnInit(this);
            }
        }

        public string Name => _name;

        public Type OwnerType => typeof(TOwner);

        public object Owner => _owner;

        TOwner IFsm<TOwner>.Owner => _owner;

        public bool IsRunning => _isRunning;

        public bool IsDestroyed => _isDestroyed;

        public int StateCount => _states.Count;

        public FsmState<TOwner> CurrentState => _currentState;

        public string CurrentStateName => _currentState?.Name;

        public float CurrentStateTime => _currentStateTime;

        public bool HasState(Type stateType)
        {
            if (stateType == null)
            {
                throw new ArgumentNullException(nameof(stateType), "状态类型不能为空。");
            }

            return _states.ContainsKey(stateType);
        }

        public bool HasState<TState>() where TState : FsmState<TOwner>
        {
            return HasState(typeof(TState));
        }

        public FsmState<TOwner> GetState(Type stateType)
        {
            if (stateType == null)
            {
                throw new ArgumentNullException(nameof(stateType), "状态类型不能为空。");
            }

            if (!_states.TryGetValue(stateType, out FsmState<TOwner> state))
            {
                throw new KeyNotFoundException($"状态机 {Name} 中未注册状态 {stateType.FullName}。");
            }

            return state;
        }

        public TState GetState<TState>() where TState : FsmState<TOwner>
        {
            return (TState)GetState(typeof(TState));
        }

        public void Start(Type stateType)
        {
            EnsureCanStart();

            _currentState = GetState(stateType);
            _currentStateTime = 0f;
            _isRunning = true;

            _currentState.OnEnter(this);
        }

        public void Start<TState>() where TState : FsmState<TOwner>
        {
            Start(typeof(TState));
        }

        public void ChangeState(Type stateType)
        {
            EnsureCanChangeState();

            FsmState<TOwner> nextState = GetState(stateType);
            if (ReferenceEquals(nextState, _currentState))
            {
                return;
            }

            _currentState.OnLeave(this, false);
            _currentState = nextState;
            _currentStateTime = 0f;
            _currentState.OnEnter(this);
        }

        public void ChangeState<TState>() where TState : FsmState<TOwner>
        {
            ChangeState(typeof(TState));
        }

        public void Stop()
        {
            if (!_isRunning || _isDestroyed)
            {
                return;
            }

            _currentState?.OnLeave(this, true);
            _currentState = null;
            _currentStateTime = 0f;
            _isRunning = false;
        }

        public void SetData<TData>(string name, TData data)
        {
            EnsureNotDestroyed();
            _dataCollection.SetData(name, data);
        }

        public bool TryGetData<TData>(string name, out TData data)
        {
            EnsureNotDestroyed();
            return _dataCollection.TryGetData(name, out data);
        }

        public TData GetData<TData>(string name)
        {
            EnsureNotDestroyed();
            return _dataCollection.GetData<TData>(name);
        }

        public bool RemoveData(string name)
        {
            EnsureNotDestroyed();
            return _dataCollection.RemoveData(name);
        }

        public void ClearData()
        {
            EnsureNotDestroyed();
            _dataCollection.Clear();
        }

        /// <summary>
        /// 每帧更新，由管理器驱动。
        /// </summary>
        public void InternalUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (!_isRunning || _isDestroyed || _currentState == null)
            {
                return;
            }

            _currentStateTime += elapseSeconds;
            _currentState.OnUpdate(this, elapseSeconds, realElapseSeconds);
        }

        /// <summary>
        /// 彻底销毁状态机。
        /// </summary>
        public void InternalShutdown()
        {
            if (_isDestroyed)
            {
                return;
            }

            Stop();
            _isDestroyed = true;

            foreach (FsmState<TOwner> state in _states.Values)
            {
                state.OnDestroy(this);
            }

            _states.Clear();
            _dataCollection.Clear();
        }

        private void EnsureNotDestroyed()
        {
            if (_isDestroyed)
            {
                throw new InvalidOperationException($"状态机 {Name} 已经被销毁。");
            }
        }

        private void EnsureCanStart()
        {
            EnsureNotDestroyed();
            if (_isRunning)
            {
                throw new InvalidOperationException($"状态机 {Name} 已经启动。");
            }

            if (_states.Count == 0)
            {
                throw new InvalidOperationException($"状态机 {Name} 尚未注册任何状态。");
            }
        }

        private void EnsureCanChangeState()
        {
            EnsureNotDestroyed();
            if (!_isRunning || _currentState == null)
            {
                throw new InvalidOperationException($"状态机 {Name} 尚未启动。");
            }
        }
    }
}
