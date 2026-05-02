using EF.Event;

namespace GameLogic
{
    public sealed partial class EventHub
    {
        private EventChannel<StartLevelRequestedEvent> _startLevelRequestedEvent;

        /// <summary>
        /// 请求进入关卡事件 Channel。
        /// </summary>
        public EventChannel<StartLevelRequestedEvent> StartLevelRequestedEvent => _startLevelRequestedEvent ??= new EventChannel<StartLevelRequestedEvent>(this);
    }
}
