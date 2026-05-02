using System;

namespace EF.Timer
{
    /// <summary>
    /// 表示一个待执行的计时器任务。
    /// </summary>
    internal sealed class TimerTask
    {
        private readonly Action<object> _callback;

        /// <summary>
        /// 创建计时器任务实例。
        /// </summary>
        public TimerTask(int timerId, double delaySeconds, bool isLoop, double intervalSeconds, Action<object> callback, object userData, double currentTime)
        {
            TimerId = timerId;
            DelaySeconds = Math.Max(0d, delaySeconds);
            IntervalSeconds = Math.Max(0d, intervalSeconds);
            IsLoop = isLoop;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            UserData = userData;
            NextTriggerTime = currentTime + DelaySeconds;
        }

        /// <summary>
        /// 计时器标识。
        /// </summary>
        public int TimerId { get; }

        /// <summary>
        /// 首次触发的延迟秒数。
        /// </summary>
        public double DelaySeconds { get; }

        /// <summary>
        /// 循环计时触发间隔秒数。
        /// </summary>
        public double IntervalSeconds { get; }

        /// <summary>
        /// 是否为循环计时。
        /// </summary>
        public bool IsLoop { get; }

        /// <summary>
        /// 下一次触发的绝对时间。
        /// </summary>
        public double NextTriggerTime { get; private set; }

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public object UserData { get; }

        /// <summary>
        /// 是否已取消。
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// 触发计时器回调。
        /// </summary>
        public void Invoke()
        {
            if (IsCancelled)
            {
                return;
            }

            _callback(UserData);
        }

        /// <summary>
        /// 将任务标记为取消状态。
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
        }

        /// <summary>
        /// 按照给定偏移调整下一次触发时间。
        /// </summary>
        public void Shift(double deltaSeconds)
        {
            NextTriggerTime += deltaSeconds;
        }

        /// <summary>
        /// 尝试计算下一轮触发时间。
        /// </summary>
        /// <param name="currentTime">当前时间。</param>
        /// <param name="minInterval">允许的最小间隔。</param>
        /// <returns>若成功设置下一次触发，则为 true。</returns>
        public bool TrySetNextTrigger(double currentTime, double minInterval)
        {
            if (!IsLoop || IsCancelled)
            {
                return false;
            }

            double interval = IntervalSeconds > 0d ? IntervalSeconds : DelaySeconds;
            if (interval < minInterval)
            {
                interval = minInterval;
            }

            NextTriggerTime = currentTime + interval;
            return true;
        }
    }
}
