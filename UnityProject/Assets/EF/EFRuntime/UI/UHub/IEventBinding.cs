using System;

namespace EF.UI
{
    /// <summary>
    /// 事件绑定接口
    /// 提供统一的事件绑定和解绑操作
    /// </summary>
    public interface IEventBinding
    {
        /// <summary>
        /// 绑定事件
        /// </summary>
        void Bind();

        /// <summary>
        /// 解绑事件
        /// </summary>
        void Unbind();

        /// <summary>
        /// 是否已绑定
        /// </summary>
        bool IsBound { get; }
    }

    /// <summary>
    /// 绑定失败处理模式
    /// </summary>
    public enum BindingFailureMode
    {
        /// <summary>
        /// 静默忽略
        /// </summary>
        Silent,

        /// <summary>
        /// 输出警告日志
        /// </summary>
        Warning,

        /// <summary>
        /// 抛出异常
        /// </summary>
        Exception
    }
}