using System;
using System.Collections.Generic;
using EF.Common;

namespace EF.Fsm
{
    /// <summary>
    /// FSM 管理器接口，负责生命周期托管与实例查询。
    /// </summary>
    public interface IFsmManager : IEFManager
    {
        /// <summary>
        /// 当前活跃状态机数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 判断是否存在指定宿主的状态机。
        /// </summary>
        bool HasFsm(Type ownerType, string name = "");

        /// <summary>
        /// 判断是否存在指定宿主的状态机。
        /// </summary>
        bool HasFsm<TOwner>(string name = "") where TOwner : class;

        /// <summary>
        /// 获取状态机。
        /// </summary>
        IFsm GetFsm(Type ownerType, string name = "");

        /// <summary>
        /// 获取状态机。
        /// </summary>
        IFsm<TOwner> GetFsm<TOwner>(string name = "") where TOwner : class;

        /// <summary>
        /// 创建状态机。
        /// </summary>
        IFsm<TOwner> CreateFsm<TOwner>(string name, TOwner owner, IEnumerable<FsmState<TOwner>> states) where TOwner : class;

        /// <summary>
        /// 创建状态机。
        /// </summary>
        IFsm<TOwner> CreateFsm<TOwner>(string name, TOwner owner, params FsmState<TOwner>[] states) where TOwner : class;

        /// <summary>
        /// 销毁指定状态机。
        /// </summary>
        bool DestroyFsm(Type ownerType, string name = "");

        /// <summary>
        /// 销毁指定状态机。
        /// </summary>
        bool DestroyFsm<TOwner>(string name = "") where TOwner : class;

        /// <summary>
        /// 销毁指定状态机实例。
        /// </summary>
        bool DestroyFsm(IFsm fsm);

        /// <summary>
        /// 将当前全部状态机输出到目标列表，避免 GC。
        /// </summary>
        void GetAllFsm(List<IFsm> results);
    }
}
