using System;

namespace EF.Fsm
{
    /// <summary>
    /// 有限状态机的公共只读视图。
    /// </summary>
    public interface IFsm
    {
        /// <summary>
        /// 状态机名称，未指定时为空字符串。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 状态机宿主对象的类型。
        /// </summary>
        Type OwnerType { get; }

        /// <summary>
        /// 状态机的宿主对象引用。
        /// </summary>
        object Owner { get; }

        /// <summary>
        /// 当前是否已经启动。
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 状态机是否已经被销毁。
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// 已注册的状态数量。
        /// </summary>
        int StateCount { get; }

        /// <summary>
        /// 当前状态名称，若尚未启动返回 <c>null</c>。
        /// </summary>
        string CurrentStateName { get; }

        /// <summary>
        /// 当前状态已经持续的时间（逻辑时间）。
        /// </summary>
        float CurrentStateTime { get; }
    }
}
