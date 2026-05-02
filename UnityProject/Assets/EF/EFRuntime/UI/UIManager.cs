using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Debugger;
using EF.Model;
using EF.Resource;
using UnityEngine;
using YooAsset;

namespace EF.UI
{
    /// <summary>
    /// 基于 MVC 的 UI 管理器，实现界面生命周期与资源调度。
    /// Model 层数据通过 ModelManager 管理。
    /// </summary>
    public sealed class UIManager : AEFManager, IUIManager
    {
        private readonly IResourceManager _resourceManager;
        private readonly ModelManager _modelManager;
        private readonly Dictionary<string, UIWindowDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<UIWindowInstance>> _activeWindows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Stack<UIWindowInstance>> _cachedWindows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<UILayer, Transform> _layerRoots = new();

        private readonly List<UIWindowInstance> _updateBuffer = new();

        private Transform _fallbackRoot;
        private uint _nextInstanceId = 1;

        /// <summary>
        /// 创建 UI 管理器。
        /// </summary>
        public UIManager(IResourceManager resourceManager, ModelManager modelManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        }

        /// <inheritdoc />
        public int RegisteredWindowCount => _descriptors.Count;

        /// <inheritdoc />
        public int ActiveWindowCount
        {
            get
            {
                int count = 0;
                foreach (List<UIWindowInstance> list in _activeWindows.Values)
                {
                    count += list.Count;
                }

                return count;
            }
        }

        /// <inheritdoc />
        public void RegisterWindow(UIWindowDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (_descriptors.ContainsKey(descriptor.Name))
            {
                throw new InvalidOperationException($"重复注册 UI：{descriptor.Name}");
            }

            _descriptors.Add(descriptor.Name, descriptor);
        }

        /// <inheritdoc />
        public bool UnregisterWindow(string windowName)
        {
            if (string.IsNullOrEmpty(windowName))
            {
                return false;
            }

            if (_activeWindows.TryGetValue(windowName, out List<UIWindowInstance> activeList) && activeList.Count > 0)
            {
                throw new InvalidOperationException($"UI {windowName} 正在运行，无法直接注销");
            }

            bool removedDescriptor = _descriptors.Remove(windowName);
            _cachedWindows.Remove(windowName);
            _activeWindows.Remove(windowName);
            return removedDescriptor;
        }

        /// <inheritdoc />
        public bool Contains(string windowName)
        {
            return _descriptors.ContainsKey(windowName);
        }

        /// <inheritdoc />
        public async UniTask<UIWindowHandle> OpenWindowAsync(string windowName, object userData = null, CancellationToken cancellationToken = default)
        {
            if (!_descriptors.TryGetValue(windowName, out UIWindowDescriptor descriptor))
            {
                throw new InvalidOperationException($"未注册的 UI：{windowName}");
            }

            List<UIWindowInstance> activeList = GetActiveList(windowName);
            if (!descriptor.AllowMultiple)
            {
                for (int index = 0; index < activeList.Count; index++)
                {
                    UIWindowInstance runningInstance = activeList[index];
                    if (runningInstance.State == UIWindowState.Opened)
                    {
                        runningInstance.Controller.InternalRefresh(userData);
                        runningInstance.View.InternalRefresh(userData);
                        return new UIWindowHandle(this, windowName, runningInstance.InstanceId, runningInstance.View, runningInstance.Controller);
                    }

                    if (runningInstance.State is UIWindowState.Loading or UIWindowState.Opening)
                    {
                        throw new InvalidOperationException($"UI {windowName} 正在打开，请稍后再试");
                    }
                }
            }

            UIWindowInstance instance = await CreateOrReuseInstanceAsync(descriptor, userData, cancellationToken);
            RegisterActiveInstance(activeList, instance);
            return new UIWindowHandle(this, windowName, instance.InstanceId, instance.View, instance.Controller);
        }

        /// <inheritdoc />
        public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
            string location,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView
            where TController : UIController, new()
        {
            return OpenWindowAsync<TView, TController>(
                location,
                UILayer.Normal,
                true,  // cacheOnClose = true (默认缓存以提高性能)
                false, // allowMultiple = false (默认单实例模式)
                userData,
                cancellationToken);
        }

        /// <inheritdoc />
        public UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
            string location,
            UILayer layer,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView
            where TController : UIController, new()
        {
            return OpenWindowAsync<TView, TController>(
                location,
                layer,
                true,  // cacheOnClose = true (默认缓存以提高性能)
                false, // allowMultiple = false (默认单实例模式)
                userData,
                cancellationToken);
        }

        /// <inheritdoc />
        public async UniTask<UIWindowHandle> OpenWindowAsync<TView, TController>(
            string location,
            UILayer layer,
            bool cacheOnClose,
            bool allowMultiple,
            object userData = null,
            CancellationToken cancellationToken = default)
            where TView : UIView
            where TController : UIController, new()
        {
            // 使用类型全名作为窗口标识符
            string windowName = typeof(TView).Name;

            // 创建窗口描述符
            var descriptor = new UIWindowDescriptor(
                windowName,
                location,
                typeof(TView),
                () => new TController(),
                layer,
                cacheOnClose,
                allowMultiple);

            // 如果尚未注册，则注册窗口描述符
            if (!_descriptors.ContainsKey(windowName))
            {
                _descriptors.Add(windowName, descriptor);
            }

            List<UIWindowInstance> activeList = GetActiveList(windowName);
            if (!descriptor.AllowMultiple)
            {
                for (int index = 0; index < activeList.Count; index++)
                {
                    UIWindowInstance runningInstance = activeList[index];
                    if (runningInstance.State == UIWindowState.Opened)
                    {
                        runningInstance.Controller.InternalRefresh(userData);
                        runningInstance.View.InternalRefresh(userData);
                        return new UIWindowHandle(this, windowName, runningInstance.InstanceId, runningInstance.View, runningInstance.Controller);
                    }

                    if (runningInstance.State is UIWindowState.Loading or UIWindowState.Opening)
                    {
                        throw new InvalidOperationException($"UI {windowName} 正在打开，请稍后再试");
                    }
                }
            }

            UIWindowInstance instance = await CreateOrReuseInstanceAsync(descriptor, userData, cancellationToken);
            RegisterActiveInstance(activeList, instance);
            return new UIWindowHandle(this, windowName, instance.InstanceId, instance.View, instance.Controller);
        }

        /// <inheritdoc />
        public UniTask CloseWindowAsync(string windowName)
        {
            if (!_activeWindows.TryGetValue(windowName, out List<UIWindowInstance> activeList) || activeList.Count == 0)
            {
                return UniTask.CompletedTask;
            }

            UIWindowInstance instance = activeList[^1];
            CloseWindowInternal(instance);
            activeList.RemoveAt(activeList.Count - 1);
            return UniTask.CompletedTask;
        }

        internal UniTask CloseWindowAsync(uint instanceId)
        {
            if (!TryFindInstance(instanceId, out List<UIWindowInstance> list, out int index))
            {
                return UniTask.CompletedTask;
            }

            UIWindowInstance instance = list[index];
            CloseWindowInternal(instance);
            list.RemoveAt(index);
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask CloseAllAsync()
        {
            List<UIWindowInstance> temp = new();
            foreach (List<UIWindowInstance> list in _activeWindows.Values)
            {
                temp.Clear();
                temp.AddRange(list);
                for (int index = temp.Count - 1; index >= 0; index--)
                {
                    CloseWindowInternal(temp[index]);
                }

                list.Clear();
            }

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public bool TryGetController<TController>(string windowName, out TController controller) where TController : UIController
        {
            controller = null;
            if (!_activeWindows.TryGetValue(windowName, out List<UIWindowInstance> list) || list.Count == 0)
            {
                return false;
            }

            UIController uiController = list[^1].Controller;
            if (uiController is TController typed)
            {
                controller = typed;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryGetView<TView>(string windowName, out TView view) where TView : UIView
        {
            view = null;
            if (!_activeWindows.TryGetValue(windowName, out List<UIWindowInstance> list) || list.Count == 0)
            {
                return false;
            }

            UIView component = list[^1].View;
            if (component is TView typed)
            {
                view = typed;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void RegisterLayerRoot(UILayer layer, Transform rootTransform)
        {
            if (rootTransform == null)
            {
                throw new ArgumentNullException(nameof(rootTransform));
            }

            _layerRoots[layer] = rootTransform;
        }

        /// <inheritdoc />
        public void SetFallbackRoot(Transform fallbackRoot)
        {
            _fallbackRoot = fallbackRoot;
        }

        internal UIWindowState GetWindowState(string windowName, uint instanceId)
        {
            if (TryFindInstance(instanceId, out _, out int index, out UIWindowInstance instance))
            {
                return instance.State;
            }

            if (_cachedWindows.TryGetValue(windowName, out Stack<UIWindowInstance> stack))
            {
                foreach (UIWindowInstance cached in stack)
                {
                    if (cached.InstanceId == instanceId)
                    {
                        return cached.State;
                    }
                }
            }

            return UIWindowState.None;
        }

        /// <inheritdoc />
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            _updateBuffer.Clear();

            foreach (List<UIWindowInstance> list in _activeWindows.Values)
            {
                for (int index = 0; index < list.Count; index++)
                {
                    UIWindowInstance instance = list[index];
                    if (instance.State == UIWindowState.Opened)
                    {
                        _updateBuffer.Add(instance);
                    }
                }
            }

            foreach (UIWindowInstance instance in _updateBuffer)
            {
                instance.Controller.InternalUpdate(elapseSeconds, realElapseSeconds);
                instance.View.InternalUpdate(elapseSeconds, realElapseSeconds);
            }
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            CloseAllAsync();

            foreach (Stack<UIWindowInstance> stack in _cachedWindows.Values)
            {
                while (stack.Count > 0)
                {
                    UIWindowInstance instance = stack.Pop();
                    DisposeInstance(instance);
                }
            }

            _cachedWindows.Clear();
            _activeWindows.Clear();
            _descriptors.Clear();
            _layerRoots.Clear();
            _fallbackRoot = null;
        }

        private async UniTask<UIWindowInstance> CreateOrReuseInstanceAsync(UIWindowDescriptor descriptor, object userData, CancellationToken cancellationToken)
        {
            UIWindowInstance instance = TryPopCache(descriptor);
            if (instance != null)
            {
                Transform layerRoot = ResolveLayerRoot(descriptor.Layer);
                if (layerRoot != null)
                {
                    instance.View.transform.SetParent(layerRoot, false);
                    instance.Context.UpdateLayerRoot(layerRoot);
                }

                instance.State = UIWindowState.Opening;
                instance.Controller.InternalEnter(userData);
                instance.View.InternalOpen(userData);
                instance.Controller.InternalRefresh(userData);
                instance.View.InternalRefresh(userData);
                instance.State = UIWindowState.Opened;
                return instance;
            }

            cancellationToken.ThrowIfCancellationRequested();

            AssetHandle handle = null;
            GameObject viewObject = null;
            UIController controller = null;
            UIView viewComponent = null;
            try
            {
                instance = new UIWindowInstance(GetNextInstanceId(), descriptor);
                instance.State = UIWindowState.Loading;

                handle = await _resourceManager.LoadAssetAsync<GameObject>(descriptor.Location, null, 0);
                cancellationToken.ThrowIfCancellationRequested();

                GameObject prefab = handle.AssetObject as GameObject;
                if (prefab == null)
                {
                    throw new InvalidOperationException($"UI {descriptor.Name} 资源不是有效的 Prefab");
                }

                Transform layerRoot = ResolveLayerRoot(descriptor.Layer);
                viewObject = UnityEngine.Object.Instantiate(prefab, layerRoot);
                viewObject.name = $"{descriptor.Name}_Instance";
                viewObject.SetActive(false);

                if (!TryResolveView(descriptor, viewObject, out UIView view))
                {
                    UnityEngine.Object.Destroy(viewObject);
                    throw new InvalidOperationException($"UI {descriptor.Name} 缺少视图脚本 {descriptor.ViewType.Name}");
                }

                viewComponent = view;

                controller = descriptor.ControllerFactory();
                if (controller == null)
                {
                    UnityEngine.Object.Destroy(viewObject);
                    throw new InvalidOperationException($"UI {descriptor.Name} 创建 Controller 失败");
                }

                var context = new UIRuntimeContext(this, _modelManager, descriptor, layerRoot);

                instance.Attach(view, controller, handle, context);

                view.InternalInitialize(context);
                controller.InternalInitialize(view, context);

                await controller.InternalPrepareAsync(userData, cancellationToken);
                await view.InternalPrepareAsync(userData, cancellationToken);

                controller.InternalEnter(userData);
                view.InternalOpen(userData);
                controller.InternalRefresh(userData);
                view.InternalRefresh(userData);
                instance.State = UIWindowState.Opened;
                return instance;
            }
            catch
            {
                if (handle != null)
                {
                    _resourceManager.Release(handle);
                }

                if (viewComponent != null)
                {
                    try
                    {
                        viewComponent.InternalRelease();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                if (viewObject != null)
                {
                    UnityEngine.Object.Destroy(viewObject);
                }

                if (controller != null)
                {
                    try
                    {
                        controller.InternalRelease();
                        controller.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                throw;
            }
        }

        private void CloseWindowInternal(UIWindowInstance instance)
        {
            if (instance.State is UIWindowState.Closing or UIWindowState.Closed or UIWindowState.Destroyed)
            {
                return;
            }

            instance.State = UIWindowState.Closing;
            instance.Controller.InternalExit();
            instance.View.InternalClose();

            if (instance.Descriptor.CacheOnClose)
            {
                instance.View.gameObject.SetActive(false);
                instance.State = UIWindowState.Closed;
                PushCache(instance);
                return;
            }

            DisposeInstance(instance);
            instance.State = UIWindowState.Destroyed;
        }

        private void DisposeInstance(UIWindowInstance instance)
        {
            if (instance.View != null)
            {
                instance.View.InternalRelease();
                UnityEngine.Object.Destroy(instance.View.gameObject);
            }

            if (instance.Controller != null)
            {
                instance.Controller.InternalRelease();
                instance.Controller.Dispose();
            }

            if (instance.AssetHandle != null)
            {
                _resourceManager.Release(instance.AssetHandle);
            }

            instance.Clear();
        }

        private void RegisterActiveInstance(List<UIWindowInstance> list, UIWindowInstance instance)
        {
            list.Add(instance);
        }

        private Transform ResolveLayerRoot(UILayer layer)
        {
            if (_layerRoots.TryGetValue(layer, out Transform root))
            {
                return root;
            }

            return _fallbackRoot;
        }

        private UIWindowInstance TryPopCache(UIWindowDescriptor descriptor)
        {
            if (!_cachedWindows.TryGetValue(descriptor.Name, out Stack<UIWindowInstance> stack) || stack.Count == 0)
            {
                return null;
            }

            return stack.Pop();
        }

        private void PushCache(UIWindowInstance instance)
        {
            Stack<UIWindowInstance> stack = GetCacheStack(instance.Descriptor.Name);
            stack.Push(instance);
        }

        private Stack<UIWindowInstance> GetCacheStack(string windowName)
        {
            if (!_cachedWindows.TryGetValue(windowName, out Stack<UIWindowInstance> stack))
            {
                stack = new Stack<UIWindowInstance>();
                _cachedWindows[windowName] = stack;
            }

            return stack;
        }

        private List<UIWindowInstance> GetActiveList(string windowName)
        {
            if (!_activeWindows.TryGetValue(windowName, out List<UIWindowInstance> list))
            {
                list = new List<UIWindowInstance>();
                _activeWindows[windowName] = list;
            }

            return list;
        }

        private bool TryResolveView(UIWindowDescriptor descriptor, GameObject viewObject, out UIView view)
        {
            Component component = viewObject.GetComponent(descriptor.ViewType);
            
            // 如果没有找到对应的脚本组件，尝试动态添加
            if (component == null)
            {
                Log.Info($"UI {descriptor.Name} Prefab缺少 {descriptor.ViewType.Name} 脚本，正在动态添加...");
                component = viewObject.AddComponent(descriptor.ViewType);
            }
            
            view = component as UIView;
            return view != null;
        }

        private bool TryFindInstance(uint instanceId, out List<UIWindowInstance> list, out int index)
        {
            foreach (List<UIWindowInstance> candidateList in _activeWindows.Values)
            {
                for (int i = 0; i < candidateList.Count; i++)
                {
                    if (candidateList[i].InstanceId == instanceId)
                    {
                        list = candidateList;
                        index = i;
                        return true;
                    }
                }
            }

            list = null;
            index = -1;
            return false;
        }

        private bool TryFindInstance(uint instanceId, out List<UIWindowInstance> list, out int index, out UIWindowInstance instance)
        {
            if (TryFindInstance(instanceId, out list, out index))
            {
                instance = list[index];
                return true;
            }

            instance = null;
            return false;
        }

        private uint GetNextInstanceId()
        {
            if (_nextInstanceId == uint.MaxValue)
            {
                _nextInstanceId = 1;
            }

            return _nextInstanceId++;
        }

        private sealed class UIWindowInstance
        {
            public UIWindowInstance(uint instanceId, UIWindowDescriptor descriptor)
            {
                InstanceId = instanceId;
                Descriptor = descriptor;
            }

            public uint InstanceId { get; }

            public UIWindowDescriptor Descriptor { get; }

            public UIView View { get; private set; }

            public UIController Controller { get; private set; }

            public AssetHandle AssetHandle { get; private set; }

            public UIRuntimeContext Context { get; private set; }

            public UIWindowState State { get; set; }

            public void Attach(UIView view, UIController controller, AssetHandle handle, UIRuntimeContext context)
            {
                View = view;
                Controller = controller;
                AssetHandle = handle;
                Context = context;
            }

            public void Clear()
            {
                View = null;
                Controller = null;
                AssetHandle = null;
                Context = null;
                State = UIWindowState.None;
            }
        }
    }
}
