namespace EF.Debugger
{
    /// <summary>
    /// 定义调试器管理器需要支持的基础能力。
    /// </summary>
    public interface IDebuggerManager
    {
        /// <summary>
        /// 编译期限定的最高日志等级。
        /// </summary>
        LogLevel CompileTimeLevel { get; }

        /// <summary>
        /// 当前运行时日志等级。
        /// </summary>
        LogLevel CurrentLevel { get; set; }

        /// <summary>
        /// 设置日志等级，可选持久化。
        /// </summary>
        void SetLevel(LogLevel level, bool persist = false);

        /// <summary>
        /// 重置到编译期限定的日志等级。
        /// </summary>
        void ResetToCompileTimeLevel(bool clearPersisted = false);

        /// <summary>
        /// 指定日志等级是否允许输出。
        /// </summary>
        bool IsLevelEnabled(LogLevel level);
    }
}
