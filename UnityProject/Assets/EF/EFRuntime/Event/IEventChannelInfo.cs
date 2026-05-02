namespace EF.Event
{
    /// <summary>
    /// 事件 Channel 的调试信息接口，供 Editor 可视化面板读取运行时状态。
    /// </summary>
    public interface IEventChannelInfo
    {
        /// <summary>
        /// 事件类型名称。
        /// </summary>
        string EventName { get; }

        /// <summary>
        /// 当前注册的 handler 数量。
        /// </summary>
        int HandlerCount { get; }

        /// <summary>
        /// 待分发队列中的事件数量。
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// 当前事件 Channel 的生命周期状态。
        /// </summary>
        EventChannelLifecycleState State { get; }

        /// <summary>
        /// 获取所有已注册 handler 的目标方法名称（格式：ClassName.MethodName）。
        /// </summary>
        string[] GetHandlerNames();
    }
}
