using System;
using System.Collections.Generic;

namespace EF.Timer
{
    /// <summary>
    /// 管理计时器任务集合，负责增删与触发决策。
    /// </summary>
    internal sealed class TimerTaskCollection
    {
        private readonly Dictionary<int, TimerTask> _lookup = new();
        private readonly List<TimerTask> _tasks = new();

        /// <summary>
        /// 当前任务数量。
        /// </summary>
        public int Count => _lookup.Count;

        /// <summary>
        /// 新增计时器任务。
        /// </summary>
        public void Add(TimerTask task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (_lookup.ContainsKey(task.TimerId))
            {
                throw new InvalidOperationException($"计时器 {task.TimerId} 已存在");
            }

            _lookup.Add(task.TimerId, task);
            _tasks.Add(task);
        }

        /// <summary>
        /// 移除指定计时器任务。
        /// </summary>
        public bool Remove(int timerId)
        {
            if (!_lookup.TryGetValue(timerId, out TimerTask task))
            {
                return false;
            }

            _lookup.Remove(timerId);
            task.Cancel();
            int index = _tasks.IndexOf(task);
            if (index >= 0)
            {
                _tasks.RemoveAt(index);
            }

            return true;
        }

        /// <summary>
        /// 判断指定计时器是否存在。
        /// </summary>
        public bool Contains(int timerId) => _lookup.ContainsKey(timerId);

        /// <summary>
        /// 收集到期的计时器任务。
        /// </summary>
        /// <param name="currentTime">当前时间。</param>
        /// <param name="tolerance">触发误差容差。</param>
        /// <param name="buffer">存放结果的缓冲区。</param>
        public void CollectDue(double currentTime, double tolerance, List<TimerTask> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            for (int i = 0; i < _tasks.Count; i++)
            {
                TimerTask task = _tasks[i];

                if (task.IsCancelled)
                {
                    _tasks.RemoveAt(i);
                    _lookup.Remove(task.TimerId);
                    i--;
                    continue;
                }

                if (currentTime + tolerance < task.NextTriggerTime)
                {
                    continue;
                }

                buffer.Add(task);
            }
        }

        /// <summary>
        /// 对所有任务应用统一的时间偏移。
        /// </summary>
        public void ShiftAll(double deltaSeconds)
        {
            if (Math.Abs(deltaSeconds) < double.Epsilon)
            {
                return;
            }

            foreach (TimerTask task in _tasks)
            {
                task.Shift(deltaSeconds);
            }
        }

        /// <summary>
        /// 清空全部计时器任务。
        /// </summary>
        public void Clear()
        {
            _lookup.Clear();
            _tasks.Clear();
        }
    }
}
