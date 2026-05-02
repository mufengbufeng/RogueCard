using System;
using System.Collections.Generic;
using EF.Event;

namespace EF.UI
{
    /// <summary>
    /// Controller 事件绑定器，为 UIController 提供自动事件生命周期管理。
    /// 通过 BindEvent 绑定的事件会在 OnExit 时自动取消订阅，防止内存泄漏。
    /// </summary>
    public sealed class ControllerEventBinder : IDisposable
    {
        private readonly List<EventSubscription> _subscriptions = new List<EventSubscription>();
        private bool _isDisposed;

        /// <summary>
        /// 获取当前已绑定的事件数量。
        /// </summary>
        public int BindingCount => _subscriptions.Count;

        /// <summary>
        /// 绑定 C# 事件，自动管理生命周期。
        /// </summary>
        /// <typeparam name="THandler">事件处理器类型</typeparam>
        /// <param name="event">要订阅的事件</param>
        /// <param name="handler">事件处理器</param>
        /// <exception cref="ObjectDisposedException">绑定器已释放时调用</exception>
        public void BindEvent<THandler>(Action<THandler> addHandler, Action<THandler> removeHandler, THandler handler)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ControllerEventBinder));
            }

            if (addHandler == null)
                throw new ArgumentNullException(nameof(addHandler));
            if (removeHandler == null)
                throw new ArgumentNullException(nameof(removeHandler));

            // 订阅事件
            addHandler(handler);

            // 记录订阅以便后续清理
            _subscriptions.Add(new EventSubscription
            {
                Unsubscribe = () => removeHandler(handler)
            });
        }

        /// <summary>
        /// 绑定 EventChannel 事件，自动管理生命周期。
        /// OnExit 时自动 Unsubscribe，无需手动管理。
        /// </summary>
        /// <typeparam name="T">事件参数类型。</typeparam>
        /// <param name="channel">事件 Channel。</param>
        /// <param name="handler">事件处理回调。</param>
        public void BindEvent<T>(EventChannel<T> channel, Action<T> handler) where T : struct
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ControllerEventBinder));
            }

            if (channel == null) throw new ArgumentNullException(nameof(channel));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            channel.Subscribe(handler);

            _subscriptions.Add(new EventSubscription
            {
                Unsubscribe = () => channel.Unsubscribe(handler)
            });
        }

        /// <summary>
        /// 清理所有事件绑定，取消所有已订阅的事件。
        /// </summary>
        public void ClearAllBindings()
        {
            foreach (var subscription in _subscriptions)
            {
                try
                {
                    subscription.Unsubscribe?.Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ControllerEventBinder] 取消事件订阅失败: {ex.Message}");
                }
            }

            _subscriptions.Clear();
        }

        /// <summary>
        /// 释放绑定器资源，清理所有事件订阅。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            ClearAllBindings();
            _isDisposed = true;
        }

        /// <summary>
        /// 事件订阅记录
        /// </summary>
        private struct EventSubscription
        {
            /// <summary>
            /// 取消订阅的操作
            /// </summary>
            public Action Unsubscribe;
        }
    }
}
