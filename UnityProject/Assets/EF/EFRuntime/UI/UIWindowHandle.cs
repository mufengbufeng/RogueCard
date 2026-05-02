using Cysharp.Threading.Tasks;

namespace EF.UI
{
    /// <summary>
    /// UI 界面实例的句柄，提供便捷操作接口。
    /// </summary>
    public sealed class UIWindowHandle
    {
        private readonly UIManager _manager;

        internal UIWindowHandle(UIManager manager, string windowName, uint instanceId, UIView view, UIController controller)
        {
            _manager = manager;
            WindowName = windowName;
            InstanceId = instanceId;
            View = view;
            Controller = controller;
        }

        /// <summary>
        /// 对应的界面名称。
        /// </summary>
        public string WindowName { get; }

        /// <summary>
        /// 实例唯一标识。
        /// </summary>
        public uint InstanceId { get; }

        /// <summary>
        /// 界面视图对象。
        /// </summary>
        public UIView View { get; }

        /// <summary>
        /// 界面 Controller。
        /// </summary>
        public UIController Controller { get; }

        /// <summary>
        /// 当前界面状态。
        /// </summary>
        public UIWindowState State => _manager.GetWindowState(WindowName, InstanceId);

        /// <summary>
        /// 关闭当前界面实例。
        /// </summary>
        public UniTask CloseAsync()
        {
            return _manager.CloseWindowAsync(InstanceId);
        }
    }
}

