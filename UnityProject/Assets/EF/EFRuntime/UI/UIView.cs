using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Model;
using UnityEngine;

namespace EF.UI
{
    /// <summary>
    /// UI 视图基类，负责处理生命周期与数据绑定。
    /// View 可以从 Model Manager 获取只读数据 , 不能t修改数据。
    /// View 不可以 直接与 Controller 交互，应通过事件或数据模型进行通信。
    /// </summary>
    public abstract class UIView : MonoBehaviour
    {
        private UIBindingCollection _bindings;
        private UHubComponent _uHub;

        /// <summary>
        /// 当前实例的运行上下文。
        /// </summary>
        protected internal UIRuntimeContext Context { get; private set; }

        /// <summary>
        /// 当前绑定集合。
        /// </summary>
        protected UIBindingCollection Bindings => _bindings ??= new UIBindingCollection();

        /// <summary>
        /// UHub 组件，提供自动组件绑定和事件管理功能。
        /// 首次访问时自动创建实例。
        /// </summary>
        protected UHubComponent UHub
        {
            get
            {
                if (_uHub == null)
                {
                    _uHub = new UHubComponent(this);
                }
                return _uHub;
            }
        }

        /// <summary>
        /// 获取 ModelManager 中注册的只读数据接口。
        /// </summary>
        protected TData GetModelData<TData>() where TData : class
        {
            return Context.ModelManager.Get<TData>();
        }

        /// <summary>
        /// 尝试获取 ModelManager 中注册的只读数据接口。
        /// </summary>
        protected bool TryGetModelData<TData>(out TData data) where TData : class
        {
            return Context.ModelManager.TryGet(out data);
        }

        /// <summary>
        /// 便利方法：获取 UHub 自动绑定的组件
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="componentName">组件名称</param>
        /// <returns>组件实例</returns>
        protected T GetComponent<T>(string componentName) where T : UnityEngine.Object
        {
            return UHub.GetComponent<T>(componentName);
        }

        /// <summary>
        /// 便利方法：绑定 Unity 事件
        /// </summary>
        /// <param name="unityEvent">Unity 事件</param>
        /// <param name="action">回调方法</param>
        protected void BindEvent(UnityEngine.Events.UnityEvent unityEvent, UnityEngine.Events.UnityAction action)
        {
            UHub.BindEvent(unityEvent, action);
        }

        /// <summary>
        /// 便利方法：绑定 C# 事件
        /// </summary>
        /// <param name="subscribeAction">订阅操作</param>
        /// <param name="unsubscribeAction">取消订阅操作</param>
        protected void BindEvent(Action subscribeAction, Action unsubscribeAction)
        {
            UHub.BindEvent(subscribeAction, unsubscribeAction);
        }

        /// <summary>
        /// 初始化视图，仅在实例创建时调用一次。
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// 初始化完成后调用，可在此注册数据绑定。
        /// </summary>
        protected virtual void OnBindings()
        {
        }

        /// <summary>
        /// 打开视图时调用。
        /// </summary>
        protected virtual void OnOpen(object userData)
        {
        }

        /// <summary>
        /// 刷新视图显示数据。
        /// </summary>
        protected virtual void OnRefresh(object userData)
        {
        }

        /// <summary>
        /// 关闭视图时调用。
        /// </summary>
        protected virtual void OnClose()
        {
        }

        /// <summary>
        /// 预加载或准备阶段的异步逻辑。
        /// </summary>
        protected virtual UniTask OnPrepareAsync(object userData, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 视图被完全释放时调用。
        /// </summary>
        protected virtual void OnRelease()
        {
        }

        /// <summary>
        /// 视图在打开状态下的逐帧更新。
        /// </summary>
        protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 绑定数据视图属性到 UI 控件更新。
        /// </summary>
        protected void BindProperty<TSource, TValue>(
            TSource source,
            Expression<Func<TSource, TValue>> expression,
            Action<TValue> setter)
            where TSource : class, INotifyPropertyChanged
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (setter == null)
            {
                throw new ArgumentNullException(nameof(setter));
            }

            var binding = new UIPropertyBinding<TSource, TValue>(source, expression, setter);
            Bindings.Add(binding);
        }

        internal void InternalInitialize(UIRuntimeContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _bindings ??= new UIBindingCollection();

            OnInitialize();

            // 如果 UHub 已被访问，则进行自动初始化
            if (_uHub != null)
            {
                _uHub.Initialize();
            }

            OnBindings();
        }

        internal UniTask InternalPrepareAsync(object userData, CancellationToken cancellationToken)
        {
            return OnPrepareAsync(userData, cancellationToken);
        }

        internal void InternalOpen(object userData)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            OnOpen(userData);
            OnRefresh(userData);
        }

        internal void InternalRefresh(object userData)
        {
            OnRefresh(userData);
        }

        internal void InternalClose()
        {
            OnClose();
        }

        internal void InternalRelease()
        {
            try
            {
                OnRelease();
            }
            finally
            {
                // 释放 UHub 资源
                _uHub?.Dispose();
                _uHub = null;

                _bindings?.Dispose();
                _bindings = null;
                Context = null;
            }
        }

        internal void InternalUpdate(float elapseSeconds, float realElapseSeconds)
        {
            OnUpdate(elapseSeconds, realElapseSeconds);
        }
    }
}
