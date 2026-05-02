namespace EF.UI
{
    /// <summary>
    /// UI 实例的生命周期状态。
    /// </summary>
    public enum UIWindowState
    {
        /// <summary>
        /// 未初始化或尚未注册。
        /// </summary>
        None = 0,

        /// <summary>
        /// 正在加载资源。
        /// </summary>
        Loading = 1,

        /// <summary>
        /// 正在执行打开流程。
        /// </summary>
        Opening = 2,

        /// <summary>
        /// 已经打开并处于显示状态。
        /// </summary>
        Opened = 3,

        /// <summary>
        /// 正在执行关闭流程。
        /// </summary>
        Closing = 4,

        /// <summary>
        /// 已经关闭，等待缓存或销毁。
        /// </summary>
        Closed = 5,

        /// <summary>
        /// 实例已被销毁。
        /// </summary>
        Destroyed = 6
    }
}

