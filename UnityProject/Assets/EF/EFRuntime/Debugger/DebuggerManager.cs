using UnityEngine;

namespace EF.Debugger
{
    /// <summary>
    /// 默认调试器管理器实现，负责控制日志等级与持久化。
    /// </summary>
    public sealed class DebuggerManager : IDebuggerManager
    {
    // Persist the runtime log level across sessions when persistence is requested.
        private const string PlayerPrefsKey = "EF.Debugger.LogLevel";

        private static readonly DebuggerManager _instance = new();
        private LogLevel _currentLevel = DebuggerMacro.CompileTimeLevel;
        private bool _initialized;

        public static DebuggerManager Instance => _instance;

        private DebuggerManager()
        {
        }

        public LogLevel CompileTimeLevel => DebuggerMacro.CompileTimeLevel;

        public LogLevel CurrentLevel
        {
            get
            {
                EnsureInitialized();
                return _currentLevel;
            }
            set
            {
                SetLevel(value, false);
            }
        }

        public void SetLevel(LogLevel level, bool persist = false)
        {
            EnsureInitialized();
            SetLevelInternal(level, persist);
        }

        public void ResetToCompileTimeLevel(bool clearPersisted = false)
        {
            EnsureInitialized();
            _currentLevel = DebuggerMacro.CompileTimeLevel;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (clearPersisted && PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
                PlayerPrefs.Save();
            }
#endif
        }

        public bool IsLevelEnabled(LogLevel level)
        {
            if (!DebuggerMacro.Allows(level))
            {
                return false;
            }

            EnsureInitialized();

            if (_currentLevel == LogLevel.None)
            {
                return false;
            }

            return level <= _currentLevel;
        }

        private void SetLevelInternal(LogLevel level, bool persist)
        {
            var clamped = Clamp(level);
            _currentLevel = clamped;

#if !UNITY_WEBGL || UNITY_EDITOR
            if (persist)
            {
                PlayerPrefs.SetInt(PlayerPrefsKey, (int)clamped);
                PlayerPrefs.Save();
            }
#endif
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

#if !UNITY_WEBGL || UNITY_EDITOR
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                var storedValue = PlayerPrefs.GetInt(PlayerPrefsKey);
                var storedLevel = (LogLevel)storedValue;
                _currentLevel = Clamp(storedLevel);
            }
#endif
        }

        private static LogLevel Clamp(LogLevel level)
        {
            if (level < LogLevel.None)
            {
                return LogLevel.None;
            }

            var max = DebuggerMacro.CompileTimeLevel;
            if (level > max)
            {
                return max;
            }

            return level;
        }
    }
}
