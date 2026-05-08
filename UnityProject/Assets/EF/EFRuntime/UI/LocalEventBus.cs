using System;
using System.Collections.Generic;
using EF.Event;

namespace EF.UI
{
    /// <summary>
    /// 局部事件总线，实现 IEventPublisher 接口。
    /// 用于窗口内 System 间的事件通信，独立于全局 EventHub。
    /// </summary>
    public sealed class LocalEventBus : IEventPublisher, IDisposable
    {
        private readonly Dictionary<Type, object> _channels = new();
        private readonly List<IEventChannel> _activeChannels = new();

        /// <summary>
        /// 获取指定类型的事件 Channel。
        /// </summary>
        public EventChannel<T> GetChannel<T>() where T : struct
        {
            var type = typeof(T);
            if (!_channels.TryGetValue(type, out var channel))
            {
                channel = new EventChannel<T>(new LocalScheduler(_activeChannels));
                _channels[type] = channel;
            }

            return (EventChannel<T>)channel;
        }

        /// <summary>
        /// 释放所有 Channel。
        /// </summary>
        public void Dispose()
        {
            foreach (var channel in _channels.Values)
            {
                (channel as IDisposable)?.Dispose();
            }

            _channels.Clear();
            _activeChannels.Clear();
        }

        private sealed class LocalScheduler : IEventChannelScheduler
        {
            private readonly List<IEventChannel> _active;

            public LocalScheduler(List<IEventChannel> active) => _active = active;

            public void OnChannelActivated(IEventChannel channel)
            {
                if (!_active.Contains(channel))
                    _active.Add(channel);
            }

            public void OnChannelBecameIdle(IEventChannel channel)
            {
                _active.Remove(channel);
            }
        }
    }
}
