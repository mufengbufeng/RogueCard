using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace EF.Debugger
{
    /// <summary>
    /// 提供统一的日志输出入口，结合调试器管理器控制等级。
    /// </summary>
    public static class Log
    {
        private static IDebuggerManager _manager;

        private static IDebuggerManager ActiveManager => _manager ?? DebuggerManager.Instance;

        public static LogLevel CompileTimeLevel => DebuggerMacro.CompileTimeLevel;

        public static LogLevel CurrentLevel
        {
            get => ActiveManager.CurrentLevel;
            set => ActiveManager.CurrentLevel = value;
        }

        public static void SetManager(IDebuggerManager manager)
        {
            _manager = manager;
        }

        public static void SetLevel(LogLevel level, bool persist = false)
        {
            ActiveManager.SetLevel(level, persist);
        }

        public static void Reset(bool clearPersisted = false)
        {
            ActiveManager.ResetToCompileTimeLevel(clearPersisted);
        }

        public static bool IsLevelEnabled(LogLevel level)
        {
            return ActiveManager.IsLevelEnabled(level);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 检查调用者方法上的 LogLevelAttribute，验证是否满足等级要求。
        /// 仅在 Unity Editor 中启用以避免运行时性能开销。
        /// </summary>
        private static bool CheckCallerAttribute(LogLevel currentLogLevel)
        {
            try
            {
                // 获取调用栈（跳过当前方法和直接调用者）
                var stackTrace = new StackTrace(2, false);
                var frame = stackTrace.GetFrame(0);
                if (frame == null)
                {
                    return true;
                }

                var method = frame.GetMethod();
                if (method == null)
                {
                    return true;
                }

                // 检查方法上的 LogLevelAttribute
                var methodAttr = method.GetCustomAttribute<LogLevelAttribute>(true);
                if (methodAttr != null)
                {
                    return currentLogLevel >= methodAttr.RequiredLevel;
                }

                // 检查类上的 LogLevelAttribute
                var declaringType = method.DeclaringType;
                if (declaringType != null)
                {
                    var classAttr = declaringType.GetCustomAttribute<LogLevelAttribute>(true);
                    if (classAttr != null)
                    {
                        return currentLogLevel >= classAttr.RequiredLevel;
                    }
                }
            }
            catch
            {
                // 如果反射失败，默认允许输出
                return true;
            }

            return true;
        }
#endif

        public static void Info(string message, UnityEngine.Object context = null)
        {
#if EF_DEBUG_LEVEL_NONE
            return;
#else
            if (!IsLevelEnabled(LogLevel.Log))
            {
                return;
            }

#if UNITY_EDITOR
            if (!CheckCallerAttribute(LogLevel.Log))
            {
                return;
            }
#endif

            UnityEngine.Debug.Log(message, context);
#endif
        }

        public static void Warning(string message, UnityEngine.Object context = null)
        {
#if EF_DEBUG_LEVEL_NONE || EF_DEBUG_LEVEL_ERROR
            return;
#else
            if (!IsLevelEnabled(LogLevel.Warning))
            {
                return;
            }

#if UNITY_EDITOR
            if (!CheckCallerAttribute(LogLevel.Warning))
            {
                return;
            }
#endif

            UnityEngine.Debug.LogWarning(message, context);
#endif
        }

        public static void Error(string message, UnityEngine.Object context = null)
        {
#if EF_DEBUG_LEVEL_NONE
            return;
#else
            if (!IsLevelEnabled(LogLevel.Error))
            {
                return;
            }

#if UNITY_EDITOR
            if (!CheckCallerAttribute(LogLevel.Error))
            {
                return;
            }
#endif

            UnityEngine.Debug.LogError(message, context);
#endif
        }

        public static void Exception(Exception exception)
        {
#if EF_DEBUG_LEVEL_NONE
            return;
#else
            if (exception == null)
            {
                return;
            }

            if (!IsLevelEnabled(LogLevel.Error))
            {
                return;
            }

#if UNITY_EDITOR
            if (!CheckCallerAttribute(LogLevel.Error))
            {
                return;
            }
#endif

            UnityEngine.Debug.LogException(exception);
#endif
        }

        public static void Verbose(string message, UnityEngine.Object context = null)
        {
#if EF_DEBUG_LEVEL_ALL
            if (!IsLevelEnabled(LogLevel.All))
            {
                return;
            }

#if UNITY_EDITOR
            if (!CheckCallerAttribute(LogLevel.All))
            {
                return;
            }
#endif

            UnityEngine.Debug.Log(message, context);
#else
            return;
#endif
        }
    }
}
