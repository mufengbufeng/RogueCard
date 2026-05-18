using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Model;

namespace EF.UI
{
    /// <summary>
    /// UI Controller 抽象基类，负责协调 Model 和 View。
    /// Controller 通过 ModelManager 访问数据 Model，持有 View 的引用。
    /// </summary>
    public abstract class UIController : IDisposable
    {
        private bool _isDisposed;
        private ControllerEventBinder _eventBinder;

        /// <summary>
        /// 当前绑定的 View。
        /// </summary>
        protected internal UIView View { get; private set; }

        /// <summary>
        /// 当前实例的运行上下文。
        /// </summary>
        protected internal UIRuntimeContext Context { get; private set; }

        /// <summary>
        /// 事件绑定器，用于管理 Controller 订阅的事件生命周期。
        /// 通过此绑定器订阅的事件会在 OnExit 时自动取消订阅，防止内存泄漏。
        /// </summary>
        protected ControllerEventBinder EventBinder => _eventBinder ??= new ControllerEventBinder();

        /// <summary>
        /// 获取 ModelManager 中注册的数据 Model。
        /// 如果模型未注册将抛出异常。
        /// </summary>
        protected TModel GetModel<TModel>() where TModel : ModelBase
        {
            return Context.ModelManager.GetModel<TModel>();
        }

        /// <summary>
        /// 尝试获取 ModelManager 中的数据 Model，如果未注册则自动创建并注册。
        /// 该方法保证返回非空实例。
        /// </summary>
        /// <typeparam name="TModel">模型类型，必须有无参构造函数。</typeparam>
        /// <returns>模型实例（保证非空）。</returns>
        protected TModel TryGetModel<TModel>() where TModel : ModelBase, new()
        {
            return Context.ModelManager.TryGetModel<TModel>();
        }

        /// <summary>
        /// 获取强类型 View。
        /// </summary>
        protected TView GetView<TView>() where TView : UIView
        {
            return View as TView;
        }

        /// <summary>
        /// 绑定 C# 事件，自动管理生命周期。
        /// 使用此方法订阅的事件会在 OnExit 时自动取消订阅。
        /// </summary>
        /// <typeparam name="THandler">事件处理器类型</typeparam>
        /// <param name="addHandler">订阅事件（+=）的操作</param>
        /// <param name="removeHandler">取消订阅（-=）的操作</param>
        /// <param name="handler">事件处理器</param>
        /// <example>
        /// 示例用法：
        /// <code>
        /// BindEvent(ref _mainView.OnStartGameRequested, HandleStartGame);
        /// </code>
        /// </example>
        protected void BindEvent<THandler>(Action<THandler> addHandler, Action<THandler> removeHandler, THandler handler)
        {
            EventBinder.BindEvent(addHandler, removeHandler, handler);
        }

        /// <summary>
        /// 初始化 Controller，仅在实例创建时调用一次。
        /// </summary>
        protected virtual void OnInitialize()
        {
        }

        /// <summary>
        /// 打开界面前的异步准备阶段，可用于加载数据。
        /// </summary>
        protected virtual UniTask OnPrepareAsync(object userData, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 进入界面时调用，可在此进行事件订阅等操作。
        /// </summary>
        protected virtual void OnEnter(object userData)
        {
        }

        /// <summary>
        /// 界面需要刷新显示数据时调用。
        /// </summary>
        protected virtual void OnRefresh(object userData)
        {
        }

        /// <summary>
        /// 界面关闭时调用，可在此取消订阅或保存状态。
        /// </summary>
        protected virtual void OnExit()
        {
        }

        /// <summary>
        /// Controller 被释放前调用，派生类可在此清理资源。
        /// </summary>
        protected virtual void OnRelease()
        {
        }

        /// <summary>
        /// 每帧更新回调，仅在 UI 处于打开状态时触发。
        /// </summary>
        protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 派生类可重写的释放操作。
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        internal void InternalInitialize(UIView view, UIRuntimeContext context)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            OnInitialize();
        }

        internal UniTask InternalPrepareAsync(object userData, CancellationToken cancellationToken)
        {
            return OnPrepareAsync(userData, cancellationToken);
        }

        internal void InternalEnter(object userData)
        {
            OnEnter(userData);
        }

        internal void InternalRefresh(object userData)
        {
            OnRefresh(userData);
        }

        internal void InternalExit()
        {
            OnExit();
            // 清理所有事件绑定
            Log.Info("[UIController] 清理事件绑定");
            _eventBinder?.ClearAllBindings();
        }

        internal void InternalRelease()
        {
            OnRelease();
        }

        internal void InternalUpdate(float elapseSeconds, float realElapseSeconds)
        {
            OnUpdate(elapseSeconds, realElapseSeconds);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            Log.Info("[UIController] 释放 Controller 资源");

            _isDisposed = true;

            // 清理事件绑定器
            _eventBinder?.Dispose();
            _eventBinder = null;

            OnDispose();
            View = null;
            Context = null;
            GC.SuppressFinalize(this);
        }
    }
}
