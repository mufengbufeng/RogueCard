using System;

namespace EF.UI
{
    /// <summary>
    /// 描述一个 UI 界面的元数据，用于 UI 管理器注册。
    /// 支持 MVC 架构：View 和 Controller，Model 通过 ModelManager 管理。
    /// </summary>
    public sealed class UIWindowDescriptor
    {
        /// <summary>
        /// 创建 <see cref="UIWindowDescriptor"/>。
        /// </summary>
        /// <param name="name">界面唯一名称。</param>
        /// <param name="location">Prefab 资源定位路径。</param>
        /// <param name="viewType">界面视图类型，必须继承 <see cref="UIView"/>。</param>
        /// <param name="controllerFactory">Controller 工厂，返回值必须继承 <see cref="UIController"/>。</param>
        /// <param name="layer">显示层级。</param>
        /// <param name="cacheOnClose">关闭时是否进入缓存而不是销毁。</param>
        /// <param name="allowMultiple">是否允许同一个界面存在多份实例。</param>
        public UIWindowDescriptor(
            string name,
            string location,
            Type viewType,
            Func<UIController> controllerFactory,
            UILayer layer = UILayer.Normal,
            bool cacheOnClose = true,
            bool allowMultiple = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("UI 名称不能为空", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Prefab 路径不能为空", nameof(location));
            }

            if (viewType == null || !typeof(UIView).IsAssignableFrom(viewType))
            {
                throw new ArgumentException("视图类型必须继承 UIView", nameof(viewType));
            }

            if (controllerFactory == null)
            {
                throw new ArgumentNullException(nameof(controllerFactory));
            }

            Name = name;
            Location = location;
            ViewType = viewType;
            ControllerFactory = controllerFactory;
            Layer = layer;
            CacheOnClose = cacheOnClose;
            AllowMultiple = allowMultiple;
        }

        /// <summary>
        /// UI 唯一名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Prefab 资源定位路径。
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// 视图脚本类型。
        /// </summary>
        public Type ViewType { get; }

        /// <summary>
        /// Controller 的创建工厂。
        /// </summary>
        public Func<UIController> ControllerFactory { get; }

        /// <summary>
        /// UI 所在层级。
        /// </summary>
        public UILayer Layer { get; }

        /// <summary>
        /// 关闭时是否进入缓存。
        /// </summary>
        public bool CacheOnClose { get; }

        /// <summary>
        /// 是否允许多个实例共存。
        /// </summary>
        public bool AllowMultiple { get; }

        /// <summary>
        /// 使用泛型帮助方法创建描述信息。
        /// </summary>
        public static UIWindowDescriptor Create<TView, TController>(
            string name,
            string location,
            UILayer layer = UILayer.Normal,
            bool cacheOnClose = true,
            bool allowMultiple = false)
            where TView : UIView
            where TController : UIController, new()
        {
            return new UIWindowDescriptor(
                name,
                location,
                typeof(TView),
                () => new TController(),
                layer,
                cacheOnClose,
                allowMultiple);
        }
    }
}

