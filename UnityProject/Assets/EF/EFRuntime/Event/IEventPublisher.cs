using System;

namespace EF.Event
{
    /// <summary>
    /// 事件发布器接口，提供类型化事件 Channel 的获取能力。
    /// 用于解耦框架层对具体 EventHub 实现的依赖。
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// 获取指定类型的事件 Channel，如果不存在则自动创建。
        /// </summary>
        /// <typeparam name="T">事件参数类型，必须是 readonly struct。</typeparam>
        /// <returns>对应类型的事件 Channel 实例。</returns>
        EventChannel<T> GetChannel<T>() where T : struct;
    }
}
