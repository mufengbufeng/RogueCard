using System;

namespace EF.Event
{
    /// <summary>
    /// 标记一个 readonly struct 为事件参数类型，供 Code Gen 工具扫描识别。
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EventArgsAttribute : Attribute
    {
    }
}
