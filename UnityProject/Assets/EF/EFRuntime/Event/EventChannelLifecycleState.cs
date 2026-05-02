namespace EF.Event
{
    /// <summary>
    /// 事件 Channel 的生命周期状态。
    /// </summary>
    public enum EventChannelLifecycleState
    {
        /// <summary>
        /// Channel 已声明但尚未创建实例。
        /// </summary>
        Uninitialized = 0,

        /// <summary>
        /// Channel 已创建，但当前没有待分发异步事件。
        /// </summary>
        Idle = 1,

        /// <summary>
        /// Channel 已创建，且存在待分发异步事件。
        /// </summary>
        Active = 2,
    }
}