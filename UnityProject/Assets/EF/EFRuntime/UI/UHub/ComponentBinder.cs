using System;
using System.Collections.Generic;
using System.Reflection;
using EF.Debugger;
using UnityEngine;

namespace EF.UI
{
    /// <summary>
    /// 组件绑定器
    /// 负责通过反射和命名规范自动绑定 UIView 中的组件引用
    /// </summary>
    public class ComponentBinder
    {
        private readonly UHubBindingConfig _config;
        private readonly Dictionary<string, Type> _runtimeOverrides = new Dictionary<string, Type>();

        /// <summary>
        /// 创建组件绑定器
        /// </summary>
        /// <param name="config">绑定配置，如果为 null 将使用默认配置</param>
        public ComponentBinder(UHubBindingConfig config = null)
        {
            _config = config;
        }

        /// <summary>
        /// 为指定的 UIView 绑定组件引用
        /// </summary>
        /// <param name="view">目标 UIView 实例</param>
        /// <param name="referenceCollector">组件引用收集器</param>
        /// <returns>绑定成功的组件数量</returns>
        public int BindComponents(UIView view, ReferenceCollector referenceCollector)
        {
            return BindComponents((object)view, referenceCollector);
        }

        /// <summary>
        /// 为指定目标对象绑定组件引用。
        /// 支持任何普通对象，不局限于 UIView。
        /// </summary>
        /// <param name="target">目标对象实例。</param>
        /// <param name="referenceCollector">组件引用收集器。</param>
        /// <returns>绑定成功的组件数量。</returns>
        public int BindComponents(object target, ReferenceCollector referenceCollector)
        {
            if (target == null)
            {
                LogError("目标对象实例不能为空");
                return 0;
            }

            if (referenceCollector == null)
            {
                LogMessage("ReferenceCollector 不存在，跳过组件绑定", BindingFailureMode.Warning);
                return 0;
            }

            int bindCount = 0;
            var targetType = target.GetType();

            // 先获取 ReferenceCollector 中所有可用的组件
            var availableComponents = GetAvailableComponents(referenceCollector);
            if (availableComponents.Count == 0)
            {
                LogMessage("ReferenceCollector 中没有可用组件", BindingFailureMode.Warning);
                return 0;
            }

            // 然后通过反射查找需要绑定的字段和属性
            bindCount += BindFieldsWithAvailableComponents(target, targetType, availableComponents);
            // bindCount += BindPropertiesWithAvailableComponents(target, targetType, availableComponents);

            LogMessage($"[UHub] {targetType.Name} 组件绑定完成，成功绑定 {bindCount} 个组件", BindingFailureMode.Silent);
            return bindCount;
        }

        /// <summary>
        /// 运行时添加或覆盖绑定规则
        /// </summary>
        /// <param name="suffix">后缀名</param>
        /// <param name="componentType">组件类型</param>
        public void OverrideRule(string suffix, Type componentType)
        {
            if (_config != null && !_config.AllowRuntimeOverride)
            {
                LogError("当前配置不允许运行时覆盖规则");
                return;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                LogError("后缀名不能为空");
                return;
            }

            if (componentType == null)
            {
                LogError("组件类型不能为空");
                return;
            }

            _runtimeOverrides[suffix] = componentType;
        }

        /// <summary>
        /// 绑定字段
        /// </summary>
        private int BindFields(UIView view, Type viewType, ReferenceCollector referenceCollector)
        {
            int bindCount = 0;
            var fields = viewType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                // 检查是否标记为忽略
                if (field.GetCustomAttribute<UHubIgnoreAttribute>() != null)
                    continue;

                // 尝试绑定字段
                if (TryBindField(view, field, referenceCollector))
                    bindCount++;
            }

            return bindCount;
        }

        /// <summary>
        /// 获取 ReferenceCollector 中所有可用的组件
        /// </summary>
        private Dictionary<string, UnityEngine.Object> GetAvailableComponents(ReferenceCollector referenceCollector)
        {
            var components = new Dictionary<string, UnityEngine.Object>();

            try
            {
                // 直接访问 ReferenceCollector 的 data 字段
                if (referenceCollector.data != null)
                {
                    foreach (var item in referenceCollector.data)
                    {
                        if (!string.IsNullOrEmpty(item.key) && item.gameObject != null)
                        {
                            components[item.key] = item.gameObject;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"获取 ReferenceCollector 组件列表失败: {ex.Message}");
            }

            return components;
        }

        /// <summary>
        /// 使用可用组件列表绑定字段
        /// </summary>
        private int BindFieldsWithAvailableComponents(object target, Type targetType, Dictionary<string, UnityEngine.Object> availableComponents)
        {
            int bindCount = 0;
            var fields = targetType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            // 遍历可用的组件，而不是所有字段
            foreach (var componentItem in availableComponents)
            {
                string componentName = componentItem.Key;
                var componentObject = componentItem.Value;

                // 查找匹配此组件名的字段
                var matchingField = FindMatchingField(fields, componentName);
                if (matchingField != null)
                {
                    // 检查是否标记为忽略
                    if (matchingField.GetCustomAttribute<UHubIgnoreAttribute>() != null)
                        continue;

                    // 尝试绑定字段
                    if (TryBindFieldDirectly(target, matchingField, componentName, componentObject))
                        bindCount++;
                }
            }

            return bindCount;
        }

        /// <summary>
        /// 使用可用组件列表绑定属性
        /// </summary>
        private int BindPropertiesWithAvailableComponents(object target, Type targetType, Dictionary<string, UnityEngine.Object> availableComponents)
        {
            int bindCount = 0;
            var properties = targetType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            // 遍历可用的组件，而不是所有属性
            foreach (var componentItem in availableComponents)
            {
                string componentName = componentItem.Key;
                var componentObject = componentItem.Value;

                // 查找匹配此组件名的属性
                var matchingProperty = FindMatchingProperty(properties, componentName);
                if (matchingProperty != null)
                {
                    // 检查是否标记为忽略
                    if (matchingProperty.GetCustomAttribute<UHubIgnoreAttribute>() != null)
                        continue;

                    // 必须可写
                    if (!matchingProperty.CanWrite)
                        continue;

                    // 尝试绑定属性
                    if (TryBindPropertyDirectly(target, matchingProperty, componentName, componentObject))
                        bindCount++;
                }
            }

            return bindCount;
        }

        /// <summary>
        /// 查找匹配组件名的字段
        /// </summary>
        private FieldInfo FindMatchingField(FieldInfo[] fields, string componentName)
        {
            foreach (var field in fields)
            {
                // 获取自定义绑定信息
                var bindAttr = field.GetCustomAttribute<UHubBindAttribute>();
                string expectedName = GetComponentName(field.Name, bindAttr);

                if (string.Equals(expectedName, componentName, StringComparison.Ordinal))
                {
                    return field;
                }
            }
            return null;
        }

        /// <summary>
        /// 查找匹配组件名的属性
        /// </summary>
        private PropertyInfo FindMatchingProperty(PropertyInfo[] properties, string componentName)
        {
            foreach (var property in properties)
            {
                // 获取自定义绑定信息
                var bindAttr = property.GetCustomAttribute<UHubBindAttribute>();
                string expectedName = GetComponentName(property.Name, bindAttr);

                if (string.Equals(expectedName, componentName, StringComparison.Ordinal))
                {
                    return property;
                }
            }
            return null;
        }

        /// <summary>
        /// 直接绑定字段
        /// </summary>
        private bool TryBindFieldDirectly(object target, FieldInfo field, string componentName, UnityEngine.Object componentObject)
        {
            // 获取自定义绑定信息
            var bindAttr = field.GetCustomAttribute<UHubBindAttribute>();
            Type componentType = GetComponentType(field.Name, field.FieldType, bindAttr);

            UnityEngine.Object targetComponent = componentObject;

            // 检查类型兼容性
            if (componentType != null && !componentType.IsInstanceOfType(componentObject))
            {
                // 如果类型不匹配，尝试通过 GetComponent 获取目标类型
                if (componentObject is GameObject gameObject)
                {
                    targetComponent = gameObject.GetComponent(componentType);
                    if (targetComponent == null)
                    {
                        LogMessage($"组件 '{componentName}' 的 GameObject 上未找到 {componentType.Name} 组件 (字段: {field.Name})", 
                                  _config?.FailureMode ?? BindingFailureMode.Warning);
                        return false;
                    }
                }
                else if (componentObject is Component component)
                {
                    targetComponent = component.GetComponent(componentType);
                    if (targetComponent == null)
                    {
                        LogMessage($"组件 '{componentName}' 上未找到 {componentType.Name} 组件 (字段: {field.Name})", 
                                  _config?.FailureMode ?? BindingFailureMode.Warning);
                        return false;
                    }
                }
                else
                {
                    LogMessage($"组件 '{componentName}' 的类型 {componentObject.GetType().Name} 与期望类型 {componentType.Name} 不兼容，且无法通过 GetComponent 获取 (字段: {field.Name})", 
                              _config?.FailureMode ?? BindingFailureMode.Warning);
                    return false;
                }
            }

            try
            {
                field.SetValue(target, targetComponent);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"设置字段 {field.Name} 值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 直接绑定属性
        /// </summary>
        private bool TryBindPropertyDirectly(object target, PropertyInfo property, string componentName, UnityEngine.Object componentObject)
        {
            // 获取自定义绑定信息
            var bindAttr = property.GetCustomAttribute<UHubBindAttribute>();
            Type componentType = GetComponentType(property.Name, property.PropertyType, bindAttr);

            UnityEngine.Object targetComponent = componentObject;

            // 检查类型兼容性
            if (componentType != null && !componentType.IsInstanceOfType(componentObject))
            {
                // 如果类型不匹配，尝试通过 GetComponent 获取目标类型
                if (componentObject is GameObject gameObject)
                {
                    targetComponent = gameObject.GetComponent(componentType);
                    if (targetComponent == null)
                    {
                        LogMessage($"组件 '{componentName}' 的 GameObject 上未找到 {componentType.Name} 组件 (属性: {property.Name})", 
                                  _config?.FailureMode ?? BindingFailureMode.Warning);
                        return false;
                    }
                }
                else if (componentObject is Component component)
                {
                    targetComponent = component.GetComponent(componentType);
                    if (targetComponent == null)
                    {
                        LogMessage($"组件 '{componentName}' 上未找到 {componentType.Name} 组件 (属性: {property.Name})", 
                                  _config?.FailureMode ?? BindingFailureMode.Warning);
                        return false;
                    }
                }
                else
                {
                    LogMessage($"组件 '{componentName}' 的类型 {componentObject.GetType().Name} 与期望类型 {componentType.Name} 不兼容，且无法通过 GetComponent 获取 (属性: {property.Name})", 
                              _config?.FailureMode ?? BindingFailureMode.Warning);
                    return false;
                }
            }

            try
            {
                property.SetValue(target, targetComponent);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"设置属性 {property.Name} 值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用可用组件列表尝试绑定字段
        /// </summary>
        private bool TryBindFieldWithAvailableComponents(UIView view, FieldInfo field, Dictionary<string, UnityEngine.Object> availableComponents)
        {
            // 获取自定义绑定信息
            var bindAttr = field.GetCustomAttribute<UHubBindAttribute>();
            string componentName = GetComponentName(field.Name, bindAttr);
            Type componentType = GetComponentType(field.Name, field.FieldType, bindAttr);

            if (string.IsNullOrEmpty(componentName))
                return false;

            // 检查是否有匹配的组件
            if (!availableComponents.TryGetValue(componentName, out var component))
            {
                LogMessage($"未找到名为 '{componentName}' 的组件 (字段: {field.Name})",
                          _config?.FailureMode ?? BindingFailureMode.Warning);
                return false;
            }

            // 检查类型兼容性
            if (componentType != null && !componentType.IsInstanceOfType(component))
            {
                LogMessage($"组件 '{componentName}' 的类型 {component.GetType().Name} 与期望类型 {componentType.Name} 不兼容 (字段: {field.Name})",
                          _config?.FailureMode ?? BindingFailureMode.Warning);
                return false;
            }

            try
            {
                field.SetValue(view, component);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"设置字段 {field.Name} 值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用可用组件列表尝试绑定属性
        /// </summary>
        private bool TryBindPropertyWithAvailableComponents(UIView view, PropertyInfo property, Dictionary<string, UnityEngine.Object> availableComponents)
        {
            // 获取自定义绑定信息
            var bindAttr = property.GetCustomAttribute<UHubBindAttribute>();
            string componentName = GetComponentName(property.Name, bindAttr);
            Type componentType = GetComponentType(property.Name, property.PropertyType, bindAttr);

            if (string.IsNullOrEmpty(componentName))
                return false;

            // 检查是否有匹配的组件
            if (!availableComponents.TryGetValue(componentName, out var component))
            {
                LogMessage($"未找到名为 '{componentName}' 的组件 (属性: {property.Name})",
                          _config?.FailureMode ?? BindingFailureMode.Warning);
                return false;
            }

            // 检查类型兼容性
            if (componentType != null && !componentType.IsInstanceOfType(component))
            {
                LogMessage($"组件 '{componentName}' 的类型 {component.GetType().Name} 与期望类型 {componentType.Name} 不兼容 (属性: {property.Name})",
                          _config?.FailureMode ?? BindingFailureMode.Warning);
                return false;
            }

            try
            {
                property.SetValue(view, component);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"设置属性 {property.Name} 值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 绑定属性
        /// </summary>
        private int BindProperties(UIView view, Type viewType, ReferenceCollector referenceCollector)
        {
            int bindCount = 0;
            var properties = viewType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // 检查是否标记为忽略
                if (property.GetCustomAttribute<UHubIgnoreAttribute>() != null)
                    continue;

                // 必须可写
                if (!property.CanWrite)
                    continue;

                // 尝试绑定属性
                if (TryBindProperty(view, property, referenceCollector))
                    bindCount++;
            }

            return bindCount;
        }

        /// <summary>
        /// 尝试绑定字段
        /// </summary>
        private bool TryBindField(UIView view, FieldInfo field, ReferenceCollector referenceCollector)
        {
            // 获取自定义绑定信息
            var bindAttr = field.GetCustomAttribute<UHubBindAttribute>();
            string componentName = GetComponentName(field.Name, bindAttr);
            Type componentType = GetComponentType(field.Name, field.FieldType, bindAttr);

            if (string.IsNullOrEmpty(componentName))
                return false;

            // 尝试从 ReferenceCollector 获取组件
            var component = GetComponentFromCollector(referenceCollector, componentName, componentType);
            if (component == null)
            {
                LogMessage($"未找到名为 '{componentName}' 的组件 (字段: {field.Name})",
                          _config?.FailureMode ?? BindingFailureMode.Warning);
                return false;
            }

            try
            {
                field.SetValue(view, component);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"设置字段 {field.Name} 值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试绑定属性
        /// </summary>
        private bool TryBindProperty(UIView view, PropertyInfo property, ReferenceCollector referenceCollector)
        {
            // 获取自定义绑定信息
            var bindAttr = property.GetCustomAttribute<UHubBindAttribute>();
            string componentName = GetComponentName(property.Name, bindAttr);
            Type componentType = GetComponentType(property.Name, property.PropertyType, bindAttr);

            if (string.IsNullOrEmpty(componentName))
                return false;

            // 尝试从 ReferenceCollector 获取组件
            var component = GetComponentFromCollector(referenceCollector, componentName, componentType);
            if (component == null)
            {
                LogMessage($"未找到名为 '{componentName}' 的组件 (属性: {property.Name})",
                          _config?.FailureMode ?? BindingFailureMode.Warning);
                return false;
            }

            try
            {
                property.SetValue(view, component);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"设置属性 {property.Name} 值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取组件名称
        /// </summary>
        private string GetComponentName(string memberName, UHubBindAttribute bindAttr)
        {
            // 优先使用自定义名称
            if (bindAttr?.ComponentName != null)
                return bindAttr.ComponentName;

            // 根据命名规范推断
            if (memberName.StartsWith("_"))
            {
                // 字段命名：_startBtn → StartBtn
                return char.ToUpper(memberName[1]) + memberName.Substring(2);
            }
            else
            {
                // 属性命名：直接使用
                return memberName;
            }
        }

        /// <summary>
        /// 获取组件类型
        /// </summary>
        private Type GetComponentType(string memberName, Type memberType, UHubBindAttribute bindAttr)
        {
            // 优先使用自定义类型
            if (bindAttr?.ComponentType != null)
                return bindAttr.ComponentType;

            // 检查运行时覆盖规则
            foreach (var kvp in _runtimeOverrides)
            {
                if (IsMatchSuffix(memberName, kvp.Key))
                    return kvp.Value;
            }

            // 使用配置文件的类型推断
            if (_config != null)
            {
                var inferredType = _config.GetComponentType(memberName);
                if (inferredType != null)
                    return inferredType;
            }

            // 使用成员声明的类型
            return memberType;
        }

        /// <summary>
        /// 检查是否匹配后缀
        /// </summary>
        private bool IsMatchSuffix(string name, string suffix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(suffix))
                return false;

            // 移除下划线前缀
            string cleanName = name.StartsWith("_") ? name.Substring(1) : name;
            return cleanName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从 ReferenceCollector 获取组件
        /// </summary>
        private UnityEngine.Object GetComponentFromCollector(ReferenceCollector referenceCollector, string componentName, Type componentType)
        {
            try
            {
                var component = referenceCollector.Get<UnityEngine.Object>(componentName);
                if (component == null)
                    return null;

                // 检查类型兼容性
                if (componentType != null && !componentType.IsInstanceOfType(component))
                {
                    LogMessage($"组件 '{componentName}' 的类型 {component.GetType().Name} 与期望类型 {componentType.Name} 不兼容",
                              _config?.FailureMode ?? BindingFailureMode.Warning);
                    return null;
                }

                return component;
            }
            catch (Exception ex)
            {
                LogError($"从 ReferenceCollector 获取组件 '{componentName}' 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 记录日志消息
        /// </summary>
        private void LogMessage(string message, BindingFailureMode mode)
        {
            switch (mode)
            {
                case BindingFailureMode.Silent:
                    break;
                case BindingFailureMode.Warning:
                    Log.Warning(message);
                    break;
                case BindingFailureMode.Exception:
                    throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            Log.Error($"[UHub] {message}");
        }
    }
}
