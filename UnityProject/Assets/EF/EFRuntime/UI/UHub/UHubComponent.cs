using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EF.UI
{
    /// <summary>
    /// UHub 组件
    /// 为 UIView 提供自动组件绑定和统一事件管理功能
    /// </summary>
    public class UHubComponent
    {
        private readonly UIView _owner;
        private readonly ComponentBinder _binder;
        private readonly List<IEventBinding> _eventBindings = new List<IEventBinding>();
        private bool _isInitialized;
        private bool _isDisposed;

        /// <summary>
        /// 绑定配置
        /// </summary>
        public UHubBindingConfig Config { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 已绑定的事件数量
        /// </summary>
        public int EventBindingCount => _eventBindings.Count;

        /// <summary>
        /// 创建 UHub 组件实例
        /// </summary>
        /// <param name="owner">所属的 UIView</param>
        /// <param name="config">绑定配置，如果为 null 将尝试加载默认配置</param>
        public UHubComponent(UIView owner, UHubBindingConfig config = null)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Config = config ?? LoadDefaultConfig();
            _binder = new ComponentBinder(Config);
        }

        /// <summary>
        /// 初始化 UHub 组件
        /// 执行自动组件绑定
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized || _isDisposed)
                return;

            try
            {
                // 自动绑定组件
                BindComponents();
                _isInitialized = true;
                
                Debug.Log($"[UHub] {_owner.GetType().Name} UHub 组件初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] {_owner.GetType().Name} UHub 组件初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 绑定 Unity 事件 (无参数)
        /// </summary>
        /// <param name="unityEvent">Unity 事件</param>
        /// <param name="action">回调方法</param>
        public void BindEvent(UnityEvent unityEvent, UnityAction action)
        {
            if (unityEvent == null || action == null)
            {
                Debug.LogWarning("[UHub] 事件或回调方法不能为空");
                return;
            }

            var binding = new UnityEventBinding(unityEvent, action);
            RegisterEventBinding(binding);
        }

        /// <summary>
        /// 绑定 Unity 事件 (bool 参数)
        /// </summary>
        public void BindEvent(UnityEvent<bool> unityEvent, UnityAction<bool> action)
        {
            if (unityEvent == null || action == null)
            {
                Debug.LogWarning("[UHub] 事件或回调方法不能为空");
                return;
            }

            var binding = new UnityEventBinding(unityEvent, action);
            RegisterEventBinding(binding);
        }

        /// <summary>
        /// 绑定 Unity 事件 (float 参数)
        /// </summary>
        public void BindEvent(UnityEvent<float> unityEvent, UnityAction<float> action)
        {
            if (unityEvent == null || action == null)
            {
                Debug.LogWarning("[UHub] 事件或回调方法不能为空");
                return;
            }

            var binding = new UnityEventBinding(unityEvent, action);
            RegisterEventBinding(binding);
        }

        /// <summary>
        /// 绑定 Unity 事件 (int 参数)
        /// </summary>
        public void BindEvent(UnityEvent<int> unityEvent, UnityAction<int> action)
        {
            if (unityEvent == null || action == null)
            {
                Debug.LogWarning("[UHub] 事件或回调方法不能为空");
                return;
            }

            var binding = new UnityEventBinding(unityEvent, action);
            RegisterEventBinding(binding);
        }

        /// <summary>
        /// 绑定 Unity 事件 (string 参数)
        /// </summary>
        public void BindEvent(UnityEvent<string> unityEvent, UnityAction<string> action)
        {
            if (unityEvent == null || action == null)
            {
                Debug.LogWarning("[UHub] 事件或回调方法不能为空");
                return;
            }

            var binding = new UnityEventBinding(unityEvent, action);
            RegisterEventBinding(binding);
        }

        /// <summary>
        /// 绑定 C# 事件
        /// </summary>
        /// <param name="subscribeAction">订阅操作 (例如: () => model.PropertyChanged += handler)</param>
        /// <param name="unsubscribeAction">取消订阅操作 (例如: () => model.PropertyChanged -= handler)</param>
        public void BindEvent(Action subscribeAction, Action unsubscribeAction)
        {
            if (subscribeAction == null || unsubscribeAction == null)
            {
                Debug.LogWarning("[UHub] 订阅和取消订阅操作不能为空");
                return;
            }

            var binding = new ActionEventBinding(subscribeAction, unsubscribeAction);
            RegisterEventBinding(binding);
        }

        /// <summary>
        /// 运行时覆盖绑定规则
        /// </summary>
        /// <param name="suffix">后缀名</param>
        /// <param name="componentType">组件类型</param>
        public void OverrideRule(string suffix, Type componentType)
        {
            _binder.OverrideRule(suffix, componentType);
        }

        /// <summary>
        /// 获取已绑定的组件 (通过 ComponentBinder 绑定的组件)
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="fieldName">字段名称</param>
        /// <returns>组件实例，如果未找到返回 null</returns>
        public T GetComponent<T>(string fieldName) where T : UnityEngine.Object
        {
            // 这里可以扩展为缓存已绑定的组件
            var referenceCollector = _owner.GetComponent<ReferenceCollector>();
            if (referenceCollector == null)
                return null;

            try
            {
                return referenceCollector.Get<T>(fieldName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                // 清理所有事件绑定
                ClearAllBindings();
                _isDisposed = true;
                
                Debug.Log($"[UHub] {_owner?.GetType().Name} UHub 组件已释放");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] {_owner?.GetType().Name} UHub 组件释放失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动绑定组件
        /// </summary>
        private void BindComponents()
        {
            var referenceCollector = _owner.GetComponent<ReferenceCollector>();
            if (referenceCollector == null)
            {
                Debug.LogWarning($"[UHub] {_owner.GetType().Name} 缺少 ReferenceCollector 组件，无法执行自动绑定");
                return;
            }

            var bindCount = _binder.BindComponents(_owner, referenceCollector);
            if (bindCount > 0)
            {
                Debug.Log($"[UHub] {_owner.GetType().Name} 自动绑定了 {bindCount} 个组件");
            }
        }

        /// <summary>
        /// 注册事件绑定
        /// </summary>
        /// <param name="binding">事件绑定</param>
        private void RegisterEventBinding(IEventBinding binding)
        {
            if (binding == null)
                return;

            try
            {
                binding.Bind();
                _eventBindings.Add(binding);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UHub] 事件绑定失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理所有事件绑定
        /// </summary>
        private void ClearAllBindings()
        {
            foreach (var binding in _eventBindings)
            {
                try
                {
                    binding?.Unbind();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UHub] 事件解绑失败: {ex.Message}");
                }
            }

            _eventBindings.Clear();
        }

        /// <summary>
        /// 加载默认配置
        /// </summary>
        private UHubBindingConfig LoadDefaultConfig()
        {
            try
            {
                // 尝试加载默认配置文件
                var configs = Resources.LoadAll<UHubBindingConfig>("UHub");
                if (configs != null && configs.Length > 0)
                {
                    return configs[0];
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UHub] 加载默认配置失败: {ex.Message}");
            }

            // 返回 null，使用内置默认规则
            return null;
        }
    }
}