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

        /// <inheritdoc />
        public string EventName { get; }

        /// <inheritdoc />
        public int HandlerCount { get; }

        /// <inheritdoc />
        public int PendingCount { get; }

        /// <inheritdoc />
        public EventChannelLifecycleState State { get; }

        /// <inheritdoc />
        public string[] GetHandlerNames()
        {
            return _handlerNames;
        }
    }
}