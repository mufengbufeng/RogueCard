namespace EF.Event
{
    /// <summary>
    /// 事件 Channel 的基础接口，用于 EventHub 统一迭代所有 Channel。
    /// </summary>
    public interface IEventChannel
    {
        /// <summary>
        /// 将异步队列中的所有事件派发给对应的 handler。
        /// </summary>
        void Flush();

        /// <summary>
        /// 清空所有 handler 注册和待分发事件。
        /// </summary>
        void Clear();
    }
}
