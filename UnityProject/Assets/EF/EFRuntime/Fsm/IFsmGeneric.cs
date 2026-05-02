using System;

namespace EF.Fsm
{
    /// <summary>
    /// 泛型有限状态机接口，提供对宿主类型友好的操作。
    /// </summary>
    /// <typeparam name="TOwner">宿主对象类型。</typeparam>
    public interface IFsm<TOwner> : IFsm where TOwner : class
    {
        /// <summary>
        /// 强类型宿主对象。
        /// </summary>
        new TOwner Owner { get; }

        /// <summary>
        /// 当前激活的状态实例，未启动时为 <c>null</c>。
        /// </summary>
        FsmState<TOwner> CurrentState { get; }

        /// <summary>
        /// 判断是否存在指定类型的状态。
        /// </summary>
        bool HasState(Type stateType);

        /// <summary>
        /// 判断是否存在指定类型的状态。
        /// </summary>
        bool HasState<TState>() where TState : FsmState<TOwner>;

        /// <summary>
        /// 获取指定类型的状态。
        /// </summary>
        FsmState<TOwner> GetState(Type stateType);

        /// <summary>
        /// 获取指定类型的状态。
        /// </summary>
        TState GetState<TState>() where TState : FsmState<TOwner>;

        /// <summary>
        /// 启动状态机并切换到指定状态。
        /// </summary>
        void Start(Type stateType);

        /// <summary>
        /// 启动状态机并切换到指定状态。
        /// </summary>
        void Start<TState>() where TState : FsmState<TOwner>;

        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        void ChangeState(Type stateType);

        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        void ChangeState<TState>() where TState : FsmState<TOwner>;

        /// <summary>
        /// 主动停止状态机运行并回收所有状态资源。
        /// </summary>
        void Stop();

        /// <summary>
        /// 设置共享数据。
        /// </summary>
        void SetData<TData>(string name, TData data);

        /// <summary>
        /// 尝试获取共享数据。
        /// </summary>
        bool TryGetData<TData>(string name, out TData data);

        /// <summary>
        /// 获取共享数据，若不存在则抛出异常。
        /// </summary>
        TData GetData<TData>(string name);

        /// <summary>
        /// 移除指定共享数据。
        /// </summary>
        bool RemoveData(string name);

        /// <summary>
        /// 清空所有共享数据。
        /// </summary>
        void ClearData();
    }
}
