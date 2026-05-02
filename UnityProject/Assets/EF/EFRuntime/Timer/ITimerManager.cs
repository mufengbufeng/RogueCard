using System;
using EF.Common;

namespace EF.Timer
{
    /// <summary>
    /// 定义计时器管理器需要具备的能力。
    /// </summary>
    public interface ITimerManager : IEFManager
    {
        /// <summary>
        /// 当前计时模式。
        /// </summary>
        TimerMode Mode { get; }

        /// <summary>
        /// 当前是否已同步服务器时间。
        /// </summary>
        bool IsServerSynchronized { get; }

        /// <summary>
        /// 活跃计时器数量。
        /// </summary>
        int TimerCount { get; }

        /// <summary>
        /// 基于当前模式的时间线（秒）。
        /// </summary>
        double CurrentTimeSeconds { get; }

        /// <summary>
        /// 基于当前模式的 UTC 时间。
        /// </summary>
        DateTime CurrentUtcTime { get; }

        /// <summary>
        /// 服务器时间相对本地时间的秒级偏移。
        /// </summary>
        double ServerOffsetSeconds { get; }

        /// <summary>
        /// 注册一次性计时器。
        /// </summary>
        /// <param name="delaySeconds">延迟时间，单位秒。</param>
        /// <param name="callback">计时器回调。</param>
        /// <returns>计时器标识。</returns>
        int ScheduleOnce(float delaySeconds, Action callback);

        /// <summary>
        /// 注册一次性计时器，并携带上下文数据。
        /// </summary>
        /// <typeparam name="T">上下文类型。</typeparam>
        /// <param name="delaySeconds">延迟时间，单位秒。</param>
        /// <param name="callback">计时器回调。</param>
        /// <param name="userData">上下文数据。</param>
        /// <returns>计时器标识。</returns>
        int ScheduleOnce<T>(float delaySeconds, Action<T> callback, T userData);

        /// <summary>
        /// 注册循环计时器。
        /// </summary>
        /// <param name="delaySeconds">首次触发延迟。</param>
        /// <param name="intervalSeconds">循环触发间隔。</param>
        /// <param name="callback">计时器回调。</param>
        /// <returns>计时器标识。</returns>
        int ScheduleLoop(float delaySeconds, float intervalSeconds, Action callback);

        /// <summary>
        /// 注册循环计时器，并携带上下文数据。
        /// </summary>
        /// <typeparam name="T">上下文类型。</typeparam>
        /// <param name="delaySeconds">首次触发延迟。</param>
        /// <param name="intervalSeconds">循环触发间隔。</param>
        /// <param name="callback">计时器回调。</param>
        /// <param name="userData">上下文数据。</param>
        /// <returns>计时器标识。</returns>
        int ScheduleLoop<T>(float delaySeconds, float intervalSeconds, Action<T> callback, T userData);

        /// <summary>
        /// 取消指定计时器。
        /// </summary>
        bool Cancel(int timerId);

        /// <summary>
        /// 判断计时器是否存在。
        /// </summary>
        bool Exists(int timerId);

        /// <summary>
        /// 清空所有计时器。
        /// </summary>
        void Clear();

        /// <summary>
        /// 切换计时模式。
        /// </summary>
        /// <param name="mode">目标模式。</param>
        void SwitchMode(TimerMode mode);

        /// <summary>
        /// 使用服务器 UTC 时间进行同步。
        /// </summary>
        /// <param name="serverUtcTime">服务器 UTC 时间。</param>
        void SyncServerTime(DateTime serverUtcTime);

        /// <summary>
        /// 使用服务器 UNIX 毫秒时间戳进行同步。
        /// </summary>
        /// <param name="serverUnixTimeMilliseconds">服务器 UNIX 毫秒时间戳。</param>
        void SyncServerTime(long serverUnixTimeMilliseconds);
    }
}
