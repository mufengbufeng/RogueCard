using System;
using UnityEngine;
using UnityEngine.Events;

namespace EF.UI
{
    /// <summary>
    /// Unity 事件绑定实现
    /// 支持 Button.onClick、Toggle.onValueChanged 等 Unity 事件
    /// </summary>
    public class UnityEventBinding : IEventBinding
    {
        private readonly UnityEventBase _unityEvent;
        private readonly UnityAction _action;
        private readonly UnityAction<bool> _boolAction;
        private readonly UnityAction<float> _floatAction;
        private readonly UnityAction<int> _intAction;
        private readonly UnityAction<string> _stringAction;

        public bool IsBound { get; private set; }

        /// <summary>
        /// 绑定无参数 Unity 事件
        /// </summary>
        public UnityEventBinding(UnityEvent unityEvent, UnityAction action)
        {
            _unityEvent = unityEvent;
            _action = action;
        }

        /// <summary>
        /// 绑定 bool 参数 Unity 事件
        /// </summary>
        public UnityEventBinding(UnityEvent<bool> unityEvent, UnityAction<bool> action)
        {
            _unityEvent = unityEvent;
            _boolAction = action;
        }

        /// <summary>
        /// 绑定 float 参数 Unity 事件
        /// </summary>
        public UnityEventBinding(UnityEvent<float> unityEvent, UnityAction<float> action)
        {
            _unityEvent = unityEvent;
            _floatAction = action;
        }

        /// <summary>
        /// 绑定 int 参数 Unity 事件
        /// </summary>
        public UnityEventBinding(UnityEvent<int> unityEvent, UnityAction<int> action)
        {
            _unityEvent = unityEvent;
            _intAction = action;
        }

        /// <summary>
        /// 绑定 string 参数 Unity 事件
        /// </summary>
        public UnityEventBinding(UnityEvent<string> unityEvent, UnityAction<string> action)
        {
            _unityEvent = unityEvent;
            _stringAction = action;
        }

        public void Bind()
        {
            if (IsBound || _unityEvent == null)
                return;

            try
            {
                if (_action != null)
                    ((UnityEvent)_unityEvent).AddListener(_action);
                else if (_boolAction != null)
                    ((UnityEvent<bool>)_unityEvent).AddListener(_boolAction);
                else if (_floatAction != null)
                    ((UnityEvent<float>)_unityEvent).AddListener(_floatAction);
                else if (_intAction != null)
                    ((UnityEvent<int>)_unityEvent).AddListener(_intAction);
                else if (_stringAction != null)
                    ((UnityEvent<string>)_unityEvent).AddListener(_stringAction);

                IsBound = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] Unity 事件绑定失败: {ex.Message}");
            }
        }

        public void Unbind()
        {
            if (!IsBound || _unityEvent == null)
                return;

            try
            {
                if (_action != null)
                    ((UnityEvent)_unityEvent).RemoveListener(_action);
                else if (_boolAction != null)
                    ((UnityEvent<bool>)_unityEvent).RemoveListener(_boolAction);
                else if (_floatAction != null)
                    ((UnityEvent<float>)_unityEvent).RemoveListener(_floatAction);
                else if (_intAction != null)
                    ((UnityEvent<int>)_unityEvent).RemoveListener(_intAction);
                else if (_stringAction != null)
                    ((UnityEvent<string>)_unityEvent).RemoveListener(_stringAction);

                IsBound = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] Unity 事件解绑失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// C# 事件绑定实现
    /// 支持 Action、Event 等 C# 事件
    /// </summary>
    public class ActionEventBinding : IEventBinding
    {
        private readonly Action _subscribeAction;
        private readonly Action _unsubscribeAction;

        public bool IsBound { get; private set; }

        /// <summary>
        /// 创建 C# 事件绑定
        /// </summary>
        /// <param name="subscribeAction">订阅操作</param>
        /// <param name="unsubscribeAction">取消订阅操作</param>
        public ActionEventBinding(Action subscribeAction, Action unsubscribeAction)
        {
            _subscribeAction = subscribeAction;
            _unsubscribeAction = unsubscribeAction;
        }

        public void Bind()
        {
            if (IsBound)
                return;

            try
            {
                _subscribeAction?.Invoke();
                IsBound = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] C# 事件绑定失败: {ex.Message}");
            }
        }

        public void Unbind()
        {
            if (!IsBound)
                return;

            try
            {
                _unsubscribeAction?.Invoke();
                IsBound = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] C# 事件解绑失败: {ex.Message}");
            }
        }
    }
}