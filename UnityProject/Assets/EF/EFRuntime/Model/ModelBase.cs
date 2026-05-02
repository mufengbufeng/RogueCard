using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EF.Model
{
    /// <summary>
    /// 模型的基类，提供受控的只读视图与数据写入能力。
    /// 实现 INotifyPropertyChanged 以支持 UI 数据绑定。
    /// </summary>
    public abstract class ModelBase : IModelInternal, INotifyPropertyChanged
    {
        private ModelManager _manager;
        private bool _initialized;

        /// <summary>
        /// 属性值变更后触发的事件，用于通知绑定系统更新 UI。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 管理当前模型的 <see cref="ModelManager"/>。
        /// </summary>
        /// <exception cref="InvalidOperationException">当模型尚未注册时抛出。</exception>
        protected ModelManager Manager => _manager ?? throw new InvalidOperationException("模型尚未注册到 ModelManager。");

        /// <summary>
        /// 当前模型是否已经完成初始化。
        /// </summary>
        protected bool IsInitialized => _initialized;

        /// <summary>
        /// 用于封装模型内部的数据，外部无法直接写入。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        protected sealed class ModelValue<T>
        {
            private T _value;

            internal ModelValue(T value)
            {
                _value = value;
            }

            /// <summary>
            /// 获取当前数据的只读值。
            /// </summary>
            public T Value => _value;

            internal void SetValue(T value)
            {
                _value = value;
            }
        }

        /// <summary>
        /// 创建一个模型内部使用的数据容器。
        /// </summary>
        protected ModelValue<T> CreateValue<T>(T initialValue = default)
        {
            return new ModelValue<T>(initialValue);
        }

        /// <summary>
        /// 读取封装字段的当前值。
        /// </summary>
        protected T GetValue<T>(ModelValue<T> value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return value.Value;
        }

        /// <summary>
        /// 更新封装字段的值，只有模型内部可以调用。
        /// </summary>
        /// <param name="value">要更新的字段。</param>
        /// <param name="newValue">新值。</param>
        /// <param name="propertyName">属性名称，用于触发属性变更通知。</param>
        protected void SetValue<T>(ModelValue<T> value, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value.SetValue(newValue);
            OnValueChanged(value);
            RaisePropertyChanged(propertyName);
        }

        /// <summary>
        /// 主动触发属性变更通知，通知绑定系统某个属性的值已经改变。
        /// </summary>
        /// <param name="propertyName">发生变更的属性名称。</param>
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 当模型字段被更新时触发，子类可用于派发事件。
        /// </summary>
        protected virtual void OnValueChanged<T>(ModelValue<T> value)
        {
        }

        /// <summary>
        /// 模型初始化阶段，已获取到所属的 <see cref="ModelManager"/>。
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// 模型注销阶段，可在此释放内部资源。
        /// </summary>
        protected virtual void OnShutdown()
        {
        }

        /// <summary>
        /// 模型逐帧更新，默认不执行任何逻辑。
        /// </summary>
        protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        void IModelInternal.Initialize(ModelManager manager)
        {
            if (_initialized)
            {
                throw new InvalidOperationException($"模型 {GetType().FullName} 已经注册，无法重复初始化。");
            }

            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            OnInitialize();
            _initialized = true;
        }

        void IModelInternal.Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            OnShutdown();
            _initialized = false;
            _manager = null;
        }

        void IModelInternal.Update(float elapseSeconds, float realElapseSeconds)
        {
            if (!_initialized)
            {
                return;
            }

            OnUpdate(elapseSeconds, realElapseSeconds);
        }

        Type IModelInternal.ViewType => GetViewType();

        object IModelInternal.ViewInstance => GetViewInstance();

        /// <summary>
        /// 返回模型对外暴露的只读视图类型。
        /// </summary>
        protected internal abstract Type GetViewType();

        /// <summary>
        /// 返回模型对外暴露的只读视图实例。
        /// </summary>
        protected internal abstract object GetViewInstance();
    }

    /// <summary>
    /// 泛型模型基类，负责创建和维护只读数据接口。
    /// </summary>
    /// <typeparam name="TData">只读数据接口或类型。</typeparam>
    public abstract class ModelBase<TData> : ModelBase where TData : class
    {
        private TData _data;

        protected internal override Type GetViewType()
        {
            return typeof(TData);
        }

        protected internal sealed override object GetViewInstance()
        {
            if (_data == null)
            {
                throw new InvalidOperationException($"模型 {GetType().FullName} 尚未完成初始化，无法获取数据 {typeof(TData).FullName}。");
            }

            return _data;
        }

        /// <summary>
        /// 获取只读数据接口实例，供子类访问。
        /// </summary>
        protected TData Data => (TData)GetViewInstance();

        /// <inheritdoc />
        protected sealed override void OnInitialize()
        {
            _data = CreateData() ?? throw new InvalidOperationException($"模型 {GetType().FullName} 未能创建数据 {typeof(TData).FullName}。");
            OnModelInitialized();
        }

        /// <inheritdoc />
        protected sealed override void OnShutdown()
        {
            OnModelReleased();
            _data = null;
        }

        /// <summary>
        /// 子类可在此完成初始化逻辑，例如绑定事件。
        /// </summary>
        protected virtual void OnModelInitialized()
        {
        }

        /// <summary>
        /// 子类可在此完成反注册逻辑。
        /// </summary>
        protected virtual void OnModelReleased()
        {
        }

        /// <summary>
        /// 创建只读数据接口实例，必须返回非空对象。
        /// </summary>
        protected abstract TData CreateData();
    }

    internal interface IModelInternal
    {
        Type ViewType { get; }

        object ViewInstance { get; }

        void Initialize(ModelManager manager);

        void Shutdown();

        void Update(float elapseSeconds, float realElapseSeconds);
    }
}
