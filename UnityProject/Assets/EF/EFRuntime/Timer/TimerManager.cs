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

        /// <inheritdoc />
        public TimerMode Mode => _clock.Mode;

        /// <inheritdoc />
        public bool IsServerSynchronized => _clock.IsServerSynchronized;

        /// <inheritdoc />
        public int TimerCount => _tasks.Count;

        /// <inheritdoc />
        public double CurrentTimeSeconds => _clock.CurrentTimeSeconds;

        /// <inheritdoc />
        public DateTime CurrentUtcTime => _clock.CurrentUtcTime;

        /// <inheritdoc />
        public double ServerOffsetSeconds => _clock.ServerOffsetSeconds;

        /// <inheritdoc />
        public int ScheduleOnce(float delaySeconds, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, false, 0f, Wrap(callback), null);
        }

        /// <inheritdoc />
        public int ScheduleOnce<T>(float delaySeconds, Action<T> callback, T userData)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, false, 0f, Wrap(callback), userData);
        }

        /// <inheritdoc />
        public int ScheduleLoop(float delaySeconds, float intervalSeconds, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, true, intervalSeconds, Wrap(callback), null);
        }

        /// <inheritdoc />
        public int ScheduleLoop<T>(float delaySeconds, float intervalSeconds, Action<T> callback, T userData)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback), "计时器回调不能为空");
            }

            return ScheduleInternal(delaySeconds, true, intervalSeconds, Wrap(callback), userData);
        }

        /// <inheritdoc />
        public bool Cancel(int timerId) => _tasks.Remove(timerId);

        /// <inheritdoc />
        public bool Exists(int timerId) => _tasks.Contains(timerId);

        /// <inheritdoc />
        public void Clear()
        {
            _tasks.Clear();
            _executionBuffer.Clear();
            _nextTimerId = 1;
        }

        /// <inheritdoc />
        public void SwitchMode(TimerMode mode)
        {
            double delta = _clock.SwitchMode(mode);
            _tasks.ShiftAll(delta);
        }

        /// <inheritdoc />
        public void SyncServerTime(DateTime serverUtcTime)
        {
            double delta = _clock.SyncServerTime(serverUtcTime);
            if (Mode == TimerMode.Server)
            {
                _tasks.ShiftAll(delta);
            }
        }

        /// <inheritdoc />
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
