using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using UnityEngine;

namespace EF.UI
{
    /// <summary>
    /// UI 绑定对象的集合，负责统一释放。
    /// </summary>
    public sealed class UIBindingCollection : IDisposable
    {
        private readonly List<IUIBinding> _bindings = new();

        /// <summary>
        /// 添加一个绑定并立即初始化。
        /// </summary>
        public void Add(IUIBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            binding.Initialize();
            _bindings.Add(binding);
        }

        /// <summary>
        /// 清理所有已注册的绑定。
        /// </summary>
        public void Dispose()
        {
            for (int index = _bindings.Count - 1; index >= 0; index--)
            {
                IUIBinding binding = _bindings[index];
                try
                {
                    binding?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _bindings.Clear();
        }
    }

    public interface IUIBinding : IDisposable
    {
        void Initialize();
    }

    internal sealed class UIPropertyBinding<TSource, TValue> : IUIBinding where TSource : class, INotifyPropertyChanged
    {
        private readonly TSource _source;
        private readonly Func<TSource, TValue> _getter;
        private readonly Action<TValue> _setter;
        private readonly string _propertyName;

        private bool _isInitialized;
        private TValue _currentValue;

        public UIPropertyBinding(TSource source, Expression<Func<TSource, TValue>> expression, Action<TValue> setter)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));

            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (expression.Body is not MemberExpression memberExpression)
            {
                throw new ArgumentException("只能绑定属性访问表达式", nameof(expression));
            }

            _propertyName = memberExpression.Member.Name;
            _getter = expression.Compile();
        }

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            _currentValue = _getter(_source);
            _setter(_currentValue);
            _source.PropertyChanged += OnPropertyChanged;
        }

        public void Dispose()
        {
            if (!_isInitialized)
            {
                return;
            }

            _isInitialized = false;
            _source.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && !string.Equals(e.PropertyName, _propertyName, StringComparison.Ordinal))
            {
                return;
            }

            TValue value = _getter(_source);
            if (EqualityComparer<TValue>.Default.Equals(_currentValue, value))
            {
                return;
            }

            _currentValue = value;
            _setter(value);
        }
    }
}
