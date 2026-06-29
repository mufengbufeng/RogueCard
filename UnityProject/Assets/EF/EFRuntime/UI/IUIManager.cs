using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Common;
using UnityEngine;

namespace EF.UI
{
    /// <summary>
    /// UI 管理器对外暴露的接口。
    /// </summary>
    public interface IUIManager : IEFManager
    {
        /// <summary>
        /// 已注册的 UI 描述数量。
        /// </summary>
        int RegisteredWindowCount { get; }

        /// <summary>
        /// 当前正在显示的 UI 数量。
        /// </summary>
        int ActiveWindowCount { get; }

        /// <summary>
        /// 注册 UI 描述信息。
        /// </summary>
        void RegisterWindow(UIWindowDescriptor descriptor);

        /// <summary>
        /// 设置 UI Controller 创建工厂。
        /// </summary>
        void SetControllerFactory(IUIControllerFactory controllerFactory);

        /// <summary>
        /// 注销 UI 描述信息。
        /// </summary>
        bool UnregisterWindow(string windowName);

        /// <summary>
        /// 检查是否已注册指定名称的 UI。
        /// </summary>
        bool Contains(string windowName);

        /// <summary>
        /// 打开界面，如已打开则执行刷新。
        /// </summary>
        UniTask<UIWindowHandle> OpenWindowAsync(string windowName, object userData = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 打开界面（单泛型）- 按命名约定解析 Controller，并使用默认配置：Normal 层级、缓存关闭、单实例模式。
        /// </summary>
        /// <typeparam name="TView">UI 视图类型，必须继承 UIView</typeparam>
        /// <param name="location">Prefab资源路径</param>
        /// <param name="userData">传递给界面的用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>界面句柄</returns>
        UniTask<UIWindowHandle> OpenWindowAsync<TView>(
            string location,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView;

        /// <summary>
        /// 打开界面（单泛型）- 按命名约定解析 Controller，并使用指定层级，其他参数使用默认值：缓存关闭，单实例模式。
        /// </summary>
        /// <typeparam name="TView">UI 视图类型，必须继承 UIView</typeparam>
        /// <param name="location">Prefab资源路径</param>
        /// <param name="layer">UI显示层级</param>
        /// <param name="userData">传递给界面的用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>界面句柄</returns>
        UniTask<UIWindowHandle> OpenWindowAsync<TView>(
            string location,
            UILayer layer,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView;

        /// <summary>
        /// 打开界面（单泛型）- 按命名约定解析 Controller，并可以指定所有参数以获得最大控制能力。
        /// </summary>
        /// <typeparam name="TView">UI 视图类型，必须继承 UIView</typeparam>
        /// <param name="location">Prefab资源路径</param>
        /// <param name="layer">UI显示层级</param>
        /// <param name="cacheOnClose">关闭时是否缓存而不销毁</param>
        /// <param name="allowMultiple">是否允许同一类型的多个实例</param>
        /// <param name="userData">传递给界面的用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>界面句柄</returns>
        UniTask<UIWindowHandle> OpenWindowAsync<TView>(
            string location,
            UILayer layer,
            bool cacheOnClose,
            bool allowMultiple,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView;

        /// <summary>
        /// 打开界面（显式指定 Controller）。
        /// </summary>
        UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
            string location,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView
            where TController : UIController;

        /// <summary>
        /// 打开界面（显式指定 Controller 和层级）。
        /// </summary>
        UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
            string location,
            UILayer layer,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView
            where TController : UIController;

        /// <summary>
        /// 打开界面（显式指定 Controller 和完整配置）。
        /// </summary>
        UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
            string location,
            UILayer layer,
            bool cacheOnClose,
            bool allowMultiple,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView
            where TController : UIController;

        /// <summary>
        /// 关闭界面。
        /// </summary>
        UniTask CloseWindowAsync(string windowName);

        /// <summary>
        /// 关闭所有已打开界面。
        /// </summary>
        UniTask CloseAllAsync();

        /// <summary>
        /// 尝试获取指定界面的 Controller。
        /// </summary>
        bool TryGetController<TController>(string windowName, out TController controller) where TController : UIController;

        /// <summary>
        /// 尝试获取指定界面的视图组件。
        /// </summary>
        bool TryGetView<TView>(string windowName, out TView view) where TView : UIView;

        /// <summary>
        /// 注册 UI 层级的根节点。
        /// </summary>
        void RegisterLayerRoot(UILayer layer, Transform rootTransform);

        /// <summary>
        /// 设置默认的父节点，当对应层没有根节点时使用。
        /// </summary>
        void SetFallbackRoot(Transform fallbackRoot);
    }
}

