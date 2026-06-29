using System;
using System.Reflection;

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
        /// <param name="controllerType">界面 Controller 类型，必须继承 <see cref="UIController"/>。</param>
        /// <param name="layer">显示层级。</param>
        /// <param name="cacheOnClose">关闭时是否进入缓存而不是销毁。</param>
        /// <param name="allowMultiple">是否允许同一个界面存在多份实例。</param>
        public UIWindowDescriptor(
            string name,
            string location,
            Type viewType,
            Type controllerType,
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

            if (controllerType == null || !typeof(UIController).IsAssignableFrom(controllerType))
            {
                throw new ArgumentException("Controller 类型必须继承 UIController", nameof(controllerType));
            }

            if (controllerType.IsAbstract || controllerType.IsInterface)
            {
                throw new ArgumentException("Controller 类型不能是抽象类型或接口", nameof(controllerType));
            }

            Name = name;
            Location = location;
            ViewType = viewType;
            ControllerType = controllerType;
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
        /// Controller 类型。
        /// </summary>
        public Type ControllerType { get; }

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
            where TController : UIController
        {
            return new UIWindowDescriptor(
                name,
                location,
                typeof(TView),
                typeof(TController),
                layer,
                cacheOnClose,
                allowMultiple);
        }

        /// <summary>
        /// 使用 View/Controller 命名约定创建描述信息。
        /// </summary>
        public static UIWindowDescriptor Create<TView>(
            string name,
            string location,
            UILayer layer = UILayer.Normal,
            bool cacheOnClose = true,
            bool allowMultiple = false)
            where TView : UIView
        {
            return new UIWindowDescriptor(
                name,
                location,
                typeof(TView),
                UIControllerTypeResolver.ResolveByConvention(typeof(TView)),
                layer,
                cacheOnClose,
                allowMultiple);
        }
    }

    internal static class UIControllerTypeResolver
    {
        private const string ViewSuffix = "View";
        private const string ControllerSuffix = "Controller";

        public static Type ResolveByConvention(Type viewType)
        {
            if (viewType == null)
            {
                throw new ArgumentNullException(nameof(viewType));
            }

            if (!typeof(UIView).IsAssignableFrom(viewType))
            {
                throw new ArgumentException($"View 类型必须继承 {nameof(UIView)}：{viewType.FullName}", nameof(viewType));
            }

            string expectedName = GetExpectedControllerName(viewType);

            Type controllerType = FindInDeclaringType(viewType, expectedName)
                ?? FindByFullName(viewType.Assembly, viewType.Namespace, expectedName)
                ?? FindBySimpleName(viewType.Assembly, expectedName)
                ?? FindInLoadedAssemblies(viewType.Namespace, expectedName);

            if (IsConcreteController(controllerType))
            {
                return controllerType;
            }

            throw new InvalidOperationException(
                $"无法按命名约定为 View {viewType.FullName} 找到 Controller：{expectedName}。请创建对应 Controller，或使用 OpenWindowAsync<TView, TController> 显式指定。");
        }

        private static string GetExpectedControllerName(Type viewType)
        {
            string viewName = viewType.Name;
            int genericMarkIndex = viewName.IndexOf('`');
            if (genericMarkIndex >= 0)
            {
                viewName = viewName[..genericMarkIndex];
            }

            if (viewName.EndsWith(ViewSuffix, StringComparison.Ordinal))
            {
                return viewName[..^ViewSuffix.Length] + ControllerSuffix;
            }

            return viewName + ControllerSuffix;
        }

        private static Type FindInDeclaringType(Type viewType, string expectedName)
        {
            return viewType.DeclaringType?.GetNestedType(
                expectedName,
                BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type FindByFullName(Assembly assembly, string viewNamespace, string expectedName)
        {
            if (assembly == null || string.IsNullOrEmpty(viewNamespace))
            {
                return null;
            }

            return assembly.GetType($"{viewNamespace}.{expectedName}", throwOnError: false);
        }

        private static Type FindBySimpleName(Assembly assembly, string expectedName)
        {
            if (assembly == null)
            {
                return null;
            }

            foreach (Type type in assembly.GetTypes())
            {
                if (type.Name == expectedName && IsConcreteController(type))
                {
                    return type;
                }
            }

            return null;
        }

        private static Type FindInLoadedAssemblies(string viewNamespace, string expectedName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type controllerType = FindByFullName(assembly, viewNamespace, expectedName)
                    ?? FindBySimpleName(assembly, expectedName);

                if (IsConcreteController(controllerType))
                {
                    return controllerType;
                }
            }

            return null;
        }

        private static bool IsConcreteController(Type type)
        {
            return type != null
                && typeof(UIController).IsAssignableFrom(type)
                && !type.IsAbstract
                && !type.IsInterface;
        }
    }
}

