using System;
using System.Collections.Generic;

namespace EF.UI
{
    /// <summary>
    /// ViewModel 基类。通过 Prop 工厂方法创建并追踪所有 ReactiveProperty，
    /// Dispose 时自动清理所有属性的监听者。
    /// </summary>
    public abstract class ViewModelBase : IDisposable
    {
        private List<ReactivePropertyBase> _tracked;
        private bool _isDisposed;

        /// <summary>
        /// 创建并追踪一个响应式属性。Dispose 时自动清理其监听者。
        /// </summary>
        protected ReactiveProperty<T> Prop<T>(T initialValue = default)
        {
            _tracked ??= new List<ReactivePropertyBase>();
            var prop = new ReactiveProperty<T>(initialValue);
            _tracked.Add(prop);
            return prop;
        }

        /// <summary>
        /// 清理所有追踪属性的监听者。幂等，多次调用安全。
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_tracked == null) return;
            foreach (var prop in _tracked) prop.ClearListeners();
            _tracked.Clear();
        }
    }
}
