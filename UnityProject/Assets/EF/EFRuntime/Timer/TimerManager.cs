using System;
using System.Collections.Generic;
using EF.Common;

namespace EF.Timer
{
    /// <summary>
    /// 计时器管理器，支持本地与服务器时间模式的任务调度。
    /// </summary>
    public sealed class TimerManager : AEFManager, ITimerManager
    {
        private const double TriggerTolerance = 0.0001d;
        private const double MinLoopIntervalSeconds = 0.0001d;

        private readonly TimerClock _clock = new();
        private readonly TimerTaskCollection _tasks = new();
        private readonly List<TimerTask> _executionBuffer = new();

        private int _nextTimerId = 1;

        /// <summary>
        /// 获取当前计时模式。
        /// </summary>
        public TimerMode Mode => _clock.Mode;

        /// <summary>
        /// 获取当前是否已经完成服务器时间同步。
        /// </summary>
        public bool IsServerSynchronized => _clock.IsServerSynchronized;

        /// <summary>
        /// 获取当前仍在管理中的活跃计时器数量。
        /// </summary>
        public int TimerCount => _tasks.Count;

        /// <summary>
        /// 获取基于当前计时模式的时间线秒数。
        /// </summary>
        public double CurrentTimeSeconds => _clock.CurrentTimeSeconds;

        /// <summary>
        /// 获取基于当前计时模式换算得到的 UTC 时间。
        /// </summary>
        public DateTime CurrentUtcTime => _clock.CurrentUtcTime;

        /// <summary>
        /// 获取服务器时间相对本地时间的秒级偏移量。
        /// </summary>
        public double ServerOffsetSeconds => _clock.ServerOffsetSeconds;

        /// <summary>
        /// 注册一个只触发一次的计时器。
        /// </summary>
        /// <param name="delaySeconds">从当前时间开始计算的延迟秒数，不能为负数。</param>
        /// <param name="callback">计时器到期时执行的回调。</param>
        /// <returns>新注册计时器的唯一标识。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="delaySeconds"/> 小于 0。</exception>
        public int ScheduleOnce(float delaySeconds, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, false, 0f, Wrap(callback), null);
        }

        /// <summary>
        /// 注册一个只触发一次且携带上下文数据的计时器。
        /// </summary>
        /// <typeparam name="T">上下文数据类型。</typeparam>
        /// <param name="delaySeconds">从当前时间开始计算的延迟秒数，不能为负数。</param>
        /// <param name="callback">计时器到期时执行的回调，参数为传入的上下文数据。</param>
        /// <param name="userData">回调执行时传入的上下文数据。</param>
        /// <returns>新注册计时器的唯一标识。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="delaySeconds"/> 小于 0。</exception>
        public int ScheduleOnce<T>(float delaySeconds, Action<T> callback, T userData)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, false, 0f, Wrap(callback), userData);
        }

        /// <summary>
        /// 注册一个循环触发的计时器。
        /// </summary>
        /// <param name="delaySeconds">首次触发前的延迟秒数，不能为负数。</param>
        /// <param name="intervalSeconds">首次触发后的循环间隔秒数，不能为负数。</param>
        /// <param name="callback">每次计时器到期时执行的回调。</param>
        /// <returns>新注册计时器的唯一标识。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="delaySeconds"/> 或 <paramref name="intervalSeconds"/> 小于 0。
        /// </exception>
        public int ScheduleLoop(float delaySeconds, float intervalSeconds, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, true, intervalSeconds, Wrap(callback), null);
        }

        /// <summary>
        /// 注册一个循环触发且携带上下文数据的计时器。
        /// </summary>
        /// <typeparam name="T">上下文数据类型。</typeparam>
        /// <param name="delaySeconds">首次触发前的延迟秒数，不能为负数。</param>
        /// <param name="intervalSeconds">首次触发后的循环间隔秒数，不能为负数。</param>
        /// <param name="callback">每次计时器到期时执行的回调，参数为传入的上下文数据。</param>
        /// <param name="userData">回调执行时传入的上下文数据。</param>
        /// <returns>新注册计时器的唯一标识。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="delaySeconds"/> 或 <paramref name="intervalSeconds"/> 小于 0。
        /// </exception>
        public int ScheduleLoop<T>(float delaySeconds, float intervalSeconds, Action<T> callback, T userData)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, true, intervalSeconds, Wrap(callback), userData);
        }

        /// <summary>
        /// 取消指定标识对应的计时器。
        /// </summary>
        /// <param name="timerId">要取消的计时器标识。</param>
        /// <returns>若找到并移除了计时器，则返回 true；否则返回 false。</returns>
        public bool Cancel(int timerId) => _tasks.Remove(timerId);

        /// <summary>
        /// 判断指定标识对应的计时器是否仍在管理中。
        /// </summary>
        /// <param name="timerId">要查询的计时器标识。</param>
        /// <returns>若计时器存在，则返回 true；否则返回 false。</returns>
        public bool Exists(int timerId) => _tasks.Contains(timerId);

        /// <summary>
        /// 清空全部计时器并重置后续生成的计时器标识。
        /// </summary>
        public void Clear()
        {
            _tasks.Clear();
            _executionBuffer.Clear();
            _nextTimerId = 1;
        }

        /// <summary>
        /// 切换计时模式，并按切换产生的时间差平移所有未触发任务。
        /// </summary>
        /// <param name="mode">要切换到的目标计时模式。</param>
        /// <exception cref="InvalidOperationException">切换到服务器时间模式前尚未同步服务器时间。</exception>
        public void SwitchMode(TimerMode mode)
        {
            double delta = _clock.SwitchMode(mode);
            _tasks.ShiftAll(delta);
        }

        /// <summary>
        /// 使用服务器 UTC 时间同步时钟。
        /// </summary>
        /// <param name="serverUtcTime">服务器当前 UTC 时间。</param>
        /// <remarks>
        /// 如果当前处于服务器时间模式，会同步平移所有未触发任务，避免同步前后触发时间发生跳变。
        /// </remarks>
        public void SyncServerTime(DateTime serverUtcTime)
        {
            double delta = _clock.SyncServerTime(serverUtcTime);
            if (Mode == TimerMode.Server)
            {
                _tasks.ShiftAll(delta);
            }
        }

        /// <summary>
        /// 使用服务器 UNIX 毫秒时间戳同步时钟。
        /// </summary>
        /// <param name="serverUnixTimeMilliseconds">服务器当前 UNIX 毫秒时间戳。</param>
        /// <remarks>
        /// 如果当前处于服务器时间模式，会同步平移所有未触发任务，避免同步前后触发时间发生跳变。
        /// </remarks>
        public void SyncServerTime(long serverUnixTimeMilliseconds)
        {
            double delta = _clock.SyncServerTime(serverUnixTimeMilliseconds);
            if (Mode == TimerMode.Server)
            {
                _tasks.ShiftAll(delta);
            }
        }

        /// <summary>
        /// Unity 生命周期中每帧调用，用于推进计时器。
        /// </summary>
        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            double frameTime = _clock.Update(realElapseSeconds);

            if (_tasks.Count == 0)
            {
                return;
            }

            _executionBuffer.Clear();
            _tasks.CollectDue(frameTime, TriggerTolerance, _executionBuffer);

            if (_executionBuffer.Count == 0)
            {
                return;
            }

            foreach (TimerTask task in _executionBuffer)
            {
                if (task.IsCancelled)
                {
                    _tasks.Remove(task.TimerId);
                    continue;
                }

                task.Invoke();

                double currentTime = _clock.CurrentTimeSeconds;

                if (task.IsCancelled)
                {
                    _tasks.Remove(task.TimerId);
                    continue;
                }

                if (!task.TrySetNextTrigger(currentTime, MinLoopIntervalSeconds))
                {
                    _tasks.Remove(task.TimerId);
                }
            }

            _executionBuffer.Clear();
        }

        /// <summary>
        /// 关闭计时器管理器并清理状态。
        /// </summary>
        public override void Shutdown()
        {
            Clear();
            _clock.Reset();
        }

        private int ScheduleInternal(double delaySeconds, bool isLoop, double intervalSeconds, Action<object> callback, object userData)
        {
            if (delaySeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "计时器延迟不能为负数");
            }

            if (isLoop && intervalSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "循环计时间隔不能为负数");
            }

            int timerId = GenerateTimerId();
            TimerTask task = new(timerId, delaySeconds, isLoop, intervalSeconds, callback, userData, _clock.CurrentTimeSeconds);
            _tasks.Add(task);
            return timerId;
        }

        private int GenerateTimerId()
        {
            int start = _nextTimerId;
            while (_tasks.Contains(_nextTimerId))
            {
                _nextTimerId++;
                if (_nextTimerId == int.MaxValue)
                {
                    _nextTimerId = 1;
                }

                if (_nextTimerId == start)
                {
                    throw new InvalidOperationException("计时器标识耗尽");
                }
            }

            int result = _nextTimerId;
            _nextTimerId++;
            if (_nextTimerId == int.MaxValue)
            {
                _nextTimerId = 1;
            }

            return result;
        }

        private static Action<object> Wrap(Action callback)
        {
            return _ => callback();
        }

        private static Action<object> Wrap<T>(Action<T> callback)
        {
            return state => callback((T)state);
        }
    }
}
