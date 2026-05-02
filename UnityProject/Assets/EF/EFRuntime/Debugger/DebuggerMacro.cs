namespace EF.Debugger
{
    /// <summary>
    /// 通过宏定义限定编译期允许的日志等级。
    /// </summary>
    internal static class DebuggerMacro
    {
        internal const LogLevel CompileTimeLevel =
#if EF_DEBUG_LEVEL_NONE
            LogLevel.None;
#elif EF_DEBUG_LEVEL_ERROR
            LogLevel.Error;
#elif EF_DEBUG_LEVEL_WARNING
            LogLevel.Warning;
#elif EF_DEBUG_LEVEL_LOG
            LogLevel.Log;
#elif EF_DEBUG_LEVEL_ALL
            LogLevel.All;
#else
            LogLevel.Log;
#endif

        internal static bool Allows(LogLevel level)
        {
            return CompileTimeLevel != LogLevel.None && level <= CompileTimeLevel;
        }
    }
}
