using System;
using System.Collections.Generic;

namespace EF.UI
{
    /// <summary>
    /// Screen 注册描述信息。
    /// </summary>
    public sealed class ScreenDescriptor
    {
        /// <summary>
        /// Screen 唯一名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// UXML 资源定位路径。
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Screen 类型（必须继承 Screen&lt;TViewModel&gt;）。
        /// </summary>
        public Type ScreenType { get; }

        /// <summary>
        /// ViewModel 类型（必须继承 ViewModelBase）。
        /// </summary>
        public Type ViewModelType { get; }

        /// <summary>
        /// 是否为弹窗类型（弹窗使用栈式管理）。
        /// </summary>
        public bool IsPopup { get; }

        /// <summary>
        /// 创建 Screen 描述信息。
        /// </summary>
        public ScreenDescriptor(string name, string location, Type screenType, Type viewModelType, bool isPopup = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Location = location ?? throw new ArgumentNullException(nameof(location));
            ScreenType = screenType ?? throw new ArgumentNullException(nameof(screenType));
            ViewModelType = viewModelType ?? throw new ArgumentNullException(nameof(viewModelType));
            IsPopup = isPopup;
        }
    }

    /// <summary>
    /// Screen 注册表。集中管理 Screen 名称到描述信息的映射。
    /// </summary>
    public sealed class ScreenRegistry
    {
        private readonly Dictionary<string, ScreenDescriptor> _screens =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册一个 Screen 及其对应的 ViewModel 类型。
        /// </summary>
        public void Register<TScreen, TViewModel>(string name, string uxmlLocation, bool isPopup = false)
            where TScreen : Screen<TViewModel>, new()
            where TViewModel : ViewModelBase
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Screen 名称不能为空", nameof(name));

            if (_screens.ContainsKey(name))
                throw new InvalidOperationException($"重复注册 Screen：{name}");

            _screens[name] = new ScreenDescriptor(name, uxmlLocation, typeof(TScreen), typeof(TViewModel), isPopup);
        }

        /// <summary>
        /// 查询指定名称的 Screen 描述信息。
        /// </summary>
        public ScreenDescriptor Get(string name)
        {
            if (_screens.TryGetValue(name, out var descriptor))
                return descriptor;

            throw new KeyNotFoundException($"未注册的 Screen：{name}");
        }
    }
}
