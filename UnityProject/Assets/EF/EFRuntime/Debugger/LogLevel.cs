namespace EF.Debugger
{
    /// <summary>
    /// Debug 日志等级定义。
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 关闭所有日志输出。
        /// </summary>
        None = 0,

        /// <summary>
        /// 仅保留错误日志。
        /// </summary>
        Error = 1,

        /// <summary>
        /// 保留警告和错误日志。
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 记录 Log、警告和错误。
        /// </summary>
        Log = 3,

        /// <summary>
        /// 输出全部日志（包含额外的详细信息）。
        /// </summary>
        All = 4
    }
}
