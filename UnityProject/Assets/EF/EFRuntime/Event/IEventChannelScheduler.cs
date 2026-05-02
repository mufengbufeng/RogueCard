namespace EF.Event
{
    /// <summary>
    /// 事件 Channel 的异步调度协作接口。
    /// 由 EventHub 实现，用于接收 Channel 的活跃状态变化通知。
    /// </summary>
    public interface IEventChannelScheduler
    {
        /// <summary>
        /// 当 Channel 首次进入待分发状态时调用。
        /// </summary>
        /// <param name="channel">变为活跃的 Channel。</param>
        void OnChannelActivated(IEventChannel channel);

        /// <summary>
        /// 当 Channel 的待分发队列被完全清空时调用。
        /// </summary>
        /// <param name="channel">恢复空闲的 Channel。</param>
        void OnChannelBecameIdle(IEventChannel channel);
    }
}