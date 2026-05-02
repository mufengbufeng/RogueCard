using System;

namespace EF.Feature
{
    /// <summary>
    /// 标记特性是否允许多实例特性。
    /// 默认情况下，每个实体只能有一个特定类型的特性。
    /// 使用此特性可以允许一个实体拥有多个同类型的特性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AllowMultipleAttribute : Attribute
    {
        /// <summary>
        /// 获取是否允许添加多个该类型的特性到同一个实体。
        /// </summary>
        public bool AllowMultiple { get; }

        /// <summary>
        /// 初始化 AllowMultipleAttribute 的新实例。
        /// </summary>
        /// <param name="allowMultiple">是否允许添加多个该类型的特性，默认为 true。</param>
        public AllowMultipleAttribute(bool allowMultiple = true)
        {
            AllowMultiple = allowMultiple;
        }
    }
}
