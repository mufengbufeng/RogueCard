using UnityEngine;

namespace EF.Debugger.Examples
{
    /// <summary>
    /// 演示如何使用 LogLevelAttribute 和宏定义控制日志输出。
    /// </summary>
    public class LogExample : MonoBehaviour
    {
        private void Start()
        {
            // 普通日志调用（不受 Attribute 限制）
            Log.Info("Normal Info log");
            Log.Warning("Normal Warning log");
            Log.Error("Normal Error log");
            Log.Verbose("Normal Verbose log");

            // 调用带有 Attribute 限制的方法
            LogWithAttributeOnMethod();

            // 调用带有类级别 Attribute 的方法
            var attributeClassExample = new LogFromAttributeClass();
            attributeClassExample.TestLogs();
        }

        /// <summary>
        /// 此方法要求当前日志等级至少为 All 才会输出。
        /// 在 Unity Editor 菜单 EF/Debugger/LogAll 启用后才能看到这些日志。
        /// </summary>
        [LogLevel(LogLevel.All)]
        private void LogWithAttributeOnMethod()
        {
            Log.Info("Info from method with LogLevel.All attribute");
            Log.Warning("Warning from method with LogLevel.All attribute");
            Log.Error("Error from method with LogLevel.All attribute");
        }
    }

    /// <summary>
    /// 整个类都要求至少 Warning 等级才输出日志。
    /// </summary>
    [LogLevel(LogLevel.Warning)]
    public class LogFromAttributeClass
    {
        public void TestLogs()
        {
            // 这些日志只有在当前等级 >= Warning 时才会输出
            Log.Info("Info from class with LogLevel.Warning attribute");
            Log.Warning("Warning from class with LogLevel.Warning attribute");
            Log.Error("Error from class with LogLevel.Warning attribute");
        }
    }

    /// <summary>
    /// 演示编译期宏定义的效果。
    ///
    /// 宏定义控制编译期行为：
    /// - EF_DEBUG_LEVEL_NONE: 完全移除所有日志代码
    /// - EF_DEBUG_LEVEL_ERROR: 只保留 Error 和 Exception
    /// - EF_DEBUG_LEVEL_WARNING: 保留 Error, Exception, Warning
    /// - EF_DEBUG_LEVEL_LOG: 保留 Error, Exception, Warning, Info（默认）
    /// - EF_DEBUG_LEVEL_ALL: 保留所有日志，包括 Verbose
    ///
    /// 在 Unity Player Settings > Scripting Define Symbols 中设置。
    /// </summary>
    public class MacroExample : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log($"Compile-time level: {Log.CompileTimeLevel}");
            Debug.Log($"Current runtime level: {Log.CurrentLevel}");

            // 根据编译期定义，某些日志可能被完全移除
            Log.Verbose("This only appears if EF_DEBUG_LEVEL_ALL is defined");
            Log.Info("This is removed if EF_DEBUG_LEVEL_NONE is defined");
            Log.Warning("This is removed if EF_DEBUG_LEVEL_NONE or EF_DEBUG_LEVEL_ERROR is defined");
            Log.Error("This is removed only if EF_DEBUG_LEVEL_NONE is defined");
        }
    }
}
