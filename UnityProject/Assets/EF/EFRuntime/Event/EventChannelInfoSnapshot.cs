using System;

namespace EF.Event
{
    /// <summary>
    /// 事件 Channel 的只读调试快照，用于表示未创建或临时聚合的状态信息。
    /// </summary>
    public sealed class EventChannelInfoSnapshot : IEventChannelInfo
    {
        private readonly string[] _handlerNames;

        /// <summary>
        /// 创建一个新的调试快照。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="handlerCount">订阅者数量。</param>
        /// <param name="pendingCount">待分发事件数量。</param>
        /// <param name="state">生命周期状态。</param>
        /// <param name="handlerNames">订阅者名称列表。</param>
        public EventChannelInfoSnapshot(
            string eventName,
            int handlerCount,
            int pendingCount,
            EventChannelLifecycleState state,
            string[] handlerNames = null)
        {
            EventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
            HandlerCount = handlerCount;
            PendingCount = pendingCount;
            State = state;
            _handlerNames = handlerNames ?? Array.Empty<string>();
        }

        /// <summary>
        /// 快照记录的事件类型名称。
        /// </summary>
        public string EventName { get; }

        /// <summary>
        /// 快照记录的订阅者数量。
        /// </summary>
        public int HandlerCount { get; }

        /// <summary>
        /// 快照记录的待分发事件数量。
        /// </summary>
        public int PendingCount { get; }

        /// <summary>
        /// 快照记录的事件 Channel 生命周期状态。
        /// </summary>
        public EventChannelLifecycleState State { get; }

        /// <summary>
        /// 获取快照保存的订阅者名称列表。
        /// </summary>
        public string[] GetHandlerNames()
        {
            return _handlerNames;
        }
    }
}