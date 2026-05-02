using System;

namespace EF.UI
{
    /// <summary>
    /// UHub 组件绑定属性标注
    /// 用于指定组件的自定义绑定规则
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class UHubBindAttribute : Attribute
    {
        /// <summary>
        /// 自定义组件名称
        /// </summary>
        public string ComponentName { get; }

        /// <summary>
        /// 指定组件类型
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        /// 使用自定义组件名称进行绑定
        /// </summary>
        /// <param name="componentName">组件名称</param>
        public UHubBindAttribute(string componentName)
        {
            ComponentName = componentName;
        }

        /// <summary>
        /// 使用自定义组件类型进行绑定
        /// </summary>
        /// <param name="componentType">组件类型</param>
        public UHubBindAttribute(Type componentType)
        {
            ComponentType = componentType;
        }

        /// <summary>
        /// 使用自定义组件名称和类型进行绑定
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <param name="componentType">组件类型</param>
        public UHubBindAttribute(string componentName, Type componentType)
        {
            ComponentName = componentName;
            ComponentType = componentType;
        }
    }

    /// <summary>
    /// UHub 组件绑定忽略标注
    /// 标记此字段或属性不进行自动绑定
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class UHubIgnoreAttribute : Attribute
    {
    }
}