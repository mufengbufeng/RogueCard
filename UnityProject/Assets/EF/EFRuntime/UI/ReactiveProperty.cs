using System;
using System.Collections.Generic;

namespace EF.UI
{
    /// <summary>
    /// 响应式属性基类，提供清理监听者的能力。
    /// </summary>
    public abstract class ReactivePropertyBase
    {
        /// <summary>
        /// 清理所有 Changed 事件的监听者。
        /// </summary>
        public abstract void ClearListeners();
    }

    /// <summary>
    /// 响应式属性，值变化时触发 Changed 事件。
    /// </summary>
    public class ReactiveProperty<T> : ReactivePropertyBase
    {
        private T _value;

        /// <summary>
        /// 当前值。设置新值（与旧值不同时）触发 Changed 事件。
        /// </summary>
        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                Changed?.Invoke(_value);
            }
        }

        /// <summary>
        /// 值变化时触发的事件。
        /// </summary>
        public event Action<T> Changed;

        /// <summary>
        /// 创建响应式属性并指定初始值。
        /// </summary>
        public ReactiveProperty(T initialValue = default) => _value = initialValue;

        /// <inheritdoc />
        public override void ClearListeners() => Changed = null;
    }
}
