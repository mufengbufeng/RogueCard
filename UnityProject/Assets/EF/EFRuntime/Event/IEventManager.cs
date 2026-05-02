using EF.Common;

namespace EF.Event
{
    /// <summary>
    /// 事件管理器接口，保留用于 ModuleSystem 注册兼容。
    /// 新代码应直接使用 EventHub。
    /// </summary>
    public interface IEventManager : IEFManager
    {
    }
}
