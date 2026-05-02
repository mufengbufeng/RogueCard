using System;
using System.Diagnostics;

namespace EF.Debugger
{
    /// <summary>
    /// 标记方法或类的日志输出等级需求。
    /// 当编译时等级或运行时等级不满足此要求时，不会输出日志。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("EF_DEBUG_ENABLED")]
    public sealed class LogLevelAttribute : Attribute
    {
        /// <summary>
        /// 需要的最低日志等级。
        /// </summary>
        public LogLevel RequiredLevel { get; }

        /// <summary>
        /// 初始化 LogLevelAttribute。
        /// </summary>
        /// <param name="requiredLevel">需要的最低日志等级</param>
        public LogLevelAttribute(LogLevel requiredLevel)
        {
            RequiredLevel = requiredLevel;
        }
    }
}
