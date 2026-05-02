using System;

namespace EF.Timer
{
    /// <summary>
    /// 提供统一的计时器时钟，实现本地与服务器时间的切换与同步。
    /// </summary>
    internal sealed class TimerClock
    {
        private double _runtimeSeconds;
        private double _serverOffsetSeconds;
        private bool _serverSynced;
        private TimerMode _mode;
        private DateTime _localStartUtc;
        private double _localStartUnixSeconds;

        /// <summary>
        /// 初始化计时器时钟，默认使用本地时间模式。
        /// </summary>
        public TimerClock()
        {
            Reset();
        }

        /// <summary>
        /// 当前计时模式。
        /// </summary>
        public TimerMode Mode => _mode;

        /// <summary>
        /// 当前时间源是否已经完成服务器同步。
        /// </summary>
        public bool IsServerSynchronized => _serverSynced;

        /// <summary>
        /// 获取基于当前模式的运行时间（秒）。
        /// </summary>
        public double CurrentTimeSeconds => _runtimeSeconds + (_mode == TimerMode.Server ? _serverOffsetSeconds : 0d);

        /// <summary>
        /// 获取基于当前模式的 UTC 时间。
        /// </summary>
        public DateTime CurrentUtcTime
        {
            get
            {
                if (_mode == TimerMode.Local)
                {
                    return _localStartUtc.AddSeconds(_runtimeSeconds);
                }

                double unixSeconds = _localStartUnixSeconds + _runtimeSeconds + _serverOffsetSeconds;
                return DateTime.UnixEpoch.AddSeconds(unixSeconds);
            }
        }

        /// <summary>
        /// 获取当前服务器时间相对本地时间的秒级偏移量。
        /// </summary>
        public double ServerOffsetSeconds => _serverOffsetSeconds;

        /// <summary>
        /// 增量更新时钟运行时间。
        /// </summary>
        /// <param name="realElapseSeconds">真实流逝时间，单位秒。</param>
        /// <returns>更新时间后的当前模式时间。</returns>
        public double Update(float realElapseSeconds)
        {
            _runtimeSeconds += realElapseSeconds;
            return CurrentTimeSeconds;
        }

        /// <summary>
        /// 切换为目标时间模式，返回切换造成的时间偏移量。
        /// </summary>
        /// <param name="targetMode">目标计时模式。</param>
        /// <returns>切换前后时间线的差值。</returns>
        public double SwitchMode(TimerMode targetMode)
        {
            if (_mode == targetMode)
            {
                return 0d;
            }

            if (targetMode == TimerMode.Server && !_serverSynced)
            {
                throw new InvalidOperationException("切换到服务器时间模式前必须同步服务器时间");
            }

            double previous = CurrentTimeSeconds;
            _mode = targetMode;
            return CurrentTimeSeconds - previous;
        }

        /// <summary>
        /// 使用服务器 UTC 时间同步时钟，并返回同步造成的时间偏移量。
        /// </summary>
        /// <param name="serverUtcTime">服务器 UTC 时间。</param>
        /// <returns>同步前后时间线的差值。</returns>
        public double SyncServerTime(DateTime serverUtcTime)
        {
            double previous = CurrentTimeSeconds;
            double serverSeconds = (serverUtcTime.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
            double localSeconds = _localStartUnixSeconds + _runtimeSeconds;
            _serverOffsetSeconds = serverSeconds - localSeconds;
            _serverSynced = true;
            return CurrentTimeSeconds - previous;
        }

        /// <summary>
        /// 使用服务器 UNIX 毫秒时间戳同步时钟，并返回同步造成的时间偏移量。
        /// </summary>
        /// <param name="serverUnixTimeMilliseconds">服务器 UNIX 毫秒时间戳。</param>
        /// <returns>同步前后时间线的差值。</returns>
        public double SyncServerTime(long serverUnixTimeMilliseconds)
        {
            DateTime serverUtcTime = DateTime.UnixEpoch.AddMilliseconds(serverUnixTimeMilliseconds);
            return SyncServerTime(serverUtcTime);
        }

        /// <summary>
        /// 重置时钟状态，恢复为本地时间模式。
        /// </summary>
        public void Reset()
        {
            _runtimeSeconds = 0d;
            _serverOffsetSeconds = 0d;
            _serverSynced = false;
            _mode = TimerMode.Local;
            _localStartUtc = DateTime.UtcNow;
            _localStartUnixSeconds = (_localStartUtc - DateTime.UnixEpoch).TotalSeconds;
        }
    }
}
