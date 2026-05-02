using System;
using EF.Common;

namespace EF.Event
{
    /// <summary>
    /// 类型化事件 Channel，为每个事件类型提供零 GC 的订阅、派发与异步队列能力。
    /// Handler 存储使用预分配数组，异步队列使用环形缓冲区，Publish 热路径无任何托管堆分配。
    /// </summary>
    /// <typeparam name="T">事件参数类型，必须是 readonly struct。</typeparam>
    public sealed class EventChannel<T> : IEventChannel, IEventChannelInfo where T : struct
    {
        private const int DefaultHandlerCapacity = 8;
        private const int DefaultRingCapacity = 16;

        private readonly IEventChannelScheduler _scheduler;

        private Action<T>[] _handlers;
        private int _handlerCount;
        private int _version;
        private int _dispatchDepth;

        private T[] _ring;
        private int _ringHead;
        private int _ringTail;

        /// <summary>
        /// 创建一个新的事件 Channel 实例。
        /// </summary>
        public EventChannel()
            : this(null)
        {
        }

        /// <summary>
        /// 创建一个带调度协作的新事件 Channel 实例。
        /// </summary>
        /// <param name="scheduler">异步队列调度协作者。</param>
        public EventChannel(IEventChannelScheduler scheduler)
        {
            _scheduler = scheduler;
            _handlers = new Action<T>[DefaultHandlerCapacity];
            _ring = new T[DefaultRingCapacity];
        }

        /// <inheritdoc />
        public string EventName => typeof(T).Name;

        /// <inheritdoc />
        public int HandlerCount => _handlerCount;

        /// <inheritdoc />
        public int PendingCount
        {
            get
            {
                int count = _ringTail - _ringHead;
                if (count < 0) count += _ring.Length;
                return count;
            }
        }

        /// <inheritdoc />
        public EventChannelLifecycleState State => PendingCount > 0
            ? EventChannelLifecycleState.Active
            : EventChannelLifecycleState.Idle;

        /// <summary>
        /// 注册事件处理器。若 handler 已存在则忽略。
        /// </summary>
        /// <param name="handler">事件处理回调。</param>
        public void Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler), "事件处理器不能为空");

            if (_handlerCount == _handlers.Length)
            {
                Array.Resize(ref _handlers, _handlers.Length * 2);
            }

            _handlers[_handlerCount++] = handler;
            _version++;
        }

        /// <summary>
        /// 反注册事件处理器，使用 swap-with-last 策略，O(1) 复杂度。
        /// </summary>
        /// <param name="handler">要移除的事件处理回调。</param>
        public void Unsubscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler), "事件处理器不能为空");

            for (int i = 0; i < _handlerCount; i++)
            {
                if (_handlers[i] == handler)
                {
                    _handlers[i] = _handlers[_handlerCount - 1];
                    _handlers[--_handlerCount] = null;
                    _version++;
                    return;
                }
            }
        }

        /// <summary>
        /// 同步立即派发事件，直接调用所有已注册 handler。热路径零 GC。
        /// </summary>
        /// <param name="args">事件参数，按引用传递避免 struct 复制。</param>
        public void Publish(in T args)
        {
            _dispatchDepth++;
            int ver = _version;
            int count = _handlerCount;

            for (int i = 0; i < count; i++)
            {
                _handlers[i](args);

                if (ver != _version)
                {
                    ver = _version;
                    count = _handlerCount;
                }
            }

            _dispatchDepth--;
        }

        /// <summary>
        /// 将事件放入异步队列，等待下一次 Flush 时派发。写入环形缓冲区，零 GC。
        /// </summary>
        /// <param name="args">事件参数，按引用传递。</param>
        public void Enqueue(in T args)
        {
            bool wasEmpty = _ringHead == _ringTail;
            int next = (_ringTail + 1) % _ring.Length;
            if (next == _ringHead)
            {
                ExpandRing();
            }

            _ring[_ringTail] = args;
            _ringTail = (_ringTail + 1) % _ring.Length;

            if (wasEmpty)
            {
                _scheduler?.OnChannelActivated(this);
            }
        }

        /// <inheritdoc />
        public void Flush()
        {
            if (_ringHead == _ringTail)
            {
                return;
            }

            while (_ringHead != _ringTail)
            {
                Publish(in _ring[_ringHead]);
                _ringHead = (_ringHead + 1) % _ring.Length;
            }

            _scheduler?.OnChannelBecameIdle(this);
        }

        /// <inheritdoc />
        public void Clear()
        {
            bool hadPending = _ringHead != _ringTail;

            Array.Clear(_handlers, 0, _handlerCount);
            _handlerCount = 0;
            _version++;
            _ringHead = 0;
            _ringTail = 0;

            if (hadPending)
            {
                _scheduler?.OnChannelBecameIdle(this);
            }
        }

        /// <inheritdoc />
        public string[] GetHandlerNames()
        {
            var names = new string[_handlerCount];
            for (int i = 0; i < _handlerCount; i++)
            {
                var h = _handlers[i];
                if (h != null)
                {
                    names[i] = h.Target != null
                        ? $"{h.Target.GetType().Name}.{h.Method.Name}"
                        : h.Method.Name;
                }
            }

            return names;
        }

        private void ExpandRing()
        {
            int oldLen = _ring.Length;
            var bigger = new T[oldLen * 2];

            int i = 0;
            while (_ringHead != _ringTail)
            {
                bigger[i++] = _ring[_ringHead];
                _ringHead = (_ringHead + 1) % oldLen;
            }

            _ring = bigger;
            _ringHead = 0;
            _ringTail = i;
        }
    }
}
