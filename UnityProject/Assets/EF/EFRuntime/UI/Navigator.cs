using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Resource;
using UnityEngine;
using UnityEngine.UIElements;

namespace EF.UI
{
    /// <summary>
    /// 导航服务。管理 Screen 内容替换和 Popup 栈式导航。
    /// </summary>
    public sealed class Navigator : INavigator
    {
        private readonly Shell _shell;
        private readonly ScreenRegistry _registry;
        private readonly IResourceManager _resources;

        private Screen _currentScreen;
        private readonly Stack<PopupEntry> _popupStack = new();

        /// <summary>
        /// 创建导航服务。
        /// </summary>
        public Navigator(Shell shell, ScreenRegistry registry, IResourceManager resources)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        /// <inheritdoc />
        public async UniTask NavigateToAsync(string screenName, ViewModelBase viewModel, CancellationToken cancellationToken = default)
        {
            var descriptor = _registry.Get(screenName);

            // 关闭当前 Screen（OnDispose 内部已自脱树并 Dispose ViewModel）
            if (_currentScreen != null)
            {
                _currentScreen.OnHide();
                _currentScreen.OnDispose();
                _currentScreen = null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 加载 UXML 资源
            var handle = await _resources.LoadAssetAsync<VisualTreeAsset>(descriptor.Location);
            cancellationToken.ThrowIfCancellationRequested();

            if (handle == null)
                throw new InvalidOperationException($"Screen {screenName} 资源加载失败：handle 为 null，location={descriptor.Location}");

            var vta = handle.AssetObject as VisualTreeAsset;
            if (vta == null)
                throw new InvalidOperationException($"Screen {screenName} 资源不是有效的 VisualTreeAsset：location={descriptor.Location}");

            // 创建 Screen 实例（通过非泛型 Screen 引用，避免泛型协变问题）
            var screen = (Screen)Activator.CreateInstance(descriptor.ScreenType);
            screen.LoadContent(vta);

            // 挂到树 + 注入 ViewModel
            _shell.ScreenLayer.Add(screen);
            screen.Setup(viewModel);
            screen.OnShow();

            _currentScreen = screen;
        }

        /// <inheritdoc />
        public async UniTask PushPopupAsync(string popupName, ViewModelBase viewModel, CancellationToken cancellationToken = default)
        {
            var descriptor = _registry.Get(popupName);

            cancellationToken.ThrowIfCancellationRequested();

            VisualElement overlay = null;
            Screen popup = null;

            try
            {
                var handle = await _resources.LoadAssetAsync<VisualTreeAsset>(descriptor.Location);
                cancellationToken.ThrowIfCancellationRequested();

                if (handle == null)
                    throw new InvalidOperationException($"弹窗 {popupName} 资源加载失败：handle 为 null，location={descriptor.Location}");

                var vta = handle.AssetObject as VisualTreeAsset;
                if (vta == null)
                    throw new InvalidOperationException($"弹窗 {popupName} 资源不是有效的 VisualTreeAsset：location={descriptor.Location}");

                popup = (Screen)Activator.CreateInstance(descriptor.ScreenType);
                popup.LoadContent(vta);

                overlay = CreateOverlay();
                _shell.PopupLayer.Add(overlay);
                _shell.PopupLayer.Add(popup);

                popup.Setup(viewModel);
                popup.OnShow();

                _popupStack.Push(new PopupEntry(popup, overlay, viewModel));
            }
            catch
            {
                // 异常路径：回滚已添加到 PopupLayer 的元素，避免悬挂
                overlay?.RemoveFromHierarchy();
                popup?.RemoveFromHierarchy();
                throw;
            }
        }

        /// <inheritdoc />
        public void PopPopup()
        {
            if (_popupStack.Count == 0) return;

            var entry = _popupStack.Pop();
            entry.Popup.OnHide();
            entry.Popup.OnDispose();
            entry.Overlay.RemoveFromHierarchy();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // 关闭所有弹窗（OnDispose 内部 Dispose ViewModel）
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
