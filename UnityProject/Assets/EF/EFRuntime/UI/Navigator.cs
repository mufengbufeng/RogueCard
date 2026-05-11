using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Resource;
using UnityEngine;
using UnityEngine.UIElements;

namespace EF.UI
{
    /// <summary>
    /// 导航服务。按类型 / 字符串名打开 Screen 或 Popup，
    /// 通过命名约定（<see cref="Screen.UxmlLocation"/> / <see cref="Screen.UssLocation"/>）解析资源，
    /// 通过基类继承关系（<see cref="Popup{TViewModel}"/>）分流到 PopupLayer。
    /// </summary>
    public sealed class Navigator : INavigator
    {
        private readonly Shell _shell;
        private readonly IResourceManager _resources;

        private Screen _currentScreen;
        private readonly Stack<PopupEntry> _popupStack = new();

        // 字符串名 → Screen 派生类型的反射缓存（首次按字符串打开时全程序集扫描后填充）
        private readonly ConcurrentDictionary<string, Type> _typeCache = new(StringComparer.Ordinal);
        private bool _typeCacheBuilt;
        private readonly object _typeCacheLock = new();

        /// <summary>
        /// 创建导航服务。
        /// </summary>
        /// <param name="shell">UI 层级容器。</param>
        /// <param name="resources">资源管理器。</param>
        public Navigator(Shell shell, IResourceManager resources)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        /// <inheritdoc />
        public UniTask OpenAsync<TScreen>(ViewModelBase viewModel = null, CancellationToken cancellationToken = default)
            where TScreen : Screen, new()
        {
            return OpenInternalAsync(typeof(TScreen), viewModel, cancellationToken);
        }

        /// <inheritdoc />
        public UniTask OpenAsync(string viewName, ViewModelBase viewModel = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(viewName))
                throw new ArgumentException("viewName 不能为空", nameof(viewName));

            var screenType = ResolveScreenType(viewName);
            return OpenInternalAsync(screenType, viewModel, cancellationToken);
        }

        /// <inheritdoc />
        public void Close()
        {
            if (_popupStack.Count == 0) return;
            var entry = _popupStack.Pop();
            DisposePopupEntry(entry);
        }

        /// <inheritdoc />
        public void CloseAll()
        {
            while (_popupStack.Count > 0)
            {
                var entry = _popupStack.Pop();
                DisposePopupEntry(entry);
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // 关闭所有 Popup
            while (_popupStack.Count > 0)
            {
                var entry = _popupStack.Pop();
                try { entry.Popup.OnHide(); } catch { }
                try { entry.Popup.OnDispose(); } catch { }
                entry.Overlay.RemoveFromHierarchy();
            }

            // 关闭当前 Screen
            if (_currentScreen != null)
            {
                try { _currentScreen.OnHide(); } catch { }
                try { _currentScreen.OnDispose(); } catch { }
                _currentScreen = null;
            }

            _shell.ScreenLayer.Clear();
            _shell.PopupLayer.Clear();
            _shell.SystemLayer.Clear();
        }

        // ── 核心打开流程 ──

        private async UniTask OpenInternalAsync(Type screenType, ViewModelBase viewModel, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (screenType == null) throw new ArgumentNullException(nameof(screenType));
            if (screenType.IsAbstract)
                throw new InvalidOperationException($"Screen 类型 {screenType.FullName} 是抽象类，无法实例化。");

            // 实例化 Screen / Popup
            var screen = (Screen)Activator.CreateInstance(screenType);
            var isPopup = IsSubclassOfRawGeneric(screenType, typeof(Popup<>));

            // 加载 UXML（必需）
            var uxmlLocation = screen.UxmlLocation;
            var uxmlHandle = await _resources.LoadAssetAsync<VisualTreeAsset>(uxmlLocation);
            cancellationToken.ThrowIfCancellationRequested();

            if (uxmlHandle == null || uxmlHandle.AssetObject is not VisualTreeAsset vta)
            {
                throw new InvalidOperationException(
                    $"Screen {screenType.Name} 加载 UXML 资源失败：location=\"{uxmlLocation}\"。"
                    + $"请检查 YooAsset 中 addressable 是否正确，或在 Screen 子类中 override UxmlLocation。");
            }
            screen.LoadContent(vta);

            // 加载 USS（可选，缺失降级警告）
            var ussLocation = screen.UssLocation;
            if (!string.IsNullOrEmpty(ussLocation))
            {
                AssetHandleSafeLoadResult ussLoad;
                try
                {
                    var ussHandle = await _resources.LoadAssetAsync<StyleSheet>(ussLocation);
                    cancellationToken.ThrowIfCancellationRequested();
                    ussLoad = new AssetHandleSafeLoadResult(ussHandle?.AssetObject as StyleSheet, null);
                }
                catch (Exception ussEx)
                {
                    ussLoad = new AssetHandleSafeLoadResult(null, ussEx);
                }

                if (ussLoad.StyleSheet != null)
                {
                    screen.AttachStyleSheet(ussLoad.StyleSheet);
                }
                else
                {
                    WarnUssMissing(screenType, ussLocation, ussLoad.Exception);
                }
            }

            // 解析并准备 ViewModel
            var viewModelType = ResolveViewModelType(screenType);
            var vm = viewModel ?? CreateViewModel(viewModelType);
            if (vm.GetType() != viewModelType && !viewModelType.IsAssignableFrom(vm.GetType()))
            {
                throw new ArgumentException(
                    $"Screen {screenType.Name} 期望的 ViewModel 类型为 {viewModelType.Name}，"
                    + $"但收到 {vm.GetType().Name}。", nameof(viewModel));
            }

            // 按类型分流：Popup 入栈，Screen 替换
            if (isPopup)
            {
                MountPopup(screen, vm);
            }
            else
            {
                MountScreen(screen, vm);
            }
        }

        private void MountScreen(Screen screen, ViewModelBase viewModel)
        {
            // 先关闭当前 Screen
            if (_currentScreen != null)
            {
                _currentScreen.OnHide();
                _currentScreen.OnDispose();
                _currentScreen = null;
            }

            _shell.ScreenLayer.Add(screen);
            screen.Setup(viewModel);
            screen.OnShow();
            _currentScreen = screen;
        }

        private void MountPopup(Screen popup, ViewModelBase viewModel)
        {
            VisualElement overlay = null;
            try
            {
                overlay = CreateOverlay();
                _shell.PopupLayer.Add(overlay);
                _shell.PopupLayer.Add(popup);

                popup.Setup(viewModel);
                popup.OnShow();

                _popupStack.Push(new PopupEntry(popup, overlay, viewModel));
            }
            catch
            {
                // 回滚已添加到 PopupLayer 的元素
                overlay?.RemoveFromHierarchy();
                popup.RemoveFromHierarchy();
                throw;
            }
        }

        private static void DisposePopupEntry(PopupEntry entry)
        {
            try { entry.Popup.OnHide(); } catch { }
            try { entry.Popup.OnDispose(); } catch { }
            entry.Overlay.RemoveFromHierarchy();
        }

        // ── 反射工具 ──

        /// <summary>
        /// 沿继承链查找 <see cref="Screen{TViewModel}"/> 闭合泛型并取其类型参数。
        /// </summary>
        private static Type ResolveViewModelType(Type screenType)
        {
            for (var t = screenType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType)
                {
                    var def = t.GetGenericTypeDefinition();
                    if (def == typeof(Screen<>) || def == typeof(Popup<>))
                    {
                        return t.GetGenericArguments()[0];
                    }
                }
            }

            throw new InvalidOperationException(
                $"无法从 Screen 类型 {screenType.FullName} 推断 ViewModel 类型——"
                + "未找到 Screen<TViewModel> 或 Popup<TViewModel> 闭合泛型基类。");
        }

        private static ViewModelBase CreateViewModel(Type viewModelType)
        {
            try
            {
                return (ViewModelBase)Activator.CreateInstance(viewModelType);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"自动创建 ViewModel {viewModelType.FullName} 失败——"
                    + "可能缺少无参公开构造函数。请由调用方显式创建并通过 OpenAsync 传入。", e);
            }
        }

        /// <summary>
        /// 判断 <paramref name="toCheck"/> 是否派生自 <paramref name="generic"/> 的某个闭合形式。
        /// 例如：判断 SettingsView 是否派生自 Popup&lt;&gt;。
        /// </summary>
        private static bool IsSubclassOfRawGeneric(Type toCheck, Type generic)
        {
            for (var t = toCheck; t != null && t != typeof(object); t = t.BaseType)
            {
                var cur = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
                if (cur == generic) return true;
            }
            return false;
        }

        // ── 字符串名 → Type 反射缓存 ──

        // 同名冲突表：key=类名，value=所有同名候选的全名列表。
        // 冲突类型 SHALL NOT 出现在 _typeCache 中；命中冲突时直接抛 InvalidOperationException 提示用类型重载。
        private readonly ConcurrentDictionary<string, string[]> _conflictMap = new(StringComparer.Ordinal);

        private Type ResolveScreenType(string viewName)
        {
            EnsureTypeCacheBuilt();

            if (_conflictMap.TryGetValue(viewName, out var conflicts))
            {
                throw new InvalidOperationException(
                    $"名为 \"{viewName}\" 的 Screen 派生类型存在多个同名定义："
                    + string.Join(", ", conflicts)
                    + "。请改用 OpenAsync<TScreen>() 类型重载消除歧义。");
            }

            if (_typeCache.TryGetValue(viewName, out var type))
            {
                return type;
            }

            throw new KeyNotFoundException(
                $"未在已加载程序集中找到名为 \"{viewName}\" 的 Screen 派生类型。"
                + "请检查类名拼写、所在程序集是否已加载（HybridCLR 热更新程序集需在 Init 后才可见），"
                + "或改用 OpenAsync<TScreen>() 类型重载。");
        }

        private void EnsureTypeCacheBuilt()
        {
            if (_typeCacheBuilt) return;

            lock (_typeCacheLock)
            {
                if (_typeCacheBuilt) return;
                BuildTypeCache();
                _typeCacheBuilt = true;
            }
        }

        private void BuildTypeCache()
        {
            // 暂存所有同名候选：key=类名，value=候选 Type 列表
            var byName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    types = rtle.Types?.Where(t => t != null).ToArray() ?? Array.Empty<Type>();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null) continue;
                    if (type.IsAbstract) continue;
                    if (!typeof(Screen).IsAssignableFrom(type)) continue;
                    // 必须有公开无参构造（new() 约束）
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    var name = type.Name;
                    if (!byName.TryGetValue(name, out var list))
                    {
                        list = new List<Type>();
                        byName[name] = list;
                    }
                    if (!list.Contains(type)) list.Add(type);
                }
            }

            foreach (var pair in byName)
            {
                if (pair.Value.Count == 1)
                {
                    _typeCache[pair.Key] = pair.Value[0];
                }
                else
                {
                    _conflictMap[pair.Key] = pair.Value.Select(t => t.FullName).ToArray();
                }
            }
        }

        // ── 杂项 ──

        private static VisualElement CreateOverlay()
        {
            var overlay = new VisualElement { name = "popup-overlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            return overlay;
        }

        // 仅警告同一个 (类型, 资源名) 组合一次，避免重复打开同一 Screen 时刷屏
        private readonly HashSet<string> _ussWarnedKeys = new(StringComparer.Ordinal);

        private void WarnUssMissing(Type screenType, string ussLocation, Exception cause)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var key = screenType.FullName + "|" + ussLocation;
            if (!_ussWarnedKeys.Add(key)) return;

            var detail = cause != null ? $"，原因：{cause.GetType().Name}: {cause.Message}" : string.Empty;
            Log.Warning(
                $"[Navigator] {screenType.Name} 未找到约定 USS 资源 \"{ussLocation}\"——"
                + $"将仅依赖 UXML 内嵌 <Style> / 全局共享样式{detail}。"
                + "若该 Screen 不需要独立 USS，可忽略此警告；"
                + "否则请检查资源名是否与 {Stem}Uss 约定一致或 override Screen.UssLocation。");
#endif
        }

        private readonly struct AssetHandleSafeLoadResult
        {
            public StyleSheet StyleSheet { get; }
            public Exception Exception { get; }

            public AssetHandleSafeLoadResult(StyleSheet styleSheet, Exception exception)
            {
                StyleSheet = styleSheet;
                Exception = exception;
            }
        }

        private readonly struct PopupEntry
        {
            public Screen Popup { get; }
            public VisualElement Overlay { get; }
            public ViewModelBase ViewModel { get; }

            public PopupEntry(Screen popup, VisualElement overlay, ViewModelBase viewModel)
            {
                Popup = popup;
                Overlay = overlay;
                ViewModel = viewModel;
            }
        }
    }
}
