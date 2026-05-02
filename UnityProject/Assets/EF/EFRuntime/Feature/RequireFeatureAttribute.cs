using System;

namespace EF.Feature
{
    /// <summary>
    /// 标记特性依赖关系特性。
    /// 用于指定添加该特性前，实体必须先拥有指定类型的特性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireFeatureAttribute : Attribute
    {
        /// <summary>
        /// 获取所需的特性类型。
        /// </summary>
        public Type RequiredType { get; }

        /// <summary>
        /// 初始化 RequireFeatureAttribute 的新实例。
        /// </summary>
        /// <param name="requiredType">必须存在的特性类型。</param>
        public RequireFeatureAttribute(Type requiredType)
        {
            if (requiredType == null)
            {
                throw new ArgumentNullException(nameof(requiredType));
            }

            if (!typeof(IFeature).IsAssignableFrom(requiredType))
            {
                throw new ArgumentException($"Required type must implement IFeature interface.", nameof(requiredType));
            }

            RequiredType = requiredType;
        }
    }
}
